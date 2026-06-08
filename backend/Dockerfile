# syntax=docker/dockerfile:1
# Bygg-/körkontext = mappen "backend/".
# I Coolify: Build Pack = Dockerfile, Base Directory = /backend, Dockerfile = /backend/Dockerfile.

# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Central pakethantering + gemensamma build-props behövs vid restore.
COPY Directory.Build.props Directory.Packages.props ./

# Projektfiler först – ger lager-cachad restore som bara körs om någon .csproj ändras.
COPY src/Valkompass.Domain/Valkompass.Domain.csproj         src/Valkompass.Domain/
COPY src/Valkompass.Application/Valkompass.Application.csproj src/Valkompass.Application/
COPY src/Valkompass.Infrastructure/Valkompass.Infrastructure.csproj src/Valkompass.Infrastructure/
COPY src/Valkompass.Api/Valkompass.Api.csproj                src/Valkompass.Api/
RUN dotnet restore src/Valkompass.Api/Valkompass.Api.csproj

# Resten av källkoden och publicering.
COPY src/ src/
RUN dotnet publish src/Valkompass.Api/Valkompass.Api.csproj \
    -c Release -o /app --no-restore /p:UseAppHost=false

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# curl behövs för plattformens hälsokoll (t.ex. Coolify kör curl mot /health);
# den slimmade runtime-imagen saknar annars både curl och wget.
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# .NET 8+ container-images lyssnar på 8080 som standard; sätts explicit här.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Hälsokoll mot /health (appen har ingen route på "/").
HEALTHCHECK --interval=30s --timeout=3s --start-period=15s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

COPY --from=build /app ./

# Kör som den icke-root-användare (UID 1654) som finns i .NET-imagen.
USER $APP_UID

ENTRYPOINT ["dotnet", "Valkompass.Api.dll"]
