namespace NexaHR.Domain.Entities;

public sealed class VacancyCriterion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid VacancyRevisionId { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public decimal Weight { get; set; }
}
