# Changelog

Entries are grouped by feature branch, newest first.
See [`docs/changelog/`](docs/changelog/) for archived entries.

---

## feature/copper-wave-dust — 2026-04-04

### Done
- Extended the incremental PreDb sync to re-fetch entries from the previous 7 days on every run, so entries that arrived without a video link get their `PrdbVideoId` populated once prdb.net links them

### Dead Ends
- *(none)*

---

## feature/prenames-view — 2026-04-03

### Done
- Added "Predb" nav entry to the PRDB navigation group (above Status), routing to `/prdb/prenames`
- New Prenames view lists prenames grouped by video with a default 7-day release date range filter (From / To); supports optional keyword search (min 3 chars), debounced
- List titles render as "Site - Video"; prename matches shown as inline chips; clicking a row navigates to the video detail page
- Backend `GET /api/prdb-prenames/search` queries the local `PrdbVideoPreNames` table, supporting keyword (`LIKE`) and date range (`releaseDateFrom` / `releaseDateTo`) filters, grouped by video and capped at 500 groups
- Mobile-friendly: filter row collapses behind the filter action button on small screens

### Dead Ends
- *(none)*

---

## feature/improve-indexer-matching — 2026-04-03

### Done
- Improved indexer row title matching to normalize separator characters (`.`, `-`, `_`) to spaces before comparison, so `Some.Scene.Title` now matches `Some-Scene-Title` and `Some Scene Title`
- Normalization is applied on both sides (indexer row titles and prenames) including in the DB pre-filter via EF-translatable `string.Replace`, with no schema changes required
- Applies consistently to both the background match run and the debug diagnostic endpoint
- Added three new integration tests covering cross-separator matching scenarios

### Dead Ends
- *(none)*

---

## feature/copper-wave-spark — 2026-04-03

### Done
- Added video-link indicator to the indexer rows table — a green `mdi-link-variant` icon when a row is matched to a known video, muted `mdi-link-variant-off` when unmatched; title column now grows to fill available space instead of truncating at a fixed 380 px
- Added **Video link** filter (Linked / Unlinked) to the indexer rows view, backed by a `HasVideoLink` query parameter that filters against `IndexerRowMatches` server-side
- Exposed `prdbVideoId` (nullable) on `IndexerRowResponse` so the frontend can determine link state without a separate request
- Fixed the indexer row match debug dialog in the prdb status page: the table now includes an **Indexer** column showing which indexer each row came from, and the previously misnamed "Indexer Title" column is correctly labelled "NZB Title"

### Dead Ends
- *(none)*

---

## feature/batch-video-detail-sync — 2026-04-03

### Done
- Switched video detail sync from individual `GET /videos/{id}` calls to the new `POST /videos/batch` endpoint (up to 50 IDs per request), reducing API requests from 200 to 20 per run while increasing throughput from 200 to 1 000 videos per 15-minute cycle
- Batch-loaded existing image IDs and VideoActor join pairs per batch to prevent EF Core identity map collisions
- Added missing-ID handling: videos silently omitted by the batch API have their `DetailSyncedAtUtc` stamped so they are not retried on the next run
- Updated local prdb.net OpenAPI spec (`docs/external/prdb-openapi-v1.json`) to include the new `/videos/batch` endpoint

### Dead Ends
- *(none)*

---

## feature/prdb-reset-button — 2026-03-30

### Done
- Added **Reset DB** button to the prdb.net settings tab — opens a confirm dialog and wipes all locally cached prdb.net data (sites, networks, videos, actors, wanted list, download logs, indexer row matches) then resets all sync cursors so the next run starts from scratch; API credentials and other settings are untouched
- Added **top-indexer stats** to the Indexer Row Match status card — shows total row count and last-7-days count for the three most active indexers
- Added **Run Now** button to the Library status card — manually triggers the full library sync (sites, networks, favourite sites, favourite actors, videos) without waiting for the 15-minute background tick; last sync and next sync times shown at the bottom of the card
- Moved the **info** button from the app bar into the Library card title, and the **refresh** button into the Rate Limits card title

### Dead Ends
- *(none)*

---

## feature/video-view — 2026-03-30

### Done
- Added **Videos** nav item and `/prdb/videos` route with a new `PrdbVideosView` — responsive card grid (1/2/3/4 cols) showing thumbnail, site title, release date, actor count, and a Wanted/Fulfilled badge overlay
- Added `GET /api/prdb-videos` list endpoint (filters: search, siteId; ordered by release date desc) and `GET /api/prdb-videos/filter-options` for the site dropdown; `PrdbVideoListResponse` includes `IsWanted` and `IsFulfilled` via correlated subqueries
- Added `POST /api/prdb-wanted-videos/{videoId}` to add a video to the wanted list (calls prdb.net then upserts locally; idempotent)
- Replaced card navigation with a tap-to-open **action overlay** on both the Videos and Wanted views; overlay buttons: Show details, Mark as fulfilled/unfulfilled (Wanted view) / Add or Remove wanted (Videos view), Filter by site (Videos view), Remove wanted

### Dead Ends
- *(none)*

---

## feature/wanted-list-mobile-friendly — 2026-03-30

### Done
- Replaced Wanted list `v-data-table-server` with an image-focused responsive card grid (1/2/3/4 cols at xs/sm/md/lg); each card shows a 16:9 thumbnail, fulfilled/unfulfilled badge overlay, edit button overlay, site title, video title, and release date; `v-pagination` replaces the table footer; page size 24
- Replaced Sites `v-list` with the same responsive card grid; each card shows a 16:9 site thumbnail (sourced from the most recently released video for that site), favourite star overlay, network title, site title, and video count; navigates to site videos on tap
- Added `ThumbnailCdnPath` to `PrdbSiteResponse` — populated via a correlated subquery on `Videos → Images` ordered by release date
- Reordered PRDB nav: Wanted now appears first, above Sites

