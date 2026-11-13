---
paths:
  - "src/backend/Modules/Compliance/**"
  - "tests/**/Compliance*/**"
---

# Compliance domain rules

- `Domain/` has zero dependencies on EF Core, Wolverine or ASP.NET. Infrastructure maps to it, not the reverse.
- Aggregates: `LimitPolicy` (versioned) and `Evaluation` (immutable after creation). Value objects: `Money` (USD),
  `Ratio`, `AssetClass`. Use `decimal` only, never `double` or `float`.
- Compare unrounded ratios. Round only for display.
- Utilization = (class value / portfolio total) / class limit.
  - `utilization >= AlertThreshold` (default 0.90) puts the class in alert.
  - `utilization == 1.00` is still within the limit (status `Warning`).
  - `utilization > 1.00` is `Breached`. Overall status: breached > warning > compliant.
- Express domain predicates as Specifications (`IsInAlert`, `IsBreached`). They are domain rules, not EF query helpers.
- The rebalance engine is a pure domain service: (class totals, policy) in, proposal out. No I/O, no clock, no AI.
  - Order: breached classes first, then classes in alert.
  - Target = `RebalanceTargetRatio` (default 0.85) x limit. If infeasible, relax to `AlertThreshold` and mark `Partial`.
  - Surplus goes to the other classes proportionally to headroom up to their target.
  - Rounding to cents must preserve the total; the remainder cent goes to the class with the largest headroom.
- Policy invariants, enforced at creation: `RebalanceTargetRatio x sum(limits) >= 1` and `RebalanceTargetRatio < AlertThreshold`.
- An `Evaluation` stores the policy version and a snapshot of limits and thresholds so it stays reproducible.
- Mandatory boundary tests per class: utilization 0.8999, 0.90, 0.9001, 0.9999, 1.00, 1.0001, plus a class with zero value.
  A portfolio total of zero is rejected upstream by Portfolio and never reaches Compliance.
