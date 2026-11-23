namespace NexaHR.Application.DTOs.Company;

public sealed class SupportContactResponse
{
    public bool IsConfigured { get; init; }

    public string? SupportEmail { get; init; }

    public string? SupportPhone { get; init; }

    public string Message { get; init; } =
        string.Empty;
}
