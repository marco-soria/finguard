@AGENTS.md

# Claude Code specifics

- For any task touching more than two files, propose a short plan first and wait for approval.
- Path-scoped rules load from `.claude/rules/`. Run `/memory` to confirm `AGENTS.md` and the rules are loaded.
- `.claude/settings.json` denies `npm`, `yarn`, and reading `.env` files. Use `pnpm` for the frontend.
- ADRs in `docs/adr/` are binding. If a request conflicts with one, say so and ask before proceeding.
- Start every brief with `scripts/dev-session.ps1 -Snapshot`; end with `-Stop` and the Close-out block (AGENTS.md).
- Prefer small, reviewable commits. Do not push or open PRs unless asked.
- Keep this file under 40 lines: Cursor also loads it, so every line costs context twice.
