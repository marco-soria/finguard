# FinGuard architecture overview

Diagrams use Mermaid and render on GitHub. Decisions behind them are in `docs/adr/`.

## 1. Containers: local (Phases 0 to 4)

Everything runs on the developer machine, orchestrated by the Aspire AppHost. See ADR-0005.

```mermaid
flowchart LR
  user(["Entity user"])

  subgraph local["Developer machine (aspire run)"]
    web["Angular app<br/>(pnpm)"]
    subgraph api["FinGuard.Api (modular monolith)"]
      ep["Minimal API endpoints"]
      bus["Wolverine bus + outbox"]
      tenancy["Tenancy"]
      portfolio["Portfolio"]
      compliance["Compliance"]
      advisory["Advisory"]
    end
    pg[("PostgreSQL container<br/>schemas per module + wolverine")]
    llm["Local or free LLM endpoint<br/>(or TemplateNarrator)"]
    dash["Aspire dashboard<br/>(OTLP traces, logs, metrics)"]
  end

  user -->|HTTPS| web
  web -->|REST + dev JWT| ep
  ep --> bus
  bus --> tenancy
  bus --> portfolio
  bus --> compliance
  bus --> advisory
  tenancy --> pg
  portfolio --> pg
  compliance --> pg
  advisory --> pg
  advisory -->|numbers only| llm
  api -.->|OpenTelemetry| dash
```

## 2. Containers: Azure target (Phase 5)

Same code, different configuration. Deployed on demand and torn down after demos.

```mermaid
flowchart LR
  user(["Entity user"])
  entra["Microsoft Entra External ID"]

  subgraph azure["Azure"]
    subgraph aca["Container Apps environment"]
      web["web container<br/>(Angular build)"]
      api["api container<br/>(FinGuard.Api)"]
    end
    registry["Container registry"]
    pg[("PostgreSQL Flexible Server")]
    kv["Key Vault"]
    ai["Azure AI Foundry<br/>(model deployment)"]
    appi["Application Insights"]
  end

  gh["GitHub Actions<br/>(OIDC deploy)"]

  user -->|HTTPS| web
  user -->|sign-in| entra
  web -->|REST + JWT| api
  api -->|validate token| entra
  api --> pg
  api -->|managed identity| kv
  api -->|numbers only| ai
  api -.->|OpenTelemetry| appi
  gh -->|push image| registry
  registry --> aca
```

## 3. Module dependencies and events

Modules never reference each other's internals. Arrows are integration events through the Wolverine outbox, except
the dashed line, which is the `ITenantContext` abstraction from BuildingBlocks.

```mermaid
flowchart LR
  tenancy["Tenancy<br/>schema: tenancy"]
  portfolio["Portfolio<br/>schema: portfolio"]
  compliance["Compliance<br/>schema: compliance"]
  advisory["Advisory<br/>schema: advisory"]
  audit["Audit (Phase 5, optional)<br/>schema: audit"]

  tenancy -.->|ITenantContext| portfolio
  portfolio -->|PortfolioSubmitted| compliance
  portfolio -->|PortfolioRejected| audit
  compliance -->|EvaluationCompleted| advisory
  advisory -->|AdvisoryGenerated / AdvisoryFailed| audit
  compliance -->|EvaluationCompleted| audit
```

## 4. Event flow for one submission

`PortfolioSubmitted` carries the class totals so Compliance never calls back into Portfolio. Delivery is at-least-once,
so consumers are idempotent.

The upload validates synchronously (D-111, D-112). Transport problems (no file, empty, over 5 MB, not `.csv`, bad or
future cut-off date) return 400/413 and store nothing. Otherwise the submission is always stored and the Api answers
`201 Created` with `status` `Accepted` (positions stored) or `Rejected` (first 100 errors stored, `errorCount` holds the
total). There is no 422 or 202 for uploads. Event names are reconciled with D-93 in Brief #12.

```mermaid
sequenceDiagram
  autonumber
  actor U as Entity user
  participant API as Api
  participant P as Portfolio
  participant OB as Outbox (PostgreSQL)
  participant C as Compliance
  participant A as Advisory
  participant N as Narrator

  U->>API: POST submission (CSV + cut-off date)
  API->>P: SubmitPortfolio command
  alt transport problem (empty, too large, not .csv, bad date)
    API-->>U: 400/413 problem, nothing stored
  end
  P->>P: hash, read and validate the CSV
  alt CSV invalid
    P->>OB: store Rejected submission + errors + PortfolioRejected (one transaction)
    API-->>U: 201 Created, status Rejected, errors
  else CSV valid
    P->>OB: store Accepted submission + positions + PortfolioSubmitted (one transaction)
    API-->>U: 201 Created, status Accepted
  end
  OB-->>C: PortfolioSubmitted
  C->>C: evaluate against policy version, build rebalance proposal
  C->>OB: store evaluation + EvaluationCompleted (one transaction)
  OB-->>A: EvaluationCompleted
  A->>N: class names, percentages, amounts, status
  alt narrator succeeds and numbers validate
    N-->>A: narrative text
    A->>OB: store advice + AdvisoryGenerated
  else narrator fails or output rejected
    A->>OB: store template advice + AdvisoryFailed
  end
  U->>API: GET evaluation and advisory
  API-->>U: status, alerts, proposal, narrative
```
