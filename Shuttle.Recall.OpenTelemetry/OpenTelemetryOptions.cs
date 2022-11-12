using System;

namespace Shuttle.Recall.OpenTelemetry
{
    public class RecallOpenTelemetryOptions
    {
        public const string SectionName = "Shuttle:Instrumentation:Recall";

        public bool Enabled { get; set; } = true;
        public bool IncludeSerializedMessage { get; set; } = true;
    }
}