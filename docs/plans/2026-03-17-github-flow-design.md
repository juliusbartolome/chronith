# GitHub Flow Migration — Design

**Date:** 2026-03-17  
**Status:** Approved

---

## Goal

Switch Chronith from a multi-level branching model (`main → develop → feat/vX.Y → task-N`) to GitHub Flow — a simpler model where `main` is the only long-lived branch and feature branches merge directly back to it via PR.

---

## 1. Branching Strategy

### Before

```
main
 └── develop
      └── feat/v{X.Y}-{feature-name}           ← parent feature branch
           ├── feat/v{X.Y}/task-{N}-{name}      ← sub-branch per task
           └── ...
```

### After (GitHub Flow)

```
main                    ← only long-lived branch; always deployable
 ├── feat/<short-desc>  ← created from main, merged back via PR
 ├── fix/<short-desc>
 └── docs/<short-desc>
```

**Rules:**

- `main` is always deployable.
- Create a branch from `main`, open a PR targeting `main`, merge when CI is green and review is approved.
- No `develop`. No parent feature branches. No `task-N` sub-branches.
- Conventional commits (`feat(scope):`, `fix:`, `docs:`, etc.) are unchanged.
- Release tags (`v1.0.0`, `v1.1.0`, ...) remain on `main`.

---

## 2. CI Pipeline Changes

File: `.github/workflows/ci.yml`

| Item                       | Current                                     | After               |
| -------------------------- | ------------------------------------------- | ------------------- |
| `on.push.branches`         | `[main, develop, "feat/v1.0-ga"]`           | `[main]`            |
| `on.pull_request.branches` | `[main, develop, "feat/v1.0-ga"]`           | `[main]`            |
| `check-target-branch` job  | Fails if PR to `main` is not from `develop` | **Removed**         |
| `benchmarks` condition     | Push to `main` or `develop`                 | Push to `main` only |
| `codeql`                   | PRs or push to `main`                       | No change           |

All other jobs (`dotnet-test`, `docker-build`, `playwright-e2e`, `k6-load-tests`) are unchanged.

---

## 3. AGENTS.md Updates

### Section 7 (Git & Branching Model)

- Replace the three-level hierarchy diagram with the GitHub Flow diagram above.
- Update branch naming convention to `feat/<short-desc>`, `fix/<short-desc>`, `docs/<short-desc>`.
- Remove all references to `develop`, parent feature branches, and `task-N` sub-branches.
- Keep the conventional commits requirement unchanged.
- Remove the note about "Only `develop` may merge into `main`".

### Section 8 (CI/CD & PR Lifecycle)

- Remove the `check-target-branch` enforcement note.
- Simplify post-merge cleanup: `git checkout main && git pull && git branch -d <branch>` (no `develop` reference).
- Keep the `act` pre-PR validation requirement.
- Keep the review loop process unchanged.

### Section 9 (Version History / Roadmap)

No changes — tags remain on `main`, version numbers unchanged.

---

## 4. Branch Cleanup

### Process

1. Identify all local and remote `feat/vX.Y/task-N-*`, `feat/vX.Y-*`, `feat/v*`, `fix/v*`, and `develop` branches.
2. For each branch, check if it is already merged into `main` using `git branch --merged main`.
3. Delete merged branches: local (`git branch -d`) and remote (`git push origin --delete`).
4. Skip unmerged branches — report them for manual review.
5. Delete `develop` locally and remotely after confirming it is fully merged into `main`.

### Safety Constraint

Only branches returned by `git branch --merged main` (or `git branch -r --merged main` for remotes) are deleted. Unmerged branches are never deleted automatically.

---

## Out of Scope

- GitHub branch protection rules (not managed in this repo's code).
- Rewriting git history or rebasing existing commits.
- Changing the release tagging strategy.
