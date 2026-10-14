# Architecture-design conversation

Saved 13 October 2026. Transcript of visible user and assistant messages for this stage. Tool calls and tool output are omitted. The supplied environment context identifies the workspace as `D:\codex\DashboardAssignment\SQL_Server_Performance_Dashboard`, using PowerShell.

## User

Read docs/PRD.md and inspect the existing project.
Design the SQL Server Monitoring Dashboard using our existing ASP.NET MVC, C#, Razor, jQuery, and SQL Server stack.

1. Identify the actual .NET/MVC version, data-access approach, authentication, and UI conventions.
2. Map each approved requirement to its controller actions, services, repository methods, view models, Razor views, and JavaScript files.
3. Design SQL snapshot tables, keys, indexes, and retention rules. Distinguish cumulative counters from point-in-time values.
4. Define KPI formulas, snapshot comparison rules, counter-reset handling, zero-denominator behaviour, and UTC timestamp handling.
5. Define JSON response contracts for jQuery AJAX, with validated filters and bounded result sets.
6. Design the dashboard layout, filters, charts, tables, loading states, empty states, and error states. Clearly label synthetic data.
7. Follow the PRD scope. Record unresolved assumptions and separate optional features.
Save the results as docs/architecture.md, docs/data-model.md, and docs/ui-design.md. Do not generate implementation code yet.

## Assistant — progress

I’ll read the PRD and inspect the project’s stack and conventions, then write the three design documents without adding implementation code.

## Assistant — progress

The repository contains only `README.md` and the draft PRD; there is no application to inspect. The .NET/MVC version, data access, authentication, and UI conventions therefore remain unverified. I’ll make the design concrete at the component and contract level, while marking integration choices as provisional.

The calculation rules will preserve per-section timestamps and coverage, and keep missing data, resets, zero activity, and synthetic scenarios distinct.

## Assistant — progress

The three design documents are drafted. They include the requirement-to-component map, snapshot schema and retention design, calculation and reset rules, bounded AJAX contracts, and UI states with synthetic review fixtures.

I’m checking cross-document consistency, particularly around query ranking, weighted I/O, historical sample selection, and failure timestamps. Integration choices remain provisional because the application source is absent.

## Assistant — final

Saved the design documents:

- [architecture.md](/D:/codex/DashboardAssignment/SQL_Server_Performance_Dashboard/docs/architecture.md): requirement mappings, component boundaries and AJAX contracts.
- [data-model.md](/D:/codex/DashboardAssignment/SQL_Server_Performance_Dashboard/docs/data-model.md): tables, indexes, retention, formulas and reset rules.
- [ui-design.md](/D:/codex/DashboardAssignment/SQL_Server_Performance_Dashboard/docs/ui-design.md): layout, filters, states and synthetic review scenarios.

The repository contains no application source, so actual framework versions, data access, authentication and UI conventions remain explicitly unverified.

Checked documentation links, AC01–AC18 coverage and the JSON specimen. No implementation code was generated.
