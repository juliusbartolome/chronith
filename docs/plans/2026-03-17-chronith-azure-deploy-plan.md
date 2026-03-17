# Chronith Azure App Service Free-Tier Deployment — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Deploy the Chronith API to Azure App Service F1 (free tier) backed by Neon (managed Postgres) and Upstash (managed Redis), with zero code changes.

**Architecture:** Azure App Service F1 on Linux uses the built-in DOTNETCORE:10.0 runtime stack (F1 does not support Docker containers). Deployment is via `dotnet publish` → zip → `az webapp deploy`. EF Core migrations run automatically on startup (`Program.cs:254`). Observability and Redis are already feature-flagged and controlled via app settings.

**Tech Stack:** Azure CLI (`az`), Azure App Service F1 Linux, Neon serverless Postgres, Upstash serverless Redis, .NET 10 SDK (`dotnet publish` + zip deploy).

**Design doc:** `docs/plans/2026-03-17-chronith-fly-deploy-design.md` (Fly.io design, but core decisions still apply)

**Key constraints:**

- F1 free tier: 60 CPU minutes/day, 1 GB RAM, shared infrastructure, no Always On, no Docker
- App sleeps after ~20 min inactivity → cold starts expected
- Web app name `chronith-api` must be globally unique in Azure — verify or choose alternate
- External services (Neon, Upstash) are already provisioned from the Fly.io plan

---

## Pre-flight Checklist

- [x] Azure CLI installed and authenticated (`az account show` works)
- [x] `.NET 10` supported: `az webapp list-runtimes --os linux | grep DOTNETCORE:10.0`
- [x] Neon connection string: `Host=ep-dark-glade-a17i5s2a-pooler.ap-southeast-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=npg_prky16vfICmV;Ssl Mode=Require;Trust Server Certificate=true`
- [x] Upstash connection string: `sacred-mastodon-74341.upstash.io:6379,password=gQAAAAAAASJlAAIncDE5Y2QxNDI0YjNhN2Q0YWNhYWNlNTM5MjhhNDBjYzI4MXAxNzQzNDE,ssl=True,abortConnect=False`
- [x] Secrets already generated: `Jwt__SigningKey`, `Security__EncryptionKey` (see `.env.fly`)

---

### Task 1: Create Azure resources

No files changed. All steps are Azure CLI commands.

**Step 1: Create the resource group**

```bash
az group create --name rg-chronith --location southeastasia
```

Expected: JSON output with `"provisioningState": "Succeeded"`.

**Step 2: Create the App Service Plan (F1 free tier, Linux)**

```bash
az appservice plan create \
  --name asp-chronith-free \
  --resource-group rg-chronith \
  --sku F1 \
  --is-linux
```

Expected: JSON output with `"status": "Ready"` and `"sku": { "name": "F1" }`.

**Step 3: Create the Web App**

```bash
az webapp create \
  --name chronith-api \
  --resource-group rg-chronith \
  --plan asp-chronith-free \
  --runtime "DOTNETCORE:10.0"
```

> If `chronith-api` is taken (globally unique across all Azure), use `chronith-api-ph` or `chronith-api-prod` instead and note the name change.

Expected: JSON output with `"state": "Running"` and `"defaultHostName": "chronith-api.azurewebsites.net"`.

---

### Task 2: Configure app settings and secrets

No files changed. All steps are Azure CLI commands.

**Step 1: Set non-secret app settings**

```bash
az webapp config appsettings set \
  --name chronith-api \
  --resource-group rg-chronith \
  --settings \
    ASPNETCORE_ENVIRONMENT="Production" \
    ASPNETCORE_URLS="http://+:8080" \
    Database__Provider="PostgreSQL" \
    Payments__Provider="Stub" \
    Payments__Currency="PHP" \
    Redis__Enabled="true" \
    Observability__EnableTracing="false" \
    Observability__EnableMetrics="false" \
    Observability__ServiceName="chronith-api" \
    Webhooks__DispatchIntervalSeconds="10" \
    Webhooks__HttpTimeoutSeconds="10" \
    RateLimiting__Auth__PermitLimit="100" \
    RateLimiting__Auth__WindowSeconds="300"
```

Expected: JSON listing all settings.

**Step 2: Set secrets as app settings**

```bash
az webapp config appsettings set \
  --name chronith-api \
  --resource-group rg-chronith \
  --settings \
    Jwt__SigningKey="b58ed59ec0abc8858c736c5d733f645ddc12f07a57de193beae43779795483e0" \
    "Security__EncryptionKey=33Y0KPsAEP3h51GM2OWJY/HEPPLYaP9A6MM9WLPKIdQ=" \
    "Database__ConnectionString=Host=ep-dark-glade-a17i5s2a-pooler.ap-southeast-1.aws.neon.tech;Database=neondb;Username=neondb_owner;Password=npg_prky16vfICmV;Ssl Mode=Require;Trust Server Certificate=true" \
    "Redis__ConnectionString=sacred-mastodon-74341.upstash.io:6379,password=gQAAAAAAASJlAAIncDE5Y2QxNDI0YjNhN2Q0YWNhYWNlNTM5MjhhNDBjYzI4MXAxNzQzNDE,ssl=True,abortConnect=False"
```

