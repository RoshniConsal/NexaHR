namespace NexaHR.Domain.Entities;

public sealed class ConsumedRefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AuthSessionId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ConsumedAtUtc { get; set; } = DateTime.UtcNow;
}
