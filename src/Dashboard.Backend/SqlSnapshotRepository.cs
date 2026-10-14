using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;

namespace Dashboard.Backend
{
    // The embedding application supplies its approved SQL provider/connection factory.
    // This library stores no connection strings, credentials or authentication mechanism.
    public sealed class SqlSnapshotRepository
    {
        public const int MaxSamples = 10000;
        public const int MaxRows = 100000;
        private readonly Func<DbConnection> connectionFactory;
        public SqlSnapshotRepository(Func<DbConnection> connectionFactory) => this.connectionFactory = connectionFactory;

        public PartitionContext GetContext(Guid authorizedInstanceId, string scenario)
        {
            if (scenario.Length > 40 || !System.Text.RegularExpressions.Regex.IsMatch(scenario, "^[a-z-]+$"))
                throw new DashboardException(400, "invalidFilter", "Invalid scenario identifier.");
            using (var connection = Open())
            using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                var contexts = Read(connection, transaction, @"
SELECT p.PartitionId,p.InstanceId,i.DisplayName,p.Mode,p.ScenarioId,p.EvaluationUtc
FROM monitoring.SourcePartition p JOIN monitoring.MonitoredInstance i ON i.InstanceId=p.InstanceId
WHERE p.InstanceId=@instance AND p.Mode='synthetic' AND p.ScenarioId=@scenario AND p.FixtureVersion=1 AND i.Enabled=1;",
                    r => new PartitionContext { PartitionId = GuidValue(r, "PartitionId"), InstanceId = GuidValue(r, "InstanceId"), InstanceName = Text(r, "DisplayName"),
                        SourceMode = Text(r, "Mode"), ScenarioId = Text(r, "ScenarioId"), EvaluationUtc = Utc(r, "EvaluationUtc")!.Value },
                    ("@instance", DbType.Guid, (object)authorizedInstanceId), ("@scenario", DbType.AnsiString, scenario)).SingleOrDefault();
                if (contexts == null) throw new DashboardException(404, "contextNotFound", "The authorized synthetic context was not found.");
                var databases = Read(connection, transaction, @"
SELECT TOP (101) d.DatabaseIdentityKey,o.CapturedName FROM monitoring.DatabaseIdentity d
OUTER APPLY(SELECT TOP(1) ob.CapturedName FROM monitoring.DatabaseObservation ob JOIN monitoring.Snapshot s ON s.SampleKey=ob.SampleKey
 WHERE ob.DatabaseIdentityKey=d.DatabaseIdentityKey AND s.PublishedUtc IS NOT NULL ORDER BY s.EndUtc DESC,s.SampleKey DESC)o
WHERE d.PartitionId=@partition ORDER BY d.DatabaseIdentityKey;",
                    r => new KeyValuePair<long, string>(Long(r, "DatabaseIdentityKey"), Text(r, "CapturedName")), ("@partition", DbType.Guid, (object)contexts.PartitionId));
                if (databases.Count > 100) throw Budget();
                foreach (var database in databases) contexts.Databases[database.Key] = database.Value;
                transaction.Commit(); return contexts;
            }
        }

