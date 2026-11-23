namespace NexaHR.Application.DTOs.Company;

public sealed class CompanySettingsOperationResponse
{
    public bool Success { get; init; }

    public string Code { get; init; } =
        string.Empty;

    public string Message { get; init; } =
        string.Empty;

    public CompanySettingsResponse? Settings
        { get; init; }
}
