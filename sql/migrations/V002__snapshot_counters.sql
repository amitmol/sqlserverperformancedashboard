SET XACT_ABORT ON;
SET NOCOUNT ON;
IF DB_NAME()<>N'SQLDashboard_Development' OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DashboardDevelopmentDatabase' AND CONVERT(nvarchar(128),value)=N'synthetic-only')
 THROW 51000, 'Only the marked development database is supported.', 1;
IF NOT EXISTS(SELECT 1 FROM monitoring.SchemaVersion WHERE Version=1) THROW 51000, 'Apply V001 first.', 1;
IF EXISTS(SELECT 1 FROM monitoring.SchemaVersion WHERE Version=2) RETURN;
BEGIN TRANSACTION;
CREATE TABLE monitoring.InstanceSnapshot(
 SampleKey bigint NOT NULL PRIMARY KEY, PartitionId uniqueidentifier NOT NULL, EpochId bigint NOT NULL,
 BatchCount bigint NULL CHECK(BatchCount>=0), CounterObject nvarchar(128) NOT NULL, CounterName nvarchar(128) NOT NULL,
 CounterInstance nvarchar(128) NOT NULL, CounterType int NOT NULL,
 PhysicalMemoryKb bigint NULL CHECK(PhysicalMemoryKb>=0), EngineStartUtc datetime2(7) NULL,
 FOREIGN KEY(PartitionId,EpochId,SampleKey) REFERENCES monitoring.Snapshot(PartitionId,EpochId,SampleKey));
CREATE TABLE monitoring.QueryCounterSnapshot(
 SampleKey bigint NOT NULL, PartitionId uniqueidentifier NOT NULL, EpochId bigint NOT NULL, GenerationKey bigint NOT NULL,
 ExecutionCount bigint NOT NULL CHECK(ExecutionCount>=0), TotalWorkerUs bigint NOT NULL CHECK(TotalWorkerUs>=0),
 TotalElapsedUs bigint NOT NULL CHECK(TotalElapsedUs>=0), TotalLogicalReads bigint NOT NULL CHECK(TotalLogicalReads>=0),
 LastExecutionRaw datetime2(7) NULL, LastExecutionUtc datetime2(7) NULL,
 PRIMARY KEY(SampleKey,GenerationKey), FOREIGN KEY(PartitionId,EpochId,SampleKey) REFERENCES monitoring.Snapshot(PartitionId,EpochId,SampleKey),
 FOREIGN KEY(PartitionId,EpochId,GenerationKey) REFERENCES monitoring.QueryGeneration(PartitionId,EpochId,GenerationKey));
CREATE TABLE monitoring.WaitCounterSnapshot(
 SampleKey bigint NOT NULL, PartitionId uniqueidentifier NOT NULL, WaitType nvarchar(120) NOT NULL,
 WaitingTasksCount bigint NOT NULL CHECK(WaitingTasksCount>=0), WaitTimeMs bigint NOT NULL CHECK(WaitTimeMs>=0),
 SignalWaitMs bigint NOT NULL CHECK(SignalWaitMs>=0), PRIMARY KEY(SampleKey,WaitType),
 FOREIGN KEY(PartitionId,SampleKey) REFERENCES monitoring.Snapshot(PartitionId,SampleKey));
CREATE TABLE monitoring.FileIoSnapshot(
 SampleKey bigint NOT NULL, PartitionId uniqueidentifier NOT NULL, FileIdentityKey bigint NOT NULL,
 NumReads bigint NOT NULL CHECK(NumReads>=0), BytesRead bigint NOT NULL CHECK(BytesRead>=0), ReadStallMs bigint NOT NULL CHECK(ReadStallMs>=0),
 NumWrites bigint NOT NULL CHECK(NumWrites>=0), BytesWritten bigint NOT NULL CHECK(BytesWritten>=0), WriteStallMs bigint NOT NULL CHECK(WriteStallMs>=0),
 SampleMs bigint NOT NULL, LogicalName nvarchar(128) NOT NULL, FileType varchar(4) NOT NULL CHECK(FileType IN ('data','log')),
 PRIMARY KEY(SampleKey,FileIdentityKey), FOREIGN KEY(PartitionId,SampleKey) REFERENCES monitoring.Snapshot(PartitionId,SampleKey),
 FOREIGN KEY(PartitionId,FileIdentityKey) REFERENCES monitoring.FileIdentity(PartitionId,FileIdentityKey));
CREATE TABLE monitoring.SessionSnapshot(
 SampleKey bigint NOT NULL, PartitionId uniqueidentifier NOT NULL, EpochId bigint NOT NULL, SessionIdentityKey bigint NOT NULL,
 IsUserProcess bit NOT NULL, IsCollector bit NOT NULL, DatabaseIdentityKey bigint NULL, Status nvarchar(30) NOT NULL,
 OpenTransactions int NOT NULL CHECK(OpenTransactions>=0), PRIMARY KEY(SampleKey,SessionIdentityKey),
 FOREIGN KEY(PartitionId,EpochId,SampleKey) REFERENCES monitoring.Snapshot(PartitionId,EpochId,SampleKey),
 FOREIGN KEY(PartitionId,EpochId,SessionIdentityKey) REFERENCES monitoring.SessionIdentity(PartitionId,EpochId,SessionIdentityKey),
 FOREIGN KEY(PartitionId,DatabaseIdentityKey) REFERENCES monitoring.DatabaseIdentity(PartitionId,DatabaseIdentityKey));
