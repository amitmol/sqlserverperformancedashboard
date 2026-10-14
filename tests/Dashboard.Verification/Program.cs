using Dashboard.Backend;
using Microsoft.Data.SqlClient;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAILED: " + name);
    passed++; Console.WriteLine("PASS " + name);
}
void Equal(decimal? actual, decimal? expected, string name) => Check(actual == expected, name + $" ({actual?.ToString() ?? "null"})");
void Throws(Action action, int status, string name)
{
    try { action(); throw new Exception("FAILED: " + name + " did not reject"); }
    catch (DashboardException ex) { Check(ex.StatusCode == status, name); }
}
Capture CaptureAt(int seconds) => new Capture { SampleKey = seconds + 1, SampleId = Guid.NewGuid(), EpochId = 1, RunId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
    Frequency = 1000000, Section = "instance", AtUtc = new DateTime(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc).AddSeconds(seconds), Tick = seconds * 1000000L,
    Status = "success", IdentityReliable = true, TimingReliable = true };
var compare = new SnapshotComparisonService();
var a = CaptureAt(0); var b = CaptureAt(15);
var x = new InstanceCounter { BatchCount = 100000, CounterType = 272696576, CounterIdentity = "batch" };
var y = new InstanceCounter { BatchCount = 101800, CounterType = 272696576, CounterIdentity = "batch" };
Equal(compare.Batch(a,b,x,y).Value,120,"batch rate PRD vector");
Equal(compare.Batch(a,CaptureAt(20),x,y).Value,90,"actual 20-second duration");
Check(compare.Compare(a,CaptureAt(45)).State=="gap","45-second gap");
Check(compare.Batch(null,b,null,y).State=="baselineRequired","first sample baseline");
y.BatchCount=50; Check(compare.Batch(a,b,x,y).State=="reset" && compare.Batch(a,b,x,y).Value==null,"decrease is reset, never clamped");
y.BatchCount=101800; b.EpochId=2; Check(compare.Batch(a,b,x,y).State=="reset","engine epoch resets increasing counters"); b.EpochId=1;
b.RunId=Guid.NewGuid(); Check(compare.Compare(a,b).Reason=="timingBaselineRequired","collector restart timing baseline"); b.RunId=a.RunId;
b.KnownReset=true; Check(compare.Compare(a,b).State=="reset","known clear despite increasing counters"); b.KnownReset=false;
b.Truncated=true; Check(!compare.Compare(a,b).Valid,"truncated population rejected"); b.Truncated=false;
b.Tick=0; Check(compare.Compare(a,b).State=="invalid","zero elapsed rejected"); b.Tick=15000000;
b.AtUtc=b.AtUtc!.Value.AddSeconds(3); Check(compare.Compare(a,b).Reason=="clockDiscontinuity","UTC discontinuity"); b.AtUtc=b.AtUtc.Value.AddSeconds(-3);
Equal(SnapshotComparisonService.Ratio(1,0),null,"zero denominator");
foreach (var item in new[]{(30,"fresh"),(31,"delayed"),(60,"delayed"),(61,"stale")})
    Check(SnapshotComparisonService.Freshness(a.AtUtc,a.AtUtc!.Value.AddSeconds(item.Item1))==item.Item2,"freshness boundary "+item.Item1);
