namespace NexaHR.Domain.Constants;

public static class VacancyEmploymentTypes
{
    public const string FullTime = "FullTime";
    public const string PartTime = "PartTime";
    public const string Contract = "Contract";
    public const string Internship = "Internship";
    public const string Temporary = "Temporary";

    public static bool IsValid(string value)
    {
        return value is FullTime or PartTime
            or Contract or Internship or Temporary;
    }
}