        public SnapshotData Load(PartitionContext context, DashboardFilter filter)
        {
            if (filter.InstanceId != context.InstanceId || filter.SourceMode != "synthetic" || filter.ScenarioId != context.ScenarioId)
                throw new DashboardException(403, "contextMismatch", "Filter context is not authorized.");
            using (var connection = Open())
            using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                var data = new SnapshotData();
                if (filter.AsOfSampleId.HasValue && !Read(connection, transaction,
                    "SELECT SampleKey FROM monitoring.Snapshot WHERE PartitionId=@partition AND SampleId=@sample AND PublishedUtc IS NOT NULL;",
                    r => Long(r,"SampleKey"), ("@partition",DbType.Guid,(object)context.PartitionId),("@sample",DbType.Guid,filter.AsOfSampleId.Value)).Any())
                    throw new DashboardException(404,"sampleNotFound","The requested watermark is unavailable.");
                // Create outside the parameterized sp_executesql scope so subsequent commands see it.
                Execute(connection, transaction, "CREATE TABLE #Selected(SampleKey bigint NOT NULL PRIMARY KEY);");
                Execute(connection, transaction, @"
DECLARE @watermarkUtc datetime2(7),@watermarkKey bigint;
IF @watermark IS NOT NULL
 SELECT @watermarkUtc=EndUtc,@watermarkKey=SampleKey FROM monitoring.Snapshot
 WHERE PartitionId=@partition AND SampleId=@watermark AND PublishedUtc IS NOT NULL;
ELSE
 SELECT TOP(1) @watermarkUtc=EndUtc,@watermarkKey=SampleKey FROM monitoring.Snapshot
 WHERE PartitionId=@partition AND EndUtc<=@to AND PublishedUtc IS NOT NULL ORDER BY EndUtc DESC,SampleKey DESC;
INSERT #Selected(SampleKey) SELECT TOP(10001) s.SampleKey FROM monitoring.Snapshot s
WHERE s.PartitionId=@partition AND s.PublishedUtc IS NOT NULL AND s.EndUtc<=@to
 AND (s.EndUtc<@watermarkUtc OR (s.EndUtc=@watermarkUtc AND s.SampleKey<=@watermarkKey))
 AND (s.EndUtc>=@from OR s.SampleKey IN
   (SELECT previous.SampleKey FROM (VALUES('instance'),('activity'),('queries'),('waits'),('fileIo'))v(Section)
    CROSS APPLY(SELECT TOP(2) older.SampleKey FROM monitoring.Snapshot older JOIN monitoring.SnapshotSection sec ON sec.SampleKey=older.SampleKey
     WHERE older.PartitionId=@partition AND older.PublishedUtc IS NOT NULL AND older.EndUtc<@from AND sec.Section=v.Section AND sec.Status='success'
     ORDER BY older.EndUtc DESC,older.SampleKey DESC)previous))
ORDER BY s.EndUtc,s.SampleKey;",
                    ("@partition", DbType.Guid, (object)context.PartitionId), ("@from", DbType.DateTime2, filter.FromUtc), ("@to", DbType.DateTime2, filter.ToUtc),
                    ("@watermark", DbType.Guid, (object?)filter.AsOfSampleId ?? DBNull.Value));
                int selectedCount=Read(connection,transaction,"SELECT COUNT(*) AS N FROM #Selected;",r=>(int)Long(r,"N")).Single();
                if(selectedCount>MaxSamples) throw Budget();
                int rowCount = 0;
                void Add<T>(List<T> target, string sql, Func<DbDataReader, T> map)
                {
                    var values = Read(connection, transaction, sql, map);
                    rowCount += values.Count;
                    if (rowCount > MaxRows) throw Budget();
                    target.AddRange(values);
                }
                Add(data.Captures, @"SELECT TOP(100001) sec.*,s.SampleId,s.EpochId,run.MonotonicFrequency FROM #Selected selected
JOIN monitoring.Snapshot s ON s.SampleKey=selected.SampleKey JOIN monitoring.SnapshotSection sec ON sec.SampleKey=s.SampleKey
JOIN monitoring.CollectorRun run ON run.RunId=sec.RunId ORDER BY s.EndUtc,s.SampleKey,sec.Section;",
                    r => new Capture { SampleKey = Long(r,"SampleKey"), SampleId = GuidValue(r,"SampleId"), EpochId = NullableLong(r,"EpochId"), RunId = GuidValue(r,"RunId"),
                        Frequency=Long(r,"MonotonicFrequency"), Section=Text(r,"Section"), AttemptUtc=Utc(r,"CaptureEndUtc")!.Value, AtUtc=Utc(r,"ReferenceUtc"), Tick=NullableLong(r,"ReferenceTick"),
                        Status=Text(r,"Status"), Truncated=Bit(r,"Truncated"), KnownReset=Bit(r,"KnownReset"), ResetGeneration=Long(r,"ResetGeneration"),
                        IdentityReliable=Bit(r,"IdentityReliable"), TimingReliable=Bit(r,"TimingReliable"), FailureReason=NullableText(r,"FailureReason") });
                Add(data.Instances, @"SELECT TOP(100001) x.* FROM monitoring.InstanceSnapshot x JOIN #Selected s ON s.SampleKey=x.SampleKey;",
                    r => new InstanceCounter { SampleKey=Long(r,"SampleKey"), BatchCount=NullableLong(r,"BatchCount"), CounterIdentity=Text(r,"CounterObject")+"|"+Text(r,"CounterName")+"|"+Text(r,"CounterInstance"),
                        CounterType=(int)Long(r,"CounterType"), MemoryKb=NullableLong(r,"PhysicalMemoryKb"), EngineStartUtc=Utc(r,"EngineStartUtc") });
                Add(data.Queries, @"SELECT TOP(100001) x.*,g.DatabaseIdentityKey,g.QueryHash,g.IdentityReliable,summary.SafeSummary FROM monitoring.QueryCounterSnapshot x
JOIN #Selected s ON s.SampleKey=x.SampleKey JOIN monitoring.QueryGeneration g ON g.GenerationKey=x.GenerationKey
OUTER APPLY(SELECT TOP(1) q.SafeSummary FROM monitoring.QuerySummary q WHERE q.GenerationKey=g.GenerationKey ORDER BY q.Version DESC)summary;",
                    r => new QueryCounter { SampleKey=Long(r,"SampleKey"), GenerationId=Long(r,"GenerationKey"), DatabaseId=NullableLong(r,"DatabaseIdentityKey"),
                        QueryHash=r.IsDBNull(r.GetOrdinal("QueryHash"))?null:BitConverter.ToString((byte[])r["QueryHash"]).Replace("-",""), Summary=NullableText(r,"SafeSummary"),
                        IdentityReliable=Bit(r,"IdentityReliable"), Executions=Long(r,"ExecutionCount"), CpuUs=Long(r,"TotalWorkerUs"), ElapsedUs=Long(r,"TotalElapsedUs"),
                        LogicalReads=Long(r,"TotalLogicalReads"), LastExecutionUtc=Utc(r,"LastExecutionUtc") });
                Add(data.Waits, @"SELECT TOP(100001) x.* FROM monitoring.WaitCounterSnapshot x JOIN #Selected s ON s.SampleKey=x.SampleKey;",
                    r => new WaitCounter { SampleKey=Long(r,"SampleKey"), WaitType=Text(r,"WaitType"), Tasks=Long(r,"WaitingTasksCount"), TotalMs=Long(r,"WaitTimeMs"), SignalMs=Long(r,"SignalWaitMs") });
                Add(data.Files, @"SELECT TOP(100001) x.*,f.DatabaseIdentityKey,f.IdentityReliable FROM monitoring.FileIoSnapshot x JOIN #Selected s ON s.SampleKey=x.SampleKey
JOIN monitoring.FileIdentity f ON f.FileIdentityKey=x.FileIdentityKey;",
                    r => new FileCounter { SampleKey=Long(r,"SampleKey"), FileId=Long(r,"FileIdentityKey"), DatabaseId=Long(r,"DatabaseIdentityKey"), Name=Text(r,"LogicalName"), Type=Text(r,"FileType"),
                        IdentityReliable=Bit(r,"IdentityReliable"), Reads=Long(r,"NumReads"), ReadBytes=Long(r,"BytesRead"), ReadStallMs=Long(r,"ReadStallMs"),
                        Writes=Long(r,"NumWrites"), WriteBytes=Long(r,"BytesWritten"), WriteStallMs=Long(r,"WriteStallMs") });
                Add(data.Sessions, @"SELECT TOP(100001) x.*,i.SessionId FROM monitoring.SessionSnapshot x JOIN #Selected s ON s.SampleKey=x.SampleKey
JOIN monitoring.SessionIdentity i ON i.SessionIdentityKey=x.SessionIdentityKey;",
                    r => new SessionObservation { SampleKey=Long(r,"SampleKey"), SessionIdentity=Long(r,"SessionIdentityKey"), SessionId=(int)Long(r,"SessionId"), DatabaseId=NullableLong(r,"DatabaseIdentityKey"),
                        UserProcess=Bit(r,"IsUserProcess"), Collector=Bit(r,"IsCollector"), Status=Text(r,"Status"), OpenTransactions=(int)Long(r,"OpenTransactions") });
                Add(data.Requests, @"SELECT TOP(100001) x.*,i.SessionIdentityKey,i.RequestId FROM monitoring.RequestSnapshot x JOIN #Selected s ON s.SampleKey=x.SampleKey
JOIN monitoring.RequestIdentity i ON i.RequestIdentityKey=x.RequestIdentityKey;",
                    r => new RequestObservation { SampleKey=Long(r,"SampleKey"), RequestIdentity=Long(r,"RequestIdentityKey"), SessionIdentity=Long(r,"SessionIdentityKey"), RequestId=(int)Long(r,"RequestId"),
                        DatabaseId=NullableLong(r,"DatabaseIdentityKey"), Status=Text(r,"Status"), WaitType=NullableText(r,"CurrentWaitType"), WaitMs=Long(r,"CurrentWaitMs"),
                        ElapsedMs=Long(r,"ElapsedRequestMs"), Transactions=(int)Long(r,"OpenTransactions"), BlockingSessionId=(int?)NullableLong(r,"RawBlockingSessionId") });
                foreach (var wait in Read(connection, transaction, "SELECT WaitType FROM monitoring.WaitExclusionType WHERE PolicyVersion='demo-v1';", r => Text(r,"WaitType"))) data.ExcludedWaitTypes.Add(wait);
                // Selected sample names, rather than today's names, determine historical labels.
                var observations = Read(connection, transaction, @"SELECT TOP(100001) ob.DatabaseIdentityKey,ob.CapturedName FROM monitoring.DatabaseObservation ob
JOIN #Selected selected ON selected.SampleKey=ob.SampleKey ORDER BY ob.SampleKey;", r => new KeyValuePair<long,string>(Long(r,"DatabaseIdentityKey"),Text(r,"CapturedName")));
                rowCount += observations.Count; if (rowCount > MaxRows) throw Budget();
                foreach (var observation in observations) context.Databases[observation.Key]=observation.Value;
                transaction.Commit(); return data;
            }
        }

