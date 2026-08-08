using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Shuttle.Recall.OpenTelemetry;

public static class RecallTelemetry
{
    public const string ActivitySourceName = "Shuttle.Recall";
    public const string MeterName = "Shuttle.Recall";

    private static readonly string? Version = typeof(RecallTelemetry).Assembly.GetName().Version?.ToString();

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version);
    public static readonly Meter Meter = new(MeterName, Version);
}