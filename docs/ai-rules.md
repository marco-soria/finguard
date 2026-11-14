# AI rules

How FinGuard instructs its AI coding assistants (Claude Code and Cursor), and how to keep those instructions small,
correct and in sync.

## Loading model
- `CLAUDE.md` imports `AGENTS.md` (`@AGENTS.md`). Both are always loaded. Cursor reads both files too.
- `.claude/rules/*.md` load in Claude Code when a file matching their `paths:` frontmatter is read or edited.
- `.cursor/rules/*.mdc` load in Cursor when a file matches their `globs:`, with `alwaysApply: false`.
- A rule without `paths:` (or with `alwaysApply: true`) loads in every session. Avoid that: put always-needed
  content in `AGENTS.md` instead.

## What goes where
- `AGENTS.md`: essentials an assistant needs before it opens any file: architecture, domain rules, commands,
  conventions.
- `CLAUDE.md`: Claude Code specifics only. Keep it tiny.
- Path rules: detailed conventions for one file area. They load only when the assistant works there.

## Current rules
| Rule | Claude Code `paths:` | Cursor `globs:` |
| --- | --- | --- |
| `backend-vsa` | `src/backend/**/*.cs` | `src/backend/**/*.cs` |
| `compliance-domain` | `src/backend/Modules/Compliance/**`, `tests/**/*Compliance*/**` | `src/backend/Modules/Compliance/**,tests/**/*Compliance*/**` |
| `frontend` | `src/frontend/**` | `src/frontend/**` |
| `testing` | `tests/**`, `**/*.spec.ts` | `tests/**,**/*.spec.ts` |

## When to add a rule
- An assistant repeats the same mistake in one area.
- A new area appears, for example Dapper query handlers or Angular feature folders.
- Prefer a test, analyzer or hook when the rule can be enforced. A rule file is the fallback for what cannot be
  checked automatically.

## Sync rule
Every `.claude/rules/<name>.md` has a mirror `.cursor/rules/<name>.mdc` with the same body and equivalent
paths/globs. Edit both in the same commit. Only the frontmatter differs (`paths:` list vs `description`, `globs`,
`alwaysApply`), plus the mirror comment in the `.mdc` file.

## How to verify
- Claude Code: run `/memory` to see the loaded files.
- After editing a file in an area, ask the assistant which rules applied. The answer should name the matching rule.

## Size limits
Keep each file short. Claude Code: well under 200 lines per file. Cursor: under 500 lines per rule.
