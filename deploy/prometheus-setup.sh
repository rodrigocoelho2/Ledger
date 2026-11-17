#!/bin/bash
# Ledger on Prometheus, from a fresh clone to a running Portainer stack:
#   .env (secrets generated, sa taken from shared-sql) → shared-sql's .env and its Portainer variables →
#   build on the NAS → apps.py → Portainer stack → checks (database, ledger login, website) → Homepage card.
# Safe to run again: what's already done is kept (the .env, shared-sql, the apps.py line, the Homepage card).
#
# Where the variables live: in the .env next to each app's docker-compose.yml (and shared-sql's). apps.sh sends them
# to the stack's variables in Portainer on every migrar/atualizar, so a change made only in Portainer is overwritten
# by the next update: change the .env, then  apps.sh atualizar <app>.
#
#   sudo bash /volume3/docker/self-hosted/Ledger/deploy/prometheus-setup.sh
set -u
SELF=/volume3/docker/self-hosted
APP=$SELF/Ledger
SHARED=$SELF/shared-sql/.env
APPS_PY=/volume3/docker/_scripts/apps/apps.py
APPS_SH=/volume3/docker/_scripts/apps/apps.sh
HOMEPAGE=/volume3/docker/homepage
SYNCED=$APP/.deploy-shared-sql     # marks that shared-sql's Portainer variables already have the ledger password

ok()   { echo "   OK  $*"; }
fail() { echo "   !!! $*"; exit 1; }
step() { echo; echo "== $*"; }

[ "$(id -u)" = 0 ] || fail "corre com sudo: sudo bash $0"
cd "$APP" || fail "não encontro $APP (faz primeiro o git clone)"

step "código"
# No NAS o git corre num contentor (alpine/git) quando não está instalado.
if command -v git >/dev/null 2>&1; then
  git -c safe.directory="$APP" pull --ff-only 2>&1 | sed 's/^/   /'
else
  docker run --rm -v "$APP:/git" -w /git alpine/git -c safe.directory=/git pull --ff-only 2>&1 | sed 's/^/   /'
fi

step ".env"
[ -f "$SHARED" ] || fail "não encontro $SHARED"
gen() { echo "Aa1$(LC_ALL=C tr -dc 'A-Za-z0-9' </dev/urandom | head -c 24)"; }
get() { grep -m1 "^$1=" "$2" 2>/dev/null | cut -d= -f2- | sed "s/^['\"]//; s/['\"]$//"; }
SA=$(get SQL_SA_PASSWORD "$SHARED")
[ -n "$SA" ] || fail "o $SHARED não tem SQL_SA_PASSWORD"

# A password própria do login ledger fica também no .env do shared-sql, como a das outras apps.
APP_PW=$(get LEDGER_DB_PASSWORD "$SHARED")
if [ -z "$APP_PW" ]; then
  APP_PW=$(get SQL_APP_PASSWORD .env)
  [ -n "$APP_PW" ] || APP_PW=$(gen)
  printf '\nLEDGER_DB_PASSWORD=%s\n' "$APP_PW" >> "$SHARED"
  ok "LEDGER_DB_PASSWORD acrescentada a $SHARED"
fi

if [ -f .env ]; then
  ok ".env já existe: fica como está (só acerto o sa e a password do ledger)"
  sed -i "s|^SQL_SA_PASSWORD=.*|SQL_SA_PASSWORD=$SA|; s|^SQL_APP_PASSWORD=.*|SQL_APP_PASSWORD=$APP_PW|" .env
else
  cat > .env <<EOF
# Ledger — gerado por deploy/prometheus-setup.sh. Guarda estes valores no Vaultwarden.
SQL_SA_PASSWORD=$SA
SQL_APP_PASSWORD=$APP_PW
LEDGER_API_KEY=$(gen)
LEDGER_SETUP_KEY=$(gen)

