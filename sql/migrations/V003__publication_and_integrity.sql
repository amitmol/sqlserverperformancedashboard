SET XACT_ABORT ON;
SET NOCOUNT ON;
IF DB_NAME()<>N'SQLDashboard_Development' OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DashboardDevelopmentDatabase' AND CONVERT(nvarchar(128),value)=N'synthetic-only')
 THROW 51000, 'Only the marked development database is supported.', 1;
IF NOT EXISTS(SELECT 1 FROM monitoring.SchemaVersion WHERE Version=2) THROW 51000, 'Apply V002 first.', 1;
IF EXISTS(SELECT 1 FROM monitoring.SchemaVersion WHERE Version=3) RETURN;
BEGIN TRANSACTION;
ALTER TABLE monitoring.Snapshot ADD CONSTRAINT UQ_SnapshotRun UNIQUE(PartitionId,RunId,SampleKey);
ALTER TABLE monitoring.SnapshotSection ADD CONSTRAINT FK_SectionSnapshotRun FOREIGN KEY(PartitionId,RunId,SampleKey)
 REFERENCES monitoring.Snapshot(PartitionId,RunId,SampleKey);
ALTER TABLE monitoring.BlockingEdgeSnapshot ADD CONSTRAINT CK_EdgeIdentity CHECK
 ((OwnerKind='session' AND BlockerSessionIdentityKey IS NOT NULL AND RelationshipState='observed') OR
 (OwnerKind='missing' AND BlockerSessionIdentityKey IS NULL AND RelationshipState='missing') OR
 (OwnerKind='special' AND BlockerSessionIdentityKey IS NULL AND RelationshipState='special'));
-- A source writer inserts pending header/facts in a transaction, then calls this
-- procedure in that SAME transaction. No live collector is implemented here.
EXEC(N'CREATE PROCEDURE monitoring.PublishSnapshot
 @PartitionId uniqueidentifier,@SampleId uniqueidentifier,@FencingToken bigint
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @@TRANCOUNT=0 THROW 51000,''Publication must be inside the writer transaction.'',1;
 DECLARE @key bigint,@status varchar(12),@run uniqueidentifier;
 SELECT @key=SampleKey,@status=Status,@run=RunId FROM monitoring.Snapshot WITH(UPDLOCK,HOLDLOCK)
 WHERE PartitionId=@PartitionId AND SampleId=@SampleId AND FencingToken=@FencingToken;
 IF @key IS NULL THROW 51004,''Sample unavailable.'',1;
 IF @status<>''pending'' RETURN;
 IF NOT EXISTS(SELECT 1 FROM monitoring.CollectorLease WITH(UPDLOCK,HOLDLOCK)
  WHERE PartitionId=@PartitionId AND FencingToken=@FencingToken AND ExpiresUtc>SYSUTCDATETIME())
  THROW 51003,''Collector lease is expired or fenced.'',1;
 IF (SELECT COUNT(*) FROM monitoring.SnapshotSection WHERE SampleKey=@key)<>5
  THROW 51000,''All section outcomes must be recorded.'',1;
 IF EXISTS(SELECT 1 FROM monitoring.SnapshotSection sec WHERE sec.SampleKey=@key AND sec.RunId<>@run)
  THROW 51000,''Section timing run mismatch.'',1;
 DECLARE @successful int=(SELECT COUNT(*) FROM monitoring.SnapshotSection WHERE SampleKey=@key AND Status=''success'');
 UPDATE monitoring.Snapshot SET Status=CASE WHEN @successful=5 THEN ''success''
  WHEN EXISTS(SELECT 1 FROM monitoring.SnapshotSection WHERE SampleKey=@key AND Status<>''failure'') THEN ''partial'' ELSE ''failure'' END,
  CollectedSections=@successful,PublishedUtc=SYSUTCDATETIME(),SchemaVersion=3 WHERE SampleKey=@key;
END');
INSERT monitoring.SchemaVersion(Version) VALUES(3);
COMMIT;
