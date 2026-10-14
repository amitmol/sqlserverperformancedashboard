SET XACT_ABORT ON;
SET NOCOUNT ON;
IF DB_NAME() <> N'SQLDashboard_Development' OR NOT EXISTS
 (SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DashboardDevelopmentDatabase' AND CONVERT(nvarchar(128),value)=N'synthetic-only')
 THROW 51000, 'Only the marked dashboard development database is supported.', 1;
IF SCHEMA_ID(N'monitoring') IS NULL EXEC(N'CREATE SCHEMA monitoring');
IF OBJECT_ID(N'monitoring.SchemaVersion') IS NULL
 CREATE TABLE monitoring.SchemaVersion(Version int NOT NULL PRIMARY KEY, AppliedAtUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME());
IF EXISTS(SELECT 1 FROM monitoring.SchemaVersion WHERE Version=1) RETURN;
BEGIN TRANSACTION;
CREATE TABLE monitoring.MonitoredInstance(
 InstanceId uniqueidentifier NOT NULL PRIMARY KEY, DisplayName nvarchar(128) NOT NULL,
 ConfigurationReference nvarchar(128) NOT NULL UNIQUE, Enabled bit NOT NULL);
CREATE TABLE monitoring.SourcePartition(
 PartitionId uniqueidentifier NOT NULL PRIMARY KEY, InstanceId uniqueidentifier NOT NULL REFERENCES monitoring.MonitoredInstance,
 Mode varchar(12) NOT NULL CHECK(Mode IN ('synthetic','live')), ScenarioId varchar(40) NULL, FixtureVersion int NULL,
 RetentionDays int NOT NULL CHECK(RetentionDays BETWEEN 1 AND 30), EvaluationUtc datetime2(7) NULL,
 CONSTRAINT CK_PartitionMode CHECK((Mode='synthetic' AND ScenarioId IS NOT NULL AND FixtureVersion IS NOT NULL AND EvaluationUtc IS NOT NULL) OR
 (Mode='live' AND ScenarioId IS NULL AND FixtureVersion IS NULL AND EvaluationUtc IS NULL)),
 CONSTRAINT UQ_Partition UNIQUE(InstanceId,Mode,ScenarioId,FixtureVersion));
CREATE TABLE monitoring.EngineEpoch(
 EpochId bigint IDENTITY NOT NULL PRIMARY KEY, PartitionId uniqueidentifier NOT NULL REFERENCES monitoring.SourcePartition,
 PhysicalIdentity nvarchar(128) NOT NULL, EngineStartRaw datetime2(7) NOT NULL, EngineStartUtc datetime2(7) NULL,
 TimezoneProvenance nvarchar(128) NOT NULL, FirstSeenUtc datetime2(7) NOT NULL, LastSeenUtc datetime2(7) NOT NULL,
 UNIQUE(PartitionId,PhysicalIdentity,EngineStartRaw), UNIQUE(PartitionId,EpochId));
CREATE TABLE monitoring.CollectorRun(
 RunId uniqueidentifier NOT NULL PRIMARY KEY, PartitionId uniqueidentifier NOT NULL REFERENCES monitoring.SourcePartition,
 OwnerToken uniqueidentifier NOT NULL, MonotonicFrequency bigint NOT NULL CHECK(MonotonicFrequency>0),
 StartUtc datetime2(7) NOT NULL, EndUtc datetime2(7) NULL, TimingProvenance nvarchar(128) NOT NULL,
 UNIQUE(PartitionId,RunId));
CREATE TABLE monitoring.CollectorLease(
 PartitionId uniqueidentifier NOT NULL PRIMARY KEY REFERENCES monitoring.SourcePartition,
 OwnerId uniqueidentifier NOT NULL, FencingToken bigint NOT NULL CHECK(FencingToken>0), ExpiresUtc datetime2(7) NOT NULL, RenewalUtc datetime2(7) NOT NULL);
CREATE TABLE monitoring.Snapshot(
 SampleKey bigint IDENTITY NOT NULL PRIMARY KEY, SampleId uniqueidentifier NOT NULL UNIQUE,
 PartitionId uniqueidentifier NOT NULL REFERENCES monitoring.SourcePartition, EpochId bigint NULL, RunId uniqueidentifier NOT NULL,
 CollectionSequence bigint NOT NULL, SchemaVersion int NOT NULL, StartUtc datetime2(7) NOT NULL, EndUtc datetime2(7) NOT NULL,
 PublishedUtc datetime2(7) NULL, DurationTicks bigint NOT NULL CHECK(DurationTicks>=0),
 Status varchar(12) NOT NULL CHECK(Status IN ('pending','success','partial','failure')),
 FailureCode varchar(64) NULL, ExpectedSections int NOT NULL CHECK(ExpectedSections=5), CollectedSections int NOT NULL CHECK(CollectedSections BETWEEN 0 AND 5),
 FencingToken bigint NOT NULL, CHECK(EndUtc>=StartUtc), CHECK((Status='pending' AND PublishedUtc IS NULL) OR (Status<>'pending' AND PublishedUtc IS NOT NULL)),
 FOREIGN KEY(PartitionId,EpochId) REFERENCES monitoring.EngineEpoch(PartitionId,EpochId),
 FOREIGN KEY(PartitionId,RunId) REFERENCES monitoring.CollectorRun(PartitionId,RunId),
 UNIQUE(PartitionId,RunId,CollectionSequence), UNIQUE(PartitionId,SampleKey), UNIQUE(PartitionId,EpochId,SampleKey));
CREATE TABLE monitoring.SnapshotSection(
 SampleKey bigint NOT NULL, PartitionId uniqueidentifier NOT NULL, Section varchar(12) NOT NULL CHECK(Section IN ('instance','activity','queries','waits','fileIo')),
 CaptureStartUtc datetime2(7) NOT NULL, CaptureEndUtc datetime2(7) NOT NULL, ReferenceUtc datetime2(7) NULL,
 ReferenceTick bigint NULL, RunId uniqueidentifier NOT NULL, Status varchar(12) NOT NULL CHECK(Status IN ('success','partial','failure')),
 ObservedRows int NOT NULL CHECK(ObservedRows>=0), StoredRows int NOT NULL CHECK(StoredRows>=0), Truncated bit NOT NULL,
 FailureReason varchar(64) NULL, ResetGeneration bigint NOT NULL, KnownReset bit NOT NULL, IdentityReliable bit NOT NULL, TimingReliable bit NOT NULL,
 PRIMARY KEY(SampleKey,Section), FOREIGN KEY(PartitionId,SampleKey) REFERENCES monitoring.Snapshot(PartitionId,SampleKey),
 FOREIGN KEY(PartitionId,RunId) REFERENCES monitoring.CollectorRun(PartitionId,RunId),
 CHECK(CaptureEndUtc>=CaptureStartUtc), CHECK(StoredRows<=ObservedRows),
 CHECK((Status='failure' AND ReferenceUtc IS NULL AND ReferenceTick IS NULL) OR
 (Status<>'failure' AND ReferenceUtc BETWEEN CaptureStartUtc AND CaptureEndUtc AND ReferenceTick IS NOT NULL)));
CREATE TABLE monitoring.DatabaseIdentity(
 DatabaseIdentityKey bigint IDENTITY NOT NULL PRIMARY KEY, PartitionId uniqueidentifier NOT NULL REFERENCES monitoring.SourcePartition,
 ServerDatabaseId int NOT NULL, IncarnationId uniqueidentifier NOT NULL, FirstSeenUtc datetime2(7) NOT NULL, LastSeenUtc datetime2(7) NOT NULL,
 RetiredUtc datetime2(7) NULL, UNIQUE(PartitionId,ServerDatabaseId,IncarnationId), UNIQUE(PartitionId,DatabaseIdentityKey));
CREATE TABLE monitoring.DatabaseObservation(
 SampleKey bigint NOT NULL, PartitionId uniqueidentifier NOT NULL, DatabaseIdentityKey bigint NOT NULL, CapturedName nvarchar(128) NOT NULL,
 AttributionState varchar(12) NOT NULL CHECK(AttributionState IN ('known','unknown')), PRIMARY KEY(SampleKey,DatabaseIdentityKey),
 FOREIGN KEY(PartitionId,SampleKey) REFERENCES monitoring.Snapshot(PartitionId,SampleKey),
 FOREIGN KEY(PartitionId,DatabaseIdentityKey) REFERENCES monitoring.DatabaseIdentity(PartitionId,DatabaseIdentityKey));
CREATE TABLE monitoring.QueryGeneration(
 GenerationKey bigint IDENTITY NOT NULL PRIMARY KEY, PartitionId uniqueidentifier NOT NULL, EpochId bigint NOT NULL,
 DatabaseIdentityKey bigint NULL, ContextState varchar(12) NOT NULL CHECK(ContextState IN ('known','unknown')),
 PlanHandle varbinary(64) NOT NULL, SqlHandle varbinary(64) NOT NULL, StartOffset int NOT NULL, EndOffset int NOT NULL,
 CreationRaw datetime2(7) NOT NULL, CreationUtc datetime2(7) NULL, PlanGeneration bigint NOT NULL, QueryHash binary(8) NULL,
 IdentityReliable bit NOT NULL, FirstSeenUtc datetime2(7) NOT NULL, LastSeenUtc datetime2(7) NOT NULL,
 FOREIGN KEY(PartitionId,EpochId) REFERENCES monitoring.EngineEpoch(PartitionId,EpochId),
 FOREIGN KEY(PartitionId,DatabaseIdentityKey) REFERENCES monitoring.DatabaseIdentity(PartitionId,DatabaseIdentityKey),
 CHECK((ContextState='known' AND DatabaseIdentityKey IS NOT NULL) OR (ContextState='unknown' AND DatabaseIdentityKey IS NULL)),
 UNIQUE(PartitionId,EpochId,DatabaseIdentityKey,PlanHandle,StartOffset,EndOffset,CreationRaw,PlanGeneration),
 UNIQUE(PartitionId,EpochId,GenerationKey));
CREATE TABLE monitoring.QuerySummary(
 SummaryKey bigint IDENTITY NOT NULL PRIMARY KEY, GenerationKey bigint NOT NULL REFERENCES monitoring.QueryGeneration,
 Version int NOT NULL, SafeSummary nvarchar(512) NULL, RedactionPolicyVersion varchar(40) NOT NULL,
 TextState varchar(12) NOT NULL CHECK(TextState IN ('redacted','unavailable')), CreatedUtc datetime2(7) NOT NULL, UNIQUE(GenerationKey,Version));
CREATE TABLE monitoring.FileIdentity(
 FileIdentityKey bigint IDENTITY NOT NULL PRIMARY KEY, PartitionId uniqueidentifier NOT NULL, DatabaseIdentityKey bigint NOT NULL,
 ServerFileId int NOT NULL, IncarnationId uniqueidentifier NOT NULL, IdentityReliable bit NOT NULL,
 FirstSeenUtc datetime2(7) NOT NULL, LastSeenUtc datetime2(7) NOT NULL, RetiredUtc datetime2(7) NULL,
 FOREIGN KEY(PartitionId,DatabaseIdentityKey) REFERENCES monitoring.DatabaseIdentity(PartitionId,DatabaseIdentityKey),
 UNIQUE(DatabaseIdentityKey,ServerFileId,IncarnationId), UNIQUE(PartitionId,FileIdentityKey));
CREATE TABLE monitoring.SessionIdentity(
 SessionIdentityKey bigint IDENTITY NOT NULL PRIMARY KEY, PartitionId uniqueidentifier NOT NULL, EpochId bigint NOT NULL,
 SessionId smallint NOT NULL CHECK(SessionId>0), LoginRaw datetime2(7) NOT NULL, LoginUtc datetime2(7) NULL, ConnectionIdentity uniqueidentifier NULL,
 FOREIGN KEY(PartitionId,EpochId) REFERENCES monitoring.EngineEpoch(PartitionId,EpochId),
 UNIQUE(PartitionId,EpochId,SessionId,LoginRaw), UNIQUE(PartitionId,EpochId,SessionIdentityKey));
CREATE TABLE monitoring.RequestIdentity(
 RequestIdentityKey bigint IDENTITY NOT NULL PRIMARY KEY, PartitionId uniqueidentifier NOT NULL, EpochId bigint NOT NULL,
 SessionIdentityKey bigint NOT NULL, RequestId int NOT NULL, StartRaw datetime2(7) NOT NULL, StartUtc datetime2(7) NULL,
 FOREIGN KEY(PartitionId,EpochId,SessionIdentityKey) REFERENCES monitoring.SessionIdentity(PartitionId,EpochId,SessionIdentityKey),
 UNIQUE(SessionIdentityKey,RequestId,StartRaw), UNIQUE(PartitionId,EpochId,RequestIdentityKey));
CREATE TABLE monitoring.WaitExclusionPolicy(PolicyVersion varchar(40) NOT NULL PRIMARY KEY, EffectiveUtc datetime2(7) NOT NULL, Rationale nvarchar(512) NOT NULL);
CREATE TABLE monitoring.WaitExclusionType(PolicyVersion varchar(40) NOT NULL REFERENCES monitoring.WaitExclusionPolicy, WaitType nvarchar(120) NOT NULL,
 PRIMARY KEY(PolicyVersion,WaitType));
CREATE TABLE monitoring.CollectionGap(GapKey bigint IDENTITY NOT NULL PRIMARY KEY, PartitionId uniqueidentifier NOT NULL REFERENCES monitoring.SourcePartition,
 Section varchar(12) NULL, StartUtc datetime2(7) NOT NULL, EndUtc datetime2(7) NOT NULL, Reason varchar(64) NOT NULL, RecordedUtc datetime2(7) NOT NULL,
 CHECK(EndUtc>=StartUtc));
CREATE TABLE monitoring.RetentionCheckpoint(PartitionId uniqueidentifier NOT NULL PRIMARY KEY REFERENCES monitoring.SourcePartition,
 PurgeCutoffUtc datetime2(7) NOT NULL, LastDeletedKey bigint NULL, UpdatedUtc datetime2(7) NOT NULL);
CREATE INDEX IX_Snapshot_Latest ON monitoring.Snapshot(PartitionId,EndUtc DESC,SampleKey DESC) INCLUDE(PublishedUtc,Status,SampleId);
CREATE INDEX IX_Snapshot_Purge ON monitoring.Snapshot(EndUtc,SampleKey);
CREATE INDEX IX_Section_History ON monitoring.SnapshotSection(PartitionId,Section,ReferenceUtc,SampleKey) INCLUDE(Status,Truncated,RunId,ResetGeneration);
CREATE INDEX IX_Database_Lookup ON monitoring.DatabaseIdentity(PartitionId,ServerDatabaseId,FirstSeenUtc);
CREATE INDEX IX_Query_Group ON monitoring.QueryGeneration(PartitionId,DatabaseIdentityKey,QueryHash);
CREATE INDEX IX_File_Lookup ON monitoring.FileIdentity(DatabaseIdentityKey,ServerFileId,FirstSeenUtc);
CREATE INDEX IX_Gap_Range ON monitoring.CollectionGap(PartitionId,StartUtc,EndUtc);
CREATE INDEX IX_Lease_Expiry ON monitoring.CollectorLease(ExpiresUtc);
INSERT monitoring.SchemaVersion(Version) VALUES(1);
COMMIT;
