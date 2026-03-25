# pmmanager

A self-hosted private media manager built with .NET 10 and Vue 3.

## Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 10, EF Core 10, SQLite |
| Frontend | Vue 3, TypeScript, Vuetify 3, Vite |
| Logging | Serilog (console + rolling file) |
| Container | Docker, Docker Compose |

## Getting Started

### Docker (recommended)

```bash
docker-compose up
```

App runs at [http://localhost:8080](http://localhost:8080). Data is persisted to `./data/app.db` and logs to `./logs/`.

### Local Development

**Backend**

```bash
dotnet run --project src/pmm.Api
# http://localhost:5000
```

**Frontend**

```bash
cd src/pmm.Frontend
npm install
npm run dev
# http://localhost:5173
```

The frontend dev server proxies `/api` requests to the backend automatically.

## Configuration

| Variable | Default | Description |
|---|---|---|
| `DB_PATH` | `./data/app.db` | SQLite database path |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Runtime environment |

## API

| Method | Path | Description |
|---|---|---|
| GET | `/api/health` | Health check |
| GET | `/api/items` | List all items |
| GET | `/api/items/{id}` | Get item by ID |
| POST | `/api/items` | Create item |
| PUT | `/api/items/{id}` | Update item |
| DELETE | `/api/items/{id}` | Delete item |

## Project Structure

```
src/
  pmm.Api/        # ASP.NET Core REST API (feature-based vertical slices)
  pmm.Database/   # EF Core DbContext, entities, migrations
  pmm.Frontend/   # Vue 3 SPA
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).
