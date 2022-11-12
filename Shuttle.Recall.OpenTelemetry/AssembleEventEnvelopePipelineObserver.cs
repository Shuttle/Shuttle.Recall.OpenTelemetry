using System;
using System.IO;
using System.Linq;
using System.Web;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Shuttle.Core.Contract;
using Shuttle.Core.Pipelines;
using Shuttle.Recall;
using Shuttle.Recall.OpenTelemetry;

namespace Shuttle.Esb.OpenTelemetry
{
    public class AssembleEventEnvelopePipelineObserver :
        IPipelineObserver<OnAfterAssembleEventEnvelope>,
        IPipelineObserver<OnAfterSerializeEvent>,
        IPipelineObserver<OnAfterEncryptEvent>,
        IPipelineObserver<OnAfterCompressEvent>,
        IPipelineObserver<OnPipelineException>
    {
        private readonly string _assembleEventEnvelopePipeline = nameof(AssembleEventEnvelopePipeline);
        private readonly RecallOpenTelemetryOptions _openTelemetryOptions;
        private readonly Tracer _tracer;

        public AssembleEventEnvelopePipelineObserver(RecallOpenTelemetryOptions openTelemetryOptions, Tracer tracer)
        {
            Guard.AgainstNull(openTelemetryOptions, nameof(openTelemetryOptions));
            Guard.AgainstNull(tracer, nameof(tracer));

            _openTelemetryOptions = openTelemetryOptions;
            _tracer = tracer;
        }

        public void Execute(OnAfterAssembleEventEnvelope pipelineEvent)
        {
            Guard.AgainstNull(pipelineEvent, nameof(pipelineEvent));

            try
            {
                var state = pipelineEvent.Pipeline.State;

                var eventStream = state.GetEventStream();
                var eventEnvelope = state.GetEventEnvelope();
                var telemetrySpan = _tracer.StartActiveSpan(_assembleEventEnvelopePipeline);

                if (telemetrySpan != null)
                {
                    telemetrySpan.SetAttribute("MachineName", Environment.MachineName);
                    telemetrySpan.SetAttribute("BaseDirectory", AppDomain.CurrentDomain.BaseDirectory);

                    Baggage.SetBaggage("Id", eventStream.Id.ToString());
                    Baggage.SetBaggage("EventId", eventEnvelope.EventId.ToString());

                    state.GetTelemetrySpan()?.SetAttribute("Id", eventStream.Id.ToString());
                    state.GetTelemetrySpan()?.SetAttribute("EventId", eventEnvelope.EventId.ToString());
                    state.GetTelemetrySpan()?.SetAttribute("EventType", eventEnvelope.EventType);
                    state.GetTelemetrySpan()?.SetAttribute("Version", eventEnvelope.Version);
                    state.GetTelemetrySpan()?.SetAttribute("AssemblyQualifiedName", eventEnvelope.AssemblyQualifiedName);
                    state.GetTelemetrySpan()?.SetAttribute("CompressionAlgorithm", eventEnvelope.CompressionAlgorithm);
                    state.GetTelemetrySpan()?.SetAttribute("EncryptionAlgorithm", eventEnvelope.EncryptionAlgorithm);

                    eventEnvelope.Headers.Add(new EnvelopeHeader
                    {
                        Key = EnvelopeHeaderKeys.ParentTraceId,
                        Value = telemetrySpan.Context.TraceId.ToString()
                    });

                    if (!eventEnvelope.Headers.Contains(EnvelopeHeaderKeys.Baggage))
                    {
                        var baggage = string.Join(",", Baggage.GetBaggage().Select(item => $"{item.Key}={HttpUtility.UrlEncode(item.Value)}"));

                        if (!string.IsNullOrEmpty(baggage))
                        {
                            eventEnvelope.Headers.Add(new EnvelopeHeader
                            {
                                Key = EnvelopeHeaderKeys.Baggage,
                                Value = baggage
                            });
                        }
                    }

                    state.SetPipelineTelemetrySpan(telemetrySpan);
                }

                telemetrySpan = _tracer.StartActiveSpan("OnFindRouteForMessage");

                state.SetTelemetrySpan(telemetrySpan);
            }
            catch
            {
                // ignored
            }
        }

        public void Execute(OnAfterSerializeEvent pipelineEvent)
        {
            Guard.AgainstNull(pipelineEvent, nameof(pipelineEvent));

            try
            {
                var state = pipelineEvent.Pipeline.State;

                if (_openTelemetryOptions.IncludeSerializedMessage)
                {
                    using (var reader = new StreamReader(new MemoryStream(state.GetEventBytes())))
                    {
                        state.GetTelemetrySpan()?.SetAttribute("SerializedMessage", reader.ReadToEnd());
                    }
                }

                state.GetTelemetrySpan()?.Dispose();
                state.SetTelemetrySpan(_tracer.StartActiveSpan("OnEncryptMessage"));
            }
            catch
            {
                // ignored
            }
        }

        public void Execute(OnAfterCompressEvent pipelineEvent)
        {
            Guard.AgainstNull(pipelineEvent, nameof(pipelineEvent));

            try
            {
                var state = pipelineEvent.Pipeline.State;

                state.GetTelemetrySpan()?.SetAttribute("CompressionAlgorithm)", state.GetEventEnvelope().CompressionAlgorithm);

                state.GetTelemetrySpan()?.Dispose();
                state.GetPipelineTelemetrySpan()?.Dispose();
            }
            catch
            {
                // ignored
            }
        }

        public void Execute(OnAfterEncryptEvent pipelineEvent)
        {
            Guard.AgainstNull(pipelineEvent, nameof(pipelineEvent));

            try
            {
                var state = pipelineEvent.Pipeline.State;

                state.GetTelemetrySpan()?.SetAttribute("EncryptionAlgorithm", state.GetEventEnvelope().EncryptionAlgorithm);

                state.GetTelemetrySpan()?.Dispose();
                state.SetTelemetrySpan(_tracer.StartActiveSpan("OnCompressMessage"));
            }
            catch
            {
                // ignored
            }
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
    }
}