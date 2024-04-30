using System.Linq;
using System.Threading.Tasks;
using OpenTelemetry.Trace;
using Shuttle.Core.Contract;
using Shuttle.Core.Pipelines;

namespace Shuttle.Recall.OpenTelemetry
{
    public class SaveEventStreamPipelineObserver :
        IPipelineObserver<OnPipelineStarting>,
        IPipelineObserver<OnPipelineException>,
        IPipelineObserver<OnAfterAssembleEventEnvelopes>,
        IPipelineObserver<OnAfterSavePrimitiveEvents>,
        IPipelineObserver<OnAfterCommitEventStream>
    {
        private const string SaveEventStreamPipelineName = nameof(SaveEventStreamPipeline);

        private readonly Tracer _tracer;

        public SaveEventStreamPipelineObserver(Tracer tracer)
        {
            Guard.AgainstNull(tracer, nameof(tracer));

            _tracer = tracer;
        }

        public void Execute(OnAfterAssembleEventEnvelopes pipelineEvent)
        {
            Guard.AgainstNull(pipelineEvent, nameof(pipelineEvent));

            try
            {
                var state = pipelineEvent.Pipeline.State;

                state.GetTelemetrySpan()?.Dispose();

                var eventEnvelopes = state.GetEventEnvelopes();

                var telemetrySpan = _tracer.StartActiveSpan("OnSavePrimitiveEvents");

                telemetrySpan.SetAttribute("EventEnvelopeCount", eventEnvelopes.Count());

                state.SetTelemetrySpan(telemetrySpan);
            }
            catch
            {
                // ignored
            }
        }

        public async Task ExecuteAsync(OnAfterAssembleEventEnvelopes pipelineEvent)
        {
            Execute(pipelineEvent);

            await Task.CompletedTask;
        }

        public void Execute(OnAfterCommitEventStream pipelineEvent)
        {
            Guard.AgainstNull(pipelineEvent, nameof(pipelineEvent));

            try
            {
                var state = pipelineEvent.Pipeline.State;

                state.GetTelemetrySpan()?.Dispose();
                state.GetPipelineTelemetrySpan()?.Dispose();
            }
            catch
            {
                // ignored
            }
        }

        public async Task ExecuteAsync(OnAfterCommitEventStream pipelineEvent)
        {
            Execute(pipelineEvent);

            await Task.CompletedTask;
        }

        public void Execute(OnAfterSavePrimitiveEvents pipelineEvent)
        {
            Guard.AgainstNull(pipelineEvent, nameof(pipelineEvent));

            try
            {
                var state = pipelineEvent.Pipeline.State;

                state.GetTelemetrySpan()?.Dispose();
                state.SetTelemetrySpan(_tracer.StartActiveSpan("OnCommitEventStream"));
            }
            catch
            {
                // ignored
            }
        }

        public async Task ExecuteAsync(OnAfterSavePrimitiveEvents pipelineEvent)
        {
            Execute(pipelineEvent);

            await Task.CompletedTask;
        }

        public void Execute(OnPipelineException pipelineEvent)
        {
            Guard.AgainstNull(pipelineEvent, nameof(pipelineEvent));

            try
            {
                var state = pipelineEvent.Pipeline.State;

                using (var telemetrySpan = state.GetTelemetrySpan())
                {
                    telemetrySpan?.RecordException(pipelineEvent.Pipeline.Exception);
                    telemetrySpan?.SetStatus(Status.Error);
                }

                state.GetPipelineTelemetrySpan()?.Dispose();
            }
            catch
            {
                // ignored
            }
        }

        public async Task ExecuteAsync(OnPipelineException pipelineEvent)
        {
            Execute(pipelineEvent);

            await Task.CompletedTask;
        }

        public void Execute(OnPipelineStarting pipelineEvent)
        {
            Guard.AgainstNull(pipelineEvent, nameof(pipelineEvent));

            try
            {
                var state = pipelineEvent.Pipeline.State;

                state.SetPipelineTelemetrySpan(_tracer.StartActiveSpan(SaveEventStreamPipelineName));
                state.SetTelemetrySpan(_tracer.StartActiveSpan("OnAssembleEventEnvelopes"));
            }
            catch
            {
                // ignored
            }
        }

        public async Task ExecuteAsync(OnPipelineStarting pipelineEvent)
        {
            Execute(pipelineEvent);

            await Task.CompletedTask;
        }
    }
}