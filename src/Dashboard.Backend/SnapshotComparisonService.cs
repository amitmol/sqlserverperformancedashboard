using System;
using System.Linq;

namespace Dashboard.Backend
{
    public sealed class SnapshotComparisonService
    {
        public Comparison Compare(Capture? a, Capture b)
        {
            if (a == null) return Bad("baselineRequired", "baselineRequired");
            if (a.Section != b.Section) return Bad("invalid", "sectionMismatch");
            if (a.Status != "success" || b.Status != "success") return Bad("unavailable", "sectionIncomplete");
            if (a.Truncated || b.Truncated) return Bad("invalid", "populationTruncated");
            if (!a.IdentityReliable || !b.IdentityReliable || !a.EpochId.HasValue || !b.EpochId.HasValue)
                return Bad("invalid", "identityUncertain");
            if (a.EpochId != b.EpochId || a.ResetGeneration != b.ResetGeneration || b.KnownReset)
                return Bad("reset", "counterReset");
            if (!a.TimingReliable || !b.TimingReliable || a.RunId != b.RunId || a.Frequency <= 0 || a.Frequency != b.Frequency)
                return Bad("baselineRequired", "timingBaselineRequired");
            if (!a.Tick.HasValue || !b.Tick.HasValue || !a.AtUtc.HasValue || !b.AtUtc.HasValue || b.Tick <= a.Tick || b.AtUtc <= a.AtUtc)
                return Bad("invalid", "invalidTime");
            decimal seconds = ((decimal)b.Tick.Value - a.Tick.Value) / a.Frequency;
            decimal utcSeconds = (decimal)(b.AtUtc.Value - a.AtUtc.Value).TotalSeconds;
            if (Math.Abs(seconds - utcSeconds) > 1m) return Bad("invalid", "clockDiscontinuity");
            if (seconds > 30m) return Bad("gap", "collectionGap");
            return new Comparison { Seconds = seconds };
        }

        public Comparison ValidateCounters(Comparison pair, params decimal[] deltas)
        { return !pair.Valid ? pair : deltas.Any(x => x < 0) ? Bad("reset", "counterDecrease") : pair; }

        public Metric Batch(Capture? a, Capture b, InstanceCounter? x, InstanceCounter? y)
        {
            var pair = Compare(a, b);
            if (x == null || y == null || !x.BatchCount.HasValue || !y.BatchCount.HasValue)
                pair = Bad(a == null ? "baselineRequired" : "unavailable", a == null ? "baselineRequired" : "counterUnavailable");
            else if (x.CounterType != 272696576 || y.CounterType != 272696576 || x.CounterIdentity != y.CounterIdentity)
                pair = Bad("unavailable", "unsupportedCounterTypeOrIdentity");
            decimal delta = x?.BatchCount == null || y?.BatchCount == null ? 0 : (decimal)y.BatchCount.Value - x.BatchCount.Value;
            pair = ValidateCounters(pair, delta);
            return new Metric { Key = "batchRequestsPerSec", Unit = "batches/s", State = pair.State, Reason = pair.Reason,
                Value = pair.Valid ? delta / pair.Seconds : (decimal?)null, SampleId = b.SampleId, CapturedAtUtc = b.AtUtc,
                IntervalStartUtc = a?.AtUtc, IntervalEndUtc = b.AtUtc, MeasuredSeconds = pair.Valid ? pair.Seconds : (decimal?)null };
        }

        public static string Freshness(DateTime? captureUtc, DateTime evaluationUtc)
        {
            if (!captureUtc.HasValue) return "unavailable";
            var age = (evaluationUtc - captureUtc.Value).TotalSeconds;
            return age < 0 ? "timestampError" : age <= 30 ? "fresh" : age <= 60 ? "delayed" : "stale";
        }

        public static decimal? Ratio(decimal numerator, decimal denominator) => denominator > 0 ? numerator / denominator : (decimal?)null;
        public static Comparison Bad(string state, string reason) => new Comparison { State = state, Reason = reason };
    }
}
