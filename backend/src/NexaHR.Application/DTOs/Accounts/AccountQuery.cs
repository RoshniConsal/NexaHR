using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.Accounts;

public sealed class AccountQuery
{
    [StringLength(200)]
    public string? Search { get; set; }

    [StringLength(30)]
    public string? Role { get; set; }

    public bool? IsActive { get; set; }

    [Range(1, 100000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
