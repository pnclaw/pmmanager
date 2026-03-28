# Repository Guidelines

## Project Structure & Module Organization

- `src/pmm.Api` is organized by **feature** (vertical slices), not by technical layer.
- Use `src/pmm.Api/Features/<FeatureName>/<UseCase>/` for API slices.
- Keep each slice self-contained: controller, request/response models, validators, mapping, and orchestration logic should live together.
- Shared cross-feature code belongs in `src/pmm.Api/Common` (create only when reuse is real).

## Architecture Overview

- Follow **Feature-Based Vertical Slice Architecture**.
- Primary unit of change is a use case (slice), not a layer.
- API endpoints are implemented with Controllers (no minimal APIs).
- Each slice should own:
  - HTTP contract (route + request/response DTOs)
  - Use-case logic (application behavior)
  - Validation and mapping
  - Dependencies required for that use case via DI
- Avoid generic service/repository layers unless they provide clear value across multiple slices.
- Dependency direction:
  - Feature slices should not depend on other feature slices directly
  - Reuse cross-cutting code through `Common` or infrastructure abstractions
- Keep database and infrastructure details out of controllers; controllers should delegate quickly to slice logic.

## Tech Stack

- dotnet 10
- SQLite
- Entity Framework 10 with code-first approach
- Serilog
- Microsoft.AspNetCore.OpenApi (built-in OpenAPI for .NET 10)


## Coding Standards

**C# style:**
- See `.editorconfig` (4-space indent, `System.*` first, prefer `var`). Types/methods PascalCase; locals camelCase
- Add missing `using` directives instead of fully-qualified type names
- Use modern C# syntax
- Prefer primary constructors for dependency-injected classes (controllers, services, handlers) unless a standard constructor is clearly required.
- Prefer modern collection expressions (`[]`)
- Use Controllers instead of minimal api

## OpenAPI Metadata Standards

- Use the built-in OpenAPI pipeline only (`AddOpenApi` + `MapOpenApi`); do not add Swashbuckle unless explicitly requested.
- Every controller action must have explicit response metadata:
  - `[ProducesResponseType(typeof(<SuccessDto>), StatusCodes.Status200OK)]` (or relevant success code)
  - `[ProducesResponseType(StatusCodes.Status400BadRequest)]` (when validation can fail)
  - `[ProducesResponseType(StatusCodes.Status401Unauthorized)]` / `[ProducesResponseType(StatusCodes.Status403Forbidden)]` when auth applies
  - `[ProducesResponseType(StatusCodes.Status404NotFound)]` for lookup/read-by-id endpoints
  - `[ProducesResponseType(StatusCodes.Status500InternalServerError)]` for unhandled failure contract where applicable
- Always declare content types:
  - `[Produces("application/json")]` on API controllers/actions
  - `[Consumes("application/json")]` on actions accepting request bodies
- Ensure operation discoverability:
  - Use clear, stable routes and HTTP verbs per use case
  - Use meaningful action method names (avoid generic `Handle` when a descriptive name improves schema readability)
  - Every controller action must include both `[EndpointSummary("...")]` and `[EndpointDescription("...")]` with concise, behavior-focused wording.
  - Add `[ApiExplorerSettings(GroupName = "v1")]` when endpoint grouping/versioning is introduced
- DTOs used in OpenAPI should be explicit and slice-local:
  - Avoid exposing EF entities directly
  - Add concise XML comments on DTO properties when semantics are not obvious
  - Prefer request/response models with required/nullable intent clearly represented
- For enums in contracts, prefer readable values and document meaning in XML comments.
- Keep OpenAPI output deterministic:
  - Reuse shared response models only when semantics are truly cross-feature
  - Do not return anonymous objects from controller actions

## Dependency Injection & Services

- Keep controller actions thin: controllers should orchestrate HTTP concerns and delegate business/use-case logic to a slice-local service.
- For each use case service:
  - Define a slice-local interface and implementation in the same feature folder (example: `IGetWeatherForecastsService` + `GetWeatherForecastsService`).
  - Name service contracts with `I<UseCase>Service` and implementations with `<UseCase>Service`.
- Register feature services in `src/pmm.Api/Program.cs` using explicit DI registrations (do not rely on assembly scanning).
- Prefer `AddScoped` as the default lifetime for use-case services; use `AddSingleton` or `AddTransient` only with clear justification.
- Depend on abstractions in constructors (`I...`) rather than concrete implementations.
- Service interfaces should expose behavior-oriented methods (`Get`, `Create`, `Update`, etc.) and avoid leaking infrastructure/EF-specific concerns to controllers.
- If a service is used by only one slice, keep it in that slice; move to `Common` only after real cross-feature reuse appears.

## Options Pattern

