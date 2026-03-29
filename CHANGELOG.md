# Changelog

Entries are grouped by feature branch, newest first.
See [`docs/changelog/`](docs/changelog/) for archived entries.

---

## feature/improve-prenames-sync — 2026-03-29

### Done
- Replaced the per-video prename upsert in `PrdbVideoDetailSyncService` with a new `PrdbLatestPreNameSyncService` that uses the `GET /prenames/latest` API endpoint
- Backfill runs up to 5,000 prenames per SyncWorker tick (10 pages × 500); a `PrenamesBackfillPage` cursor in `AppSettings` advances each tick until all historical prenames are fetched
- Once backfill is complete, each tick fetches only prenames created since `PrenamesSyncCursorUtc` (incremental sync)
- Added three new `AppSettings` columns: `PrenamesBackfillPage`, `PrenamesBackfillTotalCount`, `PrenamesSyncCursorUtc` — each with its own EF migration
- New **Prename Sync** card on the PRDB Status page: shows total prenames in DB, backfill progress ("page X of Y"), incremental cursor ("Next sync fetches from"), Run Now button, and Reset Cursor button to restart the backfill
- Added **Pre-names** count row to the Library card on the PRDB Status page
- Added `shims-vue.d.ts` to fix pre-existing TypeScript errors when running plain `tsc` on the frontend

### Dead Ends
- *(none)*

---

## feature/prenames-sync-with-indexer-data — 2026-03-29

### Done
- Added `IndexerRowMatch` entity linking `IndexerRow` to `PrdbVideo`/`PrdbVideoPreName`, with EF migration and unique index on `IndexerRowId`
- New `IndexerRowMatchService`: runs each SyncWorker tick, checks IndexerRows from the last 7 days against all `PrdbVideoPreName` titles (exact case-insensitive match); stores single matches, logs a warning and skips rows with multiple candidates
- Added `IndexerRowMatchLastRunAt` to `AppSettings` to track last run time
- Extended prdb Status page with an Indexer Row Match card (total matches, last run, Run Now button)
- Added debug endpoint (`POST /api/prdb-status/indexer-row-match/debug`) and UI button: prompts for a multi-word search string, filters all IndexerRows by every word, returns per-row match status without writing to the database

### Dead Ends
- *(none)*

---

## feature/wanted-list-improvements — 2026-03-29

### Done
- Wanted list UI overhaul: larger thumbnails (240×135 px, fixed height for uniform rows), site and title stacked in a single column, fulfilled/unfulfilled status as an overlay banner on the thumbnail, "Added" column hidden on narrow viewports
- Added edit dialog (pencil button) per row: shows site, title, added date, a one-click toggle button for fulfilled state, and a "Remove from wanted list" action — both synced to prdb and local DB
- New `PATCH /api/prdb-wanted-videos/{videoId}` endpoint to update fulfilment state; new `DELETE /api/prdb-wanted-videos/{videoId}` endpoint to remove from prdb and local DB
- New video detail page at `/prdb/videos/:id`: image carousel, cast chips, alternative titles, wanted/fulfilled status badge; reachable by clicking any row in the wanted list or site videos list
- New `GET /api/prdb-videos/{id}` endpoint returning full video detail (images, cast, pre-names, wanted status)
- Release date column now formatted consistently (was raw string in wanted list)

### Dead Ends
- *(none)*

---

## feature/wanted-videos-next-steps — 2026-03-29

### Done
- Trigger immediate background video sync when a site is favorited so videos appear straight away rather than waiting up to 15 min for the next SyncWorker tick
- Fixed `PageSize` constant from 500 → 100 to respect the `/videos` API maximum, preventing truncated results on all paginated video fetches
- Video detail sync now re-syncs details every 30 days (oldest-synced first) so pre-names added after initial sync are eventually picked up
- Fixed `DbUpdateConcurrencyException` in `UpsertWantedVideosAsync` caused by stale Modified actors leaking between services — each SyncWorker service now runs in its own DI scope with a fresh `DbContext`
- Fixed actor detail sync never making progress: two actors in the same batch sharing an image ID caused EF Core to flip the image entity from Added → Modified, generating an UPDATE against a non-existent row; fixed by pre-loading existing image IDs per batch and using `db.PrdbActorImages.Add()` directly

### Dead Ends
- *(none)*

---

## feature/wanted-list-view — 2026-03-28

### Done
- Added `GET /api/prdb-wanted-videos` — paged, server-side filtered list (search, fulfilment status, site, actor); sorted newest-added first
- Added `GET /api/prdb-wanted-videos/filter-options` — returns distinct sites and actors present in the wanted list for dropdown population
- New **Wanted** nav item under PRDB; view shows thumbnail, site, title, release date, added date, and fulfilment status chip; defaults to unfulfilled-only filter
- Added **Safe for work** toggle in Settings → Display; blurs all prdb-sourced images app-wide via a shared `useSfwMode` composable; persisted in `AppSettings` with EF migration

### Dead Ends
- *(none)*

---

## feature/wanted-list-sync — 2026-03-28

### Done
- Added `PrdbWantedVideo` entity (VideoId as PK/FK, full fulfilment fields) with EF migration
- Added `PrdbWantedVideoSyncService`: full-replace sync every 15 min — fetches all wanted videos from prdb, creates `PrdbVideo`/`PrdbSite` stubs for unknown videos (≤50/run), upserts entries, deletes removed ones
- Extended prdb Status page with a Wanted List Sync card (total/unfulfilled/fulfilled/pending detail, last synced, Run Now button)
- Added `WantedVideos` count to Library section of status page
- Fixed `DbUpdateConcurrencyException` in actor detail sync batch — conflicting entries are now detached and retried on the next run

### Dead Ends
- *(none)*

---

## feature/video-sync — 2026-03-28

### Done
- Synced favorite sites from prdb `/favorite-sites`; `IsFavorite`/`FavoritedAtUtc` on `PrdbSite` now populated
- Synced favorite actors from prdb `/favorite-actors`; missing favorite actors fetched via `/actors/{id}` and inserted automatically
- Video sync extended to fetch all videos per favorite actor via `?ActorId=`
- Add/remove favorites via `POST`/`DELETE /api/prdb-sites/{id}/favorite` and `…/prdb-actors/{id}/favorite`, proxied to prdb API
- Clickable star toggle in sites and actors tables; row removed immediately on un-favorite in favorites-only view
- Sites and actors views default to favorites-only filter with empty-state alert
- Actor backfill via `SyncWorker`: 5 000 actors per 15-min run (oldest-first), switches to `CreatedAfter`-based new-actor check on completion
- New `/prdb/status` page with Actor Backfill progress card (Run Now button) and Rate Limits card (hourly/monthly from prdb `/rate-limit`)
- Added `PrdbActorSyncPage`, `PrdbActorLastSyncedAt`, `PrdbActorTotalCount` to `AppSettings` with migrations

### Dead Ends
- Initial approach had no `/favorite-actors` endpoint in the OpenAPI spec — spec was updated and implementation followed
- `/videos` originally lacked `ActorId` filter — spec updated and actor video fetching added
- Backfill originally processed 500 actors (1 page) per run — raised to 5 000 (10 pages) after confirming rate limit headroom

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
