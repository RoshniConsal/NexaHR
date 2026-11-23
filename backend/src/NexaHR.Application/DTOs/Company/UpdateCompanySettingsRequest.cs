using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Company;

public sealed class UpdateCompanySettingsRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string CompanyName { get; set; } =
        string.Empty;

    [EmailAddress]
    [StringLength(256)]
    public string? SupportEmail { get; set; }

    [Phone]
    [StringLength(30)]
    public string? SupportPhone { get; set; }

    [StringLength(24)]
    public string? RowVersion { get; set; }
}