Expected: JSON listing all settings (secret values are shown in plain text here — Azure encrypts them at rest).

**Step 3: Set the startup command**

```bash
az webapp config set \
  --name chronith-api \
  --resource-group rg-chronith \
  --startup-file "dotnet Chronith.API.dll"
```

Expected: JSON with `"appCommandLine": "dotnet Chronith.API.dll"`.

**Step 4: Verify settings count**

```bash
az webapp config appsettings list \
  --name chronith-api \
  --resource-group rg-chronith \
  --query "length(@)"
```

Expected: `17` (13 non-secret + 4 secrets).

---

### Task 3: Build, publish, and deploy

**Step 1: Build the solution**

```bash
dotnet build src/Chronith.API/Chronith.API.csproj -c Release
```

Expected: `Build succeeded.`

**Step 2: Publish**

```bash
dotnet publish src/Chronith.API/Chronith.API.csproj \
  -c Release \
  --no-build \
  -o ./azure-publish
```

Expected: `Published to .../azure-publish`.
Key files present: `Chronith.API.dll`, `Chronith.API.runtimeconfig.json`.

**Step 3: Zip the publish output**

```bash
cd azure-publish && zip -r ../chronith-deploy.zip . && cd ..
```

Expected: `chronith-deploy.zip` created at repo root.

**Step 4: Deploy to Azure**

```bash
az webapp deploy \
  --name chronith-api \
  --resource-group rg-chronith \
  --src-path chronith-deploy.zip \
  --type zip
```

Expected: `Deployment has completed successfully.`

> First deploy triggers startup: EF Core migrations run against Neon (~10–20s), then plan seeding. Total startup ~30–40s.

**Step 5: Tail logs to confirm startup**

```bash
az webapp log tail \
  --name chronith-api \
  --resource-group rg-chronith
```

Look for:

```
Migrations applied successfully
Plans seeded successfully
Now listening on: http://[::]:8080
```

Press `Ctrl+C` to stop tailing once startup is confirmed.

---

### Task 4: Verify health endpoints

**Step 1: Liveness**

```bash
curl -s https://chronith-api.azurewebsites.net/health/live
```

Expected: HTTP 200, body `Healthy`.

**Step 2: Readiness**

```bash
curl -s https://chronith-api.azurewebsites.net/health/ready | jq .
```

Expected:

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

> If `redis` shows `Degraded` or `Unhealthy`, check the `Redis__ConnectionString` app setting — specifically the TLS format.

---

### Task 5: Smoke test

**Step 1: Check OpenAPI spec is served**

```bash
curl -s https://chronith-api.azurewebsites.net/openapi.json | python3 -c "import sys,json; d=json.load(sys.stdin); print(d['info']['title'], d['info']['version'])"
```

Expected: `Chronith API ...`

**Step 2: Create a tenant (verifies DB write)**

```bash
curl -s -X POST https://chronith-api.azurewebsites.net/v1/tenants \
  -H "Content-Type: application/json" \
  -d '{"name":"Smoke Test","slug":"smoke-test","email":"test@example.com","password":"SmokeTest123!"}' \
  | jq .
```

Expected: `201 Created` with tenant object including `id`.

**Step 3: Log in (verifies JWT issuance)**

```bash
curl -s -X POST https://chronith-api.azurewebsites.net/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"tenantSlug":"smoke-test","email":"test@example.com","password":"SmokeTest123!"}' \
  | jq .accessToken
```

Expected: a JWT string.

---

### Task 6: Update README.md and AGENTS.md

**Files:**

- Modify: `README.md`
- Modify: `AGENTS.md`

**Step 1: Add deployed section to README.md**

Add after the intro paragraph:

```markdown
## Deployed

| Environment        | URL                                                 |
| ------------------ | --------------------------------------------------- |
| Production (Azure) | https://chronith-api.azurewebsites.net              |
| OpenAPI spec       | https://chronith-api.azurewebsites.net/openapi.json |
| Health             | https://chronith-api.azurewebsites.net/health/ready |
```

**Step 2: Update Key Commands section in AGENTS.md**

In section 10 (Key Commands), add a `# Azure` block:

```bash
# Azure App Service deployment (F1 free tier, southeastasia)
# Build and publish
dotnet publish src/Chronith.API/Chronith.API.csproj -c Release -o ./azure-publish
cd azure-publish && zip -r ../chronith-deploy.zip . && cd ..

# Deploy
az webapp deploy --name chronith-api --resource-group rg-chronith --src-path chronith-deploy.zip --type zip

# Tail logs
az webapp log tail --name chronith-api --resource-group rg-chronith

# Update an app setting / secret
az webapp config appsettings set --name chronith-api --resource-group rg-chronith --settings Key="Value"

# Restart
az webapp restart --name chronith-api --resource-group rg-chronith
```

**Step 3: Commit**

```bash
git add README.md AGENTS.md
git commit -m "docs: add Azure App Service deployment URL and commands"
```
