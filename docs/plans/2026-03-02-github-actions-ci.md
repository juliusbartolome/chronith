# GitHub Actions CI Workflow Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create `.github/workflows/ci.yml` with 5 jobs covering build/test, Docker build, k6 load tests, BenchmarkDotNet benchmarks, and CodeQL SAST.

**Architecture:** Single workflow file triggered on push/PR to `main` and `develop`. Jobs run in parallel where possible; `k6-load-tests` depends on `docker-build` to reuse the built image. `benchmarks` is gated to push events only; `codeql` to PR events only.

**Tech Stack:** GitHub Actions, .NET 10, Docker Buildx, k6, BenchmarkDotNet, CodeQL, dorny/test-reporter, actionlint

---

## Context

- Solution file: `Chronith.slnx` — always use `dotnet build Chronith.slnx` / `dotnet test Chronith.slnx`
- 4 test projects: Unit (56), Integration (21), Functional (89), Performance (BenchmarkDotNet)
- Integration + Functional use Testcontainers — they spin up their own Postgres; no `services:` block needed
- k6 scripts live in `tests/Chronith.Tests.Load/scripts/`: `availability.js`, `create-booking.js`, `booking-lifecycle.js`, `concurrent-booking.js`
- Docker compose project name is `chronith`; image tag used in compose is `chronith-chronith-api:latest`
- Container names: `chronith-chronith-api-1`, `chronith-postgres-1`
- DB columns are PascalCase — raw psql inserts must quote column names (e.g. `"Id"`, `"TenantId"`)
- Health endpoints: `/health/live`, `/health/ready`
- k6 env vars: `BASE_URL=http://localhost:5001`, `JWT_SIGNING_KEY="change-me-in-production-at-least-32-chars"`
- `concurrent-booking.js` threshold `booking_successes: count<=1` — advisory lock ensures exactly 1 succeeds
- `create-booking.js` and `booking-lifecycle.js` intentionally drop `http_req_failed` threshold (409s expected)
- DB slots must be cleared between k6 runs: June 2026 bookings before lifecycle, `2026-05-04T10:00:00+00:00` slot before concurrent
- Public repo — CodeQL Security tab free, no GitHub Advanced Security needed
- Manual mapping only — no AutoMapper
- `AsNoTracking` on all reads
- Conventional commit messages

---

## Task 1: Scaffold workflow file and shared structure

**Files:**

- Create: `.github/workflows/ci.yml`

**Step 1: Create the directory and file**

```bash
mkdir -p .github/workflows
```

Create `.github/workflows/ci.yml` with:

```yaml
name: CI

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main, develop]

concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true
```

**Step 2: Verify the file is valid YAML**

```bash
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))" && echo "OK"
```

Expected: `OK`

**Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: scaffold workflow with triggers and concurrency"
```

---

## Task 2: `dotnet-test` job

**Files:**

- Modify: `.github/workflows/ci.yml`

**Step 1: Add the job**

Append to `.github/workflows/ci.yml`:

```yaml
jobs:
  dotnet-test:
    name: .NET Tests
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.x"

      - name: Restore
        run: dotnet restore Chronith.slnx

      - name: Build
        run: dotnet build Chronith.slnx --no-restore -c Release

      - name: Test
        run: |
          dotnet test Chronith.slnx --no-build -c Release \
            --logger "trx;LogFileName=results.trx" \
            --results-directory ./test-results

      - name: Test Report
        uses: dorny/test-reporter@v1
        if: always()
        with:
          name: "Unit · Integration · Functional"
          path: "./test-results/results.trx"
          reporter: dotnet-trx
          fail-on-error: true

      - name: Upload TRX artifacts
        uses: actions/upload-artifact@v4
        if: always()
        with:
          name: test-results
          path: ./test-results/
          retention-days: 7
```

**Step 2: Validate YAML**

```bash
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))" && echo "OK"
```

Expected: `OK`

**Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add dotnet-test job with TRX reporting via dorny/test-reporter"
```