        private DbConnection Open()
        {
            var connection=connectionFactory();
            try
            {
                if (connection.State!=ConnectionState.Closed || !string.Equals(connection.DataSource,@"(localdb)\SQLDashboardDev",StringComparison.OrdinalIgnoreCase) ||
                    connection.Database!="SQLDashboard_Development")
                    throw new DashboardException(503,"developmentDatabaseRequired","Supply a closed connection to the dedicated local development database.");
                connection.Open();
                using(var command=connection.CreateCommand())
                {
                    command.CommandTimeout=5;
                    command.CommandText=@"SELECT CASE WHEN DB_NAME()='SQLDashboard_Development' AND CONVERT(int,SERVERPROPERTY('IsLocalDB'))=1 AND EXISTS
(SELECT 1 FROM sys.extended_properties WHERE class=0 AND name=N'DashboardDevelopmentDatabase' AND CONVERT(nvarchar(128),value)=N'synthetic-only') THEN 1 ELSE 0 END;";
                    if(Convert.ToInt32(command.ExecuteScalar(),CultureInfo.InvariantCulture)!=1) throw new DashboardException(503,"developmentDatabaseRequired","Only the marked LocalDB development database is supported.");
                }
                return connection;
            }
            catch { connection.Dispose(); throw; }
        }
        private static DbCommand Command(DbConnection c,DbTransaction t,string sql,params (string Name,DbType Type,object Value)[] args)
        {
            var cmd=c.CreateCommand();cmd.Transaction=t;cmd.CommandTimeout=5;cmd.CommandText=sql;
            foreach(var arg in args){var p=cmd.CreateParameter();p.ParameterName=arg.Name;p.DbType=arg.Type;p.Value=arg.Value;if(arg.Type==DbType.AnsiString)p.Size=40;cmd.Parameters.Add(p);}return cmd;
        }
        private static List<T> Read<T>(DbConnection c,DbTransaction t,string sql,Func<DbDataReader,T> map,params (string Name,DbType Type,object Value)[] args)
        {
            var results=new List<T>();
            using(var cmd=Command(c,t,sql,args))using(var reader=cmd.ExecuteReader())while(reader.Read()) {if(results.Count>=MaxRows)throw Budget();results.Add(map(reader));}
            return results;
        }
        private static void Execute(DbConnection c,DbTransaction t,string sql,params (string Name,DbType Type,object Value)[] args)
        {using(var cmd=Command(c,t,sql,args))cmd.ExecuteNonQuery();}
        private static long Long(DbDataReader r,string name)=>Convert.ToInt64(r[name],CultureInfo.InvariantCulture);
        private static long? NullableLong(DbDataReader r,string name)=>r.IsDBNull(r.GetOrdinal(name))?null:(long?)Long(r,name);
        private static string Text(DbDataReader r,string name)=>Convert.ToString(r[name],CultureInfo.InvariantCulture)??"";
        private static string? NullableText(DbDataReader r,string name)=>r.IsDBNull(r.GetOrdinal(name))?null:Text(r,name);
        private static bool Bit(DbDataReader r,string name)=>Convert.ToBoolean(r[name],CultureInfo.InvariantCulture);
        private static Guid GuidValue(DbDataReader r,string name)=>(Guid)r[name];
        private static DateTime? Utc(DbDataReader r,string name)=>r.IsDBNull(r.GetOrdinal(name))?null:(DateTime?)DateTime.SpecifyKind((DateTime)r[name],DateTimeKind.Utc);
        private static DashboardException Budget()=>new DashboardException(422,"rangeTooLarge","Read budget exceeded; select a narrower range or database scope.");
    }
}
