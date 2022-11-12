using System;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using Shuttle.Core.Contract;
using Shuttle.Core.Pipelines;

namespace Shuttle.Recall.OpenTelemetry
{
    public class OpenTelemetryModule
    {
        private readonly RecallOpenTelemetryOptions _openTelemetryOptions;

        private readonly Type _eventProcessingPipelineType = typeof(EventProcessingPipeline);
        private readonly Type _saveEventStreamPipelineType = typeof(SaveEventStreamPipeline);
        private readonly Type _assembleEventEnvelopePipelineType = typeof(AssembleEventEnvelopePipeline);

        private readonly SaveEventStreamPipelineObserver _saveEventStreamPipelineObserver;
        private readonly EventProcessingPipelineObserver _eventProcessingPipelineObserver;

        private readonly Tracer _tracer;

        public OpenTelemetryModule(IOptions<RecallOpenTelemetryOptions> openTelemetryOptions, TracerProvider tracerProvider, IPipelineFactory pipelineFactory)
        {
            Guard.AgainstNull(openTelemetryOptions, nameof(openTelemetryOptions));
            Guard.AgainstNull(openTelemetryOptions.Value, nameof(openTelemetryOptions.Value));
            Guard.AgainstNull(tracerProvider, nameof(tracerProvider));
            Guard.AgainstNull(pipelineFactory, nameof(pipelineFactory));

            _openTelemetryOptions = openTelemetryOptions.Value;

            if (!_openTelemetryOptions.Enabled)
            {
                return;
            }

            _tracer = tracerProvider.GetTracer("Shuttle.Recall");

            _eventProcessingPipelineObserver = new EventProcessingPipelineObserver(_tracer);
            _saveEventStreamPipelineObserver = new SaveEventStreamPipelineObserver(_tracer);

            pipelineFactory.PipelineCreated += PipelineCreated;
        }

        private void PipelineCreated(object sender, PipelineEventArgs e)
        {
            var pipelineType = e.Pipeline.GetType();

            if (pipelineType == _eventProcessingPipelineType)
            {
                e.Pipeline.RegisterObserver(_eventProcessingPipelineObserver);
            }

            if (pipelineType == _saveEventStreamPipelineType)
            {
                e.Pipeline.RegisterObserver(_saveEventStreamPipelineObserver);
            }
        }
    }
}