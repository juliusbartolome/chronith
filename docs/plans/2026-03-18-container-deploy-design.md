# Container Deployment Design — API + Dashboard

**Date:** 2026-03-18

## Goal

Deploy both the Chronith API and the Chronith Dashboard as Docker containers on Azure App Service B1, with images hosted on GitHub Container Registry (`ghcr.io`) and automated CD via GitHub Actions.

## Context

- API is currently deployed as a zip (`dotnet publish` → zip → `az webapp deploy`) on an F1 free-tier App Service Plan.
- Dashboard (`dashboard/`) is a Next.js 16 app with a standalone Docker build. It has never been deployed.
- Both apps already have working multi-stage Dockerfiles.
- The existing CI (`ci.yml`, `dashboard-ci.yml`) builds images but never pushes them.
- F1 does not support Docker containers. B1 does.

## Decisions

| Decision               | Choice                              | Rationale                                                                                   |
| ---------------------- | ----------------------------------- | ------------------------------------------------------------------------------------------- |
| Container registry     | `ghcr.io/juliusbartolome/`          | Free, GitHub-native, no extra infra                                                         |
| Image visibility       | Public                              | No secrets in images; simplest Azure pull path (no registry credentials needed)             |
| App Service Plan       | Upgrade `asp-chronith-free` F1 → B1 | One plan hosts both apps (~$13/mo)                                                          |
| Dashboard web app name | `chronith-dashboard`                | Consistent naming with API                                                                  |
| CD mechanism           | Azure App Service CD webhook        | Azure pulls + restarts automatically when GitHub Actions calls the webhook after image push |

## Architecture

```
GitHub Actions (push to main)
  │
  ├─ build + push ghcr.io/juliusbartolome/chronith-api:latest
  │     └─→ Azure CD webhook → chronith-api.azurewebsites.net (container pull + restart)
  │
  └─ build + push ghcr.io/juliusbartolome/chronith-dashboard:latest
        └─→ Azure CD webhook → chronith-dashboard.azurewebsites.net (container pull + restart)
```

## Azure Resources

| Resource                               | Change                                                                                  |
| -------------------------------------- | --------------------------------------------------------------------------------------- |
| `asp-chronith-free` (App Service Plan) | Upgrade F1 → B1                                                                         |
| `chronith-api` (Web App)               | Switch from zip deploy to container; `ghcr.io/juliusbartolome/chronith-api:latest`      |
| `chronith-dashboard` (Web App)         | **New**; container `ghcr.io/juliusbartolome/chronith-dashboard:latest`; on same B1 plan |

## App Settings

### API (`chronith-api`) — changes only

| Setting         | Value  | Note                                            |
| --------------- | ------ | ----------------------------------------------- |
| `WEBSITES_PORT` | `8080` | Tells Azure which port the container listens on |

All existing settings (DB, Redis, JWT, EncryptionKey, etc.) are unchanged.

Remove: `--startup-file "dotnet Chronith.API.dll"` — the container `ENTRYPOINT` handles this.

### Dashboard (`chronith-dashboard`) — new app

| Setting                   | Value                                    |
| ------------------------- | ---------------------------------------- |
| `CHRONITH_API_URL`        | `https://chronith-api.azurewebsites.net` |
| `WEBSITES_PORT`           | `3000`                                   |
| `NODE_ENV`                | `production`                             |
| `NEXT_TELEMETRY_DISABLED` | `1`                                      |

## CI/CD Changes

### `ci.yml`

Add `deploy-api` job:

- Triggers on push to `main` only (after `docker-build` passes)
- Logs in to `ghcr.io` using `GITHUB_TOKEN`
- Builds and pushes `ghcr.io/juliusbartolome/chronith-api:latest`
- Calls Azure CD webhook to trigger pull + restart

### `dashboard-ci.yml`

Add `deploy-dashboard` job:

- Triggers on push to `main` only (after `docker-build` passes)
- Same pattern as `deploy-api`
- Image: `ghcr.io/juliusbartolome/chronith-dashboard:latest`

### New GitHub Actions Secrets

| Secret              | Purpose                                                                |
| ------------------- | ---------------------------------------------------------------------- |
| `AZURE_CREDENTIALS` | Service principal JSON for `az` CLI (used to retrieve CD webhook URLs) |

> The `GITHUB_TOKEN` built-in secret is sufficient for pushing to `ghcr.io` — no additional registry secret needed.

## What Does Not Change

- `Dockerfile` (API) — no modifications
- `dashboard/Dockerfile` — no modifications
- API app settings (DB, Redis, JWT, etc.)
- Local dev workflow (`docker-compose up`)
- All test workflows

## URLs

|           | URL                                            |
| --------- | ---------------------------------------------- |
| API       | `https://chronith-api.azurewebsites.net`       |
| Dashboard | `https://chronith-dashboard.azurewebsites.net` |
