# Backend development stage

Status: database and portable C# backend partially implemented/verified; MVC integration pending application source/host selection. Updated 13 October 2026.

Follow-up: the user authorized a new application. Concrete .NET 9 MVC read actions and local development authorization now run and pass HTTP/Chrome checks. See [local-application.md](local-application.md); pending-host statements below describe the earlier stage.

The user has now authorized the first database/backend milestone. See [backend-milestone.md](backend-milestone.md) for changed source files, repeatable setup, build and SQL/calculation verification, and limits. Versioned SQL scripts, nine synthetic scenarios and the framework-independent backend are implemented. Concrete MVC actions/authentication cannot be reconciled with existing conventions because no existing application is available.

## Inputs and prerequisites

Use [architecture.md](architecture.md), [data-model.md](data-model.md) and work items WI01–07/WI10–11 in [work-items.md](work-items.md). Obtain the application source and confirm its MVC/.NET, persistence, authentication and scheduler conventions before choosing framework-specific APIs or dependencies.

## Planned scope

- Snapshot storage with stable engine/entity identities, separate live/synthetic partitions, atomic idempotent publication and bounded retention.
- Synthetic source and fixtures first; later live read-only collection, durable scheduling, nonoverlapping cycles and fencing.
- Shared comparison, reset/gap, UTC normalization, freshness and coverage services.
- Overview, query, blocking, wait, file I/O and history repositories/services.
- Authorized read endpoints with validated filters, bounded work/results, pinned sample contexts and safe DTO/error contracts.

Reuse existing data-access and authorization boundaries. Never accept browser-supplied connection targets or SQL. Do not expose unauthorized query/host/login metadata. Keep collection independent of viewer requests.

## Record for a future development chat

When work occurs, append the request/date, confirmed integration decisions, changed source paths, completed work-item IDs, validation actually performed, unresolved issues and handoff. Do not record planned work as completed.

The earlier design conversation produced no application code. The subsequent backend conversation added actual C#/SQL implementation and verification. No live collector, frontend or production connection has been introduced. Work items remain partial where full implementation/integration has not been verified.
