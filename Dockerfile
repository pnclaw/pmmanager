# ─── Stage 1: Build Vue frontend ────────────────────────────────────────────
FROM node:22-alpine AS frontend-build

WORKDIR /frontend

COPY src/PrnMediamanager.Frontend/package*.json ./
RUN npm ci

COPY src/PrnMediamanager.Frontend/ ./
RUN npm run build

# ─── Stage 2: Build .NET backend ─────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build

WORKDIR /app

RUN --mount=type=bind,rw,source=src/PrnMediamanager.Api,target=/app \
    dotnet publish -c Release -o /tmp/publish

RUN cp -r /tmp/publish/. /app/publish

# ─── Stage 3: Runtime image ──────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

# Copy published .NET app
COPY --from=backend-build /app/publish ./

# Copy Vue SPA into wwwroot so ASP.NET Core serves it statically
COPY --from=frontend-build /frontend/dist ./wwwroot

# Create mount points for persistent data and logs
RUN mkdir -p /app/data /app/logs

# SQLite database path (overridable via environment variable or volume)
ENV DB_PATH=/app/data/app.db

# Bind on all interfaces, port 8080
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "PrnMediamanager.Api.dll"]
