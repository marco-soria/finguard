# ADR-0013: Rebalance engine

- Status: Accepted
- Date: 2026-11-25
- Supersedes: the "relax to the alert threshold and mark `Partial`" step of D-03 (D-233)

## Context
Every evaluation needs a deterministic rebalance proposal (ADR-0002: the engine decides, AI only explains). D-03 and
D-04 set the rules: target = `RebalanceTargetRatio` (R) x limit, breached classes first, then classes in alert,
surplus to the other classes in proportion to headroom, cent rounding preserves the total. D-03 also said "if
infeasible, relax to `AlertThreshold` and mark `Partial`". The policy invariant `R x sum(limits) >= 1` (D-03, enforced
by `LimitPolicy.Create` and by D-180's CHECK constraint) makes that step unreachable in exact arithmetic.

## Decision
A pure, static domain service `RebalanceEngine.Propose(classes, total, policy)` in `Domain/Rebalancing/`. It has no
I/O, no clock and no AI. `Evaluation.Create` calls it, so every new evaluation carries a `RebalanceProposal` (status
plus one `ClassMove` per class: current, proposed, change). Arithmetic is `decimal`, and `floor_cents(x) =
floor(x x 100) / 100`.

1. **Not needed.** If every class is Compliant, nothing moves: status `NotNeeded`.
2. **Reducers.** Classes with status Warning or Breached. Each is set to its floored target
   `t_c = floor_cents(R x L_c x T)`, giving a surplus `S = sum(v_c - t_c)`. A reducer is always above its target,
   because `u >= AlertThreshold > R`.
3. **Receivers.** The other classes with value below their floored target. Headroom `h_c = t_c - v_c` and
   `H = sum(h_c)`. Each receiver gets `floor_cents(min(S, H) x h_c / H)`.
4. **Middle classes.** Non-reducers at or above their target stay unchanged.
5. **Leftover.** `S - allocated` goes to the receiver with the largest headroom, ties by `AssetClass` order (D-04).
   If there is no receiver, the leftover goes to the non-reducer with the largest headroom (`t_c - v_c`, which may be
   0 or negative), ties by `AssetClass` order, and that class counts as a receiver.
6. **Verification.** The proposed amounts are re-evaluated with the same status rule as `Evaluation`
   (`ClassResult.For`, the Specifications). The status is `Full` if every class is Compliant, `Partial` otherwise.
   There is no relax step.

Presentation order (D-235) lives in the read slice, not in the engine. Reducers come first: Breached before Warning,
then utilization descending. Then come receivers, by amount received descending. Ties go by `AssetClass` order, and
classes that do not change are left out.

### Why the plan at the target ratio is always feasible
In exact arithmetic, with targets `R x L_c x T`:
`sum_c (target_c - value_c) = R x sum(L) x T - T = (R x sum(L) - 1) x T >= 0`.
Split the classes into reducers, middle classes and receivers. That sum is
`headroom(receivers) - surplus(reducers) - excess(middle)`. So `H >= S + excess(middle) >= S`: the receivers can
always absorb the surplus, and every reducer ends exactly at its target. Nothing ever needs relaxing.

### Why a non-reducer always exists
All four classes in Warning or Breached would need shares that sum to at least `AlertThreshold x sum(L)`. The policy
requires `R < AlertThreshold`, so this is more than `R x sum(L) >= 1`. With the seed policy, `0.90 x sum(L) >=
0.90 / 0.85 > 1`. Shares sum to exactly 1, so at least one class is Compliant, and the no-receiver fallback always has
a class.

### Bounds after rounding
Each floored target loses less than one cent, so `sum(t_c) > T - 4 cents`, that is `sum(t_c) >= T - 3 cents`. Hence
`S + excess(middle) - H = T - sum(t_c) <= 3 cents`.
- `S <= H`: each receiver gets at most its headroom, and only flooring losses remain. With at most three receivers,
  that is less than 3 cents, so at most 2 cents.
- `S > H`: every receiver gets exactly its headroom (`H x h / H = h`), and the leftover is `S - H <= 3 cents`.
- No receivers (`H = 0`): the fallback class ends at `v + S`, with `S <= 3 cents - excess(middle)`.

So every receiver ends **at most 3 cents above its floored target**. Every reducer ends exactly at its floored target.
The total is preserved to the cent. `Partial` can only come from these few cents. With the seed policy it needs a
total of a few cents, for example 0.01 USD all in cash: the cent goes to fixed income, which is then Breached.

## Consequences
- The proposal is part of the `Evaluation`. It is stored in two nullable columns
  (`compliance.evaluations.proposal_status`, `compliance.evaluation_classes.proposed_book_value`). It is published in
  `EvaluationCompleted` (additive, ADR-0011) and returned by the evaluation API. Evaluations stored before this ADR
  have no proposal (null, no backfill).
- A 10,000-case generated table pins the invariants: totals 1.00 to 10^12, seed policy, fixed seed. It checks that
  the total is preserved, reducers end at their target, receivers never decrease and end at most target + 0.03,
  middle classes are unchanged, and the status matches re-evaluation. It found no `Partial` case at totals of 1.00 or
  more (R-46).
- The proposal is per asset class, not per position.

## Alternatives
- Keep the relax-to-0.90 step: rejected. It is unreachable under the invariant, and it would leave classes in Warning
  on purpose.
- Also reduce middle classes to their target: rejected. It moves money that does not need to move.
- Distribute leftover cents by largest remainder across all receivers: rejected. D-04 already fixes the rule (largest
  headroom).
- A separate `RebalanceProposed` event or aggregate: rejected. One fact, one event, and the proposal shares the
  evaluation's policy snapshot.
- Recompute old proposals in a migration: rejected. It would put domain code in migrations.
