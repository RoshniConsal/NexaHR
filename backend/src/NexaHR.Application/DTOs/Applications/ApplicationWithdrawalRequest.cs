using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NexaHR.Application.DTOs.Applications;

[JsonUnmappedMemberHandling(
    JsonUnmappedMemberHandling.Disallow)]
public sealed class ApplicationWithdrawalRequest
{
    [Required]
    [StringLength(2000)]
    public string Reason { get; init; }
        = string.Empty;

    [Required]
    [StringLength(12, MinimumLength = 12)]
    public string RowVersion { get; init; }
        = string.Empty;
}
