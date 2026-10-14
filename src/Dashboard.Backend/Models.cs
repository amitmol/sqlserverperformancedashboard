using System;
using System.Collections.Generic;

namespace Dashboard.Backend
{
    public sealed class DashboardException : Exception
    {
        public int StatusCode { get; }
        public string Code { get; }
        public DashboardException(int statusCode, string code, string message) : base(message)
        { StatusCode = statusCode; Code = code; }
    }

    public sealed class PartitionContext
    {
        public Guid PartitionId { get; set; }
        public Guid InstanceId { get; set; }
        public string InstanceName { get; set; } = "";
        public string SourceMode { get; set; } = "";
        public string ScenarioId { get; set; } = "";
        public DateTime EvaluationUtc { get; set; }
        public Dictionary<long, string> Databases { get; } = new Dictionary<long, string>();
    }

    public sealed class DashboardFilter
    {
        public Guid InstanceId { get; set; }
        public string SourceMode { get; set; } = "synthetic";
        public string ScenarioId { get; set; } = "normal";
        public long? DatabaseId { get; set; }
        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int Top { get; set; } = 20;
        public string Sort { get; set; } = "";
        public string Direction { get; set; } = "desc";
        public bool IncludeExcluded { get; set; }
        public Guid? SampleId { get; set; }
        public Guid? AsOfSampleId { get; set; }
        public int MaxPoints { get; set; } = 240;
    }

    public sealed class Capture
    {
        public long SampleKey { get; set; }
        public Guid SampleId { get; set; }
        public long? EpochId { get; set; }
        public Guid RunId { get; set; }
        public long Frequency { get; set; }
        public string Section { get; set; } = "";
        public DateTime AttemptUtc { get; set; }
        public DateTime? AtUtc { get; set; }
        public long? Tick { get; set; }
        public string Status { get; set; } = "";
        public bool Truncated { get; set; }
        public bool KnownReset { get; set; }
        public long ResetGeneration { get; set; }
        public bool IdentityReliable { get; set; }
        public bool TimingReliable { get; set; }
        public string? FailureReason { get; set; }
    }

    public sealed class InstanceCounter
    {
        public long SampleKey { get; set; }
        public long? BatchCount { get; set; }
        public string CounterIdentity { get; set; } = "";
        public int CounterType { get; set; }
        public long? MemoryKb { get; set; }
        public DateTime? EngineStartUtc { get; set; }
    }

    public sealed class QueryCounter
    {
        public long SampleKey { get; set; }
        public long GenerationId { get; set; }
        public long? DatabaseId { get; set; }
        public string? QueryHash { get; set; }
        public string? Summary { get; set; }
        public bool IdentityReliable { get; set; }
        public long Executions { get; set; }
        public long CpuUs { get; set; }
        public long ElapsedUs { get; set; }
        public long LogicalReads { get; set; }
        public DateTime? LastExecutionUtc { get; set; }
    }

    public sealed class WaitCounter
    {
        public long SampleKey { get; set; }
        public string WaitType { get; set; } = "";
        public long Tasks { get; set; }
        public long TotalMs { get; set; }
        public long SignalMs { get; set; }
    }

    public sealed class FileCounter
    {
        public long SampleKey { get; set; }
        public long FileId { get; set; }
        public long DatabaseId { get; set; }
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public bool IdentityReliable { get; set; }
        public long Reads { get; set; }
        public long ReadBytes { get; set; }
        public long ReadStallMs { get; set; }
        public long Writes { get; set; }
        public long WriteBytes { get; set; }
        public long WriteStallMs { get; set; }
    }

    public sealed class SessionObservation
    {
        public long SampleKey { get; set; }
        public long SessionIdentity { get; set; }
        public int SessionId { get; set; }
        public long? DatabaseId { get; set; }
        public bool UserProcess { get; set; }
        public bool Collector { get; set; }
        public string Status { get; set; } = "";
        public int OpenTransactions { get; set; }
    }

    public sealed class RequestObservation
    {
        public long SampleKey { get; set; }
        public long RequestIdentity { get; set; }
        public long SessionIdentity { get; set; }
        public int RequestId { get; set; }
        public long? DatabaseId { get; set; }
        public string Status { get; set; } = "";
        public string? WaitType { get; set; }
        public long WaitMs { get; set; }
        public long ElapsedMs { get; set; }
        public int Transactions { get; set; }
        public int? BlockingSessionId { get; set; }
    }

    public sealed class SnapshotData
    {
        public List<Capture> Captures { get; } = new List<Capture>();
        public List<InstanceCounter> Instances { get; } = new List<InstanceCounter>();
        public List<QueryCounter> Queries { get; } = new List<QueryCounter>();
        public List<WaitCounter> Waits { get; } = new List<WaitCounter>();
        public List<FileCounter> Files { get; } = new List<FileCounter>();
        public List<SessionObservation> Sessions { get; } = new List<SessionObservation>();
        public List<RequestObservation> Requests { get; } = new List<RequestObservation>();
        public HashSet<string> ExcludedWaitTypes { get; } = new HashSet<string>(StringComparer.Ordinal);
    }

