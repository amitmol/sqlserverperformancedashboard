using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Dashboard.Backend
{
    public sealed class QueryResult
    {
        public PagedResult<QueryRow> Queries { get; set; } = new PagedResult<QueryRow>();
        public int MissingBaselines { get; set; }
        public int InvalidGenerations { get; set; }
        public int UnknownDatabaseRows { get; set; }
        public string State { get; set; } = "valid";
    }
    public sealed class WaitResult
    {
        public PagedResult<WaitRow> Waits { get; set; } = new PagedResult<WaitRow>();
        public string ExclusionPolicyVersion => "demo-v1";
        public List<string> ExcludedTypes { get; set; } = new List<string>();
        public bool IncludeExcluded { get; set; }
        public decimal IncludedTotalWaitMs { get; set; }
        public string State { get; set; } = "valid";
    }
    public sealed class FileIoResult
    {
        public PagedResult<FileRow> Files { get; set; } = new PagedResult<FileRow>();
        public IoTotals Summary { get; set; } = new IoTotals();
        public string SummaryState { get; set; } = "baselineRequired";
    }
    public sealed class BlockingNode
    {
        public string NodeId { get; set; } = "";
        public int? SessionId { get; set; }
        public int? RequestId { get; set; }
        public string? DatabaseId { get; set; }
        public string Database { get; set; } = "Unknown";
        public string Status { get; set; } = "";
        public string? CurrentWaitType { get; set; }
        public long CurrentWaitMs { get; set; }
        public long ElapsedRequestMs { get; set; }
        public int OpenTransactions { get; set; }
        public bool ContextOnly { get; set; }
        public string TextState => "unavailable";
    }
    public sealed class BlockingEdge
    {
        public string ParentId { get; set; } = "";
        public string ChildId { get; set; } = "";
        public bool Cycle { get; set; }
    }
    public sealed class BlockingResult
    {
        public Guid? SampleId { get; set; }
        public DateTime? SampleUtc { get; set; }
        public bool Historical { get; set; }
        public int FullSampleBlockedRequestCount { get; set; }
        public int RootCount { get; set; }
        public int SpecialOwnerCount { get; set; }
        public List<string> Roots { get; set; } = new List<string>();
        public List<BlockingNode> Nodes { get; set; } = new List<BlockingNode>();
        public List<BlockingEdge> Edges { get; set; } = new List<BlockingEdge>();
        public string State { get; set; } = "valid";
    }

    public sealed class DashboardService
    {
        public const string SyntheticLabel = "Synthetic data — demonstration only — not connected to SQL Server.";
        private readonly SnapshotComparisonService comparison = new SnapshotComparisonService();

        public List<SectionStatus> Status(SnapshotData data, PartitionContext context)
        {
            return new[] { "instance", "activity", "queries", "waits", "fileIo" }.Select(section =>
            {
                var captures = data.Captures.Where(x => x.Section == section).OrderBy(x => x.AttemptUtc).ThenBy(x => x.SampleKey).ToList();
                var attempt = captures.LastOrDefault(); var success = captures.LastOrDefault(x => x.Status == "success");
                return new SectionStatus { Section = section, AttemptStatus = attempt?.Status ?? "unavailable", LastAttemptUtc = attempt?.AttemptUtc,
                    LastSuccessUtc = success?.AtUtc, SampleId = success?.SampleId, Reason = attempt?.FailureReason,
                    AgeSeconds = success?.AtUtc == null ? null : (decimal?)(context.EvaluationUtc - success.AtUtc.Value).TotalSeconds,
                    FreshnessState = context.SourceMode == "synthetic" ? "frozenSynthetic" : SnapshotComparisonService.Freshness(success?.AtUtc, context.EvaluationUtc) };
            }).ToList();
        }

        public List<Metric> Overview(SnapshotData data, PartitionContext context, DashboardFilter filter)
        {
            var output = new List<Metric>();
            var instance = Successful(data, "instance").ToList();
            var b = instance.LastOrDefault(); var a = instance.Count > 1 ? instance[instance.Count - 2] : null;
            var gauge = b == null ? null : data.Instances.SingleOrDefault(x => x.SampleKey == b.SampleKey);
            if (b != null) output.Add(comparison.Batch(a, b, data.Instances.SingleOrDefault(x => x.SampleKey == a?.SampleKey), gauge));
            else output.Add(new Metric { Key = "batchRequestsPerSec", Unit = "batches/s", State = "unavailable" });
            output.Add(Gauge("processMemoryMiB", "MiB", gauge?.MemoryKb == null ? null : gauge.MemoryKb / 1024m, b));
            decimal? uptime = b?.AtUtc == null || gauge?.EngineStartUtc == null ? null : (decimal?)(b.AtUtc.Value - gauge.EngineStartUtc.Value).TotalSeconds;
            var uptimeMetric = Gauge("uptimeSeconds", "s", uptime.HasValue && uptime >= 0 ? uptime : null, b);
            if (uptime < 0) { uptimeMetric.State = "invalid"; uptimeMetric.Reason = "invalidEngineStart"; }
            output.Add(uptimeMetric);
            var activity = Successful(data, "activity").LastOrDefault();
            var sessions = data.Sessions.Where(x => x.SampleKey == activity?.SampleKey && x.UserProcess && !x.Collector).ToList();
            var eligible = new HashSet<long>(sessions.Select(x => x.SessionIdentity));
            var requests = data.Requests.Where(x => x.SampleKey == activity?.SampleKey && eligible.Contains(x.SessionIdentity)).ToList();
            output.Add(Gauge("userSessions", "sessions", activity == null ? null : (decimal?)sessions.Count(x => Applies(filter, x.DatabaseId)), activity, filter));
            output.Add(Gauge("activeRequests", "requests", activity == null ? null : (decimal?)requests.Count(x => Applies(filter, x.DatabaseId)), activity, filter));
            output.Add(Gauge("blockedRequests", "requests", activity == null ? null : (decimal?)requests.Count(x => Applies(filter, x.DatabaseId) && x.BlockingSessionId > 0), activity, filter));
            // Latest complete I/O interval is independent of the selected history range.
            var ioCaptures = Successful(data, "fileIo").ToList();
            var latestIo = ioCaptures.LastOrDefault();
            var ioFilter = CopyFilter(filter);
            if (latestIo?.AtUtc != null && ioCaptures.Count > 1) ioFilter.FromUtc = ioCaptures[ioCaptures.Count - 2].AtUtc!.Value;
            var io = FileIo(data, context, ioFilter);
            foreach (var direction in new[] { "read", "write" })
            {
                decimal? value = direction == "read" ? io.Summary.ReadLatencyMs : io.Summary.WriteLatencyMs;
                var m = Gauge(direction + "LatencyMs", "ms/op", value, latestIo, filter);
                m.IntervalStartUtc = ioCaptures.Count > 1 ? ioCaptures[ioCaptures.Count - 2].AtUtc : null;
                m.IntervalEndUtc = latestIo?.AtUtc; m.MeasuredSeconds = io.Summary.Seconds > 0 ? io.Summary.Seconds : (decimal?)null;
                m.State = io.Summary.Seconds > 0 ? value.HasValue ? "valid" : "noActivity" : io.SummaryState;
                m.Reason = io.Summary.Seconds > 0 ? null : io.SummaryState;
                output.Add(m);
            }
            return output;
        }

        public QueryResult Queries(SnapshotData data, PartitionContext context, DashboardFilter filter)
        {
            var result = new QueryResult(); var coverage = NewCoverage(filter);
            var rows = new Dictionary<string, QueryRow>();
            foreach (var pair in Pairs(data, "queries", filter, coverage))
            {
                var before = data.Queries.Where(x => x.SampleKey == pair.A.SampleKey).ToDictionary(x => x.GenerationId);
                var after = data.Queries.Where(x => x.SampleKey == pair.B.SampleKey).ToList();
                foreach (var y in after)
                {
                    if (y.DatabaseId == null) result.UnknownDatabaseRows++;
                    if (!Applies(filter, y.DatabaseId)) continue;
                    string key = Id(y.DatabaseId) + ":" + (y.QueryHash ?? "generation:" + Id(y.GenerationId));
                    if (!rows.TryGetValue(key, out var row))
                    {
                        row = new QueryRow { QueryId = key, DatabaseId = IdOrNull(y.DatabaseId), Database = Database(context, y.DatabaseId), Summary = y.Summary };
                        rows[key] = row;
                    }
                    if (!before.TryGetValue(y.GenerationId, out var x) || !x.IdentityReliable || !y.IdentityReliable)
                    { row.MissingBaselines++; result.MissingBaselines++; row.CoverageState = "partialCache"; continue; }
                    decimal count = (decimal)y.Executions - x.Executions, cpu = (decimal)y.CpuUs - x.CpuUs,
                        elapsed = (decimal)y.ElapsedUs - x.ElapsedUs, reads = (decimal)y.LogicalReads - x.LogicalReads;
                    if (count < 0 || cpu < 0 || elapsed < 0 || reads < 0 || (count == 0 && (cpu > 0 || elapsed > 0 || reads > 0)))
                    { result.InvalidGenerations++; row.CoverageState = "partialCache"; continue; }
                    row.AcceptedIntervals++;
                    if (count == 0) continue;
                    row.Executions += count; row.TotalCpuMs += cpu / 1000m; row.TotalElapsedMs += elapsed / 1000m; row.LogicalReads += reads;
                    if (row.LastExecutionUtc == null || y.LastExecutionUtc > row.LastExecutionUtc) row.LastExecutionUtc = y.LastExecutionUtc;
                    row.LastObservedCompletionUtc = pair.B.AtUtc;
                }
                if (before.Keys.Except(after.Select(x => x.GenerationId)).Any()) result.State = "partialCache";
            }
            var active = rows.Values.Where(x => x.Executions > 0).ToList();
            Func<QueryRow, decimal?> sort = r => filter.Sort == "totalElapsedMs" ? r.TotalElapsedMs : filter.Sort == "logicalReads" ? r.LogicalReads :
                filter.Sort == "executions" ? r.Executions : filter.Sort == "avgCpuMs" ? r.AvgCpuMs : filter.Sort == "avgElapsedMs" ? r.AvgElapsedMs : r.TotalCpuMs;
            var ranked = Sort(active, sort, r => r.QueryId, filter.Direction).Take(filter.Top).ToList();
            result.Queries = Page(ranked, filter, coverage, active.Count);
            if (coverage.RejectedReasons.ContainsKey("populationTruncated")) { result.Queries.Rows.Clear(); result.Queries.TotalEligibleRows = 0; result.Queries.TotalRankedRows = 0; result.State = "unavailable"; }
            else if (result.MissingBaselines > 0 || result.InvalidGenerations > 0) result.State = "partialCache";
            else if (coverage.AcceptedIntervals == 0) result.State = "insufficientIntervalCoverage";
            else if (active.Count == 0) result.State = "noActivity";
            return result;
        }

        public WaitResult Waits(SnapshotData data, DashboardFilter filter)
        {
            var coverage = NewCoverage(filter); var rows = new Dictionary<string, WaitRow>();
            foreach (var pair in Pairs(data, "waits", filter, coverage))
            {
                var before = data.Waits.Where(x => x.SampleKey == pair.A.SampleKey).ToDictionary(x => x.WaitType);
                var after = data.Waits.Where(x => x.SampleKey == pair.B.SampleKey).ToList();
                // One counter decrease rebases the ENTIRE wait section, before applying exclusions.
                if (after.Any(y => before.TryGetValue(y.WaitType, out var x) && (y.TotalMs < x.TotalMs || y.SignalMs < x.SignalMs || y.Tasks < x.Tasks)))
                { RejectAccepted(coverage, pair.Check.Seconds, "counterReset"); continue; }
                foreach (var y in after)
                {
                    if (!before.TryGetValue(y.WaitType, out var x)) { Reject(coverage, "waitBaselineRequired"); continue; }
                    decimal total = (decimal)y.TotalMs - x.TotalMs, signal = (decimal)y.SignalMs - x.SignalMs, tasks = (decimal)y.Tasks - x.Tasks;
                    if (signal > total) { Reject(coverage, "signalExceedsTotal"); continue; }
                    if (!filter.IncludeExcluded && data.ExcludedWaitTypes.Contains(y.WaitType)) continue;
                    if (!rows.TryGetValue(y.WaitType, out var row)) rows[y.WaitType] = row = new WaitRow { WaitType = y.WaitType };
                    row.TotalWaitMs += total; row.SignalWaitMs += signal; row.TaskCount += tasks;
                }
            }
            decimal denominator = rows.Values.Sum(x => x.TotalWaitMs);
            foreach (var row in rows.Values) row.SharePct = SnapshotComparisonService.Ratio(100m * row.TotalWaitMs, denominator);
            Func<WaitRow, decimal?> sort = r => filter.Sort == "signalWaitMs" ? r.SignalWaitMs : filter.Sort == "resourceWaitMs" ? r.ResourceWaitMs :
                filter.Sort == "taskCount" ? r.TaskCount : filter.Sort == "sharePct" ? r.SharePct : r.TotalWaitMs;
            return new WaitResult { Waits = Page(Sort(rows.Values, sort, x => x.WaitType, filter.Direction).ToList(), filter, coverage),
                IncludeExcluded = filter.IncludeExcluded, ExcludedTypes = data.ExcludedWaitTypes.OrderBy(x => x).ToList(), IncludedTotalWaitMs = denominator,
                State = coverage.AcceptedIntervals == 0 ? "insufficientIntervalCoverage" : denominator == 0 ? "noActivity" : coverage.RejectedReasons.Count > 0 ? "partial" : "valid" };
        }

        public FileIoResult FileIo(SnapshotData data, PartitionContext context, DashboardFilter filter)
        {
            var result = new FileIoResult(); var coverage = NewCoverage(filter); var rows = new Dictionary<long, FileRow>();
            foreach (var pair in Pairs(data, "fileIo", filter, coverage))
            {
                var before = data.Files.Where(x => x.SampleKey == pair.A.SampleKey && Applies(filter, x.DatabaseId)).ToDictionary(x => x.FileId);
                var after = data.Files.Where(x => x.SampleKey == pair.B.SampleKey && Applies(filter, x.DatabaseId)).ToList();
                bool complete = before.Count == after.Count && before.Keys.ToHashSetCompat().SetEquals(after.Select(x => x.FileId));
                var intervalTotals = new IoTotals { Seconds = pair.Check.Seconds };
                foreach (var y in after)
                {
                    if (!rows.TryGetValue(y.FileId, out var row)) rows[y.FileId] = row = new FileRow { FileId = Id(y.FileId), DatabaseId = Id(y.DatabaseId),
                        Database = Database(context, y.DatabaseId), FileName = y.Name, FileType = y.Type };
                    if (!before.TryGetValue(y.FileId, out var x) || !x.IdentityReliable || !y.IdentityReliable)
                    { complete = false; row.State = "baselineRequired"; Reject(coverage, "fileBaselineRequired"); continue; }
                    var delta = new IoTotals { Reads = (decimal)y.Reads - x.Reads, Writes = (decimal)y.Writes - x.Writes,
                        ReadBytes = (decimal)y.ReadBytes - x.ReadBytes, WriteBytes = (decimal)y.WriteBytes - x.WriteBytes,
                        ReadStallMs = (decimal)y.ReadStallMs - x.ReadStallMs, WriteStallMs = (decimal)y.WriteStallMs - x.WriteStallMs, Seconds = pair.Check.Seconds };
                    if (new[] { delta.Reads, delta.Writes, delta.ReadBytes, delta.WriteBytes, delta.ReadStallMs, delta.WriteStallMs }.Any(v => v < 0))
                    { complete = false; row.State = "reset"; Reject(coverage, "fileCounterReset"); continue; }
                    if ((delta.Reads == 0 && (delta.ReadBytes > 0 || delta.ReadStallMs > 0)) || (delta.Writes == 0 && (delta.WriteBytes > 0 || delta.WriteStallMs > 0)))
                    { complete = false; row.State = "invalid"; Reject(coverage, "inconsistentFileCounters"); continue; }
                    Add(row.Metrics, delta, true); row.AcceptedIntervals++; row.State = "valid";
                    Add(intervalTotals, delta, false);
                }
                if (complete && after.Count > 0) { Add(result.Summary, intervalTotals, true); result.SummaryState = "valid"; }
                else { RejectAccepted(coverage, pair.Check.Seconds, "incompleteFileScope"); result.SummaryState = "partial"; }
            }
            Func<FileRow, decimal?> numeric = r => filter.Sort == "writeIops" ? r.Metrics.WriteIops : filter.Sort == "readMiBps" ? r.Metrics.ReadMiBps :
                filter.Sort == "writeMiBps" ? r.Metrics.WriteMiBps : filter.Sort == "readLatencyMs" ? r.Metrics.ReadLatencyMs : filter.Sort == "writeLatencyMs" ? r.Metrics.WriteLatencyMs : r.Metrics.ReadIops;
            List<FileRow> ordered;
            if (filter.Sort == "database" || filter.Sort == "fileName" || filter.Sort == "fileType")
            {
                Func<FileRow, string> key = r => filter.Sort == "database" ? r.Database : filter.Sort == "fileType" ? r.FileType : r.FileName;
                ordered = (filter.Direction == "asc" ? rows.Values.OrderBy(key, StringComparer.Ordinal) : rows.Values.OrderByDescending(key, StringComparer.Ordinal)).ThenBy(x => x.FileId, StringComparer.Ordinal).ToList();
            }
            else ordered = Sort(rows.Values, numeric, r => r.FileId, filter.Direction).ToList();
            result.Files = Page(ordered, filter, coverage);
            if (coverage.AcceptedIntervals == 0 && result.SummaryState != "partial") result.SummaryState = coverage.RejectedReasons.Keys.FirstOrDefault() ?? "baselineRequired";
            return result;
        }

        public BlockingResult Blocking(SnapshotData data, PartitionContext context, DashboardFilter filter)
        {
            var capture = filter.SampleId.HasValue ? data.Captures.SingleOrDefault(x => x.Section == "activity" && x.SampleId == filter.SampleId.Value && x.Status == "success") : Successful(data, "activity").LastOrDefault();
            if (filter.SampleId.HasValue && capture == null) throw new DashboardException(404, "sampleNotFound", "The requested activity sample is unavailable.");
            if (capture == null) return new BlockingResult { State = "unavailable" };
            var result = new BlockingResult { SampleId = capture.SampleId, SampleUtc = capture.AtUtc, Historical = filter.SampleId.HasValue };
            var sessions = data.Sessions.Where(x => x.SampleKey == capture.SampleKey).ToDictionary(x => x.SessionIdentity);
            var byNumber = sessions.Values.ToDictionary(x => x.SessionId);
            var requests = data.Requests.Where(x => x.SampleKey == capture.SampleKey && sessions.TryGetValue(x.SessionIdentity, out var s) && s.UserProcess && !s.Collector).ToList();
            var blocked = requests.Where(x => x.BlockingSessionId.HasValue && x.BlockingSessionId != 0).ToList();
            result.FullSampleBlockedRequestCount = blocked.Count(x => x.BlockingSessionId > 0 && Applies(filter, x.DatabaseId));
            result.SpecialOwnerCount = blocked.Count(x => x.BlockingSessionId < 0 && Applies(filter, x.DatabaseId));
            var nodes = new Dictionary<string, BlockingNode>(); var edges = new List<BlockingEdge>();
            foreach (var r in blocked)
            {
                var session = sessions[r.SessionIdentity]; string child = "request:" + Id(r.RequestIdentity);
                nodes[child] = new BlockingNode { NodeId = child, SessionId = session.SessionId, RequestId = r.RequestId, DatabaseId = IdOrNull(r.DatabaseId), Database = Database(context, r.DatabaseId),
                    Status = r.Status, CurrentWaitType = r.WaitType, CurrentWaitMs = r.WaitMs, ElapsedRequestMs = r.ElapsedMs, OpenTransactions = r.Transactions,
                    ContextOnly = !Applies(filter, r.DatabaseId) };
                string parent;
                if (r.BlockingSessionId > 0 && byNumber.TryGetValue(r.BlockingSessionId.Value, out var blocker))
                {
                    parent = "session:" + Id(blocker.SessionIdentity);
                    nodes[parent] = new BlockingNode { NodeId = parent, SessionId = blocker.SessionId, DatabaseId = IdOrNull(blocker.DatabaseId), Database = Database(context, blocker.DatabaseId),
                        Status = blocker.Status, OpenTransactions = blocker.OpenTransactions, ContextOnly = !Applies(filter, blocker.DatabaseId) };
                }
                else
                {
                    parent = (r.BlockingSessionId > 0 ? "missing:" : "special:") + r.BlockingSessionId;
                    nodes[parent] = new BlockingNode { NodeId = parent, SessionId = r.BlockingSessionId > 0 ? r.BlockingSessionId : null,
                        Status = r.BlockingSessionId > 0 ? "Blocker not captured" : SpecialOwner(r.BlockingSessionId!.Value) };
                }
                edges.Add(new BlockingEdge { ParentId = parent, ChildId = child });
            }
            foreach (var r in blocked)
            {
                string sessionNode = "session:" + Id(r.SessionIdentity);
                if (nodes.ContainsKey(sessionNode)) edges.Add(new BlockingEdge { ParentId = "request:" + Id(r.RequestIdentity), ChildId = sessionNode });
            }
            // Walk UP from matching descendants to preserve cross-database roots and ancestors.
            var kept = new HashSet<string>();
            foreach (var r in blocked.Where(x => Applies(filter, x.DatabaseId))) PreserveAncestors("request:" + Id(r.RequestIdentity), edges, kept, new HashSet<string>(), 0);
            edges = edges.Where(x => kept.Contains(x.ParentId) && kept.Contains(x.ChildId)).ToList();
            var roots = kept.Where(x => !edges.Any(e => e.ChildId == x)).ToList();
            if (roots.Count == 0 && kept.Count > 0) { roots.Add(kept.OrderBy(x => x, StringComparer.Ordinal).First()); result.State = "cycle"; }
            result.RootCount = roots.Count;
            Func<string, decimal?> rootSort = id => filter.Sort == "currentWaitMs" ? nodes[id].CurrentWaitMs : filter.Sort == "blockedDescendants" ? Descendants(id, edges).Count : nodes[id].SessionId;
            roots = filter.Sort == "database" ? (filter.Direction == "asc" ? roots.OrderBy(x => nodes[x].Database, StringComparer.Ordinal) : roots.OrderByDescending(x => nodes[x].Database, StringComparer.Ordinal)).ToList() : Sort(roots, rootSort, x => x, filter.Direction).ToList();
            var selected = roots.Skip((filter.Page - 1) * filter.PageSize).Take(Math.Min(50, filter.PageSize)).ToList();
            var displayed = new HashSet<string>();
            foreach (var root in selected) displayed.UnionWith(Descendants(root, edges));
            if (displayed.Count > 500) throw new DashboardException(422, "blockingTooLarge", "This page exceeds 500 blocking nodes; reduce the page size or database scope.");
            result.Roots = selected; result.Nodes = displayed.Select(x => nodes[x]).OrderBy(x => x.NodeId, StringComparer.Ordinal).ToList();
            result.Edges = edges.Where(x => displayed.Contains(x.ParentId) && displayed.Contains(x.ChildId)).ToList();
            return result;
        }

        private static string SpecialOwner(int id) => id == -2 ? "Orphaned distributed transaction" : id == -3 ? "Deferred recovery transaction" : id == -4 ? "Latch owner unresolved" : id == -5 ? "Latch owner not tracked" : "Special or unknown owner";
        private static void PreserveAncestors(string id, List<BlockingEdge> edges, HashSet<string> kept, HashSet<string> path, int depth)
        {
            if (depth > 64) throw new DashboardException(422, "blockingTooDeep", "Blocking traversal exceeds 64 levels.");
            kept.Add(id); if (!path.Add(id)) return;
            foreach (var edge in edges.Where(x => x.ChildId == id))
            { if (path.Contains(edge.ParentId)) { edge.Cycle = true; kept.Add(edge.ParentId); } else PreserveAncestors(edge.ParentId, edges, kept, path, depth + 1); }
            path.Remove(id);
        }
        private static HashSet<string> Descendants(string root, List<BlockingEdge> edges)
        {
            var result = new HashSet<string>(); var pending = new Queue<string>(); pending.Enqueue(root);
            while (pending.Count > 0) { var id = pending.Dequeue(); if (!result.Add(id)) continue; foreach (var edge in edges.Where(x => x.ParentId == id)) pending.Enqueue(edge.ChildId); }
            return result;
        }
        private sealed class Pair { public Capture A = null!; public Capture B = null!; public Comparison Check = null!; }
        private IEnumerable<Pair> Pairs(SnapshotData data, string section, DashboardFilter filter, Coverage coverage)
        {
            Capture? before = null;
            foreach (var current in Successful(data, section))
            {
                if (current.AtUtc > filter.ToUtc) break;
                if (before == null) { if (current.AtUtc >= filter.FromUtc) Reject(coverage, "baselineRequired"); before = current; continue; }
                var pair = comparison.Compare(before, current);
                if (before.AtUtc >= filter.FromUtc && current.AtUtc <= filter.ToUtc)
                {
                    if (!pair.Valid) Reject(coverage, pair.Reason ?? pair.State);
                    else { coverage.AcceptedIntervals++; coverage.CoveredSeconds += pair.Seconds; yield return new Pair { A = before, B = current, Check = pair }; }
                }
                before = current;
            }
            coverage.CoveragePct = 100m * coverage.CoveredSeconds / (decimal)(filter.ToUtc - filter.FromUtc).TotalSeconds;
            if (coverage.RejectedReasons.ContainsKey("populationTruncated")) coverage.PopulationCompleteness = "truncated";
        }
        private static IEnumerable<Capture> Successful(SnapshotData data, string section) => data.Captures.Where(x => x.Section == section && x.Status == "success").OrderBy(x => x.AtUtc).ThenBy(x => x.SampleKey);
        private static Metric Gauge(string key, string unit, decimal? value, Capture? capture, DashboardFilter? filter = null) => new Metric { Key = key, Unit = unit, Value = value,
            State = value.HasValue ? "valid" : "unavailable", SampleId = capture?.SampleId, CapturedAtUtc = capture?.AtUtc,
            Scope = filter?.DatabaseId == null ? "instance" : "database", DatabaseFilterApplied = filter?.DatabaseId != null };
        private static Coverage NewCoverage(DashboardFilter f) => new Coverage { RequestedFromUtc = f.FromUtc, RequestedToUtc = f.ToUtc };
        private static void Reject(Coverage c, string reason) { c.RejectedReasons.TryGetValue(reason, out var count); c.RejectedReasons[reason] = count + 1; }
        private static void RejectAccepted(Coverage c, decimal seconds, string reason) { c.AcceptedIntervals--; c.CoveredSeconds -= seconds; Reject(c, reason); }
        private static bool Applies(DashboardFilter f, long? database) => !f.DatabaseId.HasValue || f.DatabaseId == database;
        private static string Database(PartitionContext c, long? id) => id.HasValue && c.Databases.TryGetValue(id.Value, out var name) ? name : "Unknown";
        private static string Id(long? id) => id?.ToString(CultureInfo.InvariantCulture) ?? "unknown";
        private static string? IdOrNull(long? id) => id?.ToString(CultureInfo.InvariantCulture);
        private static void Add(IoTotals target, IoTotals delta, bool includeTime)
        { target.Reads += delta.Reads; target.Writes += delta.Writes; target.ReadBytes += delta.ReadBytes; target.WriteBytes += delta.WriteBytes; target.ReadStallMs += delta.ReadStallMs; target.WriteStallMs += delta.WriteStallMs; if (includeTime) target.Seconds += delta.Seconds; }
        private static IEnumerable<T> Sort<T>(IEnumerable<T> rows, Func<T, decimal?> key, Func<T, string> id, string direction)
        { var nonnull = rows.OrderBy(x => key(x).HasValue ? 0 : 1); return (direction == "asc" ? nonnull.ThenBy(key) : nonnull.ThenByDescending(key)).ThenBy(id, StringComparer.Ordinal); }
        private static PagedResult<T> Page<T>(List<T> rows, DashboardFilter f, Coverage coverage, int? eligible = null) => new PagedResult<T> { Rows = rows.Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToList(),
            TotalEligibleRows = eligible ?? rows.Count, TotalRankedRows = rows.Count, Page = f.Page, PageSize = f.PageSize, HasMore = (long)f.Page * f.PageSize < rows.Count, Coverage = coverage };
        private static DashboardFilter CopyFilter(DashboardFilter f) => new DashboardFilter { DatabaseId = f.DatabaseId, FromUtc = f.FromUtc, ToUtc = f.ToUtc, Page = 1, PageSize = 100, Direction = "asc" };
    }
    internal static class EnumerableCompatibility
    { internal static HashSet<T> ToHashSetCompat<T>(this IEnumerable<T> values) => new HashSet<T>(values); }
}
