# Podman Migration Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace all local-development Docker references with Podman in `README.md` and `AGENTS.md`, leaving CI untouched.

**Architecture:** Pure documentation/config change. No source code modifications. No tests required. CI pipeline (`ci.yml`) is intentionally excluded — GitHub Actions runners provide Docker natively.

**Tech Stack:** Podman Desktop v1.x+ (includes `podman compose` built into Podman 4+), Docker Compatibility socket at `/var/run/docker.sock`.

---

### Task 1: Update `README.md` — Prerequisites

**Files:**

- Modify: `README.md:56` (Prerequisites section)

**Step 1: Edit the prerequisites block**

Replace:

```markdown
- [Docker](https://docs.docker.com/get-docker/) and Docker Compose (for local development and load tests)
```

With:

```markdown
- [Podman Desktop](https://podman-desktop.io/) v1.x+ (includes Podman 4+ with `podman compose`)
  - After installation, enable **Docker Compatibility** in Podman Desktop → Settings → Docker Compatibility.
    This exposes `/var/run/docker.sock` so Testcontainers and `act` work without any extra configuration.
```

**Step 2: Verify the change looks correct**

Open `README.md` and confirm the Prerequisites section now mentions Podman Desktop with the Docker Compatibility note.

---

### Task 2: Update `README.md` — Getting Started

**Files:**

- Modify: `README.md:60-64` (Getting Started section)

**Step 1: Edit the Getting Started compose command**

Replace:

````markdown
**Run with Docker Compose:**

```bash
docker compose up
```
````

````

With:
```markdown
**Run with Podman Compose:**

```bash
podman compose up
````

````

**Step 2: Verify**

Confirm the Getting Started section now reads `podman compose up`.

---

### Task 3: Update `README.md` — Load Tests

**Files:**
- Modify: `README.md:322-344` (Load Tests section)

**Step 1: Replace `docker compose` with `podman compose`**

Replace:
```markdown
# Start the stack
docker compose up -d
````

With:

```markdown
# Start the stack

podman compose up -d
```

**Step 2: Replace `docker exec` with `podman exec`**

Replace:

```markdown
docker exec -i chronith-postgres-1 psql -U chronith -d chronith <<'SQL'
```

With:

```markdown
podman exec -i chronith-postgres-1 psql -U chronith -d chronith <<'SQL'
```

**Step 3: Verify**

Confirm the Load Tests section has no remaining `docker` references (the k6 run command doesn't reference docker, so only the two above need changing).

---

### Task 4: Update `AGENTS.md` — Key Commands section

**Files:**

- Modify: `AGENTS.md` (Section 10 — Key Commands)

**Step 1: Replace `docker compose up -d`**

Replace:

```markdown
docker compose up -d # Start full stack
docker compose down # Stop
```

With:

```markdown
podman compose up -d # Start full stack
podman compose down # Stop
```

**Step 2: Add prerequisite note above the docker compose commands**

Add a comment above the compose commands (within the Key Commands code block):

```markdown
# Requires Podman Desktop with Docker Compatibility enabled (Settings → Docker Compatibility)

# This provides /var/run/docker.sock — Testcontainers and act auto-discover it

podman compose up -d # Start full stack
podman compose down # Stop
```

**Step 3: Verify**

Confirm the Key Commands section has no remaining `docker compose` references.

---

### Task 5: Commit

**Step 1: Stage and commit all changes**

```bash
git add README.md AGENTS.md docs/plans/2026-03-17-podman-migration-design.md
git commit -m "docs: migrate local dev tooling from Docker to Podman"
```

**Step 2: Verify**

```bash
git log -1 --oneline
```

Expected: commit appears with the message above.
