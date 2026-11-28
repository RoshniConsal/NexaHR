using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Applications;

public sealed class ApplicationAiConsentRequest
{
    [Required]
    [StringLength(200)]
    public string Provider { get; init; }
        = string.Empty;

    [Required]
    public Guid? ProcessingPolicyId { get; init; }

    [Required]
    [StringLength(100)]
    public string TextVersion { get; init; }
        = string.Empty;

    [Required]
    public bool? Granted { get; init; }

    [Required]
    [StringLength(12, MinimumLength = 12)]
    public string RowVersion { get; init; }
        = string.Empty;
}
