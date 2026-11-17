# Ledger.WebApi — the engine: users, accounts, entries and the sums. Not published on any port: only the website reaches it.
# Build context: the solution folder.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY Ledger.Library/Ledger.Library.csproj Ledger.Library/
COPY Ledger.WebApi/Ledger.WebApi.csproj Ledger.WebApi/
RUN dotnet restore Ledger.WebApi/Ledger.WebApi.csproj
COPY Ledger.Library/ Ledger.Library/
COPY Ledger.WebApi/ Ledger.WebApi/
# Files copied from a NAS share can keep owner-only permissions; the app runs as the non-root "app" user.
RUN dotnet publish Ledger.WebApi/Ledger.WebApi.csproj -c Release -o /app --no-restore \
    && chmod -R a+rX /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    TZ=Europe/Lisbon

EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Ledger.WebApi.dll"]