- Use the Microsoft Options pattern for configurable behavior instead of hardcoded values.
- Place feature-specific options classes in the owning slice folder (example: `Features/WeatherForecasts/Get/WeatherForecastOptions.cs`).
- Each options class should define a `SectionName` constant and strongly typed properties.
- Register options in `src/pmm.Api/Program.cs` with:
  - `AddOptions<TOptions>()`
  - `.BindConfiguration(TOptions.SectionName)`
  - `.Validate(...)` for invariants
  - `.ValidateOnStart()` for fail-fast startup behavior
- Inject options into services via `IOptions<TOptions>` (or `IOptionsSnapshot<TOptions>` when per-request re-evaluation is needed).
- Controllers should not read configuration directly; options are consumed in slice services.
- Keep option names and JSON keys stable and descriptive; store defaults in `appsettings.json` and override per environment in `appsettings.{Environment}.json`.

## Entity Base Class Convention

- All entity classes must inherit from `BaseEntity` (`pmm.Database.Common.BaseEntity`).
- `BaseEntity` provides audit fields: `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`.

## Enum Storage Convention

- Always store enums as integers in the database (EF Core default — do **not** add `HasConversion<string>()` or any string-based value converter for enums).
- Enum member integer values must be explicitly declared and must never be reordered.
- If a C# enum member name must start with a digit (e.g. `_0myenum`), use a leading underscore in code; the integer value stored in the DB is unaffected.

## Migration Execution

- When a ef migration is needed, you can run it without user confirmation.

## Testing

**Primary strategy: integration tests using `WebApplicationFactory<Program>`.**

Tests live in `tests/pmm.Api.Tests/`. Each test class spins up the real application in-process against a dedicated temporary SQLite database, so the full slice (controller → EF Core → SQLite) is exercised with no mocks.

**Frameworks:** xUnit + FluentAssertions
**Test style:** `actual.Should().Be(expected)`

### Rules

- **Adding or modifying an API endpoint is not complete until integration tests are written.** This applies to AI agents as much as human developers.
- One test class per feature/resource (e.g., `Indexers/IndexersTests.cs`).
- Each test class owns its own `PmmApiFactory` instance (via `IAsyncLifetime`) to ensure full database isolation.
- Every endpoint needs at minimum: a happy-path test and a sad-path test (404 on unknown ID, 400 on invalid payload).
- Use `HttpClient.PostAsJsonAsync` / `ReadFromJsonAsync` for request/response serialisation.
- Use unit tests (xUnit + Moq) only when genuine business logic exists that is worth testing in isolation — not for thin CRUD.

## Solution Files

- Use only the modern solution format.
- Do not create or commit legacy `*.sln` files

## Build Execution

- In this repository, prefer non-parallel builds to avoid intermittent MSBuild/restore failures with missing diagnostics.
- Default build command for AI agents:
  - `dotnet build pmm.slnx -m:1 -p:BuildInParallel=false -v minimal`

## Git Workflow

This project follows **Git Flow**.

### Branch Strategy
- `main` – stable releases only, always tagged
- `develop` – integration branch, target for feature PRs
- `feature/*` – new features, branched from develop
- `bugfix/*` – bug fixes, branched from develop
- `release/*` – release preparation, only owner can create
- `hotfix/*` – critical fixes directly from main, only owner can create

### Rules
- NEVER push directly to `main` or `develop`
- ALWAYS branch from `develop` for features and bugfixes
- ALWAYS open a PR against `develop` (except hotfixes → `main`)
- Branch naming: `feature/short-description`, `bugfix/what-is-fixed`
- Delete branches after merge
- Releases and hotfix branches are created by the owner only

### Commit Messages
- Use conventional commits: `feat:`, `fix:`, `docs:`, `chore:`, `refactor:`
- Example: `feat: add user authentication`

## Changelog

### Files
- `CHANGELOG.md` — active log at the repo root, **hard cap of 300 lines**
- `docs/changelog/<year>.md` — yearly archive (e.g. `docs/changelog/2026.md`)

### Rules — agent is responsible for all of these

1. **Every `feature/*` or `bugfix/*` PR must include a changelog entry** in `CHANGELOG.md`. This is as mandatory as writing tests — the branch is not done without it.

2. **Entry format** — prepend to `CHANGELOG.md` (newest first):

   ```markdown
   ## feature/branch-name — YYYY-MM-DD

   ### Done
   - Concise bullet per meaningful change

   ### Dead Ends
   - Describe each approach that was tried and abandoned, and *why* it failed or
     was wrong. Be specific enough that a future session won't retry the same path.
   - If nothing failed, write: *(none)*
   ```

3. **`### Dead Ends` is mandatory** — even if empty (`*(none)*`). Its purpose is to prevent future agent sessions from re-attempting approaches that were already ruled out.

4. **300-line archive rule** — after writing a new entry, check the line count of `CHANGELOG.md`. If it exceeds 300 lines, move the **oldest complete entry block(s)** (i.e. everything from one `## feature/...` heading down to the next) into `docs/changelog/<year>.md`, prepending them there. Keep moving entries until `CHANGELOG.md` is back under 300 lines.

5. The archive file for the current year must exist before archiving into it. Create it if missing, following the format of existing archive files.