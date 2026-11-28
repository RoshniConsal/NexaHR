using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Applications;

public sealed class ApplicationStageTransitionRequest
{
    [Required]
    [StringLength(30)]
    public string TargetStage { get; init; }
        = string.Empty;

    [StringLength(2000)]
    public string? Reason { get; init; }

    [Required]
    [StringLength(12, MinimumLength = 12)]
    public string RowVersion { get; init; }
        = string.Empty;
}
