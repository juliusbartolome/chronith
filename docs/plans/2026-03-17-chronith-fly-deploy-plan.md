# Chronith Fly.io Minimum-Set Deployment — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Deploy the Chronith API to Fly.io backed by Neon (managed Postgres) and Upstash (managed Redis), with zero code changes.

**Architecture:** A single `fly.toml` is the only new file. The existing `Dockerfile` is used as-is. EF Core migrations run automatically on startup (`Program.cs:254`). Observability and Redis are already feature-flagged and disabled/enabled via environment variables.

**Tech Stack:** Fly.io CLI (`flyctl`), Neon serverless Postgres, Upstash serverless Redis, existing .NET 10 `Dockerfile`.

**Design doc:** `docs/plans/2026-03-17-chronith-fly-deploy-design.md`

---

### Task 0: Prerequisites — accounts and connection strings

This task is manual setup. No code changes.

**Step 1: Install the Fly.io CLI if not already installed**

```bash
brew install flyctl
fly version
```

Expected: `fly v...` printed.

**Step 2: Authenticate with Fly.io**

```bash
fly auth login
```

Follow the browser prompt to log in.

**Step 3: Create a Neon project**

1. Go to https://neon.tech and sign up / log in (free tier).
2. Create a new project named `chronith`.
3. In the project dashboard → **Connection Details**, select **Npgsql** as the connection string format.
4. Copy the connection string. It looks like:
   ```
   Host=ep-xxx.region.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=xxx;Ssl Mode=Require;Trust Server Certificate=true
   ```
5. Save it — you will need it in Task 2.

**Step 4: Create an Upstash Redis database**

1. Go to https://console.upstash.com and sign up / log in (free tier).
2. Create a new Redis database named `chronith`, region: `ap-southeast-1` (Singapore — matches Fly.io `sin` region).
3. Enable **TLS**.
4. In the database details, find the **StackExchange.Redis** connection string under "Connect to your database → .NET".
   It looks like:
   ```
   <host>:<port>,password=<password>,ssl=True,abortConnect=False
   ```
5. Save it — you will need it in Task 2.

---

### Task 1: Create `fly.toml`

**Files:**

- Create: `fly.toml` (repo root)

**Step 1: Create the file**

Create `fly.toml` at the repo root with the following content:

```toml
app = "chronith-api"
primary_region = "sin"

[build]

[env]
  ASPNETCORE_ENVIRONMENT = "Production"
  Database__Provider = "PostgreSQL"
  Payments__Provider = "Stub"
  Payments__Currency = "PHP"
  Redis__Enabled = "true"
  Observability__EnableTracing = "false"
  Observability__EnableMetrics = "false"
  Observability__ServiceName = "chronith-api"
  Webhooks__DispatchIntervalSeconds = "10"
  Webhooks__HttpTimeoutSeconds = "10"
  RateLimiting__Auth__PermitLimit = "100"
  RateLimiting__Auth__WindowSeconds = "300"

[http_service]
  internal_port = 8080
  force_https = true
  auto_stop_machines = "stop"
  auto_start_machines = true
  min_machines_running = 0

  [http_service.concurrency]
    type = "connections"
    hard_limit = 25
    soft_limit = 20

  [[http_service.checks]]
    grace_period = "40s"
    interval = "30s"
    method = "GET"
    path = "/health/live"
    port = 8080
    timeout = "5s"
    type = "http"

[[vm]]
  memory = "256mb"
  cpu_kind = "shared"
  cpus = 1
```

**Step 2: Verify the file looks correct**

```bash
cat fly.toml
```

Expected: file prints without errors.

**Step 3: Commit**

```bash
git add fly.toml
git commit -m "feat(deploy): add fly.toml for Fly.io minimum-set deployment"
```

---

### Task 2: Register app and set secrets

No files changed. All steps are CLI commands.

**Step 1: Register the app name on Fly.io**

```bash
fly apps create chronith-api
```

Expected: `New app created: chronith-api`

> If `chronith-api` is already taken, pick a unique name (e.g. `chronith-api-prod`) and update the `app =` field in `fly.toml` before proceeding.

**Step 2: Generate secrets locally**

```bash
# Jwt__SigningKey (at least 32 chars)
openssl rand -hex 32

# Security__EncryptionKey (Base64-encoded 32 bytes)
openssl rand -base64 32
```

Copy both outputs.

**Step 3: Set all secrets**

