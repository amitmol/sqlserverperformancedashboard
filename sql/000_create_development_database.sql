-- Execute ONLY on your local development SQL Server. No credentials are embedded.
SET NOCOUNT ON;
-- LocalDB exposes a generated physical instance name, not its friendly alias.
-- The runner pins the friendly alias to SQLDashboardDev; this guard rejects servers.
IF ISNULL(CONVERT(int, SERVERPROPERTY('IsLocalDB')),0) <> 1
    THROW 51000, 'Bootstrap requires local development LocalDB.', 1;
IF DB_ID(N'SQLDashboard_Development') IS NULL
    EXEC(N'CREATE DATABASE SQLDashboard_Development');
GO
USE SQLDashboard_Development;
GO
IF NOT EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND name = N'DashboardDevelopmentDatabase')
    EXEC sys.sp_addextendedproperty @name=N'DashboardDevelopmentDatabase', @value=N'synthetic-only';
