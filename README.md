# SQL Server Performance Dashboard

A project for building a dashboard to monitor and analyze SQL Server performance.

## Project status

The repository now contains a new ASP.NET Core MVC application with Razor, jQuery, a shared C# backend and a dedicated SQL Server LocalDB database. It runs locally with clearly labelled synthetic data. Live collection and production authentication remain pending.

Run the Start-Development.ps1 script with -OpenChrome, then open http://127.0.0.1:5080 manually if a visible browser launch is unavailable. See [local application setup](docs/local-application.md).

See [the backend milestone setup and verification record](docs/backend-milestone.md) and [work-item status](docs/work-items.md). Build the backend/verification solution with `dotnet build SQLServerDashboard.sln --configuration Release`.

## Planned features

- An overview of SQL Server health and performance.
- Visibility into resource usage, query performance, and wait statistics.
- Insights into blocking and other performance bottlenecks.

## Getting started

Clone the repository:

```sh
git clone https://github.com/amitmol/sqlserverperformancedashboard.git
cd sqlserverperformancedashboard
```

Development prerequisites and repeatable LocalDB/synthetic-data setup are documented in [docs/backend-milestone.md](docs/backend-milestone.md). Do not use these development scripts against production.
