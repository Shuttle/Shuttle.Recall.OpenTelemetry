# Shuttle.Recall.OpenTelemetry

```
PM> Install-Package Shuttle.Recall.OpenTelemetry
```

OpenTelemetry instrumentation for Shuttle.Recall implementations.

## Configuration

```c#
services.AddRecallInstrumentation(builder =>
{
	// default values
    builder.Options.Enabled = true;
    builder.Options.IncludeSerializedMessage = true;

	// or bind from configuration
	configuration
		.GetSection(RecallOpenTelemetryOptions.SectionName)
		.Bind(builder.Options);
});


## Options

| Option | Default	| Description |
| --- | --- | --- | 
| `Enabled` | `true` | Indicates whether to perform instrumentation. |
| `IncludeSerializedMessage` | `true` | If 'true', includes the serialized message as attribute `SerializedMessage` in the trace. |