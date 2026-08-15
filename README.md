# OpenTelemetry

Adds OpenTelemetry-compatible metrics and distributed tracing to Shuttle.Recall by observing the events already exposed on `RecallOptions` — no changes are required to your event handlers or projections.

This package is scoped to Recall-domain telemetry only (event envelopes, trace-context propagation, per-event spans). Pipeline-level instrumentation (execution duration, stage/event timing, failure counts) is already covered by [`Shuttle.Pipelines.OpenTelemetry`](https://github.com/shuttle/Shuttle.Pipelines.OpenTelemetry), which observes the generic events on `PipelineOptions` that every Recall pipeline raises — add it alongside this package rather than duplicating that here.

## Installation

```bash
dotnet add package Shuttle.Recall.OpenTelemetry
dotnet add package Shuttle.Pipelines.OpenTelemetry
```

## Registration

```csharp
services.AddRecall()
    .AddOpenTelemetry();

services.AddPipelines()
    .AddOpenTelemetry();
```

`RecallBuilder.AddOpenTelemetry()` subscribes to `EventStore.PrimitiveEventsSaved`, `EventProcessing.EventHandled`, and `Operation` on `RecallOptions`, recording metrics and tracing against sources named `Shuttle.Recall`.

The instrumentation is built on `System.Diagnostics.ActivitySource` / `System.Diagnostics.Metrics.Meter` directly, so this package has no dependency on the `OpenTelemetry` SDK — it only becomes "live" once something subscribes to those names, for example:

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder.AddSource("Shuttle.Recall"))
    .WithMetrics(builder => builder.AddMeter("Shuttle.Recall").AddMeter("Shuttle.Pipelines"));
```

(`Shuttle.Pipelines.OpenTelemetry` only publishes metrics — it has no `ActivitySource` of its own.)

## Metrics

| Name | Instrument | Unit | Description |
| --- | --- | --- | --- |
| `recall.primitive_events.saved` | Counter | `{event}` | Number of primitive events saved to the event store, tagged `recall.event.type`. |
| `recall.events.handled` | Counter | `{event}` | Number of events successfully handled by a projection, tagged `recall.event.type` and `recall.projection.name`. |
| `recall.operations` | Counter | `{operation}` | Number of Shuttle.Recall infrastructure operations (event store, event processing, storage), tagged `recall.operation`. |

## Tracing

Tracing here is scoped to the event itself, not the pipelines that carry it — pair with `Shuttle.Pipelines.OpenTelemetry` if you also want pipeline-execution spans.

- **Process** – once a projection has finished handling an event, `EventEnvelope.Headers` are extracted (via `TraceContext.ExtractContext`/`ExtractBaggage`) and used as the parent for a new `Activity` named after the event type, covering the handling of that one event. It is tagged with `recall.id` (the aggregate id), `recall.event.id`, `recall.event.type`, `recall.event.version`, `recall.projection.name` and `recall.projection.sequence_number`.

This span is deliberately opened and closed within the `EventHandled` handler rather than spanning from when the envelope was deserialized: deserialization also happens during plain aggregate replay (`EventStore.GetAsync`), which has no matching "handled" checkpoint to close a longer-lived span against.

This package does not itself write trace context into `EventEnvelope.Headers` when an event is saved — there is currently no "Save"-side span or automatic context propagation. `TraceContext.Inject` is exposed as an extension point (see below) if you want to propagate context yourself, e.g. from a custom pipeline observer that runs on save.

## Extension points

- `RecallTelemetry.ActivitySource` / `RecallTelemetry.Meter` – the shared instances used throughout; exposed so you can add your own spans or measurements under the same names.
- `TraceContext.Inject` / `TraceContext.ExtractContext` / `TraceContext.ExtractBaggage` – W3C trace-context/baggage header helpers (`traceparent`/`tracestate`/`baggage`) for reading or writing `EventEnvelope.Headers` yourself. Only `ExtractContext`/`ExtractBaggage` are currently used by this package (on the processing side); `Inject` is unused internally and provided purely as a building block.
