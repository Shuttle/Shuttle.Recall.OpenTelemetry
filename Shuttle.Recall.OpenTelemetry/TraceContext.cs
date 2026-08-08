using System.Diagnostics;
using Shuttle.Contract;

namespace Shuttle.Recall.OpenTelemetry;

/// <summary>
///     Propagates W3C Trace Context (https://www.w3.org/TR/trace-context/) and Baggage
///     (https://www.w3.org/TR/baggage/) across an `EventEnvelope` using the same header
///     names as the HTTP spec, so the same context works whether an event is processed
///     in-process or picked up later by a projection.
/// </summary>
public static class TraceContext
{
    public const string BaggageHeaderKey = "baggage";
    public const string TraceParentHeaderKey = "traceparent";
    public const string TraceStateHeaderKey = "tracestate";

    public static void ExtractBaggage(List<EnvelopeHeader> headers, Activity activity)
    {
        Guard.AgainstNull(headers);
        Guard.AgainstNull(activity);

        if (!headers.Contains(BaggageHeaderKey))
        {
            return;
        }

        foreach (var pair in headers.GetHeaderValue(BaggageHeaderKey).Split(','))
        {
            var index = pair.IndexOf('=');

            if (index <= 0)
            {
                continue;
            }

            activity.SetBaggage(Uri.UnescapeDataString(pair[..index]), Uri.UnescapeDataString(pair[(index + 1)..]));
        }
    }

    public static ActivityContext ExtractContext(List<EnvelopeHeader> headers)
    {
        Guard.AgainstNull(headers);

        if (!headers.Contains(TraceParentHeaderKey))
        {
            return default;
        }

        var traceParent = headers.GetHeaderValue(TraceParentHeaderKey);
        var traceState = headers.Contains(TraceStateHeaderKey) ? headers.GetHeaderValue(TraceStateHeaderKey) : null;

        return ActivityContext.TryParse(traceParent, traceState, out var context) ? context : default;
    }

    public static void Inject(List<EnvelopeHeader> headers, Activity? activity)
    {
        Guard.AgainstNull(headers);

        if (activity?.Id == null)
        {
            return;
        }

        SetHeaderValue(headers, TraceParentHeaderKey, activity.Id);

        if (!string.IsNullOrEmpty(activity.TraceStateString))
        {
            SetHeaderValue(headers, TraceStateHeaderKey, activity.TraceStateString);
        }

        var baggage = activity.Baggage.ToList();

        if (baggage.Count > 0)
        {
            SetHeaderValue(headers, BaggageHeaderKey, string.Join(',', baggage.Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value ?? string.Empty)}")));
        }
    }

    private static void SetHeaderValue(List<EnvelopeHeader> headers, string key, string value)
    {
        var header = headers.FirstOrDefault(candidate => candidate.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));

        if (header == null)
        {
            headers.Add(new() { Key = key, Value = value });
        }
        else
        {
            header.Value = value;
        }
    }
}