CREATE TABLE monitoring.RequestSnapshot(
 SampleKey bigint NOT NULL, PartitionId uniqueidentifier NOT NULL, EpochId bigint NOT NULL, RequestIdentityKey bigint NOT NULL,
 DatabaseIdentityKey bigint NULL, Status nvarchar(30) NOT NULL, CurrentWaitType nvarchar(120) NULL,
 CurrentWaitMs bigint NOT NULL CHECK(CurrentWaitMs>=0), ElapsedRequestMs bigint NOT NULL CHECK(ElapsedRequestMs>=0),
 OpenTransactions int NOT NULL CHECK(OpenTransactions>=0), RawBlockingSessionId smallint NULL,
 PRIMARY KEY(SampleKey,RequestIdentityKey), FOREIGN KEY(PartitionId,EpochId,SampleKey) REFERENCES monitoring.Snapshot(PartitionId,EpochId,SampleKey),
 FOREIGN KEY(PartitionId,EpochId,RequestIdentityKey) REFERENCES monitoring.RequestIdentity(PartitionId,EpochId,RequestIdentityKey),
 FOREIGN KEY(PartitionId,DatabaseIdentityKey) REFERENCES monitoring.DatabaseIdentity(PartitionId,DatabaseIdentityKey));
CREATE TABLE monitoring.BlockingEdgeSnapshot(
 SampleKey bigint NOT NULL, PartitionId uniqueidentifier NOT NULL, EpochId bigint NOT NULL, BlockedRequestIdentityKey bigint NOT NULL,
 BlockerSessionIdentityKey bigint NULL, RawOwnerId smallint NOT NULL, OwnerKind varchar(12) NOT NULL CHECK(OwnerKind IN ('session','missing','special')),
 RelationshipState varchar(12) NOT NULL CHECK(RelationshipState IN ('observed','missing','special')), ObservedUtc datetime2(7) NOT NULL,
 PRIMARY KEY(SampleKey,BlockedRequestIdentityKey), FOREIGN KEY(SampleKey,BlockedRequestIdentityKey) REFERENCES monitoring.RequestSnapshot(SampleKey,RequestIdentityKey),
 FOREIGN KEY(PartitionId,EpochId,SampleKey) REFERENCES monitoring.Snapshot(PartitionId,EpochId,SampleKey),
 FOREIGN KEY(PartitionId,EpochId,BlockedRequestIdentityKey) REFERENCES monitoring.RequestIdentity(PartitionId,EpochId,RequestIdentityKey),
 FOREIGN KEY(PartitionId,EpochId,BlockerSessionIdentityKey) REFERENCES monitoring.SessionIdentity(PartitionId,EpochId,SessionIdentityKey),
 CHECK((RawOwnerId>0 AND OwnerKind IN ('session','missing')) OR (RawOwnerId<0 AND OwnerKind='special')));
CREATE INDEX IX_QueryCounter_Generation ON monitoring.QueryCounterSnapshot(GenerationKey,SampleKey) INCLUDE(ExecutionCount,TotalWorkerUs,TotalElapsedUs,TotalLogicalReads);
CREATE INDEX IX_WaitCounter_Type ON monitoring.WaitCounterSnapshot(WaitType,SampleKey) INCLUDE(WaitingTasksCount,WaitTimeMs,SignalWaitMs);
CREATE INDEX IX_FileCounter_Identity ON monitoring.FileIoSnapshot(FileIdentityKey,SampleKey) INCLUDE(NumReads,BytesRead,ReadStallMs,NumWrites,BytesWritten,WriteStallMs);
CREATE INDEX IX_Session_Database ON monitoring.SessionSnapshot(SampleKey,DatabaseIdentityKey) INCLUDE(IsUserProcess,IsCollector);
CREATE INDEX IX_Request_Blocker ON monitoring.RequestSnapshot(SampleKey,RawBlockingSessionId) INCLUDE(DatabaseIdentityKey);
CREATE INDEX IX_Edge_Blocker ON monitoring.BlockingEdgeSnapshot(SampleKey,BlockerSessionIdentityKey);
INSERT monitoring.WaitExclusionPolicy VALUES('demo-v1','2026-10-13',N'Proposed demonstration exclusions; requires DBA approval for live deployment. WAITFOR may reflect application behavior.');
INSERT monitoring.WaitExclusionType SELECT 'demo-v1',v.WaitType FROM (VALUES(N'SLEEP_TASK'),(N'SLEEP_SYSTEMTASK'),(N'LAZYWRITER_SLEEP'),(N'SQLTRACE_BUFFER_FLUSH'),(N'WAITFOR'),(N'BROKER_TASK_STOP'),(N'XE_TIMER_EVENT'),(N'XE_DISPATCHER_WAIT'))v(WaitType);
INSERT monitoring.SchemaVersion(Version) VALUES(2);
COMMIT;
