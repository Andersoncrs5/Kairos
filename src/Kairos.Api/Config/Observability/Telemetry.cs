using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Kairos.Api.Config.Observability;

public static class Telemetry
{
    public const string Name = "Kairos.Api";

    public static readonly ActivitySource ActivitySource = new(Name);

    public static readonly Meter Meter = new(Name);

    public static readonly Counter<long> InboxConflicts =
        Meter.CreateCounter<long>(
            "kairos.inbox.conflicts",
            description: "Inbox messages that could not be acquired for processing.");
}