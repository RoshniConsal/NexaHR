using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Policies;

public sealed class PolicyListQuery
{
    [StringLength(200)]
    public string? Search { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public class CreatePolicyRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(50000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;

    public bool RequiresAcknowledgement { get; set; } = true;

    public bool VisibleToEmployees { get; set; } = true;

    public bool VisibleToHr { get; set; } = true;

    public bool VisibleToCeo { get; set; }
}

public sealed class UpdatePolicyDraftRequest : CreatePolicyRequest
{
    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class PublishPolicyRequest
{
    [Required]
    public string RowVersion { get; set; } = string.Empty;
}
