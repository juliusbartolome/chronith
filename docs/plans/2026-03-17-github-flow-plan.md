# GitHub Flow Migration — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Migrate Chronith from a multi-level branching model (`main → develop → feat/vX.Y → task-N`) to GitHub Flow, where `main` is the only long-lived branch and all feature branches PR directly to it.

**Architecture:** Three changes: (1) update the CI pipeline to remove the `develop`-gate and narrow triggers to `main`, (2) rewrite the branching/PR sections of AGENTS.md, (3) delete stale merged branches and the `develop` branch.

**Tech Stack:** git, GitHub Actions (YAML), Markdown.

---

## Task 1: Create the migration branch

**Files:**

- No files modified yet — just branch setup.

**Step 1: Branch from main**

```bash
git fetch origin
git checkout main
git checkout -b feat/github-flow-migration
```

**Step 2: Verify clean state**

```bash
git status
```

Expected: `nothing to commit, working tree clean` on `feat/github-flow-migration` at the same commit as `main`.

---

## Task 2: Update `.github/workflows/ci.yml`

**Files:**

- Modify: `.github/workflows/ci.yml`

### Changes

**2a. Narrow `on:` triggers**

Current `on:` block:

```yaml
on:
  push:
    branches: [main, develop, "feat/v1.0-ga"]
  pull_request:
    branches: [main, develop, "feat/v1.0-ga"]
```

Replace with:

```yaml
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
```

**2b. Update `benchmarks` condition**

Current:

```yaml
if: github.event_name == 'push' && (github.ref == 'refs/heads/main' || github.ref == 'refs/heads/develop')
```

Replace with:

```yaml
if: github.event_name == 'push' && github.ref == 'refs/heads/main'
```

**2c. Remove `check-target-branch` job**

Delete the entire job block (lines 481–495 in current file):

```yaml
check-target-branch:
  name: Enforce PR target branch policy
  runs-on: ubuntu-latest
  if: github.event_name == 'pull_request' && github.base_ref == 'main'
  steps:
    - name: Fail if source branch is not develop
      run: |
        if [ "${{ github.head_ref }}" != "develop" ]; then
          echo "❌ Direct PRs to 'main' are not allowed."
          echo "   Source branch: '${{ github.head_ref }}'"
          echo "   Only 'develop' may be merged into 'main'."
          echo "   Please open your PR against 'develop' instead."
          exit 1
        fi
        echo "✅ Source branch is 'develop' — merge to main is allowed."
```

**Step 1: Apply all three edits**

Use your editor or the Edit tool to make the three changes described above.

**Step 2: Verify the YAML is valid**

```bash
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))" && echo "YAML OK"
```

Expected: `YAML OK`

**Step 3: Confirm `check-target-branch` is gone and triggers are correct**

```bash
grep -n "check-target-branch\|develop\|feat/v1.0" .github/workflows/ci.yml
```

Expected: no output (zero matches).

**Step 4: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: switch to GitHub Flow — drop develop gate, narrow triggers to main"
```

---

## Task 3: Update `AGENTS.md` — Section 7 (Branching Model)

**Files:**

- Modify: `AGENTS.md`

Replace the entire **Section 7** content (from `## 7. Git & Branching Model` through the end of the Tags subsection) with the following:

---

```markdown
## 7. Git & Branching Model

### Conventional Commits

All commits must use conventional commit format with scope:
```

feat(domain): add staff member model
feat(api): add public booking endpoints
fix: resolve query filter issue for public endpoints
test: add functional tests for analytics
refactor(infra): extract notification channel factory
docs: update AGENTS.md

```

### Branching Strategy (GitHub Flow)

```

main ← only long-lived branch; always deployable
├── feat/<short-desc> ← new feature, created from main
├── fix/<short-desc> ← bug fix, created from main
└── docs/<short-desc> ← documentation only, created from main

```

- `main` is always deployable.
- Create a branch from `main`. Open a PR targeting `main`. Merge when CI is green and review is approved.
- No `develop` branch. No parent feature branches. No `task-N` sub-branches.
- Keep branches short-lived — prefer small, focused PRs.

### Tags

Tag releases on `main` after merge: `v0.1.0`, `v0.2.0`, ..., `v1.0.0`.
```

---

**Step 1: Apply the edit to AGENTS.md**

Locate the current Section 7 block (starts at `## 7. Git & Branching Model`, ends just before `## 8. CI/CD & PR Lifecycle`) and replace it with the content above.

**Step 2: Verify the section looks correct**

```bash
grep -n "develop\|task-N\|feat/v{" AGENTS.md
```

