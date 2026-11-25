---
paths:
  - "src/backend/Modules/Compliance/**"
  - "tests/**/*Compliance*/**"
---

# Compliance domain rules

- `Domain/` depends only on `System.*`, itself and `FinGuard.BuildingBlocks.Results` (allow-list, architecture test,
  ADR-0012). Infrastructure maps to it, not the reverse.
- `Result` for expected domain failures (`LimitPolicy.Create`); guards that throw `ArgumentException` for impossible
  inputs (`Evaluation.Create`: missing class, total 0).
- Aggregates: `LimitPolicy` (versioned) and `Evaluation` (immutable after creation). Value objects: `Money` (USD),
  `Ratio`, `AssetClass`. Use `decimal` only, never `double` or `float`.
- Compare unrounded ratios. Round only for display.
- Utilization = (class value / portfolio total) / class limit.
  - `utilization >= AlertThreshold` (default 0.90) puts the class in alert.
  - `utilization == 1.00` is still within the limit (status `Warning`).
  - `utilization > 1.00` is `Breached`. Overall status: breached > warning > compliant.
  - An alert is raised for every class with status `Warning` or `Breached` (class, status, utilization).
- Express domain predicates as Specifications (`IsInAlert`, `IsBreached`). They are domain rules, not EF query helpers.
- The rebalance engine is a pure domain service: (class totals, policy) in, proposal out. No I/O, no clock, no AI.
  `RebalanceEngine.Propose` in `Domain/Rebalancing/`, called by `Evaluation.Create` (ADR-0013).
  - Reducers (Warning and Breached) go to their target `floor_cents(RebalanceTargetRatio x limit x total)`.
  - Receivers (below their target) get `floor_cents(min(S, H) x headroom / H)`; leftover cents go to the largest
    headroom, ties by `AssetClass` order. No receiver: the non-reducer with the largest headroom. Middle classes stay.
  - No relax step (D-233). Verification re-evaluates the proposal with the same status rule: `NotNeeded` (Compliant
    evaluation), `Full` (every class ends Compliant), `Partial` (cent rounding only). The total is preserved.
  - Presentation order (Breached, Warning, then receivers by amount) lives in the read slice, not in the engine.
- Policy invariants, enforced at creation: `RebalanceTargetRatio x sum(limits) >= 1` and `RebalanceTargetRatio < AlertThreshold`.
- Policy versions are immutable and global (`compliance.limit_policies`, not tenant-owned). An evaluation uses the
  policy in force at the cut-off date (highest `effective_from` <= cut-off); before the first version the repository
  returns null. Portfolio rejects cut-off dates before 2000-01-01 (D-190), so for an accepted submission null is a bug.
- An `Evaluation` stores the policy version and a snapshot of limits and thresholds so it stays reproducible.
- Received facts and derived results commit separately (snapshot, then `EvaluatePortfolio`). One evaluation per
  submission (unique `submission_id`); a missing snapshot or policy throws and goes to the error queue (D-191).
- Ratios are stored as unconstrained `numeric` (exact unrounded values); amounts `numeric(18,2)`, limits
  `numeric(5,4)`. Alerts are derived from class statuses, not stored.
- Persistence maps the aggregate to row models (`EvaluationRow`, `LimitPolicyRow`); `Domain/` gets no EF constructors.
- Mandatory boundary tests per class: utilization 0.8999, 0.90, 0.9001, 0.9999, 1.00, 1.0001, plus a class with zero value.
  A portfolio total of zero is rejected upstream by Portfolio and never reaches Compliance.
