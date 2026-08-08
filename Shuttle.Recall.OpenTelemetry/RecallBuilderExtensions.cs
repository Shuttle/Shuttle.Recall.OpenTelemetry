using Microsoft.Extensions.DependencyInjection;
using Shuttle.Contract;

namespace Shuttle.Recall.OpenTelemetry;

public static class RecallBuilderExtensions
{
    extension(RecallBuilder recallBuilder)
    {
        /// <summary>
        ///     Adds Recall-specific metrics and tracing (event envelopes, trace-context propagation,
        ///     per-event spans). For pipeline-level metrics (duration, stage/event timing, failure counts)
        ///     add `Shuttle.Pipelines.OpenTelemetry`'s `PipelineBuilder.AddOpenTelemetry()` alongside this.
        /// </summary>
        public RecallBuilder AddOpenTelemetry()
        {
            var services = Guard.AgainstNull(recallBuilder).Services;

            services.AddOptions<RecallOptions>().Configure(options =>
            {
                options.AddOpenTelemetryMetrics();
                options.AddOpenTelemetryTracing();
            });

            return recallBuilder;
        }
    }
}