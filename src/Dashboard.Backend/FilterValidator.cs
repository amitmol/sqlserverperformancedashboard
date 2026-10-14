using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Dashboard.Backend
{
    public sealed class FilterValidator
    {
        private static readonly Dictionary<string, string[]> Sorts = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Queries"] = new[] { "totalCpuMs", "totalElapsedMs", "logicalReads", "executions", "avgCpuMs", "avgElapsedMs" },
            ["Waits"] = new[] { "totalWaitMs", "signalWaitMs", "resourceWaitMs", "taskCount", "sharePct" },
            ["FileIo"] = new[] { "database", "fileName", "fileType", "readIops", "writeIops", "readMiBps", "writeMiBps", "readLatencyMs", "writeLatencyMs" },
            ["Blocking"] = new[] { "sessionId", "database", "currentWaitMs", "blockedDescendants" }
        };
        public DashboardFilter Parse(IReadOnlyDictionary<string, string> input, PartitionContext context, string endpoint)
        {
            var allowed = new HashSet<string>(new[] { "instanceId", "sourceMode", "scenarioId", "databaseId", "range", "fromUtc", "toUtc", "asOfSampleId" }, StringComparer.Ordinal);
            if (Sorts.ContainsKey(endpoint)) allowed.UnionWith(new[] { "sort", "direction", "page", "pageSize" });
            if (endpoint == "Queries") allowed.Add("top");
            if (endpoint == "Waits") allowed.Add("includeExcluded");
            if (endpoint == "Blocking" || endpoint == "Samples") allowed.Add("sampleId");
            if (endpoint == "History") allowed.UnionWith(new[] { "metric", "maxPoints" });
            if (endpoint == "Samples") allowed.UnionWith(new[] { "page", "pageSize" });
            if (input.Keys.Any(x => !allowed.Contains(x))) Fail("Unknown or unsupported filter.");
            foreach (var value in input.Values) if (value == null || value.Length > 128) Fail("Filter value is too long.");
            if (!Guid.TryParse(Get(input, "instanceId", ""), out var instance) || instance != context.InstanceId) Fail("Invalid instance.");
            string mode = Get(input, "sourceMode", ""), scenario = Get(input, "scenarioId", "");
            if (mode != "synthetic" || mode != context.SourceMode || scenario != context.ScenarioId) Fail("A known synthetic scenario is required for this milestone.");
            var f = new DashboardFilter { InstanceId = instance, SourceMode = mode, ScenarioId = scenario };
            if (input.TryGetValue("databaseId", out var db))
            {
                if (!long.TryParse(db, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || !context.Databases.ContainsKey(id)) Fail("Database does not belong to this context.");
                f.DatabaseId = id;
            }
            string range = Get(input, "range", "15m");
            bool hasFrom = input.ContainsKey("fromUtc"), hasTo = input.ContainsKey("toUtc");
            if (range == "custom")
            {
                if (!hasFrom || !hasTo) Fail("Custom range requires both timestamps.");
                f.FromUtc = ParseUtc(Get(input, "fromUtc", "")); f.ToUtc = ParseUtc(Get(input, "toUtc", ""));
            }
            else
            {
                if (hasFrom || hasTo) Fail("Explicit timestamps require range=custom.");
                int minutes = range == "15m" ? 15 : range == "1h" ? 60 : range == "24h" ? 1440 : 0;
                if (minutes == 0) Fail("Invalid range.");
                f.ToUtc = context.EvaluationUtc; f.FromUtc = f.ToUtc.AddMinutes(-minutes);
            }
            if (f.FromUtc >= f.ToUtc || f.ToUtc > context.EvaluationUtc || f.FromUtc < context.EvaluationUtc.AddDays(-30)) Fail("Range is outside the retained policy horizon.");
            f.Page = Integer(input, "page", 1); f.PageSize = Integer(input, "pageSize", 20); f.Top = Integer(input, "top", 20);
            if (f.Page < 1 || f.Page > 1000 || !(endpoint == "Queries" ? new[] { 10, 20, 50 } : new[] { 20, 50, 100 }).Contains(f.PageSize)) Fail("Invalid pagination.");
            if (f.Top != 20 && f.Top != 50) Fail("Top must be 20 or 50.");
            if (Sorts.TryGetValue(endpoint, out var sorts))
            {
                f.Sort = Get(input, "sort", sorts[0]);
                if (!sorts.Contains(f.Sort)) Fail("Invalid sort.");
            }
            f.Direction = Get(input, "direction", "desc");
            if (f.Direction != "asc" && f.Direction != "desc") Fail("Invalid direction.");
            if (input.TryGetValue("includeExcluded", out var include))
            { if (!bool.TryParse(include, out var parsed)) Fail("Invalid boolean."); f.IncludeExcluded = parsed; }
            f.SampleId = OptionalGuid(input, "sampleId"); f.AsOfSampleId = OptionalGuid(input, "asOfSampleId");
            f.MaxPoints = Integer(input, "maxPoints", 240);
            if (f.MaxPoints < 30 || f.MaxPoints > 600) Fail("maxPoints must be between 30 and 600.");
            return f;
        }
        public static DateTime ParseUtc(string value)
        {
            // Require ISO timestamp and explicit zone; never infer server-local timezone.
            if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?(Z|[+-]\d{2}:\d{2})$") ||
                !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
                throw new DashboardException(400, "invalidFilter", "Timestamp must include an explicit ISO 8601 offset.");
            return timestamp.UtcDateTime;
        }
        private static string Get(IReadOnlyDictionary<string, string> input, string key, string fallback) => input.TryGetValue(key, out var value) ? value : fallback;
        private static int Integer(IReadOnlyDictionary<string, string> input, string key, int fallback)
        { if (!input.TryGetValue(key, out var value)) return fallback; if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n)) Fail("Invalid integer filter."); return n; }
        private static Guid? OptionalGuid(IReadOnlyDictionary<string, string> input, string key)
        { if (!input.TryGetValue(key, out var value)) return null; if (!Guid.TryParse(value, out var id)) Fail("Invalid sample ID."); return id; }
        private static void Fail(string message) => throw new DashboardException(400, "invalidFilter", message);
    }
}
