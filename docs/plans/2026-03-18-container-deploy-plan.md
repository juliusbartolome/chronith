# Container Deployment Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Deploy both the Chronith API and Dashboard as Docker containers on Azure App Service B1, with images on `ghcr.io` and automated CD via GitHub Actions.

**Architecture:** Upgrade the existing `asp-chronith-free` plan from F1 to B1, convert the API from zip-deploy to container, create a new `chronith-dashboard` Web App on the same plan, push both images to `ghcr.io` (public), and wire up Azure CD webhooks so each merge to `main` triggers an automatic pull + restart.

**Tech Stack:** Azure CLI, Docker Buildx, `ghcr.io`, GitHub Actions (`docker/login-action`, `docker/build-push-action`), Azure App Service CD webhooks.

**Design doc:** `docs/plans/2026-03-18-container-deploy-design.md`

---

## Pre-flight

- `az account show` works (already authenticated)
- `gh auth status` works (already authenticated)
- Repo: `juliusbartolome/chronith`
- Resource group: `rg-chronith`, region: `southeastasia`
- Existing App Service Plan: `asp-chronith-free` (F1 → will become B1)
- Existing API Web App: `chronith-api`
- New Dashboard Web App: `chronith-dashboard`
- Images: `ghcr.io/juliusbartolome/chronith-api:latest`, `ghcr.io/juliusbartolome/chronith-dashboard:latest`

---

### Task 1: Upgrade App Service Plan F1 → B1

No files changed. Azure CLI only.

**Step 1: Upgrade the plan**

```bash
az appservice plan update \
  --name asp-chronith-free \
  --resource-group rg-chronith \
  --sku B1
```

Expected: JSON output with `"sku": { "name": "B1", "tier": "Basic" }`.

**Step 2: Verify**

```bash
az appservice plan show \
  --name asp-chronith-free \
  --resource-group rg-chronith \
  --query "sku.name" -o tsv
```

Expected: `B1`

---

### Task 2: Convert API to container deployment

No files changed. Azure CLI only.

**Step 1: Set the container image on the API app**

```bash
az webapp config container set \
  --name chronith-api \
  --resource-group rg-chronith \
  --container-image-name ghcr.io/juliusbartolome/chronith-api:latest
```

Expected: JSON with `"DOCKER_CUSTOM_IMAGE_NAME": "ghcr.io/juliusbartolome/chronith-api:latest"`.

**Step 2: Add WEBSITES_PORT app setting**

```bash
az webapp config appsettings set \
  --name chronith-api \
  --resource-group rg-chronith \
  --settings WEBSITES_PORT=8080
```

Expected: JSON listing all settings, including `WEBSITES_PORT: 8080`.

**Step 3: Clear the startup-file command (container ENTRYPOINT handles this)**

```bash
az webapp config set \
  --name chronith-api \
  --resource-group rg-chronith \
  --startup-file ""
```

Expected: JSON with `"appCommandLine": ""` or `null`.

**Step 4: Enable CD and retrieve webhook URL**

```bash
az webapp deployment container config \
  --name chronith-api \
  --resource-group rg-chronith \
  --enable-cd true
```

Expected: JSON with `"CI_CD_URL"` containing a webhook URL.

**Step 5: Get the webhook URL**

```bash
az webapp deployment container show-cd-url \
  --name chronith-api \
  --resource-group rg-chronith \
  --query "CI_CD_URL" -o tsv
```

Expected: a URL like `https://\$chronith-api:<token>@chronith-api.scm.azurewebsites.net/docker/hook`

**Step 6: Store webhook URL as GitHub Actions secret**

```bash
gh secret set CHRONITH_API_CD_WEBHOOK_URL \
  --repo juliusbartolome/chronith \
  --body "<webhook-url-from-step-5>"
```

Expected: `✓ Set Actions secret CHRONITH_API_CD_WEBHOOK_URL`

---

### Task 3: Create and configure dashboard Web App

No files changed. Azure CLI only.

**Step 1: Create the dashboard Web App**

```bash
az webapp create \
  --name chronith-dashboard \
  --resource-group rg-chronith \
  --plan asp-chronith-free \
  --deployment-container-image-name ghcr.io/juliusbartolome/chronith-dashboard:latest
```