---

## Task 3: `docker-build` job

**Files:**

- Modify: `.github/workflows/ci.yml`

**Step 1: Add the job** (parallel with `dotnet-test`)

Append under `jobs:`:

```yaml
docker-build:
  name: Docker Build
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v4

    - name: Set up Docker Buildx
      uses: docker/setup-buildx-action@v3

    - name: Build image
      uses: docker/build-push-action@v6
      with:
        context: .
        push: false
        tags: chronith-api:ci
        cache-from: type=gha
        cache-to: type=gha,mode=max
```

**Step 2: Validate YAML**

```bash
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))" && echo "OK"
```

Expected: `OK`

**Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add docker-build job with GHA layer cache"
```

---

## Task 4: `k6-load-tests` job

**Files:**

- Modify: `.github/workflows/ci.yml`

This is the most complex job. It:

1. Rebuilds the Docker image with `load: true` so it's available to `docker compose`
2. Starts the stack via `docker compose`
3. Health-polls `/health/live`
4. Seeds the DB with a tenant, two booking types, and availability windows
5. Runs 4 k6 scripts sequentially with cleanup between runs
6. Writes a GitHub Job Summary markdown table with per-script pass/fail
7. Uploads k6 output artifacts
8. Tears down the stack

**Step 1: Add the job** (depends on `docker-build`)

Append under `jobs:`:

````yaml
k6-load-tests:
  name: k6 Load Tests
  runs-on: ubuntu-latest
  needs: docker-build
  steps:
    - uses: actions/checkout@v4

    - name: Set up Docker Buildx
      uses: docker/setup-buildx-action@v3

    - name: Build image (load into Docker daemon)
      uses: docker/build-push-action@v6
      with:
        context: .
        push: false
        load: true
        tags: chronith-chronith-api:latest
        cache-from: type=gha

    - name: Start stack
      run: docker compose -f docker-compose.yml -f docker-compose.override.yml up -d

    - name: Wait for health
      run: |
        echo "Waiting for /health/live..."
        for i in $(seq 1 30); do
          if curl -sf http://localhost:5001/health/live > /dev/null 2>&1; then
            echo "Healthy after ${i}s"
            exit 0
          fi
          sleep 1
        done
        echo "Timed out waiting for health endpoint"
        docker compose logs
        exit 1

    - name: Seed database
      run: |
        docker exec chronith-postgres-1 psql -U postgres -d chronith -c "
          INSERT INTO tenants (\"Id\", \"Name\", \"Slug\", \"TimeZone\", \"IsDeleted\", \"CreatedAt\")
          VALUES ('00000000-0000-0000-0000-000000000001','Test Tenant','test-tenant','UTC',false,NOW())
          ON CONFLICT (\"Id\") DO NOTHING;

          INSERT INTO booking_types (\"Id\", \"TenantId\", \"Name\", \"Slug\", \"DurationMinutes\", \"Capacity\", \"Kind\", \"PaymentMode\", \"IsDeleted\", \"CreatedAt\")
          VALUES
            ('00000000-0000-0000-0000-000000000010','00000000-0000-0000-0000-000000000001','Test Type','test-type',60,100,'Fixed','Manual',false,NOW()),
            ('00000000-0000-0000-0000-000000000011','00000000-0000-0000-0000-000000000001','Capacity One Type','capacity-one-type',60,1,'Fixed','Manual',false,NOW())
          ON CONFLICT (\"Id\") DO NOTHING;

          INSERT INTO availability_windows (\"Id\", \"BookingTypeId\", \"DayOfWeek\", \"StartTime\", \"EndTime\", \"IsDeleted\", \"CreatedAt\")
          SELECT gen_random_uuid(), bt.\"Id\", d.day, '08:00'::time, '18:00'::time, false, NOW()
          FROM booking_types bt
          CROSS JOIN (SELECT generate_series(0,6) AS day) d
          WHERE bt.\"Id\" IN (
            '00000000-0000-0000-0000-000000000010',
            '00000000-0000-0000-0000-000000000011'
          )
          ON CONFLICT DO NOTHING;
        "

    - name: Install k6
      uses: grafana/setup-k6-action@v1

    - name: Run availability.js
      env:
        BASE_URL: http://localhost:5001
        JWT_SIGNING_KEY: change-me-in-production-at-least-32-chars
      run: |
        k6 run tests/Chronith.Tests.Load/scripts/availability.js \
          --env BASE_URL=$BASE_URL \
          --env JWT_SIGNING_KEY=$JWT_SIGNING_KEY \
          2>&1 | tee k6-availability.txt

    - name: Run create-booking.js
      env:
        BASE_URL: http://localhost:5001
        JWT_SIGNING_KEY: change-me-in-production-at-least-32-chars
      run: |
        k6 run tests/Chronith.Tests.Load/scripts/create-booking.js \
          --env BASE_URL=$BASE_URL \
          --env JWT_SIGNING_KEY=$JWT_SIGNING_KEY \
          2>&1 | tee k6-create-booking.txt

    - name: Clear bookings before lifecycle
      run: |
        docker exec chronith-postgres-1 psql -U postgres -d chronith -c "
          DELETE FROM bookings
          WHERE \"StartTime\" >= '2026-06-01T00:00:00+00:00'
            AND \"StartTime\" <  '2026-07-01T00:00:00+00:00';
        "

    - name: Run booking-lifecycle.js
      env:
        BASE_URL: http://localhost:5001
        JWT_SIGNING_KEY: change-me-in-production-at-least-32-chars
      run: |
        k6 run tests/Chronith.Tests.Load/scripts/booking-lifecycle.js \
          --env BASE_URL=$BASE_URL \
          --env JWT_SIGNING_KEY=$JWT_SIGNING_KEY \
          2>&1 | tee k6-booking-lifecycle.txt

    - name: Clear slot before concurrent test
      run: |
        docker exec chronith-postgres-1 psql -U postgres -d chronith -c "
          DELETE FROM bookings
          WHERE \"StartTime\" = '2026-05-04T10:00:00+00:00';
        "

    - name: Run concurrent-booking.js
      env:
        BASE_URL: http://localhost:5001
        JWT_SIGNING_KEY: change-me-in-production-at-least-32-chars
      run: |
        k6 run tests/Chronith.Tests.Load/scripts/concurrent-booking.js \
          --env BASE_URL=$BASE_URL \
          --env JWT_SIGNING_KEY=$JWT_SIGNING_KEY \
          2>&1 | tee k6-concurrent-booking.txt

    - name: Write job summary
      if: always()
      run: |
        echo "## k6 Load Test Results" >> $GITHUB_STEP_SUMMARY
        echo "" >> $GITHUB_STEP_SUMMARY
        echo "| Script | Result |" >> $GITHUB_STEP_SUMMARY
        echo "|--------|--------|" >> $GITHUB_STEP_SUMMARY
        for script in availability create-booking booking-lifecycle concurrent-booking; do
          file="k6-${script}.txt"
          if [ -f "$file" ]; then
            if grep -q "default ✓" "$file"; then
              result="✅ PASS"
            else
              result="❌ FAIL"
            fi
          else
            result="⚠️ NOT RUN"
          fi
          echo "| ${script} | ${result} |" >> $GITHUB_STEP_SUMMARY
        done
        echo "" >> $GITHUB_STEP_SUMMARY
        for script in availability create-booking booking-lifecycle concurrent-booking; do
          file="k6-${script}.txt"
          if [ -f "$file" ]; then
            echo "<details><summary>${script} full output</summary>" >> $GITHUB_STEP_SUMMARY
            echo "" >> $GITHUB_STEP_SUMMARY
            echo '```' >> $GITHUB_STEP_SUMMARY
            cat "$file" >> $GITHUB_STEP_SUMMARY
            echo '```' >> $GITHUB_STEP_SUMMARY
            echo "</details>" >> $GITHUB_STEP_SUMMARY
            echo "" >> $GITHUB_STEP_SUMMARY
          fi
        done

    - name: Upload k6 artifacts
      uses: actions/upload-artifact@v4
      if: always()
      with:
        name: k6-results
        path: k6-*.txt
        retention-days: 7

    - name: Tear down stack
      if: always()
      run: docker compose down
