## Summary
<!-- One paragraph: what changed and why. One slice or one concern per PR. -->

## Brief and decisions
- Brief: <!-- docs/briefs/brief-NN-....md -->
- Decisions: <!-- D-xx ... from docs/decisions.md, or "none" -->
- ADR: <!-- docs/adr/....md, or "none" -->

## Changes
<!-- Bullets grouped by area: Backend, Frontend, Database (migrations), Tests, Docs/Rules. -->

## How it was tested
<!-- Commands run and results: backend unit/integration/architecture counts, frontend Vitest count,
     E2E run (required when the frontend or a Portfolio slice changed, R-25), manual check. -->

## Deviations and risks
<!-- Deviations from the brief (approved), new or changed risks (R-xx), follow-ups. "None" if none. -->

## Screenshots
<!-- UI changes only: light and dark. Delete this section otherwise. -->

## Checklist
- [ ] PR title is the exact Conventional Commits title from the brief (it becomes the squash commit)
- [ ] Tests added or updated (unit + integration through HTTP, tenant-isolation case where relevant)
- [ ] Architecture tests green; no new analyzer warnings
- [ ] No secrets, file names, CSV content, amounts or personal data in logs
- [ ] Migrations reviewed (if any): additive, tenant_id on tenant-owned tables, no cross-schema references
- [ ] Rules files (`.claude/rules` and `.cursor/rules`) updated together when a rule changed
- [ ] `docs/decisions.md` execution delta written