# Entrar com o Discord (opcional): a mesma aplicação Discord das outras apps; acrescenta o redirect
#   https://ledger.prometheus-nas.com/signin-discord   e depois:  sudo bash $APPS_SH atualizar ledger
# DISCORD_CLIENT_ID=
# DISCORD_CLIENT_SECRET=
EOF
  ok ".env criado"
fi
chmod 600 .env
PORT=$(get LEDGER_PORT .env); PORT=${PORT:-5100}

step "shared-sql"
# The ledger password goes into shared-sql's .env (above) and into its variables in Portainer, like the other apps'.
# Sending them to Portainer redeploys shared-sql: the SQL Server restarts and the other apps wait for it (~30 s).
if [ -f "$SYNCED" ]; then
  ok "as variáveis do shared-sql no Portainer já têm a LEDGER_DB_PASSWORD"
else
  echo "   a enviar o .env do shared-sql para o Portainer (o SQL Server reinicia ~30 s)"
  set -o pipefail
  bash "$APPS_SH" atualizar shared-sql 2>&1 | sed 's/^/   /' || fail "o apps.sh atualizar shared-sql falhou (vê acima)"
  set +o pipefail
  for i in $(seq 1 40); do
    [ "$(docker inspect -f '{{.State.Health.Status}}' shared-sql 2>/dev/null)" = healthy ] && break
    sleep 3
  done
  [ "$(docker inspect -f '{{.State.Health.Status}}' shared-sql 2>/dev/null)" = healthy ] || fail "o shared-sql não ficou healthy (docker logs shared-sql)"
  touch "$SYNCED"
  ok "shared-sql atualizado no Portainer e healthy"
fi

step "porta $PORT"
if ss -ltn 2>/dev/null | grep -q ":$PORT " && ! docker ps --format '{{.Names}}' | grep -q '^ledger-website$'; then
  fail "a porta $PORT já está ocupada: acrescenta LEDGER_PORT=<outra> ao .env e corre outra vez"
fi
ok "livre"

step "a compilar no NAS (alguns minutos)"
if ! docker compose build > /tmp/ledger-build.log 2>&1; then
  tail -40 /tmp/ledger-build.log
  fail "a compilação falhou (log completo em /tmp/ledger-build.log). Nada foi alterado no Portainer."
fi
ok "imagens ledger-db-deploy, ledger-webapi e ledger-website"
docker volume inspect ledger_keys >/dev/null 2>&1 || docker volume create ledger_keys >/dev/null
ok "volume ledger_keys"

step "apps.py"
[ -f "$APPS_PY" ] || fail "não encontro $APPS_PY"
if grep -q '"ledger"' "$APPS_PY"; then
  ok "o Ledger já está lá"
else
  cp "$APPS_PY" "$APPS_PY.bak-$(date +%Y%m%d%H%M%S)"
  python3 - "$APPS_PY" <<'PY' || fail "não consegui acrescentar o Ledger ao apps.py"
import re, sys
p = sys.argv[1]; s = open(p).read()
m = re.search(r'^(\s*)"holo":[^\n]*\n', s, re.M) or re.search(r'^(\s*)"obol":[^\n]*\n', s, re.M)
if not m: sys.exit("no holo/obol line")
s = s[:m.end()] + f'{m.group(1)}"ledger":      ("Ledger",      "ledger"),\n' + s[m.end():]
s = re.sub(r'^(Apps: [^\n]*?)(\n)', lambda x: x.group(1) + ("" if "ledger" in x.group(1) else ", ledger") + x.group(2), s, count=1, flags=re.M)
open(p, "w").write(s)
PY
  python3 -m py_compile "$APPS_PY" || fail "o apps.py ficou inválido (cópia em $APPS_PY.bak-*)"
  ok "linha \"ledger\" acrescentada (cópia de segurança ao lado)"
fi