    public sealed class Metric
    {
        public string Key { get; set; } = "";
        public decimal? Value { get; set; }
        public string Unit { get; set; } = "";
        public string Scope { get; set; } = "instance";
        public bool DatabaseFilterApplied { get; set; }
        public string State { get; set; } = "valid";
        public string? Reason { get; set; }
        public Guid? SampleId { get; set; }
        public DateTime? CapturedAtUtc { get; set; }
        public DateTime? IntervalStartUtc { get; set; }
        public DateTime? IntervalEndUtc { get; set; }
        public decimal? MeasuredSeconds { get; set; }
        public string SyntheticLabel { get; set; } = DashboardService.SyntheticLabel;
    }

    public sealed class Comparison
    {
        public string State { get; set; } = "valid";
        public string? Reason { get; set; }
        public decimal Seconds { get; set; }
        public bool Valid => State == "valid";
    }

    public sealed class QueryRow
    {
        public string QueryId { get; set; } = "";
        public string? DatabaseId { get; set; }
        public string Database { get; set; } = "Unknown";
        public string? Summary { get; set; }
        public string TextState => Summary == null ? "unavailable" : "redacted";
        public decimal Executions { get; set; }
        public decimal TotalCpuMs { get; set; }
        public decimal TotalElapsedMs { get; set; }
        public decimal LogicalReads { get; set; }
        public decimal? AvgCpuMs => Executions > 0 ? TotalCpuMs / Executions : (decimal?)null;
        public decimal? AvgElapsedMs => Executions > 0 ? TotalElapsedMs / Executions : (decimal?)null;
        public DateTime? LastExecutionUtc { get; set; }
        public DateTime? LastObservedCompletionUtc { get; set; }
        public int AcceptedIntervals { get; set; }
        public int MissingBaselines { get; set; }
        public string CoverageState { get; set; } = "observedCache";
    }

    public sealed class WaitRow
    {
        public string WaitType { get; set; } = "";
        public decimal TotalWaitMs { get; set; }
        public decimal SignalWaitMs { get; set; }
        public decimal ResourceWaitMs => TotalWaitMs - SignalWaitMs;
        public decimal TaskCount { get; set; }
        public decimal? SharePct { get; set; }
    }

    public sealed class IoTotals
    {
        public decimal Reads { get; set; }
        public decimal Writes { get; set; }
        public decimal ReadBytes { get; set; }
        public decimal WriteBytes { get; set; }
        public decimal ReadStallMs { get; set; }
        public decimal WriteStallMs { get; set; }
        public decimal Seconds { get; set; }
        public decimal? ReadIops => Seconds > 0 ? Reads / Seconds : (decimal?)null;
        public decimal? WriteIops => Seconds > 0 ? Writes / Seconds : (decimal?)null;
        public decimal? ReadMiBps => Seconds > 0 ? ReadBytes / (1048576m * Seconds) : (decimal?)null;
        public decimal? WriteMiBps => Seconds > 0 ? WriteBytes / (1048576m * Seconds) : (decimal?)null;
        public decimal? ReadLatencyMs => Reads > 0 ? ReadStallMs / Reads : (decimal?)null;
        public decimal? WriteLatencyMs => Writes > 0 ? WriteStallMs / Writes : (decimal?)null;
    }

    public sealed class FileRow
    {
        public string FileId { get; set; } = "";
        public string DatabaseId { get; set; } = "";
        public string Database { get; set; } = "";
        public string FileName { get; set; } = "";
        public string FileType { get; set; } = "";
        public IoTotals Metrics { get; } = new IoTotals();
        public int AcceptedIntervals { get; set; }
        public string State { get; set; } = "baselineRequired";
        public string ReadLatencyState => Metrics.Seconds <= 0 ? State : Metrics.Reads == 0 ? "noActivity" : "valid";
        public string WriteLatencyState => Metrics.Seconds <= 0 ? State : Metrics.Writes == 0 ? "noActivity" : "valid";
    }

    public sealed class SectionStatus
    {
        public string Section { get; set; } = "";
        public string AttemptStatus { get; set; } = "unavailable";
        public DateTime? LastAttemptUtc { get; set; }
        public DateTime? LastSuccessUtc { get; set; }
        public Guid? SampleId { get; set; }
        public decimal? AgeSeconds { get; set; }
        public string FreshnessState { get; set; } = "unavailable";
        public string? Reason { get; set; }
    }

    public sealed class Coverage
    {
        public DateTime RequestedFromUtc { get; set; }
        public DateTime RequestedToUtc { get; set; }
        public decimal CoveredSeconds { get; set; }
        public decimal CoveragePct { get; set; }
        public int AcceptedIntervals { get; set; }
        public Dictionary<string, int> RejectedReasons { get; } = new Dictionary<string, int>();
        public string PopulationCompleteness { get; set; } = "complete";
    }

    public sealed class PagedResult<T>
    {
        public List<T> Rows { get; set; } = new List<T>();
        public int TotalEligibleRows { get; set; }
        public int TotalRankedRows { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasMore { get; set; }
        public Coverage Coverage { get; set; } = new Coverage();
    }
}