Replace the placeholders with your actual values:

```bash
fly secrets set \
  Jwt__SigningKey="<output of openssl rand -hex 32>" \
  Security__EncryptionKey="<output of openssl rand -base64 32>" \
  "Database__ConnectionString=Host=ep-xxx...;Database=neondb;Username=xxx;Password=xxx;Ssl Mode=Require;Trust Server Certificate=true" \
  "Redis__ConnectionString=<host>:<port>,password=<password>,ssl=True,abortConnect=False" \
  --app chronith-api
```

Expected: `Secrets are staged for the first deployment`

**Step 4: Verify secrets are registered (values are not shown)**

```bash
fly secrets list --app chronith-api
```

Expected: four secrets listed (`Jwt__SigningKey`, `Security__EncryptionKey`, `Database__ConnectionString`, `Redis__ConnectionString`).

---

### Task 3: Deploy

**Step 1: Run the deployment**

```bash
fly deploy --app chronith-api
```

Fly will:

1. Build the image using the existing `Dockerfile` (multi-stage .NET 10 build)
2. Push the image to the Fly registry
3. Start the machine
4. Wait for the health check at `/health/live` to return HTTP 200

Expected output ends with:

```
--> v1 deployed successfully
```

> First deploy takes ~3–5 minutes for the .NET SDK build stage.

**Step 2: Verify the machine is running**

```bash
fly status --app chronith-api
```

Expected: one machine listed with status `started`.

**Step 3: Check health endpoints**

```bash
# Liveness
curl https://chronith-api.fly.dev/health/live

# Readiness (database + redis + background-services)
curl https://chronith-api.fly.dev/health/ready
```

Expected for `/health/live`: HTTP 200, body `Healthy`

Expected for `/health/ready`:

```json
{
  "status": "Healthy",
  "checks": [
    { "name": "database", "status": "Healthy" },
    { "name": "background-services", "status": "Healthy" },
    { "name": "redis", "status": "Healthy" }
  ]
}
```

**Step 4: Check logs if health check fails**

```bash
fly logs --app chronith-api
```

Look for migration errors (Neon connection string wrong) or Redis errors (Upstash TLS format wrong).

---

### Task 4: Smoke test

**Step 1: Hit the Swagger / OpenAPI endpoint**

```bash
curl -s https://chronith-api.fly.dev/openapi.json | head -5
```

Expected: JSON starting with `{"openapi":"3.0"...`

**Step 2: Create a tenant (verifies DB write + JWT)**

```bash
curl -s -X POST https://chronith-api.fly.dev/v1/tenants \
  -H "Content-Type: application/json" \
  -d '{"name":"Smoke Test Tenant","slug":"smoke-test","email":"test@example.com","password":"SmokeTest123!"}' \
  | jq .
```

Expected: `201 Created` with tenant object including `id`.

**Step 3: Log in (verifies JWT issuance)**

```bash
curl -s -X POST https://chronith-api.fly.dev/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"tenantSlug":"smoke-test","email":"test@example.com","password":"SmokeTest123!"}' \
  | jq .accessToken
```

Expected: a JWT string printed.

**Step 4: Commit any fixes made during smoke test, if any**

```bash
git add -p
git commit -m "fix(deploy): ..."
```

---

### Task 5: Update AGENTS.md and README with deployed URL

**Files:**

- Modify: `README.md`
- Modify: `AGENTS.md`

**Step 1: Add deployed URL to README**

In `README.md`, under a new `## Deployed` section (after the intro), add:

```markdown
## Deployed

| Environment         | URL                                       |
| ------------------- | ----------------------------------------- |
| Production (Fly.io) | https://chronith-api.fly.dev              |
| OpenAPI spec        | https://chronith-api.fly.dev/openapi.json |
| Health              | https://chronith-api.fly.dev/health/ready |
```

**Step 2: Add Fly.io deploy command to AGENTS.md Key Commands**

In `AGENTS.md` section 10 (Key Commands), add a `# Deploy` block:

```bash
# Deploy to Fly.io
fly deploy --app chronith-api

# Check status
fly status --app chronith-api

# Tail logs
fly logs --app chronith-api

# Update a secret
fly secrets set JWT_SIGNING_KEY="..." --app chronith-api
```

**Step 3: Commit**

```bash
git add README.md AGENTS.md
git commit -m "docs: add Fly.io deployment URL and commands"
```
