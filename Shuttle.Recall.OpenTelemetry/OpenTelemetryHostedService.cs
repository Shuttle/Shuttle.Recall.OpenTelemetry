using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using Shuttle.Core.Contract;
using Shuttle.Core.Pipelines;

namespace Shuttle.Recall.OpenTelemetry
{
    public class OpenTelemetryHostedService : IHostedService
    {
        private readonly RecallOpenTelemetryOptions _openTelemetryOptions;

        private readonly Type _eventProcessingPipelineType = typeof(EventProcessingPipeline);
        private readonly Type _saveEventStreamPipelineType = typeof(SaveEventStreamPipeline);

        private readonly SaveEventStreamPipelineObserver _saveEventStreamPipelineObserver;
        private readonly EventProcessingPipelineObserver _eventProcessingPipelineObserver;

        private readonly IPipelineFactory _pipelineFactory;

        public OpenTelemetryHostedService(IOptions<RecallOpenTelemetryOptions> openTelemetryOptions, TracerProvider tracerProvider, IPipelineFactory pipelineFactory)
        {
            Guard.AgainstNull(openTelemetryOptions, nameof(openTelemetryOptions));
            Guard.AgainstNull(openTelemetryOptions.Value, nameof(openTelemetryOptions.Value));
            Guard.AgainstNull(tracerProvider, nameof(tracerProvider));

            _openTelemetryOptions = openTelemetryOptions.Value;

            if (!_openTelemetryOptions.Enabled)
            {
                return;
            }

            _pipelineFactory = Guard.AgainstNull(pipelineFactory, nameof(pipelineFactory));

            var tracer = tracerProvider.GetTracer("Shuttle.Recall");

            _eventProcessingPipelineObserver = new EventProcessingPipelineObserver(tracer);
            _saveEventStreamPipelineObserver = new SaveEventStreamPipelineObserver(tracer);

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

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_openTelemetryOptions.Enabled)
            {
                _pipelineFactory.PipelineCreated -= PipelineCreated;
            }

            await Task.CompletedTask;
        }
    }
}