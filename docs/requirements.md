# Requirements stage

Status: requirements captured; PRD remains draft. Updated 13 October 2026.

This file keeps the requirements-stage record separate from design and later development stages. The authoritative requirements and acceptance criteria remain in [PRD.md](PRD.md); this record does not replace or approve them.

## Recorded scope

Design a SQL Server Monitoring Dashboard using the existing ASP.NET MVC, C#, Razor, jQuery and SQL Server stack. First-release sections are Overview, Expensive Queries, Blocking, Waits and Database File I/O, with persisted historical snapshots. Select one configured instance at a time and filter by database only where attribution is supported.

The initial demonstration uses clearly labelled synthetic data. Live collection is a later implementation stage. Proposed defaults are 15-second collection and 30-day raw retention, subject to capacity and deployment validation.

Required behavior includes validated bounded AJAX responses, interval-based cumulative calculations, reset/gap handling, UTC storage, explicit coverage, server-side authorization, accessible controls and loading/empty/error states. PRD acceptance criteria AC01–AC18 define later validation.

## Open requirements

The repository contains no application source. Framework/MVC version, data-access approach, authentication, UI conventions, SQL Server deployment and collector hosting remain unverified. Text permissions/retention, wait exclusions, workload limits and draft PRD acceptance need resolution before implementation.

Optional features remain separate: a supported process CPU card, privileged raw text/physical paths, rollups and exports. Session termination, query execution, tuning/configuration changes, alerts, execution plans, deadlock capture and enabling Query Store are outside scope.

## Handoff

Continue with [architecture-design.md](architecture-design.md). Preserve the PRD as the source of product requirements and record future requirement changes here with their date and affected acceptance criteria.
