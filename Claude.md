# Git Workflow

This project follows **Git Flow**.

## Branch Strategy
- `main` – stable releases only, always tagged
- `develop` – integration branch, target for feature PRs
- `feature/*` – new features, branched from develop
- `bugfix/*` – bug fixes, branched from develop
- `release/*` – release preparation, only owner can create
- `hotfix/*` – critical fixes directly from main, only owner can create

## Rules
- NEVER push directly to `main` or `develop`
- ALWAYS branch from `develop` for features and bugfixes
- ALWAYS open a PR against `develop` (except hotfixes → `main`)
- Branch naming: `feature/short-description`, `bugfix/what-is-fixed`
- Delete branches after merge
- Releases and hotfix branches are created by the owner only

## Commit Messages
- Use conventional commits: `feat:`, `fix:`, `docs:`, `chore:`, `refactor:`
- Example: `feat: add user authentication`