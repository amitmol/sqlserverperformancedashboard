using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Dashboard.Backend
{
    public sealed class ResponseContext
    {
        public string InstanceId { get; set; } = "";
        public string InstanceName { get; set; } = "";
        public string? DatabaseId { get; set; }
        public string SourceMode => "synthetic";
        public string ScenarioId { get; set; } = "";
        public string SyntheticLabel => DashboardService.SyntheticLabel;
        public string ViewMode { get; set; } = "frozenSynthetic";
        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }
        public DateTime ScenarioEvaluationUtc { get; set; }
        public string DisplayTimezone => "UTC";
        public string? AsOfSampleId { get; set; }
    }
    public sealed class ReadEnvelope
    {
        public int ContractVersion => 1;
        public string RequestId { get; set; } = "";
        public DateTime GeneratedAtUtc { get; set; }
        public ResponseContext Context { get; set; } = new ResponseContext();
        public List<SectionStatus> Sections { get; set; } = new List<SectionStatus>();
        public object Data { get; set; } = new object();
        public List<string> Warnings { get; set; } = new List<string>();
    }
    public sealed class HistoryPoint
    {
        public DateTime? StartUtc { get; set; }
        public DateTime? EndUtc { get; set; }
        public string SampleId { get; set; } = "";
        public decimal? Value { get; set; }
        public string State { get; set; } = "";
        public decimal? MeasuredSeconds { get; set; }
    }

    // HTTP-independent read boundary. MVC routing/auth/JSON serialization must be
    // supplied by the real application; this class does not invent an identity provider.
    public sealed class DashboardReadService
    {
        private readonly SqlSnapshotRepository repository;
        private readonly DashboardService dashboard = new DashboardService();
        private readonly FilterValidator validator = new FilterValidator();
        public DashboardReadService(SqlSnapshotRepository repository) => this.repository = repository;

        // authorizedInstanceIds MUST come from server-side authorization, never query parameters.
        public ReadEnvelope Read(string action, IReadOnlyDictionary<string,string> query, ISet<Guid> authorizedInstanceIds)
        {
            var actions = new[] { "Context", "Overview", "Queries", "Blocking", "Waits", "FileIo", "History", "Samples" };
            if (!actions.Contains(action)) throw new DashboardException(404,"actionNotFound","Read action not found.");
            if (!query.TryGetValue("instanceId",out var instanceText) || !Guid.TryParse(instanceText,out var instance))
                throw new DashboardException(400,"invalidFilter","A configured instance ID is required.");
            if (!authorizedInstanceIds.Contains(instance)) throw new DashboardException(403,"instanceDenied","Instance access denied.");
            if (!query.TryGetValue("sourceMode",out var mode) || mode!="synthetic" || !query.TryGetValue("scenarioId",out var scenario))
                throw new DashboardException(400,"invalidFilter","Select an explicit synthetic scenario; live is not implemented.");
            var context=repository.GetContext(instance,scenario);
            var filter=validator.Parse(query,context,action);
            var data=repository.Load(context,filter);
            var watermark=data.Captures.OrderBy(x=>x.AttemptUtc).ThenBy(x=>x.SampleKey).LastOrDefault()?.SampleId;
            var result=new ReadEnvelope { RequestId=Guid.NewGuid().ToString(), GeneratedAtUtc=DateTime.UtcNow,
                Context=new ResponseContext { InstanceId=instance.ToString(), InstanceName=context.InstanceName, DatabaseId=filter.DatabaseId?.ToString(CultureInfo.InvariantCulture),
                    ScenarioId=scenario, FromUtc=filter.FromUtc, ToUtc=filter.ToUtc, ScenarioEvaluationUtc=context.EvaluationUtc, AsOfSampleId=watermark?.ToString(),
                    ViewMode=filter.SampleId.HasValue?"historical":"frozenSynthetic" }, Sections=dashboard.Status(data,context) };
            switch(action)
            {
                case "Context": result.Data=new { databases=context.Databases.Select(x=>new {databaseId=x.Key.ToString(CultureInfo.InvariantCulture),name=x.Value}),
                    sourceMode="synthetic",scenarioId=scenario,retentionDays=30,collectionCadenceSeconds=15,maxRows=SqlSnapshotRepository.MaxRows,maxSamples=SqlSnapshotRepository.MaxSamples }; break;
                case "Overview": result.Data=new { metrics=dashboard.Overview(data,context,filter), collectionHealth=CollectionHealth(result.Sections) }; break;
                case "Queries": result.Data=dashboard.Queries(data,context,filter);result.Warnings.Add("Observed completed cache work is not a complete query audit.");break;
                case "Waits": result.Data=dashboard.Waits(data,filter);result.Warnings.Add("Waits are instance-wide; database filter is not applied.");break;
                case "FileIo": result.Data=dashboard.FileIo(data,context,filter);break;
                case "Blocking": result.Data=dashboard.Blocking(data,context,filter);result.Warnings.Add("Sampled relationships do not prove continuous blocking or deadlock.");break;
                case "History": result.Data=History(data,filter,query);break;
                case "Samples":
                    var samples=data.Captures.Where(c=>c.Section=="activity" && c.AtUtc>=filter.FromUtc && c.AtUtc<=filter.ToUtc).OrderByDescending(c=>c.AtUtc).ThenByDescending(c=>c.SampleKey).ToList();
                    result.Data=new { rows=samples.Skip((filter.Page-1)*filter.PageSize).Take(filter.PageSize).Select(c=>new {sampleId=c.SampleId.ToString(),captureUtc=c.AtUtc,status=c.Status}),
                        totalRows=samples.Count,page=filter.Page,pageSize=filter.PageSize,hasMore=(long)filter.Page*filter.PageSize<samples.Count };break;
            }
            return result;
        }
        private static string CollectionHealth(List<SectionStatus> statuses)=>statuses.All(s=>s.AttemptStatus=="success")?"success":statuses.All(s=>s.AttemptStatus=="failure" || s.AttemptStatus=="unavailable")?"failure":"partial";
        private static object History(SnapshotData data,DashboardFilter f,IReadOnlyDictionary<string,string> query)
        {
            string metric=query.TryGetValue("metric",out var selected)?selected:"batchRequestsPerSec";
            if(metric!="batchRequestsPerSec" && metric!="processMemoryMiB") throw new DashboardException(400,"metricNotImplemented","This milestone supports batch throughput and process memory history.");
            var captures=data.Captures.Where(c=>c.Section=="instance" && c.Status=="success").OrderBy(c=>c.AtUtc).ThenBy(c=>c.SampleKey).ToList();
            var points=new List<HistoryPoint>();var comparison=new SnapshotComparisonService();Capture? a=null;
            foreach(var b in captures)
            {
                var counter=data.Instances.SingleOrDefault(x=>x.SampleKey==b.SampleKey);
                if(metric=="processMemoryMiB" && b.AtUtc>=f.FromUtc && b.AtUtc<=f.ToUtc)
                    points.Add(new HistoryPoint { EndUtc=b.AtUtc,SampleId=b.SampleId.ToString(),Value=counter?.MemoryKb/1024m,State=counter?.MemoryKb==null?"unavailable":"valid" });
                if(metric=="batchRequestsPerSec" && b.AtUtc>=f.FromUtc && b.AtUtc<=f.ToUtc && (a==null || a.AtUtc>=f.FromUtc))
                {
                    var value=comparison.Batch(a,b,data.Instances.SingleOrDefault(x=>x.SampleKey==a?.SampleKey),counter);
                    points.Add(new HistoryPoint {StartUtc=a?.AtUtc,EndUtc=b.AtUtc,SampleId=b.SampleId.ToString(),Value=value.Value,State=value.State,MeasuredSeconds=value.MeasuredSeconds});
                }
                a=b;
            }
            // Refuse excessive resolution rather than dropping reset/gap/null separators.
            if(points.Count>f.MaxPoints) throw new DashboardException(422,"rangeTooLarge","History exceeds the point budget; select a narrower range.");
            return new {metric,unit=metric=="batchRequestsPerSec"?"batches/s":"MiB",scope="instance",aggregation="raw",points};
        }
    }
}