````

**Step 2: Validate YAML**

```bash
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))" && echo "OK"
```

Expected: `OK`

**Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add k6-load-tests job with DB seed, 4 scripts, and job summary"
```

---

## Task 5: `benchmarks` job

**Files:**

- Modify: `.github/workflows/ci.yml`

**Step 1: Add the job** (push to `main` or `develop` only)

Append under `jobs:`:

```yaml
benchmarks:
  name: Benchmarks
  runs-on: ubuntu-latest
  if: github.event_name == 'push' && (github.ref == 'refs/heads/main' || github.ref == 'refs/heads/develop')
  steps:
    - uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: "10.x"

    - name: Restore
      run: dotnet restore Chronith.slnx

    - name: Run benchmarks
      run: |
        dotnet run -c Release --project tests/Chronith.Tests.Performance \
          -- --filter "*" --exporters json markdown

    - name: Write benchmark summary
      if: always()
      run: |
        echo "## BenchmarkDotNet Results" >> $GITHUB_STEP_SUMMARY
        echo "" >> $GITHUB_STEP_SUMMARY
        for f in BenchmarkDotNet.Artifacts/results/*.md; do
          if [ -f "$f" ]; then
            cat "$f" >> $GITHUB_STEP_SUMMARY
            echo "" >> $GITHUB_STEP_SUMMARY
          fi
        done

    - name: Upload BenchmarkDotNet artifacts
      uses: actions/upload-artifact@v4
      if: always()
      with:
        name: benchmark-results
        path: BenchmarkDotNet.Artifacts/
        retention-days: 30
```

**Step 2: Validate YAML**

```bash
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))" && echo "OK"
```

Expected: `OK`

**Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add benchmarks job (main + develop push only) with BenchmarkDotNet summary"
```

---

## Task 6: `codeql` job

**Files:**

- Modify: `.github/workflows/ci.yml`

**Step 1: Add the job** (all PRs)

Append under `jobs:`:

```yaml
codeql:
  name: CodeQL (${{ matrix.language }})
  runs-on: ubuntu-latest
  if: github.event_name == 'pull_request'
  permissions:
    security-events: write
    actions: read
    contents: read
  strategy:
    fail-fast: false
    matrix:
      language: [csharp, javascript]
  steps:
    - uses: actions/checkout@v4

    - name: Initialize CodeQL
      uses: github/codeql-action/init@v3
      with:
        languages: ${{ matrix.language }}
        queries: security-and-quality

    - name: Setup .NET (C# only)
      if: matrix.language == 'csharp'
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: "10.x"

    - name: Autobuild
      uses: github/codeql-action/autobuild@v3

    - name: Analyze
      uses: github/codeql-action/analyze@v3
      with:
        category: /language:${{ matrix.language }}
```

**Step 2: Validate YAML**

```bash
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))" && echo "OK"
```

Expected: `OK`

**Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: add codeql job for C# and JavaScript SAST on PRs"
```

---

## Task 7: Verify with actionlint

**Step 1: Install actionlint (if not present)**

```bash
brew install actionlint  # macOS
# or: go install github.com/rhysd/actionlint/cmd/actionlint@latest
```

**Step 2: Lint the workflow**

```bash
actionlint .github/workflows/ci.yml
```

Expected: no errors or warnings. Fix any issues found before considering this task done.

**Step 3: No commit needed** — lint is a verification step only.
