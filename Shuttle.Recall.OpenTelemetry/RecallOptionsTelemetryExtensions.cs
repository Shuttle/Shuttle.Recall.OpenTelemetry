using System.Diagnostics;
using Shuttle.Contract;

namespace Shuttle.Recall.OpenTelemetry;

public static class RecallOptionsTelemetryExtensions
{
    // Operation strings look like "[GetAsync] : id = '...'" or "[PrimitiveEventSequencer/Completed] :
    // rows affected = 42" - the bracketed prefix is always the stable part, so that's what gets tagged.
    private static string OperationTag(string operation)
    {
        var start = operation.IndexOf('[');
        var end = operation.IndexOf(']');

        return start < 0 || end < 0 || end <= start ? operation : operation[(start + 1)..end];
    }

    extension(RecallOptions recallOptions)
    {
        /// <summary>
        ///     Subscribes to every event on `RecallOptions` and records it against the `Shuttle.Recall`
        ///     meter. Call once per `RecallOptions` instance (see `RecallBuilder.AddOpenTelemetry()`).
        /// </summary>
        public void AddOpenTelemetryMetrics()
        {
            Guard.AgainstNull(recallOptions);

            var meter = RecallTelemetry.Meter;

            var primitiveEventsSaved = meter.CreateCounter<long>("recall.primitive_events.saved", "{event}", "Number of primitive events saved to the event store.");
            var eventsHandled = meter.CreateCounter<long>("recall.events.handled", "{event}", "Number of events successfully handled by a projection.");
            var operations = meter.CreateCounter<long>("recall.operations", "{operation}", "Number of Shuttle.Recall infrastructure operations (event store, event processing, storage).");

            recallOptions.EventStore.PrimitiveEventsSaved += (eventArgs, _) =>
            {
                foreach (var group in eventArgs.PrimitiveEvents.GroupBy(primitiveEvent => primitiveEvent.EventType))
                {
                    primitiveEventsSaved.Add(group.Count(), new KeyValuePair<string, object?>("recall.event.type", group.Key));
                }

                return Task.CompletedTask;
            };

            recallOptions.EventProcessing.EventHandled += (eventArgs, _) =>
            {
                eventsHandled.Add(1,
                    new("recall.event.type", eventArgs.EventEnvelope.EventType),
                    new("recall.projection.name", eventArgs.ProjectionEvent.Projection.Name));

                return Task.CompletedTask;
            };

            recallOptions.Operation += (eventArgs, _) =>
            {
                operations.Add(1, new KeyValuePair<string, object?>("recall.operation", OperationTag(eventArgs.Operation)));

                return Task.CompletedTask;
            };
        }

        /// <summary>
        ///     Subscribes to `EventHandled` on `RecallOptions` to record a per-event span for each event
        ///     a projection handles. Call once per `RecallOptions` instance (see
        ///     `RecallBuilder.AddOpenTelemetry()`).
        /// </summary>
        public void AddOpenTelemetryTracing()
        {
            Guard.AgainstNull(recallOptions);

            // Processing side: the event has just been handled by a projection. The span is opened
            // and closed here, rather than spanning from when the envelope was deserialized, because
            // deserialization also happens for plain aggregate replay (EventStore.GetAsync), which has
            // no matching "handled" checkpoint to close a longer-lived span against.
            recallOptions.EventProcessing.EventHandled += (eventArgs, _) =>
            {
                var eventEnvelope = eventArgs.EventEnvelope;
                var projectionEvent = eventArgs.ProjectionEvent;
                var parentContext = TraceContext.ExtractContext(eventEnvelope.Headers);

                using var activity = RecallTelemetry.ActivitySource.StartActivity(eventEnvelope.EventType, ActivityKind.Consumer, parentContext);

                if (activity == null)
                {
                    return Task.CompletedTask;
                }

                TraceContext.ExtractBaggage(eventEnvelope.Headers, activity);

                activity.SetTag("recall.id", projectionEvent.PrimitiveEvent.Id.ToString());
                activity.SetTag("recall.event.id", eventEnvelope.EventId.ToString());
                activity.SetTag("recall.event.type", eventEnvelope.EventType);
                activity.SetTag("recall.event.version", eventEnvelope.Version);
                activity.SetTag("recall.projection.name", projectionEvent.Projection.Name);
                activity.SetTag("recall.projection.sequence_number", projectionEvent.Projection.SequenceNumber);

                return Task.CompletedTask;
            };
        }
    }
}