# Changelog

Entries are grouped by feature branch, newest first.
See [`docs/changelog/`](docs/changelog/) for archived entries.

---

## feature/changelog — 2026-03-28

### Done
- Established `CHANGELOG.md` at repo root with backfilled entries for all prior feature branches
- Created `docs/changelog/2026.md` and `docs/changelog/2025.md` archive files
- Added `## Changelog` section to `Claude.md` with rules: mandatory entry per PR, entry format, mandatory `### Dead Ends` section, 300-line archive trigger, and archive file creation

### Dead Ends
- *(none)*

---

## feature/prdb-integration — 2026-03-26

### Done
- Added `AppSettings` options class with `PrdbApiUrl` and `PrdbApiKey` configuration
- Added `PrdbSite`, `PrdbNetwork`, `PrdbVideo`, `PrdbActor` EF Core entities with migrations
- Implemented `PrdbSyncService` to fetch and upsert sites, networks, videos, and actors from prdb.net API
- Added Settings UI page for managing API credentials
- Added frontend views: Sites, Videos, Actors — all with stub sync triggers
- Wired sync endpoint and hooked into frontend sidebar navigation

### Dead Ends
- *(No dead ends documented — backfilled entry)*

---

## feature/parse-indexers — 2026-03-26

### Done
- Added `IndexerRow` entity and Newznab XML scraper
- Implemented `IndexerRows` listing view with filters (title search, size filter), backfill, and clear actions
- Added `DownloadClient` entity, full CRUD API, test-connection endpoint, and frontend management page
- Added NZB send-to-download-client flow from the `IndexerRows` view
- Added per-indexer API request tracking (Search and Grab calls recorded to DB)
- Added indexer stats view with charts (requests over time, hit/miss ratio)
- Added delete confirmation dialogs on Indexers and DownloadClients pages
- Added indexer connection test with save-guard (prevents saving without a successful test)

### Dead Ends
- *(No dead ends documented — backfilled entry)*

---

## feature/add-indexer — 2026-03-26

### Done
- Added `Indexer` entity with `ApiPath` field, EF Core migration
- Full CRUD REST API for indexers with OpenAPI metadata
- Frontend indexer management view
- Integration tests covering happy path and sad paths (404, 400) via `WebApplicationFactory`
- CI pipeline integration test run

### Dead Ends
- *(No dead ends documented — backfilled entry)*

---

## feature/installer — 2026-03-26

### Done
- Windows installer project (WiX or similar) producing an `.msi`
- Background service host so pmm runs as a Windows Service
- GitHub Actions workflow step to build and attach installer artifact

### Dead Ends
- *(No dead ends documented — backfilled entry)*

---

## feature/go-init — 2026-03-25

### Done
- Renamed solution and all projects to `pmm` prefix
- Extracted `pmm.Database` project; migrated persistence to EF Core (code-first, SQLite)
- Added Docker Compose setup and `Dockerfile`
- Added GitHub Actions workflow to build and publish Docker image to Docker Hub
- Wrote initial `README.md` with stack overview, setup steps, and API reference

### Dead Ends
- *(No dead ends documented — backfilled entry)*

---

## feature/get-started — 2026-03-25

### Done
- Scaffolded .NET 10 + Vue 3 (Vite) application skeleton
- Added `docker-compose.yml`, `.gitignore`, `.editorconfig`
- Added `Claude.md` with initial project guidelines and issue templates

### Dead Ends
- *(No dead ends documented — backfilled entry)*