Expected: no output in Section 7. (Section 9 references old versions by name — that's fine.)

**Step 3: Commit**

```bash
git add AGENTS.md
git commit -m "docs: update AGENTS.md Section 7 for GitHub Flow branching model"
```

---

## Task 4: Update `AGENTS.md` — Section 8 (PR Lifecycle)

**Files:**

- Modify: `AGENTS.md`

### Changes

**4a. Remove the `develop`-only merge enforcement note**

In Section 8, find and remove this line (it appears in the "Branching Strategy" or introductory note):

> **Only `develop` may merge into `main`** — enforced by CI (`check-target-branch` job).

**4b. Update the post-merge cleanup command**

Find the post-merge `git checkout` command:

```bash
git checkout develop && git pull && git branch -d {branch-name}
```

Replace with:

```bash
git checkout main && git pull && git branch -d {branch-name}
```

**Step 1: Apply the two edits**

**Step 2: Verify no stray `develop` references remain in Section 8**

```bash
grep -n "develop" AGENTS.md
```

Review the output — any remaining `develop` references in Section 8 should be removed. References in Section 9's version history table are fine.

**Step 3: Commit**

```bash
git add AGENTS.md
git commit -m "docs: update AGENTS.md Section 8 — remove develop gate, fix post-merge cleanup"
```

---

## Task 5: Branch cleanup — audit merged branches

**No files modified.** This task is read-only; it produces a list to act on in Task 6.

**Step 1: Fetch all remote refs**

```bash
git fetch --prune origin
```

**Step 2: List local branches already merged into main**

```bash
git branch --merged main | grep -v "^\*\|^  main$"
```

Record the output. These are candidates for local deletion.

**Step 3: List remote branches already merged into main**

```bash
git branch -r --merged main | grep -v "origin/main\|origin/HEAD"
```

Record the output. These are candidates for remote deletion.

**Step 4: Check `develop` specifically**

```bash
git log origin/main..origin/develop --oneline
```

If this outputs **nothing**, `develop` is fully merged into `main` and is safe to delete. If it outputs commits, those are unmerged — note them for manual review.

---

## Task 6: Branch cleanup — delete merged branches

> **Safety rule:** Only delete branches that appeared in Task 5 Steps 2 and 3. Never delete branches with unmerged commits.

**Step 1: Delete merged local branches (excluding main and current branch)**

```bash
git branch --merged main \
  | grep -v "^\*\|^  main$" \
  | xargs -r git branch -d
```

**Step 2: Delete merged remote branches**

For each branch listed in Task 5 Step 3, run:

```bash
git push origin --delete <branch-name>
```

Or batch them:

```bash
git branch -r --merged main \
  | grep -v "origin/main\|origin/HEAD" \
  | sed 's|origin/||' \
  | xargs -I{} git push origin --delete {}
```

**Step 3: Delete `develop` (if confirmed merged in Task 5 Step 4)**

```bash
# Local
git branch -d develop

# Remote
git push origin --delete develop
```

If `develop` has unmerged commits, skip this step and add a note for manual review.

**Step 4: Verify cleanup**

```bash
git branch -a | grep -v "feat/github-flow-migration\|main\|HEAD"
```

Expected: only a handful of branches remain (any unmerged ones you intentionally kept).

**Step 5: Commit (just confirming — no file changes)**

There is nothing to commit in this task. The cleanup is purely git branch management.

---

## Task 7: Open the PR

**Step 1: Push the migration branch**

```bash
git push -u origin feat/github-flow-migration
```

**Step 2: Validate CI locally with act**

```bash
act pull_request --workflows .github/workflows/ci.yml
```

All jobs must pass: `dotnet-test`, `docker-build`, `playwright-e2e`, `k6-load-tests`, `codeql`.

> Note: `check-target-branch` is now removed, so it should no longer appear in the act output.

**Step 3: Create the PR**

```bash
gh pr create \
  --title "feat: migrate to GitHub Flow" \
  --body "$(cat <<'EOF'
## Summary
- Removes the `develop` branch gate from CI (`check-target-branch` job deleted)
- Narrows CI triggers to `main` only (removes `develop` and `feat/v1.0-ga`)
- Updates `benchmarks` condition to push to `main` only
- Rewrites AGENTS.md Sections 7 & 8 to document GitHub Flow
- Cleans up 50+ stale merged `feat/vX.Y/task-N-*` and `develop` branches

## CI changes
- Removed `check-target-branch` job
- `on.push.branches` → `[main]`
- `on.pull_request.branches` → `[main]`
- `benchmarks` condition → push to `main` only

## AGENTS.md changes
- Section 7: replaced multi-level hierarchy with GitHub Flow diagram and naming convention
- Section 8: removed develop-gate note, fixed post-merge `git checkout` to target `main`
EOF
)"
```

**Step 4: Monitor and address review comments**

Follow the post-PR review loop defined in AGENTS.md Section 8.

---

## Unmerged branches (manual review)

Any branches reported by Task 5 as having unmerged commits should be listed here and reviewed manually. They are not touched by this plan.
