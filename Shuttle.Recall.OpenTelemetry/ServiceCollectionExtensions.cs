using System;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Shuttle.Core.Contract;
using Shuttle.Core.Pipelines;

namespace Shuttle.Recall.OpenTelemetry
{
    public static class ServiceCollectionExtensions
    {
        public static TracerProviderBuilder AddRecallInstrumentation(this TracerProviderBuilder tracerProviderBuilder, Action<OpenTelemetryBuilder> builder = null)
        {
            Guard.AgainstNull(tracerProviderBuilder, nameof(tracerProviderBuilder));

            tracerProviderBuilder.AddSource("Shuttle.Recall");

            tracerProviderBuilder.ConfigureServices(services =>
            {
                var openTelemetryBuilder = new OpenTelemetryBuilder(services);

                builder?.Invoke(openTelemetryBuilder);

                services.AddOptions<RecallOpenTelemetryOptions>().Configure(options =>
                {
                    options.Enabled = openTelemetryBuilder.Options.Enabled;
                    options.IncludeSerializedMessage = openTelemetryBuilder.Options.IncludeSerializedMessage;
                });

                services.AddPipelineModule<OpenTelemetryModule>();
            });

            return tracerProviderBuilder;
        }
    }
}