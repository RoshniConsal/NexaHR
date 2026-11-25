namespace NexaHR.Application.DTOs.Dashboard;

public sealed class DashboardMetric
{
    public string Key { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public int? Value { get; init; }

    public string Status { get; init; } = "available";
}

public sealed class DashboardResponse
{
    public string Role { get; init; } = string.Empty;

    public DateTime GeneratedAtUtc { get; init; }

    public bool IsPartial { get; init; }

    public IReadOnlyList<DashboardMetric> Metrics
        { get; init; } = [];

    public IReadOnlyList<string> PendingIntegrations
        { get; init; } = [];
}

public sealed class PendingActionItem
{
    public string Key { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public int? Count { get; init; }

    public string? DeepLink { get; init; }

    public string Status { get; init; } = "available";
}

public sealed class PendingActionsResponse
{
    public string Role { get; init; } = string.Empty;

    public DateTime GeneratedAtUtc { get; init; }

    public bool IsPartial { get; init; }

    public int? TotalPendingActions { get; init; }

    public IReadOnlyList<PendingActionItem> Actions
        { get; init; } = [];

    public IReadOnlyList<string> PendingIntegrations
        { get; init; } = [];
}
