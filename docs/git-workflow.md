# Git workflow

FinGuard is built by one developer with AI assistants. Every change still goes through a pull request (PR) so that CI
checks it and a human reviews the AI-written diff before it reaches `main`.

## Rules
- `main` always builds and passes CI. Nobody commits to `main` directly.
- One brief = one branch = one PR. Keep PRs small (one slice or one concern).
- Branch names: `<type>/<short-description>`, for example `chore/repo-hygiene`, `feat/portfolio-upload`, `fix/tenant-filter`.
  Types follow Conventional Commits: `feat`, `fix`, `chore`, `docs`, `test`, `refactor`, `ci`.
- Commits on a branch are small and use Conventional Commits.
- PRs are merged with **squash merge**: each PR becomes one commit on `main`. The PR title is that commit message,
  so it must follow Conventional Commits.
- `main` is protected: the `ci-gate` check must pass. No approval is required (a solo author cannot approve their own PR).

## Cycle for each brief
1. Start from an up-to-date `main`:
   ```powershell
   git switch main
   git pull
   git switch -c feat/<name>
   ```
2. Claude Code (or Cursor) implements the brief and commits on the branch. It never pushes.
3. Review the work locally (`git log`, `git diff main`), then push the branch: `git push -u origin feat/<name>`
   (or "Publish branch" in the IDE).
4. Open the PR against `main` (GitHub or the IDE). Fill the template; link the brief and any ADR.
5. CI runs. If it fails, fix on the same branch and push again; the PR updates itself.
6. Read the PR diff on GitHub as a reviewer. This is the human review of AI-generated code.
7. Squash and merge when `ci-gate` is green. Delete the branch on GitHub.
8. Clean up locally:
   ```powershell
   git switch main
   git pull
   git branch -D feat/<name>   # -D because squash merge leaves the branch unmerged from Git's point of view
   ```
9. Paste the Decisions delta into `docs/decisions.md` (it can go in the same PR before merging) and re-upload it to the
   Claude Project knowledge.
