using OpenTelemetry;
using OpenTelemetry.Trace;
using Shuttle.Core.Contract;
using Shuttle.Core.Pipelines;
using Shuttle.Core.PipelineTransaction;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using Shuttle.Esb.OpenTelemetry;

namespace Shuttle.Recall.OpenTelemetry
{
    public class EventProcessingPipelineObserver :
        IPipelineObserver<OnPipelineStarting>,
        IPipelineObserver<OnAfterStartTransactionScope>,
        IPipelineObserver<OnPipelineException>,
        IPipelineObserver<OnAfterGetProjectionEvent>,
        IPipelineObserver<OnAfterGetProjectionEventEnvelope>,
        IPipelineObserver<OnAfterProcessEvent>,
        IPipelineObserver<OnAfterAcknowledgeEvent>
    {
        private const string EventProcessingPipelineName = nameof(EventProcessingPipeline);

        private readonly Tracer _tracer;

        public EventProcessingPipelineObserver(Tracer tracer)
        {
            Guard.AgainstNull(tracer, nameof(tracer));

            _tracer = tracer;
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

        public void Execute(OnAfterGetProjectionEvent pipelineEvent)
        {
            Guard.AgainstNull(pipelineEvent, nameof(pipelineEvent));

            try
            {
                var state = pipelineEvent.Pipeline.State;

                var projectionEvent = state.GetProjectionEvent();

                state.GetTelemetrySpan()?.SetAttribute("HasPrimitiveEvent", projectionEvent.HasPrimitiveEvent);
                state.GetTelemetrySpan()?.SetAttribute("SequenceNumber", projectionEvent.SequenceNumber);

                if (projectionEvent.HasPrimitiveEvent)
                {
                    state.GetTelemetrySpan()?.SetAttribute("PrimitiveEvent.Id", projectionEvent.PrimitiveEvent.Id.ToString());
                    state.GetTelemetrySpan()?.SetAttribute("PrimitiveEvent.EventId", projectionEvent.PrimitiveEvent.EventId.ToString());
                    state.GetTelemetrySpan()?.SetAttribute("PrimitiveEvent.EventType", projectionEvent.PrimitiveEvent.EventType);
                    state.GetTelemetrySpan()?.SetAttribute("PrimitiveEvent.IsSnapshot", projectionEvent.PrimitiveEvent.IsSnapshot);
                    state.GetTelemetrySpan()?.SetAttribute("PrimitiveEvent.Version", projectionEvent.PrimitiveEvent.Version);
                    state.GetTelemetrySpan()?.SetAttribute("PrimitiveEvent.DateRegistered", projectionEvent.PrimitiveEvent.DateRegistered.ToString("O"));
                    state.GetTelemetrySpan()?.Dispose();
                    state.SetTelemetrySpan(_tracer.StartActiveSpan("OnGetProjectionEventEnvelope"));
                }
            }
            catch
            {
                // ignored
            }
        }

        public async Task ExecuteAsync(OnAfterGetProjectionEvent pipelineEvent)
        {
            Execute(pipelineEvent);

            await Task.CompletedTask;
        }

        public void Execute(OnPipelineStarting pipelineEvent)
        {
            Guard.AgainstNull(pipelineEvent, nameof(pipelineEvent));

            try
            {
                var telemetrySpan = _tracer.StartActiveSpan(EventProcessingPipelineName);

                telemetrySpan.SetAttribute("MachineName", Environment.MachineName);
                telemetrySpan.SetAttribute("BaseDirectory", AppDomain.CurrentDomain.BaseDirectory);

                pipelineEvent.Pipeline.State.SetPipelineTelemetrySpan(telemetrySpan);
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

        public void Execute(OnAfterGetProjectionEventEnvelope pipelineEvent)
        {
            Guard.AgainstNull(pipelineEvent, nameof(pipelineEvent));

            try
            {
                var state = pipelineEvent.Pipeline.State;
                var projectionEvent = state.GetProjectionEvent();

                if (!projectionEvent.HasPrimitiveEvent)
                {
                    return;
                }

                var eventEnvelope = state.GetEventEnvelope();

                var parentTraceId = eventEnvelope.Headers.Contains(EnvelopeHeaderKeys.ParentTraceId) ? eventEnvelope.Headers.GetHeaderValue(EnvelopeHeaderKeys.ParentTraceId) : null;
                var baggage = eventEnvelope.Headers.Contains(EnvelopeHeaderKeys.Baggage) ? eventEnvelope.Headers.GetHeaderValue(EnvelopeHeaderKeys.Baggage) : null;

                var telemetrySpan = string.IsNullOrEmpty(parentTraceId)
                    ? _tracer.StartActiveSpan("OnProcessEvent")
                    : _tracer.StartActiveSpan("OnProcessEvent", SpanKind.Consumer, new SpanContext(ActivityTraceId.CreateFromString(parentTraceId.ToCharArray()), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded));

                if (string.IsNullOrEmpty(baggage))
                {
                    Baggage.Current.SetBaggage("EventId", eventEnvelope.EventId.ToString());
                    Baggage.Current.SetBaggage("EventType", eventEnvelope.EventType);
                }
                else
                {
                    var baggagePairs = new List<KeyValuePair<string, string>>();
                    var baggageItems = baggage.Split(',');

                    if (baggageItems.Length > 0)
                    {
                        foreach (var item in baggageItems)
                        {
                            if (NameValueHeaderValue.TryParse(item, out var baggageItem))
                            {
                                baggagePairs.Add(new KeyValuePair<string, string>(baggageItem.Name, HttpUtility.UrlDecode(baggageItem.Value)));
                            }
                        }
                    }

                    for (var i = baggagePairs.Count - 1; i >= 0; i--)
                    {
                        var baggagePair = baggagePairs[i];

                        Baggage.Current.SetBaggage(baggagePair.Key, baggagePair.Value);
                    }
                }

                telemetrySpan.SetAttribute("EventId", eventEnvelope.EventId.ToString());
                telemetrySpan.SetAttribute("EventType", eventEnvelope.EventType);

                state.SetTelemetrySpan(telemetrySpan);
            }
            catch
            {
                // ignored
            }
        }

        public async Task ExecuteAsync(OnAfterGetProjectionEventEnvelope pipelineEvent)
        {
            Execute(pipelineEvent);

            await Task.CompletedTask;
        }

        public void Execute(OnAfterProcessEvent pipelineEvent)
        {
            Guard.AgainstNull(pipelineEvent, nameof(pipelineEvent));

            try
            {
                var state = pipelineEvent.Pipeline.State;
                var projectionEvent = state.GetProjectionEvent();

                if (!projectionEvent.HasPrimitiveEvent)
                {
                    return;
                }

                var projection = state.GetProjection();
                var telemetrySpan = state.GetTelemetrySpan();

                telemetrySpan?.SetAttribute("Projection",projection.Name);
                telemetrySpan?.SetAttribute("SequenceNumber", projection.SequenceNumber);
                telemetrySpan?.SetAttribute("AggregationId", projection.AggregationId.ToString());
                telemetrySpan?.Dispose();
                
                state.SetTelemetrySpan(_tracer.StartActiveSpan("OnAcknowledgeEvent"));
            }
            catch
            {
                // ignore
            }
        }

        public async Task ExecuteAsync(OnAfterProcessEvent pipelineEvent)
        {
            Execute(pipelineEvent);

            await Task.CompletedTask;
        }

        public void Execute(OnAfterAcknowledgeEvent pipelineEvent)
        {
            Guard.AgainstNull(pipelineEvent, nameof(pipelineEvent));

            var state = pipelineEvent.Pipeline.State;

            try
            {
                var projectionEvent = state.GetProjectionEvent();

                if (!projectionEvent.HasPrimitiveEvent)
                {
                    return;
                }

                state.GetTelemetrySpan()?.Dispose();
            }
            catch
            {
                // ignore
            }
            finally
            {
                state.GetPipelineTelemetrySpan()?.Dispose();
            }
        }

        public async Task ExecuteAsync(OnAfterAcknowledgeEvent pipelineEvent)
        {
            Execute(pipelineEvent);

            await Task.CompletedTask;
        }

        public void Execute(OnAfterStartTransactionScope pipelineEvent)
        {
            Guard.AgainstNull(pipelineEvent, nameof(pipelineEvent));

            try
            {
                pipelineEvent.Pipeline.State.SetTelemetrySpan(_tracer.StartActiveSpan("OnGetProjectionEvent"));
            }
            catch
            {
                // ignored
            }
        }

        public async Task ExecuteAsync(OnAfterStartTransactionScope pipelineEvent)
        {
            Execute(pipelineEvent);

            await Task.CompletedTask;
        }
    }
}