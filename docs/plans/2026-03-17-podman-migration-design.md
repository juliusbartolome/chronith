# Podman Migration Design

**Date:** 2026-03-17
**Scope:** Local development tooling only

## Context

The project is switching from Docker Desktop to Podman Desktop for local container management. CI (GitHub Actions `ubuntu-latest`) continues to use Docker natively — no changes to `ci.yml`.

## Approach

Use **Podman Desktop with Docker Compatibility enabled**. Podman Desktop exposes a Docker-compatible socket at `/var/run/docker.sock`, which allows all existing tooling (Testcontainers, `act`) to discover it without any code changes or environment variable exports.

Local Compose operations switch from `docker compose` to `podman compose` (built into Podman 4+).

## Changes

### `README.md`

- Prerequisites: replace Docker Desktop with Podman Desktop (v4+) + Docker Compatibility note.
- Getting Started: `docker compose up` → `podman compose up`.
- Load Tests: `docker compose up -d` / `docker exec` → `podman compose up -d` / `podman exec`.
- CI/CD table: no change (CI uses Docker).

### `AGENTS.md` — Key Commands section

- `docker compose up -d` → `podman compose up -d`
- `docker compose down` → `podman compose down`
- Add prerequisite note: Docker Compatibility must be enabled in Podman Desktop.

## Files That Do Not Change

| File                          | Reason                                                        |
| ----------------------------- | ------------------------------------------------------------- |
| `docker-compose.yml`          | Podman Compose reads the same Compose spec                    |
| `docker-compose.override.yml` | Same                                                          |
| `Dockerfile`                  | OCI-compatible; no Docker-specific directives required        |
| `ci.yml`                      | GitHub Actions runners provide Docker natively                |
| Testcontainers fixtures       | Auto-discover `/var/run/docker.sock` via Docker Compatibility |

## Prerequisites for Contributors

1. Install [Podman Desktop](https://podman-desktop.io/) v1.x+ (includes Podman 4+).
2. Start a Podman Machine (created automatically on first launch).
3. Enable **Docker Compatibility** in Podman Desktop → Settings → Docker Compatibility.

No shell profile changes or `DOCKER_HOST` exports are needed.
