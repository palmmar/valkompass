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

# .NET 8+ container-images lyssnar på 8080 som standard; sätts explicit här.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app ./

# Kör som den icke-root-användare (UID 1654) som finns i .NET-imagen.
USER $APP_UID

ENTRYPOINT ["dotnet", "Valkompass.Api.dll"]