step "stack no Portainer"
set -o pipefail
if bash "$APPS_SH" 2>/dev/null | grep -qE '^ledger +sim'; then
  bash "$APPS_SH" atualizar ledger 2>&1 | sed 's/^/   /' || fail "o apps.sh atualizar falhou (vê acima)"
else
  bash "$APPS_SH" migrar ledger 2>&1 | sed 's/^/   /' || fail "o apps.sh migrar falhou (vê acima)"
fi
set +o pipefail

step "verificações"
for i in $(seq 1 60); do
  state=$(docker inspect -f '{{.State.Status}} {{.State.ExitCode}}' ledger-db-deploy 2>/dev/null)
  [ "${state%% *}" = exited ] && break
  sleep 3
done
[ "$state" = "exited 0" ] || { docker logs ledger-db-deploy 2>&1 | tail -30; fail "o ledger-db-deploy não terminou bem ($state)"; }
ok "base de dados Ledger publicada"

SQLCMD="docker exec shared-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -C -b -h -1 -W"
n=$($SQLCMD -U ledger -P "$APP_PW" -d Ledger -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.procedures" 2>&1 | tr -d '[:space:]')
[[ "$n" =~ ^[0-9]+$ ]] && [ "$n" -ge 20 ] || fail "o login ledger não entra na base Ledger: $n"
ok "login ledger entra na Ledger ($n procedures)"
for other in Obol Milo Kardia LivingDex Ludex; do
  if $SQLCMD -U ledger -P "$APP_PW" -d "$other" -Q "SELECT 1" >/dev/null 2>&1; then
    fail "o login ledger consegue abrir a base $other (não devia)"
  fi
done
ok "e não consegue abrir as bases das outras apps"

for i in $(seq 1 40); do
  code=$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:$PORT/create-account")
  [ "$code" = 200 ] && break
  sleep 3
done
[ "$code" = 200 ] || { docker logs ledger-webapi 2>&1 | tail -20; docker logs ledger-website 2>&1 | tail -20; fail "o site não responde (HTTP $code)"; }
ok "site a responder em http://<ip-do-nas>:$PORT"

step "Homepage"
add_card() {
  local f=$1
  [ -f "$f" ] || return 0
  if grep -q -- '- Ledger:' "$f"; then ok "$(basename "$(dirname "$f")")/services.yaml já tem o Ledger"; return 0; fi
  cp "$f" "$f.bak-$(date +%Y%m%d%H%M%S)"
  python3 - "$f" "$PORT" <<'PY' || { echo "   (não encontrei o cartão do Obol em $1: acrescenta o Ledger à mão)"; return 0; }
import re, sys
p = sys.argv[1]; s = open(p).read()
m = re.search(r'^( +)- Obol:\n((?:\1 +[^\n]*\n)+)', s, re.M)
if not m: sys.exit(1)
i, j = m.group(1), m.group(1) + "    "
card = (f"{i}- Ledger:\n{j}icon: mdi-book-open-variant-#7FC7AE\n{j}href: http://{{{{HOMEPAGE_VAR_NAS}}}}:{sys.argv[2]}\n"
        f"{j}description: Finanças pessoais — entradas, saídas e o que sobra\n{j}container: ledger-website\n")
end = m.end()
s = s[:end] + card + s[end:]
open(p, "w").write(s)
PY
  ok "cartão do Ledger em $f"
}
add_card "$HOMEPAGE/config/services.yaml"
add_card "$HOMEPAGE/themes/shared/services.yaml"      # o use-theme.sh copia este para config
grep -q -- '- Ledger:' "$HOMEPAGE/config/services.yaml" 2>/dev/null && ok "o Homepage recarrega sozinho (refresca a página)"

echo
echo "Feito. Abre http://<ip-do-nas>:$PORT e cria a primeira conta com o setup key:"
echo "   $(get LEDGER_SETUP_KEY .env)"
echo "Guarda o .env no Vaultwarden:  sudo cat $APP/.env"
