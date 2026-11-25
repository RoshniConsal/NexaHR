
using Microsoft.AspNetCore.Identity;

namespace NexaHR.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? DeactivatedAtUtc { get; set; }

    public bool HasPrivilegedAccess { get; set; } = false;

    public bool HasPrivacyGovernanceAccess { get; set; } = false;
}
