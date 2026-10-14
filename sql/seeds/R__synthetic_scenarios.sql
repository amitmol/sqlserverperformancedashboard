-- Fabricated counters and observations only. No DMV reads and no workload execution.
-- Repeatable: atomically replaces ONLY these version-1 synthetic fixture partitions.
SET XACT_ABORT ON;
SET NOCOUNT ON;
IF DB_NAME()<>N'SQLDashboard_Development' OR NOT EXISTS(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DashboardDevelopmentDatabase' AND CONVERT(nvarchar(128),value)=N'synthetic-only')
 THROW 51000, 'Synthetic seed requires the marked dashboard development database.', 1;
IF NOT EXISTS(SELECT 1 FROM monitoring.SchemaVersion WHERE Version=3) THROW 51000, 'Apply all migrations first.', 1;
DECLARE @instance uniqueidentifier='11111111-1111-1111-1111-111111111111';
DECLARE @scenarios TABLE(N int PRIMARY KEY,Name varchar(40));
INSERT @scenarios VALUES(1,'normal'),(2,'blocking'),(3,'high-query-reads'),(4,'io-slowdown'),(5,'no-activity'),(6,'counter-reset'),(7,'weighted-io'),(8,'gap'),(9,'partial');
BEGIN TRANSACTION;
DECLARE @lockResult int;
EXEC @lockResult=sys.sp_getapplock @Resource=N'DashboardSyntheticSeedV1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=5000;
IF @lockResult<0 THROW 51000, 'Another synthetic seed is running.', 1;
DECLARE @old TABLE(PartitionId uniqueidentifier PRIMARY KEY);
INSERT @old SELECT p.PartitionId FROM monitoring.SourcePartition p JOIN @scenarios s ON s.Name=p.ScenarioId
 WHERE p.InstanceId=@instance AND p.Mode='synthetic' AND p.FixtureVersion=1;
DELETE x FROM monitoring.BlockingEdgeSnapshot x JOIN @old p ON p.PartitionId=x.PartitionId;
DELETE x FROM monitoring.RequestSnapshot x JOIN @old p ON p.PartitionId=x.PartitionId;
DELETE x FROM monitoring.SessionSnapshot x JOIN @old p ON p.PartitionId=x.PartitionId;
DELETE x FROM monitoring.FileIoSnapshot x JOIN @old p ON p.PartitionId=x.PartitionId;
DELETE x FROM monitoring.WaitCounterSnapshot x JOIN @old p ON p.PartitionId=x.PartitionId;
DELETE x FROM monitoring.QueryCounterSnapshot x JOIN @old p ON p.PartitionId=x.PartitionId;
DELETE x FROM monitoring.InstanceSnapshot x JOIN @old p ON p.PartitionId=x.PartitionId;
DELETE x FROM monitoring.DatabaseObservation x JOIN @old p ON p.PartitionId=x.PartitionId;
DELETE x FROM monitoring.SnapshotSection x JOIN @old p ON p.PartitionId=x.PartitionId;
DELETE x FROM monitoring.Snapshot x JOIN @old p ON p.PartitionId=x.PartitionId;
DELETE x FROM monitoring.QuerySummary x JOIN monitoring.QueryGeneration g ON g.GenerationKey=x.GenerationKey JOIN @old p ON p.PartitionId=g.PartitionId;
DELETE x FROM monitoring.QueryGeneration x JOIN @old p ON p.PartitionId=x.PartitionId;
DELETE x FROM monitoring.RequestIdentity x JOIN @old p ON p.PartitionId=x.PartitionId;
DELETE x FROM monitoring.SessionIdentity x JOIN @old p ON p.PartitionId=x.PartitionId;
DELETE x FROM monitoring.FileIdentity x JOIN @old p ON p.PartitionId=x.PartitionId;
DELETE x FROM monitoring.DatabaseIdentity x JOIN @old p ON p.PartitionId=x.PartitionId;
DELETE x FROM monitoring.CollectionGap x JOIN @old p ON p.PartitionId=x.PartitionId;
DELETE x FROM monitoring.RetentionCheckpoint x JOIN @old p ON p.PartitionId=x.PartitionId;
DELETE x FROM monitoring.CollectorLease x JOIN @old p ON p.PartitionId=x.PartitionId;
DELETE x FROM monitoring.CollectorRun x JOIN @old p ON p.PartitionId=x.PartitionId;
DELETE x FROM monitoring.EngineEpoch x JOIN @old p ON p.PartitionId=x.PartitionId;
DELETE x FROM monitoring.SourcePartition x JOIN @old p ON p.PartitionId=x.PartitionId;
IF NOT EXISTS(SELECT 1 FROM monitoring.MonitoredInstance WHERE InstanceId=@instance)
 INSERT monitoring.MonitoredInstance VALUES(@instance,N'DEMO-SQL-01',N'synthetic-demo',1);
DECLARE @n int=1;
WHILE @n<=9
BEGIN
 DECLARE @scenario varchar(40)=(SELECT Name FROM @scenarios WHERE N=@n);
 DECLARE @suffix varchar(12)=RIGHT('000000000000'+CONVERT(varchar(12),@n),12);
 DECLARE @partition uniqueidentifier=CONVERT(uniqueidentifier,'22222222-2222-2222-2222-'+@suffix);
 DECLARE @run uniqueidentifier=CONVERT(uniqueidentifier,'33333333-3333-3333-3333-'+@suffix);
 DECLARE @a datetime2(7)='2026-10-13T10:00:00', @b datetime2(7)=DATEADD(second,CASE WHEN @scenario='gap' THEN 45 ELSE 15 END,CONVERT(datetime2(7),'2026-10-13T10:00:00'));
 DECLARE @engine datetime2(7)='2026-10-06T10:00:15';
 INSERT monitoring.SourcePartition VALUES(@partition,@instance,'synthetic',@scenario,1,30,DATEADD(second,5,@b));
 INSERT monitoring.EngineEpoch(PartitionId,PhysicalIdentity,EngineStartRaw,EngineStartUtc,TimezoneProvenance,FirstSeenUtc,LastSeenUtc)
 VALUES(@partition,N'SYNTHETIC-PHYSICAL-01',@engine,@engine,N'synthetic UTC',@a,@b);
 DECLARE @epoch bigint=SCOPE_IDENTITY();
 INSERT monitoring.CollectorRun VALUES(@run,@partition,@run,1000000,@a,NULL,N'synthetic deterministic ticks');
 INSERT monitoring.DatabaseIdentity(PartitionId,ServerDatabaseId,IncarnationId,FirstSeenUtc,LastSeenUtc)
 VALUES(@partition,5,@partition,@a,@b);
 DECLARE @database bigint=SCOPE_IDENTITY();
 DECLARE @files TABLE(K bigint,N int);
 DELETE @files;
 INSERT monitoring.FileIdentity(PartitionId,DatabaseIdentityKey,ServerFileId,IncarnationId,IdentityReliable,FirstSeenUtc,LastSeenUtc)
 OUTPUT inserted.FileIdentityKey,inserted.ServerFileId INTO @files
 SELECT @partition,@database,v.N,@partition,1,@a,@b FROM(VALUES(1),(2))v(N);
 DECLARE @queries TABLE(K bigint,N int);
 DELETE @queries;
 INSERT monitoring.QueryGeneration(PartitionId,EpochId,DatabaseIdentityKey,ContextState,PlanHandle,SqlHandle,StartOffset,EndOffset,CreationRaw,CreationUtc,PlanGeneration,QueryHash,IdentityReliable,FirstSeenUtc,LastSeenUtc)
 OUTPUT inserted.GenerationKey,CONVERT(int,inserted.PlanGeneration) INTO @queries
 SELECT @partition,@epoch,@database,'known',CONVERT(binary(8),CONVERT(bigint,v.N)),CONVERT(binary(8),CONVERT(bigint,v.N)),0,-1,@a,@a,v.N,CONVERT(binary(8),CONVERT(bigint,v.N)),1,@a,@b FROM(VALUES(1),(2),(3))v(N);
 INSERT monitoring.QuerySummary(GenerationKey,Version,SafeSummary,RedactionPolicyVersion,TextState,CreatedUtc)
 SELECT K,1,CASE N WHEN 1 THEN N'Synthetic parameterized sales lookup' WHEN 2 THEN N'Synthetic parameterized report scan' ELSE N'Synthetic idle cached statement' END,'synthetic-v1','redacted',@a FROM @queries;
 DECLARE @sessions TABLE(K bigint,N int);
 DELETE @sessions;
 DECLARE @s int=1;
 WHILE @s<=24
 BEGIN
  DECLARE @sid smallint=CASE WHEN @s=1 THEN 51 WHEN @s=2 THEN 62 WHEN @s=3 THEN 63 ELSE 100+@s END;
  INSERT monitoring.SessionIdentity(PartitionId,EpochId,SessionId,LoginRaw,LoginUtc) VALUES(@partition,@epoch,@sid,DATEADD(minute,-5,@a),DATEADD(minute,-5,@a));
  INSERT @sessions VALUES(SCOPE_IDENTITY(),@sid);
  SET @s+=1;
 END;
 DECLARE @requests TABLE(K bigint,S bigint,N int);
 DELETE @requests;
 INSERT monitoring.RequestIdentity(PartitionId,EpochId,SessionIdentityKey,RequestId,StartRaw,StartUtc)
 OUTPUT inserted.RequestIdentityKey,inserted.SessionIdentityKey,inserted.RequestId INTO @requests
 SELECT TOP(8) @partition,@epoch,K,0,DATEADD(second,-20,@a),DATEADD(second,-20,@a) FROM @sessions WHERE N<>51 ORDER BY N;
 DECLARE @i int=0;
 WHILE @i<CASE WHEN @scenario='counter-reset' THEN 3 ELSE 2 END
 BEGIN
  DECLARE @time datetime2(7)=CASE @i WHEN 0 THEN @a WHEN 1 THEN @b ELSE DATEADD(second,15,@b) END;
  DECLARE @sample uniqueidentifier=CONVERT(uniqueidentifier,'44444444-4444-4444-'+RIGHT('0000'+CONVERT(varchar(4),@n),4)+'-'+RIGHT('000000000000'+CONVERT(varchar(12),@i+1),12));
  DECLARE @active int=CASE WHEN @scenario='no-activity' THEN 0 ELSE 1 END;
  INSERT monitoring.Snapshot(SampleId,PartitionId,EpochId,RunId,CollectionSequence,SchemaVersion,StartUtc,EndUtc,PublishedUtc,DurationTicks,Status,ExpectedSections,CollectedSections,FencingToken)
  VALUES(@sample,@partition,@epoch,@run,@i+1,3,@time,@time,NULL,0,'pending',5,CASE WHEN @scenario='partial' AND @i=1 THEN 4 ELSE 5 END,1);
  DECLARE @key bigint=SCOPE_IDENTITY();
  INSERT monitoring.SnapshotSection
  SELECT @key,@partition,v.Section,@time,@time,
   CASE WHEN @scenario='partial' AND @i=1 AND v.Section='waits' THEN NULL ELSE @time END,
   CASE WHEN @scenario='partial' AND @i=1 AND v.Section='waits' THEN NULL ELSE DATEDIFF_BIG(microsecond,@a,@time) END,
   @run,CASE WHEN @scenario='partial' AND @i=1 AND v.Section='waits' THEN 'failure' ELSE 'success' END,
   0,0,0,CASE WHEN @scenario='partial' AND @i=1 AND v.Section='waits' THEN 'permissionDenied' END,0,0,1,1
  FROM(VALUES('instance'),('activity'),('queries'),('waits'),('fileIo'))v(Section);
  INSERT monitoring.InstanceSnapshot VALUES(@key,@partition,@epoch,
   CASE WHEN @scenario='counter-reset' AND @i>0 THEN CASE @i WHEN 1 THEN 50 ELSE 350 END ELSE 100000+1800*@i*@active END,
   N'SQLServer:SQL Statistics',N'Batch Requests/sec',N'',272696576,4194304,@engine);
  INSERT monitoring.DatabaseObservation VALUES(@key,@partition,@database,N'DemoSales','known');
  INSERT monitoring.QueryCounterSnapshot
  SELECT @key,@partition,@epoch,K,CASE N WHEN 1 THEN 100+20*@i*@active WHEN 2 THEN 200+10*@i*@active ELSE 10000 END,
   CASE N WHEN 1 THEN 5000000+2000000*@i*@active WHEN 2 THEN 9000000+3000000*@i*@active ELSE 900000000 END,
   CASE N WHEN 1 THEN 8000000+4000000*@i*@active WHEN 2 THEN 12000000+5000000*@i*@active ELSE 1000000000 END,
   CASE N WHEN 1 THEN 80000+30000*@i*@active WHEN 2 THEN 100000+CASE WHEN @scenario='high-query-reads' THEN 5000000 ELSE 5000 END*@i*@active ELSE 10000000 END,@time,@time FROM @queries;
  IF NOT(@scenario='partial' AND @i=1)
   INSERT monitoring.WaitCounterSnapshot SELECT @key,@partition,v.W,
    100+@i*10*@active,v.T+v.D*@i*@active,v.S+v.SD*@i*@active
   FROM(VALUES(N'PAGEIOLATCH_SH',10000,6000,1000,600),(N'LCK_M_X',2000,2000,200,200),(N'SLEEP_TASK',1000,8000,0,0))v(W,T,D,S,SD);
  INSERT monitoring.FileIoSnapshot
  SELECT @key,@partition,K,CASE WHEN @scenario='weighted-io' THEN 1000+@i*CASE N WHEN 1 THEN 10 ELSE 90 END ELSE 1000+@i*CASE N WHEN 1 THEN 300 ELSE 0 END*@active END,
   CONVERT(bigint,104857600)+@i*CASE WHEN @scenario='weighted-io' THEN CASE N WHEN 1 THEN 81920 ELSE 737280 END ELSE CASE N WHEN 1 THEN 31457280 ELSE 0 END*@active END,
   5000+@i*CASE WHEN @scenario='weighted-io' THEN CASE N WHEN 1 THEN 20 ELSE 900 END WHEN @scenario='io-slowdown' THEN CASE N WHEN 1 THEN 15000 ELSE 0 END ELSE CASE N WHEN 1 THEN 1500 ELSE 0 END*@active END,
   500+60*@i*@active,CONVERT(bigint,52428800)+6291456*@i*@active,
   1000+@i*CASE WHEN @scenario='io-slowdown' THEN 6000 ELSE 180*@active END,
   DATEDIFF_BIG(millisecond,@a,@time),CASE N WHEN 1 THEN N'DemoSales_Data' ELSE N'DemoSales_Log' END,CASE N WHEN 1 THEN 'data' ELSE 'log' END FROM @files;
  INSERT monitoring.SessionSnapshot SELECT @key,@partition,@epoch,K,1,0,@database,CASE N WHEN 51 THEN N'sleeping' ELSE N'running' END,
   CASE WHEN N=51 AND @scenario='blocking' THEN 1 ELSE 0 END FROM @sessions;
  IF @active=1
  BEGIN
   INSERT monitoring.RequestSnapshot
   SELECT @key,@partition,@epoch,r.K,@database,
    CASE WHEN @scenario='blocking' AND s.N IN(62,63) THEN N'suspended' ELSE N'running' END,
    CASE WHEN @scenario='blocking' AND s.N IN(62,63) THEN N'LCK_M_X' END,
    CASE WHEN @scenario='blocking' AND s.N=62 THEN 12000 WHEN @scenario='blocking' AND s.N=63 THEN 8000 ELSE 0 END,
    20000+15000*@i,0,CASE WHEN @scenario='blocking' AND s.N IN(62,63) THEN 51 ELSE 0 END
   FROM @requests r JOIN @sessions s ON s.K=r.S;
   INSERT monitoring.BlockingEdgeSnapshot
   SELECT @key,@partition,@epoch,r.RequestIdentityKey,(SELECT K FROM @sessions WHERE N=51),51,'session','observed',@time
   FROM monitoring.RequestSnapshot r WHERE r.SampleKey=@key AND r.RawBlockingSessionId=51;
  END;
  UPDATE sec SET ObservedRows=c.N,StoredRows=c.N FROM monitoring.SnapshotSection sec
   CROSS APPLY(SELECT CASE sec.Section WHEN 'instance' THEN 1 WHEN 'queries' THEN 3 WHEN 'waits' THEN CASE WHEN sec.Status='failure' THEN 0 ELSE 3 END WHEN 'fileIo' THEN 2 ELSE 24+CASE WHEN @active=1 THEN 8 ELSE 0 END END N)c
   WHERE sec.SampleKey=@key;
  UPDATE monitoring.Snapshot SET Status=CASE WHEN @scenario='partial' AND @i=1 THEN 'partial' ELSE 'success' END,PublishedUtc=@time WHERE SampleKey=@key;
  SET @i+=1;
 END;
 UPDATE monitoring.SourcePartition SET EvaluationUtc=DATEADD(second,5,@time) WHERE PartitionId=@partition;
 SET @n+=1;
END;
COMMIT;
SELECT N'Synthetic data — demonstration only — not connected to SQL Server.' AS Label,
 p.ScenarioId,COUNT(*) AS Samples FROM monitoring.SourcePartition p JOIN monitoring.Snapshot s ON s.PartitionId=p.PartitionId
 WHERE p.InstanceId=@instance AND p.Mode='synthetic' GROUP BY p.ScenarioId;
