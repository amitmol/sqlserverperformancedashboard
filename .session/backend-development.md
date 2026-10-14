# Backend-development conversation

Saved 13 October 2026.

The earlier session had no backend-development conversation. The subsequent backend milestone's visible user/assistant messages are recorded below. Tool calls/output are omitted. Verification evidence is recorded separately in [docs/backend-milestone.md](../docs/backend-milestone.md).

## User

Read docs/PRD.md, docs/architecture.md, docs/data-model.md,
docs/ui-design.md, and docs/work-items.md.

Inspect the source code and identify which work items are actually
implemented. Distinguish completed work from documentation or plans.

Implement the first database and backend milestone using the existing
ASP.NET MVC, C#, jQuery, and SQL Server conventions.

Tasks:
1. Create versioned SQL scripts for the approved snapshot tables,
   keys, and indexes.
2. Create a repeatable synthetic-data script with normal activity,
   blocking, high query reads, and file I/O slowdown scenarios.
   Target a dedicated dashboard development database.
3. Implement the required C# models, data-access methods, services,
   and MVC JSON actions.
4. Implement validated filters, bounded results, metric calculations,
   zero-denominator handling, and counter-reset handling.
5. Keep credentials outside source code.
6. Build the project and verify the implemented calculations.
   Report any checks that cannot be run.

Follow the approved scope and update work-item status only when
supported by implementation and verification.

Do not connect to production or implement the frontend in this milestone.
Finish with setup steps, changed files, verification results,
and remaining work.

## Assistant — progress

I’ll read the design and work-item documents, then inspect the repository for implemented code. I’ll keep this milestone limited to database and backend work, use no production connections, and report any checks the available environment cannot support.

## Assistant — clarification

This repository still has no ASP.NET application or source conventions to reuse. What should the backend target? Provide the existing application's path, or select a new standalone development host.

Suggested choices: Use an existing application (provide its path); Create a standalone ASP.NET Core MVC development host; Create a standalone ASP.NET MVC 5 development host.

No answer to this clarification was available when this record was saved.

## Assistant — progress

There is still no application source, so none of the development work items are implemented; the existing files are designs and plans. This machine has a .NET SDK, SQLCMD, and LocalDB available.

I’ve asked which MVC application or host to target because there are no existing conventions to inspect. While that’s pending, I’ll build the SQL scripts, synthetic fixtures, and reusable calculation layer, and verify them against a dedicated local development database.

## Assistant — progress

The migrations and synthetic seed have run successfully against the new local `SQLDashboardDev` instance and dedicated `SQLDashboard_Development` database. They contain nine scenarios, including blocking, high query reads, I/O slowdown, zero activity, resets, gaps, partial capture, and weighted latency.

I’m now implementing and testing the C# calculation layer. MVC integration is still waiting on the host/source choice; I haven’t connected to a monitored SQL Server or added frontend files.

## Assistant — progress

The C# layer now has typed snapshot models, parameterized repository reads with explicit limits, filter validation, and services for interval calculations, query ranking, waits, file I/O, and blocking chains. The first build is restoring dependencies.

Next I’ll check the computed outputs against the PRD fixtures, including zero denominators, reset recovery, gaps, and weighted latency.

## Assistant — progress

The build passes with zero warnings and errors. All 67 checks pass, including tests that read the persisted SQL fixtures for query ranking, wait shares, blocking, I/O, reset recovery, gaps, and partial capture. The migration and seed runner also succeeded on a second run.

The remaining integration blocker is the MVC host and its authentication model. I’ll keep that work marked incomplete until the application path or host choice is supplied.

## Assistant — progress

SQL verification also passed for cross-partition foreign-key rejection, duplicate-section rejection, transactional publication, same-ID retry, and expired-lease rejection. The expanded C# suite passes 73 checks.

I’m documenting the setup and updating work-item statuses with this evidence. Full MVC integration remains pending; I’ll distinguish it from the backend core that now builds and passes tests.

## Saved outcome

Database scripts, nine repeatable synthetic scenarios and the portable C# backend are implemented. The final build has zero warnings/errors, 73 C# verification checks pass, and SQL integrity/publication checks pass. The seed also rejects the local master database before mutations. No production connection or frontend implementation occurred.

Concrete MVC JSON actions remain pending because the existing application/host is absent and no host choice was supplied. Work items were updated as partial, with setup, changed-file inventory, verification and remaining work in [docs/backend-milestone.md](../docs/backend-milestone.md).
