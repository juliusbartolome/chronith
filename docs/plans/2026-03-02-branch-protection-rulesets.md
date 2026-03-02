# Branch Protection Rulesets Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Create a `develop` branch and apply a GitHub repository ruleset that blocks direct pushes to `main` and `develop`, requiring a PR with passing CI checks before any merge.

**Architecture:** One GitHub ruleset targeting both `main` and `develop` branch patterns via the GitHub REST API (`gh api`). Required status checks are the five CI jobs that run on PRs. The `Benchmarks` job is excluded because it only runs on push, not on PRs.

**Tech Stack:** GitHub CLI (`gh`), GitHub REST API v3 rulesets endpoint.

---

### Task 1: Create the `develop` branch

**Files:** none (git + GitHub remote only)

**Step 1: Create `develop` from current `main` HEAD locally**

```bash
git checkout -b develop
```

Expected: `Switched to a new branch 'develop'`

**Step 2: Push `develop` to remote**

```bash
git push -u origin develop
```

Expected: branch appears on GitHub, tracking set.

**Step 3: Switch back to `main`**

```bash
git checkout main
```

**Step 4: Verify both branches exist on remote**

```bash
gh api repos/juliusbartolome/chronith/branches --jq '.[].name'
```

Expected output contains both `main` and `develop`.

**Step 5: Commit**

No file changes — nothing to commit.

---

### Task 2: Create the repository ruleset

**Files:** none (API call only)

The ruleset targets both branch name patterns and enforces:

- No direct pushes (`deletion` + `non_fast_forward` + `required_linear_history` are NOT required here — just `creation` restriction and `update` restriction via the pull-request rule)
- Force-push blocked
- PR required (1 approval — bypassed by repo owner when needed)
- Five required status checks

**Step 1: Create the ruleset via the GitHub REST API**

Run the following command exactly (it is a single `gh api` call with a JSON body):

```bash
gh api repos/juliusbartolome/chronith/rulesets \
  --method POST \
  --header "Content-Type: application/json" \
  --input - <<'EOF'
{
  "name": "protect-main-and-develop",
  "target": "branch",
  "enforcement": "active",
  "conditions": {
    "ref_name": {
      "include": ["refs/heads/main", "refs/heads/develop"],
      "exclude": []
    }
  },
  "rules": [
    {
      "type": "deletion"
    },
    {
      "type": "non_fast_forward"
    },
    {
      "type": "pull_request",
      "parameters": {
        "required_approving_review_count": 1,
        "dismiss_stale_reviews_on_push": true,
        "require_code_owner_review": false,
        "require_last_push_approval": false,
        "required_review_thread_resolution": false
      }
    },
    {
      "type": "required_status_checks",
      "parameters": {
        "strict_required_status_checks_policy": false,
        "do_not_enforce_on_create": false,
        "required_status_checks": [
          { "context": ".NET Tests", "integration_id": null },
          { "context": "Docker Build", "integration_id": null },
          { "context": "k6 Load Tests", "integration_id": null },
          { "context": "CodeQL (csharp)", "integration_id": null },
          { "context": "CodeQL (javascript)", "integration_id": null }
        ]
      }
    }
  ]
}
EOF
```

Expected: JSON response with `"id": <number>` and `"enforcement": "active"`.

**Step 2: Verify the ruleset was created**

```bash
gh api repos/juliusbartolome/chronith/rulesets --jq '.[].name'
```

Expected: `protect-main-and-develop`

**Step 3: Verify it lists the correct branches**

```bash
gh api repos/juliusbartolome/chronith/rulesets --jq '.[0].conditions'
```

Expected: `include` array contains `refs/heads/main` and `refs/heads/develop`.

**Step 4: Verify rules are present**

```bash
gh api repos/juliusbartolome/chronith/rulesets --jq '.[0].rules[].type'
```

Expected output (order may vary):

```
deletion
non_fast_forward
pull_request
required_status_checks
```

**Step 5: Attempt a direct push to confirm the block works (optional smoke test)**

```bash
git commit --allow-empty -m "test: verify branch protection" && git push origin main
```

Expected: push is **rejected** with a ruleset violation message. Then clean up:

```bash
git reset HEAD~1
```

---

### Notes

- **Owner bypass:** As the repository owner on a personal account, GitHub allows you to bypass rulesets. The block prevents accidental pushes but can be overridden in the GitHub web UI when necessary (e.g., hotfixes).
- **`required_approving_review_count: 1`:** On a solo repo you will need to self-approve PRs, or set this to `0` if you want only the PR workflow enforced without a review gate. Adjust after creation if needed via `gh api ... --method PUT`.
- **Status check names** must exactly match the `name:` field of each job in `ci.yml`. The names used here match the current workflow exactly.
