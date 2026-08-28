# Contributing to FairShare

Thanks for wanting to help. FairShare computes child support that real people may bring to court, so the bar for changes is correctness first, convenience second. This page is everything you need to land a conforming PR.

## The flow

1. **Start from an issue.** Every PR closes (or references) an issue; open one first if none exists. Small doc typo fixes are the only exception.
2. **Branch from `main`**, one concern per branch (`feat/...`, `fix/...`, `docs/...`).
3. **Open a PR against `main`.** Direct pushes are blocked by the ruleset; PRs need the `build` status check green, every review thread resolved, and signed commits.
4. **Squash-merge** once the gates pass. The PR title becomes the release-visible commit subject, so it must be a conventional commit line too.

## Conventional commits (release-please depends on this)

Releases are cut automatically by release-please from commit subjects on `main`:

| Prefix | Effect on the next release |
| --- | --- |
| `fix:` | patch bump, listed under Bug Fixes |
| `feat:` | minor bump, listed under Features |
| `feat!:` / `BREAKING CHANGE:` footer | major bump |
| `docs:`, `chore:`, `ci:`, `test:`, `refactor:` | no release triggered |

A user-visible behavior change is `feat:`/`fix:` even when the diff looks like markup — the changelog is written for users, not for the diff.

## Commit signing

The `main` ruleset requires **signed commits**. Set up [commit signing](https://docs.github.com/en/authentication/managing-commit-signature-verification) (SSH key signing is the low-friction path) before your first PR; an unsigned commit blocks the merge, and squash-merge does not fix it retroactively.

## The review gate

Every PR gets an automatic **GitHub Copilot review**, and every push triggers a fresh round. The merge bar is a **clean round**: the latest review, on the current head commit, with zero comments.

- Address every comment — either change the code or reply with a concrete reason the code is right; then push and wait for the next round.
- Don't resolve threads to make them disappear; resolve them after the round that no longer raises them.

## Tests — and the golden-file contract

```bash
dotnet test FairShare.sln
```

The full suite (unit + integration + golden) runs on every PR with zero skips, and it must stay that way.

**Golden files are the correctness contract.** The JSON cases under `src/FairShare.Tests/Domain/Golden/` were generated *from the official state workbooks* (`Generate-GoldenCases.ps1`, `Generate-OregonGoldenCases.ps1` — Windows + Excel COM + the official workbook, so most contributors never run them). Every line of every case is pinned.

- A golden diff in a PR that isn't deliberately changing results is a stop-the-line bug in the PR.
- A PR that *intends* to change results (new rule vintage, new guideline feature) must add or regenerate goldens **with evidence from the official calculator/workbook in the PR description**, and the release notes must disclose the change.

## Legal copy is load-bearing

`Privacy.razor` and `Terms.razor` operate under an in-file contract: **every sentence must stay literally true** of the running code and how the project is operated. If your change falsifies a sentence (stores new data, changes retention, adds a network dependency), update that sentence in the same PR — reviewers treat a stale legal sentence as a failing test.

## Security tab review (standing practice)

When picking up the repo — and after merges land — check the **Security** tab (code scanning, Dependabot, secret scanning):

- Real findings: fix via PR, or file a tracking issue if the fix isn't immediate.
- False positives: dismiss **with a written rationale** in the dismissal comment, never silently.

Precedent: PR #194 fixed two real log-forging findings; alert #1 (a name-heuristic hit on `AuditActions.Password*` constants that never carry secrets) was dismissed with the rationale recorded.

## Local dev quick start

```bash
cp .env.example .env   # JWT_SIGNING_KEY is the only hard requirement
docker compose up --build
```

or run bare: `dotnet run --project src/FairShare.Api` and `dotnet run --project src/FairShare.Web`. Details, including the full configuration reference, live in [docs/SETUP.md](docs/SETUP.md); architecture decisions in [docs/adr/](docs/adr/); the domain glossary in [CONTEXT.md](CONTEXT.md).