Check(SnapshotComparisonService.Freshness(b.AtUtc,a.AtUtc!.Value)=="timestampError","future capture is timestamp error");
var context=new PartitionContext{InstanceId=Guid.Parse("11111111-1111-1111-1111-111111111111"),SourceMode="synthetic",ScenarioId="normal",EvaluationUtc=b.AtUtc!.Value.AddSeconds(5)};
context.Databases[1]="DemoSales";
var input=new Dictionary<string,string>{{"instanceId",context.InstanceId.ToString()},{"sourceMode","synthetic"},{"scenarioId","normal"}};
var validator=new FilterValidator();
var filter=validator.Parse(input,context,"Queries"); Check(filter.PageSize==20 && filter.Top==20,"bounded defaults");
input["sort"]="cpu; DROP TABLE x"; Throws(()=>validator.Parse(input,context,"Queries"),400,"sort injection rejected"); input.Remove("sort");
input["pageSize"]="10000"; Throws(()=>validator.Parse(input,context,"Queries"),400,"oversized page rejected"); input.Remove("pageSize");
input["databaseId"]="99"; Throws(()=>validator.Parse(input,context,"Queries"),400,"foreign database rejected"); input.Remove("databaseId");
input["connectionString"]="server=x"; Throws(()=>validator.Parse(input,context,"Queries"),400,"unknown target filter rejected"); input.Remove("connectionString");
Throws(()=>FilterValidator.ParseUtc("2026-10-13T10:00:00"),400,"naive timestamp rejected");
Check(FilterValidator.ParseUtc("2026-10-13T15:30:00+05:30")==a.AtUtc,"offset normalized to UTC");
input["range"]="custom"; input["fromUtc"]="2026-10-13T10:00:15Z"; input["toUtc"]="2026-10-13T10:00:00Z";
Throws(()=>validator.Parse(input,context,"Queries"),400,"reversed range rejected");
var weighted=new IoTotals{Reads=100,ReadStallMs=920,Seconds=15};Equal(weighted.ReadLatencyMs,9.2m,"operation-weighted latency");
Equal(new IoTotals{Seconds=15}.ReadIops,0,"no I/O throughput zero"); Equal(new IoTotals{Seconds=15}.ReadLatencyMs,null,"no I/O latency undefined");
Equal(new QueryRow().AvgCpuMs,null,"no executions average undefined");

