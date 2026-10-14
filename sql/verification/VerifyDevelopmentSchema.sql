SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME()<>N'SQLDashboard_Development' OR ISNULL(CONVERT(int,SERVERPROPERTY('IsLocalDB')),0)<>1
 THROW 51000, 'Verification is restricted to the local development database.',1;
IF (SELECT COUNT(*) FROM monitoring.SchemaVersion WHERE Version IN(1,2,3))<>3 THROW 51000,'Migration versions missing.',1;
IF (SELECT COUNT(*) FROM monitoring.SourcePartition WHERE Mode='synthetic' AND FixtureVersion=1)<>9 THROW 51000,'Fixture partitions missing.',1;
IF (SELECT COUNT(*) FROM monitoring.Snapshot s JOIN monitoring.SourcePartition p ON p.PartitionId=s.PartitionId WHERE p.Mode='synthetic' AND p.FixtureVersion=1)<>19
 THROW 51000,'Repeatability failed: expected 19 fixture samples.',1;
IF EXISTS(SELECT 1 FROM monitoring.Snapshot WHERE Status='pending') THROW 51000,'Seed left visible pending work.',1;
DECLARE @normal uniqueidentifier=(SELECT PartitionId FROM monitoring.SourcePartition WHERE ScenarioId='normal' AND Mode='synthetic');
DECLARE @blocking uniqueidentifier=(SELECT PartitionId FROM monitoring.SourcePartition WHERE ScenarioId='blocking' AND Mode='synthetic');
DECLARE @sample bigint,@epoch bigint,@run uniqueidentifier;
SELECT TOP(1) @sample=SampleKey,@epoch=EpochId,@run=RunId FROM monitoring.Snapshot WHERE PartitionId=@normal ORDER BY SampleKey;
DECLARE @otherGeneration bigint=(SELECT MIN(GenerationKey) FROM monitoring.QueryGeneration WHERE PartitionId=@blocking);
BEGIN TRY
 BEGIN TRANSACTION;
 INSERT monitoring.QueryCounterSnapshot VALUES(@sample,@normal,@epoch,@otherGeneration,1,1,1,1,NULL,NULL);
 ROLLBACK;
 THROW 51000,'Cross-partition query identity was accepted.',1;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 IF ERROR_NUMBER()<>547 THROW;
 PRINT 'PASS: cross-partition counter identity rejected';
END CATCH;
BEGIN TRY
 BEGIN TRANSACTION;
 INSERT monitoring.SnapshotSection SELECT SampleKey,PartitionId,Section,CaptureStartUtc,CaptureEndUtc,ReferenceUtc,ReferenceTick,RunId,Status,
  ObservedRows,StoredRows,Truncated,FailureReason,ResetGeneration,KnownReset,IdentityReliable,TimingReliable
 FROM monitoring.SnapshotSection WHERE SampleKey=@sample AND Section='instance';
 ROLLBACK;
 THROW 51000,'Duplicate section was accepted.',1;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 IF ERROR_NUMBER() NOT IN(2601,2627) THROW;
 PRINT 'PASS: duplicate section rejected';
END CATCH;
-- Publication checks are rolled back; fixed fixtures remain unchanged.
BEGIN TRANSACTION;
DECLARE @newSample uniqueidentifier=NEWID(),@newKey bigint,@now datetime2(7)=SYSUTCDATETIME();
INSERT monitoring.CollectorLease VALUES(@normal,@run,777,DATEADD(minute,1,@now),@now);
INSERT monitoring.Snapshot(SampleId,PartitionId,EpochId,RunId,CollectionSequence,SchemaVersion,StartUtc,EndUtc,PublishedUtc,DurationTicks,Status,ExpectedSections,CollectedSections,FencingToken)
 VALUES(@newSample,@normal,@epoch,@run,999,3,@now,@now,NULL,0,'pending',5,0,777);
SET @newKey=SCOPE_IDENTITY();
INSERT monitoring.SnapshotSection SELECT @newKey,@normal,v.Section,@now,@now,@now,100000000,@run,'success',0,0,0,NULL,0,0,1,1
 FROM(VALUES('instance'),('activity'),('queries'),('waits'),('fileIo'))v(Section);
EXEC monitoring.PublishSnapshot @PartitionId=@normal,@SampleId=@newSample,@FencingToken=777;
IF NOT EXISTS(SELECT 1 FROM monitoring.Snapshot WHERE SampleKey=@newKey AND PublishedUtc IS NOT NULL AND Status='success') THROW 51000,'Publication failed.',1;
DECLARE @published datetime2(7)=(SELECT PublishedUtc FROM monitoring.Snapshot WHERE SampleKey=@newKey);
EXEC monitoring.PublishSnapshot @PartitionId=@normal,@SampleId=@newSample,@FencingToken=777;
IF (SELECT PublishedUtc FROM monitoring.Snapshot WHERE SampleKey=@newKey)<>@published THROW 51000,'Retry changed publication time.',1;
ROLLBACK;
PRINT 'PASS: transactional publication and same-ID retry';
BEGIN TRY
 BEGIN TRANSACTION;
 INSERT monitoring.CollectorLease VALUES(@normal,@run,888,DATEADD(second,-1,@now),@now);
 SET @newSample=NEWID();
 INSERT monitoring.Snapshot(SampleId,PartitionId,EpochId,RunId,CollectionSequence,SchemaVersion,StartUtc,EndUtc,PublishedUtc,DurationTicks,Status,ExpectedSections,CollectedSections,FencingToken)
  VALUES(@newSample,@normal,@epoch,@run,999,3,@now,@now,NULL,0,'pending',5,0,888);
 EXEC monitoring.PublishSnapshot @PartitionId=@normal,@SampleId=@newSample,@FencingToken=888;
 ROLLBACK;
 THROW 51000,'Expired lease was accepted.',1;
END TRY
BEGIN CATCH
 IF XACT_STATE()<>0 ROLLBACK;
 IF ERROR_NUMBER()<>51003 THROW;
 PRINT 'PASS: expired collector lease rejected';
END CATCH;
PRINT 'PASS: 3 versions, 9 scenarios and 19 samples; all verification mutations rolled back';
