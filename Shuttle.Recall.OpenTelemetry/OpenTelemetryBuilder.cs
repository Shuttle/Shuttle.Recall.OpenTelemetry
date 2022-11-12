using System;
using Microsoft.Extensions.DependencyInjection;
using Shuttle.Core.Contract;

namespace Shuttle.Recall.OpenTelemetry
{
    public class OpenTelemetryBuilder
    {
        private RecallOpenTelemetryOptions _openTelemetryOptions = new RecallOpenTelemetryOptions();

        public OpenTelemetryBuilder(IServiceCollection services)
        {
            Guard.AgainstNull(services, nameof(services));

            Services = services;
        }

        public RecallOpenTelemetryOptions Options
        {
            get => _openTelemetryOptions;
            set => _openTelemetryOptions = value ?? throw new ArgumentNullException(nameof(value));
        }

        public IServiceCollection Services { get; }
    }
}