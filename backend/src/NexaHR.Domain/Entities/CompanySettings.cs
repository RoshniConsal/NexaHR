namespace NexaHR.Domain.Entities;

public sealed class CompanySettings
{
    public int Id { get; set; } = 1;

    public string CompanyName { get; set; } = string.Empty;

    public string? SupportEmail { get; set; }

    public string? SupportPhone { get; set; }

    public string BusinessTimeZone { get; set; } =
        "Asia/Colombo";

    public DateTime? UpdatedAtUtc { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
