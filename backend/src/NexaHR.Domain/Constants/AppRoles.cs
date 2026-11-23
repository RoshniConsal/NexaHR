namespace NexaHR.Domain.Constants;

public static class AppRoles
{
    public const string CEO = "CEO";
    public const string HRManager = "HRManager";
    public const string Candidate = "Candidate";
    public const string Employee = "Employee";

    public static IReadOnlyList<string> All { get; } =
        Array.AsReadOnly(new[]
        {
            CEO,
            HRManager,
            Candidate,
            Employee
        });
}