if(args.Contains("--sql"))
{
    string raw=Environment.GetEnvironmentVariable("DashboardDevelopmentConnection") ?? throw new Exception("Set DashboardDevelopmentConnection for --sql.");
    var connection=new SqlConnectionStringBuilder(raw);
    Check(string.Equals(connection.DataSource,"(localdb)\\SQLDashboardDev",StringComparison.OrdinalIgnoreCase) &&
        connection.InitialCatalog=="SQLDashboard_Development" && connection.IntegratedSecurity,"integration target is fixed local development database");
    var repo=new SqlSnapshotRepository(()=>new SqlConnection(raw)); var service=new DashboardService();
    (PartitionContext Context,DashboardFilter Filter,SnapshotData Data) Load(string scenario,string endpoint)
    {
        var c=repo.GetContext(context.InstanceId,scenario);
        var f=validator.Parse(new Dictionary<string,string>{{"instanceId",c.InstanceId.ToString()},{"sourceMode","synthetic"},{"scenarioId",scenario}},c,endpoint);
        return (c,f,repo.Load(c,f));
    }
    var normal=Load("normal","Queries");
    var overview=service.Overview(normal.Data,normal.Context,normal.Filter);
    Equal(overview.Single(m=>m.Key=="batchRequestsPerSec").Value,120,"persisted batch calculation");
    Equal(overview.Single(m=>m.Key=="processMemoryMiB").Value,4096,"persisted process memory conversion");
    Equal(overview.Single(m=>m.Key=="uptimeSeconds").Value,604800,"persisted UTC uptime");
    Equal(overview.Single(m=>m.Key=="userSessions").Value,24,"sleeping user sessions included");
    Equal(overview.Single(m=>m.Key=="activeRequests").Value,8,"persisted active requests");
    var queries=service.Queries(normal.Data,normal.Context,normal.Filter);
    Check(queries.Queries.Rows.Count==2 && queries.Queries.Rows[0].TotalCpuMs==3000,"interval ranking excludes idle lifetime-heavy plan");
    var q1=queries.Queries.Rows.Single(q=>q.TotalCpuMs==2000);
    Equal(q1.Executions,20,"Q1 observed completions");Equal(q1.AvgCpuMs,100,"Q1 CPU average");Equal(q1.TotalElapsedMs,4000,"Q1 elapsed total");Equal(q1.AvgElapsedMs,200,"Q1 elapsed average");Equal(q1.LogicalReads,30000,"Q1 logical reads");
    var waits=service.Waits(normal.Data,normal.Filter);
    Equal(waits.Waits.Rows.Single(w=>w.WaitType=="PAGEIOLATCH_SH").SharePct,75,"wait share 75 percent");
    Equal(waits.Waits.Rows.Single(w=>w.WaitType=="LCK_M_X").ResourceWaitMs,1800,"resource wait reconciliation");
    normal.Filter.IncludeExcluded=true;Equal(service.Waits(normal.Data,normal.Filter).Waits.Rows.Single(w=>w.WaitType=="SLEEP_TASK").SharePct,50,"exclusion toggle changes denominator");
    var files=service.FileIo(normal.Data,normal.Context,normal.Filter);var file=files.Files.Rows.Single(f=>f.FileType=="data");
    Equal(file.Metrics.ReadIops,20,"file read IOPS");Equal(file.Metrics.ReadMiBps,2,"file read MiB/s");Equal(file.Metrics.ReadLatencyMs,5,"file read latency");
    Equal(file.Metrics.WriteIops,4,"file write IOPS");Equal(file.Metrics.WriteMiBps,0.4m,"file write MiB/s");Equal(file.Metrics.WriteLatencyMs,3,"file write latency");
    var block=Load("blocking","Blocking");var chain=service.Blocking(block.Data,block.Context,block.Filter);
    Check(chain.RootCount==1 && chain.FullSampleBlockedRequestCount==2 && chain.Nodes.Single(n=>n.SessionId==51).Status=="sleeping" && chain.Edges.Count==2,"sleeping root 51 with children 62 and 63");
    var high=Load("high-query-reads","Queries");high.Filter.Sort="logicalReads";
    Equal(service.Queries(high.Data,high.Context,high.Filter).Queries.Rows[0].LogicalReads,5000000,"high reads scenario");
    var slow=Load("io-slowdown","FileIo");Equal(service.FileIo(slow.Data,slow.Context,slow.Filter).Files.Rows.Single(f=>f.FileType=="data").Metrics.ReadLatencyMs,50,"I/O slowdown scenario");
    var no=Load("no-activity","Queries");
    Check(service.Queries(no.Data,no.Context,no.Filter).State=="noActivity","no observed query activity");
    var noIo=service.FileIo(no.Data,no.Context,no.Filter);Equal(noIo.Summary.ReadLatencyMs,null,"persisted no I/O latency");Equal(noIo.Summary.ReadIops,0,"persisted zero throughput");
    Check(service.Waits(no.Data,no.Filter).State=="noActivity" && service.Waits(no.Data,no.Filter).Waits.Rows.All(w=>w.SharePct==null),"persisted no waits shares undefined");
    var weight=Load("weighted-io","FileIo");Equal(service.FileIo(weight.Data,weight.Context,weight.Filter).Summary.ReadLatencyMs,9.2m,"persisted weighted summary");
    var reset=Load("counter-reset","Overview");Equal(service.Overview(reset.Data,reset.Context,reset.Filter).Single(m=>m.Key=="batchRequestsPerSec").Value,20,"next compatible pair resumes after reset");
    var resetB=reset.Data.Captures.Where(c=>c.Section=="instance").OrderBy(c=>c.AtUtc).Skip(1).First();
    reset.Filter.AsOfSampleId=resetB.SampleId;var resetPinned=repo.Load(reset.Context,reset.Filter);
    Check(service.Overview(resetPinned,reset.Context,reset.Filter).Single(m=>m.Key=="batchRequestsPerSec").State=="reset","pinned reset sample has null rate");
    var gap=Load("gap","Overview");Check(service.Overview(gap.Data,gap.Context,gap.Filter).Single(m=>m.Key=="batchRequestsPerSec").State=="gap","persisted 45-second gap");
    var partial=Load("partial","Waits");var statuses=service.Status(partial.Data,partial.Context);
    var denied=statuses.Single(s=>s.Section=="waits");Check(denied.AttemptStatus=="failure" && denied.LastSuccessUtc==a.AtUtc,"failure leaves last success unchanged");
    Equal(service.FileIo(partial.Data,partial.Context,partial.Filter).Summary.ReadLatencyMs,5,"file metrics survive wait permission failure");
    var expired=Load("normal","Overview");expired.Filter.AsOfSampleId=Guid.NewGuid();
    Throws(()=>repo.Load(expired.Context,expired.Filter),404,"missing watermark rejected");
    // Mutation fixtures exercise affected-section resets without any database writes.
    var clear=Load("normal","Waits");var lastWait=clear.Data.Waits.OrderBy(w=>w.SampleKey).Last(w=>w.WaitType=="LCK_M_X");lastWait.TotalMs=1;
    Check(service.Waits(clear.Data,clear.Filter).Waits.Rows.Count==0,"one wait decrease rebases entire section");
    var signal=Load("normal","Waits");signal.Data.Waits.OrderBy(w=>w.SampleKey).Last(w=>w.WaitType=="LCK_M_X").SignalMs=100000;
    Check(service.Waits(signal.Data,signal.Filter).Waits.Coverage.RejectedReasons.ContainsKey("signalExceedsTotal"),"invalid signal delta recorded");
    var generation=Load("normal","Queries");generation.Data.Queries.OrderBy(q=>q.SampleKey).Last(q=>q.Summary!.Contains("sales")).GenerationId=999999;
    Check(service.Queries(generation.Data,generation.Context,generation.Filter).MissingBaselines==1,"new plan generation needs baseline");
    var reads=new DashboardReadService(repo);
    var readQuery=new Dictionary<string,string>{{"instanceId",context.InstanceId.ToString()},{"sourceMode","synthetic"},{"scenarioId","normal"}};
    Throws(()=>reads.Read("Overview",readQuery,new HashSet<Guid>()),403,"read boundary denies unauthorized instance");
    var grants=new HashSet<Guid>{context.InstanceId};
    var envelope=reads.Read("Overview",readQuery,grants);
    Check(envelope.ContractVersion==1 && envelope.Context.SyntheticLabel==DashboardService.SyntheticLabel && envelope.Context.ViewMode=="frozenSynthetic","read envelope preserves synthetic mode and label");
    Check(envelope.Sections.All(s=>s.FreshnessState=="frozenSynthetic"),"synthetic never claims live freshness");
    readQuery["maxPoints"]="30";readQuery["metric"]="batchRequestsPerSec";
    var history=reads.Read("History",readQuery,grants);Check(history.Data!=null,"bounded history read");
    readQuery.Remove("maxPoints");readQuery.Remove("metric");
    readQuery["page"]="1000";var emptyPage=reads.Read("Queries",readQuery,grants);
    Check(((QueryResult)emptyPage.Data).Queries.Rows.Count==0 && ((QueryResult)emptyPage.Data).Queries.TotalEligibleRows==2,"out-of-range page preserves totals");
    readQuery.Remove("page");readQuery["asOfSampleId"]=normal.Data.Captures.Where(c=>c.Section=="activity").OrderBy(c=>c.AtUtc).First().SampleId.ToString();
    Check(((QueryResult)reads.Read("Queries",readQuery,grants).Data).State=="insufficientIntervalCoverage","pinned first sample cannot invent interval");
    Console.WriteLine("SQL integration verification completed against dedicated LocalDB only.");
}
Console.WriteLine($"Verified {passed} checks.");
