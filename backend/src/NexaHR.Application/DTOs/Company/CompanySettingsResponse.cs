namespace NexaHR.Application.DTOs.Company;

public sealed class CompanySettingsResponse
{
    public string? CompanyName { get; init; }

    public string? SupportEmail { get; init; }

    public string? SupportPhone { get; init; }

    public string BusinessTimeZone { get; init; } =
        "Asia/Colombo";

    public bool IsConfigured { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }

    public string? RowVersion { get; init; }
}