### Dead Ends
- *(none)*

---

## feature/mobile-friendly-part-2 — 2026-03-30

### Done
- Replaced Sites `v-data-table-server` with a two-line `v-list` + `v-pagination` (star toggle prepend, network · video count subtitle, movie icon append, max-width 900px)
- Replaced Site Videos `v-data-table-server` with a `v-list` + `v-pagination` (click navigates to video detail, pre-names count chip in append slot); added mobile filter panel toggle via `useFilterPanel`
- Replaced Actors `v-data-table-server` with a responsive card grid (3 cols mobile / 4 sm / 6 md+): 96px circular avatar, gender label, birthday, star toggle as absolute top-right overlay; images blurred in SFW mode
- Sync: actor summary sync now stores `ProfileImageUrl` as a `PrdbActorImage` on first insert; `PrdbActorResponse` now returns the first image URL

### Dead Ends
- *(none)*

---

## feature/mobile-friendly — 2026-03-30

### Done
- Replaced `v-data-table` with responsive card grids in Indexers and Download Clients views (2-column on tablet+, single column on mobile, max-width 900px centered)
- Replaced `v-data-table` with a `v-list` two-line layout in the Downloads view for high-density mobile display (status dot, name, client/date subtitle, inline progress bar for active downloads)
- Replaced SFW toggle in the app bar with a contextual page action button — each view registers its primary action (e.g. New Indexer, New Client) on mount and clears it on unmount
- Moved Refresh buttons from Status and Health views into the app bar; moved Sync button from Sites view into the app bar with a loading spinner
- Added collapsible filter panel toggle (`mdi-tune`) in the app bar for Wanted, Downloads, Sites, and Actors views — visible on mobile only, with a badge dot when non-default filters are active; filters collapse/expand with `v-expand-transition`
- Moved Status view info button into the app bar as a second action (opens a dialog); removed broken "next run" countdown text

### Dead Ends
- *(none)*

---

## feature/wanted-download-improvements — 2026-03-30

### Done
- Downloads view: Started/Completed columns now show hours and minutes (e.g. `Mar 30, 14:23`) with no year; date/time no longer wraps in table cells
- Downloads detail dialog: storage path now wraps fully instead of being truncated
- Added responsive `v-app-bar` across all screen sizes: hamburger toggle, page title (`PMManager — {page}` on desktop, `{page}` on mobile), and SFW mode toggle button (eye icon)
- Nav drawer collapses to an overlay on mobile and auto-closes after navigation; starts expanded on desktop
- Removed all in-page `<h1>` headings — the app bar is now the single source of truth for the page title
- Removed PMManager branding header from the nav drawer (redundant with app bar)
- Grouped Indexers, Download Clients, and Downloads under a "Usenet" nav section; Health moved below Settings
- Removed Items feature entirely: frontend view, route, API types, backend controller, entity, DbSet, and EF migration to drop the table; default route now redirects to `/prdb/wanted`

### Dead Ends
- *(none)*

---

## feature/claud-commands-improvement — 2026-03-29

### Done
- `start-feature` command no longer asks for branch name confirmation — proceeds immediately after slugifying the argument
- `start-feature` generates a random 3-word slug when invoked with no argument
- `start-feature` safety check now compares against `origin/develop` (was `origin/HEAD`) to avoid false positives
- `finish-feature` command now writes and commits a changelog entry automatically when none exists, instead of stopping to ask the user

### Dead Ends
- *(none)*

---

## feature/sync-improvements — 2026-03-29

### Done
- Reorganised the Settings page into three tabs: **General** (preferred video quality, safe-for-work toggle), **PRDB.net** (API key and URL), and **Folder Mapping** (new); the Save button is hidden on the Folder Mapping tab since that tab manages its own API calls
- Added **Folder Mapping** feature: `FolderMapping` entity with unique indexes on both `OriginalFolder` and `MappedToFolder`, EF migration, full CRUD API (`GET`/`POST`/`PUT`/`DELETE /api/folder-mappings`), and a tab UI with add/edit/delete dialog and a collapsible info panel explaining when mappings are needed (download client on Docker/remote with different mount paths)
- Added **Download Log** tracking: `DownloadLog` entity (`DownloadStatus` enum, progress fields, storage path, filenames, error message), EF migration; `DownloadClientSender` now captures the SABnzbd `nzo_id` and NZBGet NZBID as `ClientItemId`; the send endpoint creates a `DownloadLog` on success when `IndexerRowId` is provided
- Added `DownloadPollingWorker` background service: 5-second initial delay then 20-second polling interval; `SabnzbdPoller` checks queue then history, maps statuses, and best-effort extracts filenames from stage_log; `NzbgetPoller` mirrors this via JSON-RPC (`listgroups` + `history`); both share a common `DownloadPollResult` type
- When a download reaches `Completed`, the worker automatically marks the linked `PrdbWantedVideo` as fulfilled via the `IndexerRowMatch` chain
- Added **Downloads** view (`/downloads`): table with status chips, progress bars with byte counts, started/completed timestamps; filters for search, status, and an active-only toggle; clicking a row opens a detail dialog with storage path, file list, and error; auto-refreshes every 20 seconds while the view is mounted

### Dead Ends
- *(none)*

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