Expected: JSON with `"state": "Running"` and `"defaultHostName": "chronith-dashboard.azurewebsites.net"`.

**Step 2: Set app settings**

```bash
az webapp config appsettings set \
  --name chronith-dashboard \
  --resource-group rg-chronith \
  --settings \
    CHRONITH_API_URL="https://chronith-api.azurewebsites.net" \
    WEBSITES_PORT=3000 \
    NODE_ENV=production \
    NEXT_TELEMETRY_DISABLED=1
```

Expected: JSON listing all 4 settings.

**Step 3: Enable CD and retrieve webhook URL**

```bash
az webapp deployment container config \
  --name chronith-dashboard \
  --resource-group rg-chronith \
  --enable-cd true
```

**Step 4: Get the webhook URL**

```bash
az webapp deployment container show-cd-url \
  --name chronith-dashboard \
  --resource-group rg-chronith \
  --query "CI_CD_URL" -o tsv
```

**Step 5: Store webhook URL as GitHub Actions secret**

```bash
gh secret set CHRONITH_DASHBOARD_CD_WEBHOOK_URL \
  --repo juliusbartolome/chronith \
  --body "<webhook-url-from-step-4>"
```

Expected: `✓ Set Actions secret CHRONITH_DASHBOARD_CD_WEBHOOK_URL`

---

### Task 4: Initial manual image push to ghcr.io

This is the first-ever push. After this, CI handles future pushes.

**Step 1: Log in to ghcr.io**

```bash
echo $GITHUB_TOKEN | docker login ghcr.io -u juliusbartolome --password-stdin
```

If `$GITHUB_TOKEN` is not set, use a Personal Access Token with `write:packages` scope:

```bash
gh auth token | docker login ghcr.io -u juliusbartolome --password-stdin
```

Expected: `Login Succeeded`

**Step 2: Build and push API image**

```bash
docker build -t ghcr.io/juliusbartolome/chronith-api:latest .
docker push ghcr.io/juliusbartolome/chronith-api:latest
```

Expected: `latest: digest: sha256:...` on push completion.

**Step 3: Build and push dashboard image**

```bash
docker build -t ghcr.io/juliusbartolome/chronith-dashboard:latest ./dashboard
docker push ghcr.io/juliusbartolome/chronith-dashboard:latest
```

Expected: `latest: digest: sha256:...` on push completion.

**Step 4: Make both packages public on GitHub**

```bash
gh api \
  --method PATCH \
  /user/packages/container/chronith-api \
  --field visibility=public

gh api \
  --method PATCH \
  /user/packages/container/chronith-dashboard \
  --field visibility=public
```

Expected: JSON with `"visibility": "public"` for each.

**Step 5: Trigger Azure CD webhooks to pull the new images**

```bash
CHRONITH_API_CD_WEBHOOK_URL=$(az webapp deployment container show-cd-url \
  --name chronith-api \
  --resource-group rg-chronith \
  --query "CI_CD_URL" -o tsv)

CHRONITH_DASHBOARD_CD_WEBHOOK_URL=$(az webapp deployment container show-cd-url \
  --name chronith-dashboard \
  --resource-group rg-chronith \
  --query "CI_CD_URL" -o tsv)

curl -X POST "$CHRONITH_API_CD_WEBHOOK_URL"
curl -X POST "$CHRONITH_DASHBOARD_CD_WEBHOOK_URL"
```

Expected: HTTP 200 response from both.

**Step 6: Wait ~60s and verify both apps are healthy**

```bash
sleep 60

curl -sf https://chronith-api.azurewebsites.net/health/live && echo "API: healthy"
curl -sf https://chronith-dashboard.azurewebsites.net && echo "Dashboard: reachable"
```

Expected: `API: healthy` and `Dashboard: reachable`

---

### Task 5: Add `deploy-api` job to `ci.yml`

**File:** Modify `.github/workflows/ci.yml`

Add this job at the end of the file, after the `codeql` job:

