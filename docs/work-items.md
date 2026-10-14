# Work items stage

Status: database/portable backend milestone partially implemented and verified. Updated 13 October 2026.

This backlog was updated after authorization for backend work, then a new local application and Chrome preview. Frontend work for the local demonstration is now authorized; production connections remain excluded. Evidence: [backend-milestone.md](backend-milestone.md) and [local-application.md](local-application.md).

## Ordered backlog

| ID | Work item | Dependency | Acceptance reference | Status |
| --- | --- | --- | --- | --- |
| WI01 | Locate existing application; record framework/MVC version, representative data-access/auth/UI paths and deployment conventions | Application source | AC01 | Resolved for new local app: user authorized creating it; .NET 9 MVC, injected ADO.NET, local identity and Razor/jQuery paths documented |
| WI02 | Resolve PRD draft decisions: SQL deployment/permissions, access/text policy, scheduler, timezone, wait list, cadence/retention and supported workload | WI01 and deployment evidence | AC01, AC16, AC18 | LocalDB 15.0.4382.1 development storage verified; actual application/live decisions pending |
| WI03 | Reconcile design paths, routes, serializer and schema with verified application conventions | WI01–02 | AC01 | Local MVC host, camelCase JSON, Razor layout and jQuery integration verified; production identity/deployment pending |
| WI04 | Implement snapshot storage, identity/source isolation, atomic publication and bounded retention | WI03 | AC02, AC11, AC15, AC18 | Partial: V001–V003 applied; keys/isolation and publication verified. Retention worker/pruning pending |
| WI05 | Implement deterministic synthetic fixture source, section status and persisted sample publication | WI04 | AC02, AC04, AC07–14 | Partial: repeatable seed with 9 scenarios/19 samples verified; remaining release fixtures pending |
| WI06 | Implement comparison/formula/freshness services, query grouping, blocking traversal and coverage | WI04–05 | AC03–15 | Partial: C# services build and calculation assertions pass; detailed coverage/direction-specific validity and full history pending |
| WI07 | Implement authorized bounded read endpoints, validation, DTOs and safe error responses | WI03, WI06 | AC03, AC15–16, AC18 | Partial: 8 MVC read actions, local claim authorization, structured errors/filter limits verified over HTTP; details/production identity pending |
| WI08 | Integrate Razor shell, five sections, jQuery refresh/filters/details and chart-table equivalents | WI03, WI07 | AC02–03, AC17 | Partial: five-section Razor/jQuery UI, shared filters, sorting/paging, historical samples, refresh/pause and chart table verified in Chrome; full details/accessibility pending |
| WI09 | Validate synthetic calculations, states, security, historical behavior and accessibility | WI05–08 | AC02–17 | Partial: 73 backend assertions, SQL checks and 22 Chrome/HTTP checks pass; full accessibility/production security/load unrun |
| WI10 | Implement later live read-only collection with durable scheduling, fencing and deadline handling | WI02, WI04, WI06 | AC11–16, AC18 | Later implementation stage |
| WI11 | Measure supported workload, endpoint/cycle targets, restart/retry behavior and retention capacity | WI09–10 and declared workload | AC15, AC18 | Not started |
| WI12 | Review completed code, resolve findings and prepare operational/user documentation | Relevant implementation and validation | AC01–18 | Partial: development setup/evidence documented; full code review and operational/user documentation pending |

## Stage records

- [backend-development.md](backend-development.md): storage, services, endpoints and later collector work.
- [frontend-development.md](frontend-development.md): Razor/jQuery dashboard work.
- [testing.md](testing.md): actual validation and results when available.
- [code-review.md](code-review.md): actual review findings when code exists.
- [documentation.md](documentation.md): documentation inventory and future handoff work.

Optional features require separate scope decisions and are not backlog commitments. No item includes session termination, query execution, target configuration changes, alerts, execution plans, deadlock capture or Query Store enablement.
