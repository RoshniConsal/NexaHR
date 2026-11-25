namespace NexaHR.Domain.Constants;

public static class VacancyWorkModes
{
    public const string Onsite = "Onsite";
    public const string Hybrid = "Hybrid";
    public const string Remote = "Remote";

    public static bool IsValid(string value)
    {
        return value is Onsite or Hybrid or Remote;
    }
}