```yaml
deploy-api:
  name: Deploy API
  needs: [docker-build]
  if: github.event_name == 'push' && github.ref == 'refs/heads/main'
  runs-on: ubuntu-latest
  permissions:
    contents: read
    packages: write
  steps:
    - uses: actions/checkout@v6

    - name: Log in to ghcr.io
      uses: docker/login-action@v3
      with:
        registry: ghcr.io
        username: ${{ github.actor }}
        password: ${{ secrets.GITHUB_TOKEN }}

    - name: Set up Docker Buildx
      uses: docker/setup-buildx-action@v4

    - name: Build and push API image
      uses: docker/build-push-action@v7
      with:
        context: .
        push: true
        tags: ghcr.io/juliusbartolome/chronith-api:latest
        cache-from: type=gha
        cache-to: type=gha,mode=max

    - name: Trigger Azure CD webhook
      run: curl -fsS -X POST "${{ secrets.CHRONITH_API_CD_WEBHOOK_URL }}"
```

**Step 1: Add the job to `ci.yml`**

Insert the `deploy-api` job above after the last job in the file.

**Step 2: Verify the file is valid YAML**

```bash
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))" && echo "valid"
```

Expected: `valid`

**Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add deploy-api job to push image to ghcr.io and trigger Azure CD"
```

---

### Task 6: Add `deploy-dashboard` job to `dashboard-ci.yml`

**File:** Modify `.github/workflows/dashboard-ci.yml`

Add this job at the end of the file, after the `docker-build` job:

```yaml
deploy-dashboard:
  name: Deploy Dashboard
  needs: [docker-build]
  if: github.event_name == 'push' && github.ref == 'refs/heads/main'
  runs-on: ubuntu-latest
  permissions:
    contents: read
    packages: write
  steps:
    - uses: actions/checkout@v6

    - name: Log in to ghcr.io
      uses: docker/login-action@v3
      with:
        registry: ghcr.io
        username: ${{ github.actor }}
        password: ${{ secrets.GITHUB_TOKEN }}

    - name: Set up Docker Buildx
      uses: docker/setup-buildx-action@v4

    - name: Build and push dashboard image
      uses: docker/build-push-action@v7
      with:
        context: ./dashboard
        file: ./dashboard/Dockerfile
        push: true
        tags: ghcr.io/juliusbartolome/chronith-dashboard:latest
        cache-from: type=gha
        cache-to: type=gha,mode=max

    - name: Trigger Azure CD webhook
      run: curl -fsS -X POST "${{ secrets.CHRONITH_DASHBOARD_CD_WEBHOOK_URL }}"
```

**Step 1: Add the job to `dashboard-ci.yml`**

**Step 2: Verify the file is valid YAML**

```bash
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/dashboard-ci.yml'))" && echo "valid"
```

Expected: `valid`

**Step 3: Commit**

```bash
git add .github/workflows/dashboard-ci.yml
git commit -m "ci(dashboard): add deploy-dashboard job to push image to ghcr.io and trigger Azure CD"
```

---

### Task 7: Push branch and verify CI

**Step 1: Push to main**

```bash
git push
```

**Step 2: Watch CI**

```bash
gh run list --repo juliusbartolome/chronith --limit 5
```

Watch for the `deploy-api` and `deploy-dashboard` jobs to complete green.

**Step 3: Final smoke test**

```bash
# API health
curl -s https://chronith-api.azurewebsites.net/health/ready | jq .

# Dashboard reachable
curl -o /dev/null -s -w "%{http_code}" https://chronith-dashboard.azurewebsites.net
```

Expected: API returns all-Healthy JSON; dashboard returns `200`.

---

### Task 8: Update README.md and AGENTS.md

**File:** Modify `README.md`

Add dashboard to the Deployed table:

```markdown
| Dashboard (Azure) | https://chronith-dashboard.azurewebsites.net |
```

**File:** Modify `AGENTS.md`

In section 10 (Key Commands), update the Azure block to include dashboard commands:

```bash
# Dashboard
az webapp create --name chronith-dashboard --resource-group rg-chronith --plan asp-chronith-free --deployment-container-image-name ghcr.io/juliusbartolome/chronith-dashboard:latest
az webapp log tail --name chronith-dashboard --resource-group rg-chronith
az webapp restart --name chronith-dashboard --resource-group rg-chronith
```

**Step 1: Update both files**

**Step 2: Commit**

```bash
git add README.md AGENTS.md
git commit -m "docs: add dashboard deployment URL and container deploy commands"
git push
```
