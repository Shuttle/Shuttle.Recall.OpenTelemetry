﻿using OpenTelemetry.Trace;
using Shuttle.Core.Pipelines;

namespace Shuttle.Recall.OpenTelemetry
{
    public static class PipelineStateExtensions
    {
        public static TelemetrySpan GetTelemetrySpan(this IState state)
        {
            return state.Get<TelemetrySpan>(StateKeys.TelemetrySpan);
        }

        public static void SetTelemetrySpan(this IState state, TelemetrySpan telemetrySpan)
        {
            state.Replace(StateKeys.TelemetrySpan, telemetrySpan);
        }

        public static TelemetrySpan GetPipelineTelemetrySpan(this IState state)
        {
            return state.Get<TelemetrySpan>(StateKeys.PipelineTelemetrySpan);
        }

        public static void SetPipelineTelemetrySpan(this IState state, TelemetrySpan telemetrySpan)
        {
            state.Replace(StateKeys.PipelineTelemetrySpan, telemetrySpan);
        }
    }
}