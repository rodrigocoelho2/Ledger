/* Ledger — the one script of the app. No framework: the pages are rendered on the server and this only adds the bits a
   browser needs to feel quick — dialogs, forms that answer without a reload, quick add, dragging accounts into order,
   the look and the language. Everything is wired by data- attributes, so the views say what they want. */
'use strict';

(function () {
    var root = document.documentElement;
    var tokenMeta = document.querySelector('meta[name="request-verification-token"]');
    var token = tokenMeta ? tokenMeta.getAttribute('content') : '';
    var words = document.getElementById('words');
    var say = function (key, fallback) { return (words && words.getAttribute('data-' + key)) || fallback; };

    /* ---------- Small helpers ---------- */
    function post(url, body, json) {
        var options = { method: 'POST', headers: { 'RequestVerificationToken': token }, credentials: 'same-origin' };
        if (json) {
            options.headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(body);
        } else {
            options.body = body instanceof FormData ? body : toForm(body || {});
        }
        return fetch(url, options).then(read);
    }

    function read(response) {
        if (response.status === 401 || response.redirected && /\/sign-in/.test(response.url)) {
            location.href = '/sign-in?returnUrl=' + encodeURIComponent(location.pathname + location.search);
            return new Promise(function () { });
        }
        return response.json().catch(function () { return { isValid: response.ok, validationErrors: [] }; });
    }

    function toForm(values) {
        var form = new FormData();
        Object.keys(values).forEach(function (key) { if (values[key] !== null && values[key] !== undefined) form.append(key, values[key]); });
        return form;
    }

    function messages(result) {
        var list = (result && result.validationErrors) || [];
        return list.length ? list.map(function (error) { return error.message; }) : [say('failed', "That didn't work.")];
    }

    function toast(text, problem) {
        var box = document.getElementById('toast');
        if (!box || !text) return;
        box.textContent = text;
        box.classList.toggle('err', !!problem);
        box.classList.add('on');
        clearTimeout(box._timer);
        box._timer = setTimeout(function () { box.classList.remove('on'); }, 3600);
    }

    function later(text) { try { sessionStorage.setItem('ledger-toast', text); } catch (e) { } }

    function reload(text) {
        if (text) later(text);
        location.reload();
    }

    function showProblem(form, list) {
        var box = form.querySelector('.form-msg');
        if (!box) { toast(list.join(' '), true); return; }
        box.classList.remove('ok');
        box.textContent = list.join(' ');
        box.hidden = false;
    }

    function busy(button, on) { if (button) button.classList.toggle('busy', on); }

    /* ---------- The toast a page arrives with ---------- */
    (function () {
        var box = document.getElementById('toast');
        var waiting = null;
        try { waiting = sessionStorage.getItem('ledger-toast'); sessionStorage.removeItem('ledger-toast'); } catch (e) { }
        if (box && box.getAttribute('data-show')) setTimeout(function () { toast(box.getAttribute('data-show'), box.classList.contains('err')); }, 250);
        else if (waiting) setTimeout(function () { toast(waiting); }, 250);
    })();

    /* ---------- Look (kept on this device) ---------- */
    function theme(name) {
        var light = name === 'light';
        if (light) root.setAttribute('data-theme', 'light'); else root.setAttribute('data-theme', 'dark');
        var meta = document.querySelector('meta[name=theme-color]');
        if (meta) meta.setAttribute('content', light ? '#F3F0EA' : '#0F0F14');
        try { localStorage.setItem('ledger-theme', light ? 'light' : 'dark'); } catch (e) { }
        document.querySelectorAll('[data-theme-set]').forEach(function (button) {
            button.setAttribute('aria-pressed', button.getAttribute('data-theme-set') === (light ? 'light' : 'dark') ? 'true' : 'false');
        });
    }
    if (document.querySelector('[data-theme-set]')) theme(root.getAttribute('data-theme'));

    /* ---------- Dialogs ---------- */
    function open(dialog) {
        if (!dialog.open) dialog.showModal();
        var first = dialog.querySelector('[autofocus]:not([hidden])') || dialog.querySelector('input:not([type=hidden]):not([type=radio]), select');
        if (first && window.matchMedia('(pointer: fine)').matches) setTimeout(function () { first.focus(); }, 30);
    }

    function fill(dialog, values) {
        var form = dialog.querySelector('form');
        if (!form) return;
        form.reset();
        form.querySelectorAll('input[type=hidden]').forEach(function (input) { input.value = ''; });
        Object.keys(values).forEach(function (name) {
            var field = form.elements[name];
            if (field) field.value = values[name];
        });
        var editing = !!values.id;
        form.querySelectorAll('[data-edit-only]').forEach(function (element) { element.hidden = !editing; });
        var title = form.querySelector('[data-title-new]');
        if (title) title.textContent = title.getAttribute(editing ? 'data-title-edit' : 'data-title-new');
        var box = form.querySelector('.form-msg');
        if (box) box.hidden = true;
        disarm(form);
        if (form._valueSource) form._valueSource();
    }

    /* ---------- The account dialog: a balance from the entries, or the value in Obol ---------- */
    document.querySelectorAll('[data-value-source]').forEach(function (select) {
        var form = select.closest('form');
        form._valueSource = function () {
            var obol = select.value === 'Obol';
            var editing = !!(form.elements.id && form.elements.id.value);
            form.querySelectorAll('[data-obol-only]').forEach(function (element) {
                element.hidden = !obol || (element.hasAttribute('data-edit-only') && !editing);
            });
            form.querySelectorAll('[data-from-entries]').forEach(function (element) { element.hidden = obol; });
            var opening = form.elements.openingBalance;
            if (opening && opening.closest('.field')) opening.closest('.field').hidden = obol;
        };
        select.addEventListener('change', form._valueSource);
    });

    /* ---------- Settings: off to the bank ---------- */
    document.querySelectorAll('form[data-bank-start]').forEach(function (form) {
        form.addEventListener('submit', function (event) {
            event.preventDefault();
            var button = form.querySelector('[type=submit]');
            busy(button, true);
            post(form.getAttribute('action'), new FormData(form)).then(function (result) {
                if (!result || !result.isValid || !result.data || !result.data.url) { busy(button, false); showProblem(form, messages(result)); return; }
                location.href = result.data.url;
            }).catch(function () { busy(button, false); showProblem(form, [say('failed', "That didn't work.")]); });
        });
    });

    document.addEventListener('click', function (event) {
        var opener = event.target.closest('[data-open]');
        if (opener) {
            var dialog = document.getElementById(opener.getAttribute('data-open'));
            if (dialog) {
                var values = {};
                try { values = JSON.parse(opener.getAttribute('data-set') || '{}'); } catch (e) { }
                fill(dialog, values);
                open(dialog);
            }
            return;
        }
        var closer = event.target.closest('[data-close]');
        if (closer) { closer.closest('dialog').close(); return; }

        // A tap on the shade around a dialog closes it.
        if (event.target.tagName === 'DIALOG' && event.target.open) {
            var box = event.target.getBoundingClientRect();
            if (event.clientX < box.left || event.clientX > box.right || event.clientY < box.top || event.clientY > box.bottom) event.target.close();
        }
    });

    /* ---------- The entry dialog ---------- */
    function entry(query) {
        var dialog = document.getElementById('entry-dialog');
        if (!dialog) return;
        fetch('/entries/form' + (query ? '?' + query : ''), { credentials: 'same-origin' })
            .then(function (response) {
                if (!response.ok) throw new Error(String(response.status));
                return response.text();
            })
            .then(function (html) {
                dialog.innerHTML = html;
                wireEntry(dialog.querySelector('[data-entry-form]'));
                open(dialog);
            })
            .catch(function () { toast(say('failed', "That didn't work."), true); });
    }

    document.addEventListener('click', function (event) {
        var trigger = event.target.closest('[data-entry]');
        if (trigger) { event.preventDefault(); entry(trigger.getAttribute('data-entry')); }
    });

    function checked(form, name) {
        var field = form.querySelector('input[name="' + name + '"]:checked') || form.querySelector('input[type=hidden][name="' + name + '"]');
        return field ? field.value : '';
    }

    function wireEntry(form) {
        if (!form) return;
        var sync = function () {
            var kind = checked(form, 'kind') || 'Outflow';
            var scope = kind === 'Transfer' ? 'Personal' : (checked(form, 'scope') || 'Personal');

            form.querySelectorAll('[data-for-kinds]').forEach(function (element) {
                element.hidden = element.getAttribute('data-for-kinds').split(' ').indexOf(kind) < 0;
            });

            // Only the categories of this kind and this part of life; keep the pick if it still fits.
            var first = null, keep = false;
            form.querySelectorAll('[data-categories] > [data-kind]').forEach(function (holder) {
                var fits = holder.getAttribute('data-kind') === kind && holder.getAttribute('data-scopes').split(' ').indexOf(scope) >= 0;
                holder.hidden = !fits;
                var radio = holder.querySelector('input');
                if (fits && !first) first = radio;
                if (fits && radio.checked) keep = true;
                if (!fits) radio.checked = false;
            });
            if (!keep && first && kind !== 'Transfer') first.checked = true;

            var category = checked(form, 'category');
            var booking = form.querySelector('[data-for-booking]');
            if (booking) booking.hidden = !(kind === 'Inflow' && scope === 'Rental' && category === 'bookings');

            var flips = form.querySelector('[data-for-scope="Flips"]');
            if (flips) flips.hidden = !(scope === 'Flips' && kind !== 'Transfer');
            var project = form.querySelector('[data-project]');
            var named = form.querySelector('[data-new-project]');
            if (project && named) named.hidden = project.value !== '';

            var label = form.querySelector('[data-label-account]');
            if (label) label.textContent = label.getAttribute(kind === 'Transfer' ? 'data-from' : 'data-plain');
        };
        form.addEventListener('change', sync);
        sync();
        wirePlan(form);
    }

    /* ---------- Instalments in the entry form ---------- */
    // "1.234,56", "1,234.56", "9,80" and "9.80" all read as amounts, like the server does.
    function amountOf(text) {
        var t = String(text || '').replace(/[^0-9.,]/g, '');
        if (!/[0-9]/.test(t)) return NaN;
        var comma = t.lastIndexOf(','), dot = t.lastIndexOf('.');
        if (comma >= 0 && dot >= 0) {
            var mark = comma > dot ? ',' : '.';
            t = t.split(mark === ',' ? '.' : ',').join('').replace(mark, '.');
        } else if (comma >= 0 || dot >= 0) {
            var sep = comma >= 0 ? ',' : '.', pieces = t.split(sep);
            var thousands = pieces.length > 2 || (pieces[1].length === 3 && pieces[0] !== '0' && pieces[0] !== '');
            t = thousands ? pieces.join('') : pieces.join('.');
        }
        return parseFloat(t);
    }

    function wirePlan(form) {
        var toggle = form.querySelector('[data-plan-toggle]');
        if (!toggle) return;
        var body = form.querySelector('[data-plan]');
        var day = form.querySelector('[data-day]');
        var label = form.querySelector('[data-amount-label]');
        var count = form.querySelector('input[name=instalments]');
        var amount = form.querySelector('input[name=amount]');
        var preview = form.querySelector('[data-plan-preview]');
        var money = new Intl.NumberFormat(root.lang === 'pt' ? 'pt-PT' : 'en-IE', { style: 'currency', currency: 'EUR' });

        var update = function () {
            var on = toggle.checked && checked(form, 'kind') === 'Outflow';
            body.hidden = !toggle.checked;
            if (day) day.hidden = on;
            if (label) label.textContent = label.getAttribute(on ? 'data-total' : 'data-plain');
            var n = parseInt(count.value, 10);
            form.querySelectorAll('[data-parts]').forEach(function (chip) { chip.setAttribute('aria-pressed', chip.getAttribute('data-parts') === String(n) ? 'true' : 'false'); });
            var total = amountOf(amount.value);
            if (!on || !(n >= 2 && n <= 60) || !(total > 0)) { preview.textContent = ''; return; }
            var cents = Math.round(total * 100), part = Math.floor(cents / n), first = part + (cents - part * n);
            var text = first === part ? preview.getAttribute('data-one') : preview.getAttribute('data-first');
            preview.textContent = text.replace('{n}', n).replace('{a}', money.format(part / 100)).replace('{f}', money.format(first / 100));
        };

        form.querySelectorAll('[data-parts]').forEach(function (chip) {
            chip.addEventListener('click', function () { count.value = chip.getAttribute('data-parts'); update(); });
        });
        form.addEventListener('change', update);
        count.addEventListener('input', update);
        amount.addEventListener('input', update);
        update();
    }

    /* ---------- Forms that answer without a reload ---------- */
    document.addEventListener('submit', function (event) {
        var form = event.target.closest('form[data-ajax]');
        if (!form) return;
        event.preventDefault();
        var button = form.querySelector('[type=submit]');
        busy(button, true);
        var box = form.querySelector('.form-msg');
        if (box) box.hidden = true;

        var data = new FormData(form);
        // The entry form leaves out what doesn't apply to the kind picked.
        if (form.hasAttribute('data-entry-form') && data.get('kind') === 'Transfer') { data.delete('category'); data.set('scope', 'Personal'); }

        post(form.getAttribute('action'), data).then(function (result) {
            busy(button, false);
            if (!result || !result.isValid) { showProblem(form, messages(result)); return; }
            if (form.hasAttribute('data-result') && result.data && result.data.message) {
                if (box) { box.textContent = result.data.message; box.classList.add('ok'); box.hidden = false; }
                setTimeout(function () { reload(result.data.message); }, 1400);
                return;
            }
            reload(say('saved', 'Saved'));
        }).catch(function () {
            busy(button, false);
            showProblem(form, [say('failed', "That didn't work.")]);
        });
    });

    /* ---------- Buttons that post (deletes ask for a second tap) ---------- */
    function disarm(scope) {
        (scope || document).querySelectorAll('.armed').forEach(function (button) {
            button.classList.remove('armed');
            if (button._label !== undefined) { button.setAttribute('aria-label', button._label || ''); if (!button._label) button.removeAttribute('aria-label'); }
        });
    }

    document.addEventListener('click', function (event) {
        var button = event.target.closest('[data-post]');
        if (!button) { if (!event.target.closest('.armed')) disarm(); return; }
        event.preventDefault();

        if (button.hasAttribute('data-confirm') && !button.classList.contains('armed')) {
            disarm();
            button.classList.add('armed');
            button._label = button.getAttribute('aria-label');
            button.setAttribute('aria-label', say('sure', 'Tap again to confirm'));
            toast(say('sure', 'Tap again to confirm'));
            return;
        }

        var id = button.getAttribute('data-id');
        if (!id && button.hasAttribute('data-id-from')) {
            var form = button.closest('form');
            var field = form && form.elements[button.getAttribute('data-id-from')];
            id = field ? field.value : '';
        }
        busy(button, true);
        post(button.getAttribute('data-post'), id ? { id: id } : {}).then(function (result) {
            busy(button, false);
            if (!result || !result.isValid) {
                var form = button.closest('form');
                if (form) showProblem(form, messages(result)); else toast(messages(result).join(' '), true);
                disarm();
                return;
            }
            var next = button.getAttribute('data-then');
            if (next) location.href = next; else location.reload();
        }).catch(function () { busy(button, false); toast(say('failed', "That didn't work."), true); });
    });

    /* ---------- Quick add ---------- */
    document.querySelectorAll('form[data-quick]').forEach(function (form) {
        var input = form.querySelector('input[name=text]');
        var chips = form.querySelector('[data-quick-chips]');
        var hint = form.parentNode.querySelector('[data-quick-hint]');
        var reading = null, timer = null, asked = 0;

        function setHint(text, problem) {
            if (!hint) return;
            hint.textContent = text || hint.getAttribute('data-default');
            hint.classList.toggle('err', !!problem);
        }

        function chip(text, color) {
            var button = document.createElement('button');
            button.type = 'button';
            if (color) {
                var dot = document.createElement('span');
                dot.className = 'dot';
                dot.style.background = color;
                button.appendChild(dot);
            }
            button.appendChild(document.createTextNode(text));
            button.addEventListener('click', function () { entry('text=' + encodeURIComponent(input.value)); });
            return button;
        }

        function show(data) {
            reading = data;
            chips.innerHTML = '';
            if (!data || !data.understood) { chips.hidden = true; return; }
            chips.appendChild(chip(data.kindText + ' ' + data.amountText, data.kind === 'Inflow' ? 'var(--in)' : 'var(--out)'));
            chips.appendChild(chip(data.categoryText, data.categoryColor));
            chips.appendChild(chip(data.accountText));
            chips.hidden = false;
        }

        function ask(then) {
            var text = input.value.trim();
            if (!text) { show(null); setHint(); return; }
            var mine = ++asked;
            fetch('/entries/quick?text=' + encodeURIComponent(text), { credentials: 'same-origin' })
                .then(read)
                .then(function (result) {
                    if (mine !== asked) return;
                    show(result && result.isValid ? result.data : null);
                    setHint();
                    if (then) then();
                })
                .catch(function () { if (mine === asked) show(null); });
        }

        input.addEventListener('input', function () {
            clearTimeout(timer);
            reading = null;
            timer = setTimeout(function () { ask(); }, 260);
        });

        form.addEventListener('submit', function (event) {
            event.preventDefault();
            clearTimeout(timer);
            var save = function () {
                if (!reading || !reading.understood) { setHint(hint && hint.getAttribute('data-none'), true); return; }
                var button = form.querySelector('[type=submit]');
                busy(button, true);
                post('/entries/save', {
                    kind: reading.kind, scope: reading.scope, name: reading.name, amount: reading.amount,
                    date: reading.date, category: reading.category, accountId: reading.accountId || ''
                }).then(function (result) {
                    busy(button, false);
                    if (!result || !result.isValid) { setHint(messages(result).join(' '), true); return; }
                    reload(say('saved', 'Saved') + ' · ' + reading.name + ' ' + reading.amountText);
                }).catch(function () { busy(button, false); setHint(say('failed', "That didn't work."), true); });
            };
            if (reading && reading.understood) save(); else ask(save);
        });
    });

    /* ---------- Panels with a two-way switch (biggest movers) ---------- */
    document.querySelectorAll('[data-switch]').forEach(function (panel) {
        panel.querySelectorAll('[data-show]').forEach(function (button) {
            button.addEventListener('click', function () {
                var want = button.getAttribute('data-show');
                panel.querySelectorAll('[data-show]').forEach(function (other) { other.setAttribute('aria-pressed', other === button ? 'true' : 'false'); });
                panel.querySelectorAll('[data-pane]').forEach(function (pane) { pane.hidden = pane.getAttribute('data-pane') !== want; });
            });
        });
    });

    /* ---------- Accounts: drag by the handle (or arrow keys) to reorder ---------- */
    document.querySelectorAll('[data-reorder]').forEach(function (list) {
        var url = list.getAttribute('data-reorder');
        var save = function () {
            var order = Array.prototype.map.call(list.querySelectorAll('.acct'), function (row) { return row.getAttribute('data-id'); });
            post(url, order, true).then(function (result) {
                if (!result || !result.isValid) toast(messages(result).join(' '), true);
            }).catch(function () { toast(say('failed', "That didn't work."), true); });
        };

        list.addEventListener('pointerdown', function (event) {
            var grip = event.target.closest('.acct-grip');
            if (!grip || event.button > 0) return;
            event.preventDefault();
            var row = grip.closest('.acct');
            var startY = event.clientY, moved = false;
            row.classList.add('dragging');
            grip.setPointerCapture(event.pointerId);

            var move = function (e) {
                var dy = e.clientY - startY;
                row.style.transform = 'translateY(' + dy + 'px)';
                var before = row.previousElementSibling, after = row.nextElementSibling;
                if (after && dy > after.offsetHeight / 2) {
                    list.insertBefore(after, row);
                    startY += after.offsetHeight; moved = true;
                    row.style.transform = 'translateY(' + (e.clientY - startY) + 'px)';
                } else if (before && -dy > before.offsetHeight / 2) {
                    list.insertBefore(row, before);
                    startY -= before.offsetHeight; moved = true;
                    row.style.transform = 'translateY(' + (e.clientY - startY) + 'px)';
                }
            };
            var end = function () {
                grip.removeEventListener('pointermove', move);
                grip.removeEventListener('pointerup', end);
                grip.removeEventListener('pointercancel', end);
                row.classList.remove('dragging');
                row.style.transform = '';
                if (moved) save();
            };
            grip.addEventListener('pointermove', move);
            grip.addEventListener('pointerup', end);
            grip.addEventListener('pointercancel', end);
        });

        list.addEventListener('keydown', function (event) {
            var grip = event.target.closest('.acct-grip');
            if (!grip || (event.key !== 'ArrowUp' && event.key !== 'ArrowDown')) return;
            event.preventDefault();
            var row = grip.closest('.acct');
            if (event.key === 'ArrowUp' && row.previousElementSibling) list.insertBefore(row, row.previousElementSibling);
            else if (event.key === 'ArrowDown' && row.nextElementSibling) list.insertBefore(row.nextElementSibling, row);
            else return;
            grip.focus();
            save();
        });
    });

    /* ---------- Settings ---------- */
    var switches = document.querySelectorAll('[data-module]');
    switches.forEach(function (button) {
        button.addEventListener('click', function () {
            var on = button.getAttribute('aria-checked') !== 'true';
            button.setAttribute('aria-checked', on ? 'true' : 'false');
            var card = button.closest('.module');
            if (card) card.classList.toggle('on', on);
            var state = {};
            switches.forEach(function (item) { state[item.getAttribute('data-module')] = item.getAttribute('aria-checked') === 'true'; });
            var tab = document.querySelector('[data-tab="' + button.getAttribute('data-module') + '"]');
            if (tab) tab.hidden = !on;
            post('/settings/modules', { rental: state.rental, flips: state.flips }).then(function (result) {
                if (!result || !result.isValid) { toast(messages(result).join(' '), true); return; }
                reload(say('saved', 'Saved'));
            }).catch(function () { toast(say('failed', "That didn't work."), true); });
        });
    });

    document.querySelectorAll('[data-language]').forEach(function (button) {
        button.addEventListener('click', function () {
            if (button.getAttribute('aria-pressed') === 'true') return;
            post('/settings/language', { language: button.getAttribute('data-language') }).then(function (result) {
                if (result && result.isValid) location.reload(); else toast(messages(result).join(' '), true);
            });
        });
    });

    document.querySelectorAll('[data-theme-set]').forEach(function (button) {
        button.addEventListener('click', function () { theme(button.getAttribute('data-theme-set')); });
    });

    /* ---------- Installable as an app ---------- */
    if ('serviceWorker' in navigator && location.protocol === 'https:') {
        window.addEventListener('load', function () { navigator.serviceWorker.register('/sw.js').catch(function () { }); });
    }
})();
