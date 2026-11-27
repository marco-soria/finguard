import { httpResource, HttpResourceRef } from '@angular/common/http';
import { Injectable, Signal } from '@angular/core';
import { API_BASE } from './api-base';

/** `Cash`, `FixedIncome`, `Shares` or `RealEstate` (enum names, D-202). */
export type AssetClass = string;

/** `Compliant`, `Warning` or `Breached`, decided by the API; the SPA never derives it (D-207). */
export type ComplianceStatus = string;

/** One asset class of an evaluation. Ratios are exact and unrounded (D-197): format them with `formatRatio`. */
export interface ClassEvaluation {
  assetClass: AssetClass;
  bookValue: number;
  share: number;
  limit: number;
  utilization: number;
  status: ComplianceStatus;
}

export interface EvaluationAlert {
  assetClass: AssetClass;
  status: ComplianceStatus;
  utilization: number;
}

/** `NotNeeded`, `Full` or `Partial`, decided by the API (ADR-0013); the SPA never derives it. */
export type ProposalStatus = string;

/** One class that changes. `change` is signed: negative reduces the class, positive adds to it. */
export interface ProposalMove {
  assetClass: AssetClass;
  currentBookValue: number;
  proposedBookValue: number;
  change: number;
}

/** The rebalance proposal; `moves` come in presentation order from the API (D-235) and are empty for `NotNeeded`. */
export interface RebalanceProposal {
  status: ProposalStatus;
  moves: ProposalMove[];
}

/**
 * `GET /api/v1/compliance/submissions/{id}/evaluation`. `cutOffDate` is a `yyyy-MM-dd` calendar date (D-103).
 * `alertThreshold` comes from the evaluation's policy snapshot (D-206).
 */
export interface EvaluationResponse {
  evaluationId: string;
  submissionId: string;
  cutOffDate: string;
  overallStatus: ComplianceStatus;
  policyVersion: number;
  evaluatedAt: string;
  totalBookValue: number;
  alertThreshold: number;
  classes: ClassEvaluation[];
  alerts: EvaluationAlert[];
  /** `null` for evaluations stored before the rebalance engine existed (D-238). */
  proposal: RebalanceProposal | null;
}

/** The API's 404 code for an unknown, foreign or not-yet-evaluated submission (D-205). */
export const EVALUATION_NOT_FOUND = 'Compliance.Evaluation.NotFound';

/** Compliance endpoints. Resource factories must run in an injection context (D-102). */
@Injectable({ providedIn: 'root' })
export class ComplianceApi {
  /** No request while `submissionId` is undefined (for example a rejected submission, D-204). */
  evaluation(
    submissionId: Signal<string | undefined>,
  ): HttpResourceRef<EvaluationResponse | undefined> {
    return httpResource<EvaluationResponse>(() => {
      const id = submissionId();
      return id
        ? `${API_BASE}/compliance/submissions/${encodeURIComponent(id)}/evaluation`
        : undefined;
    });
  }
}
