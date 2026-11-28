namespace NexaHR.Application.DTOs.Employees;

public sealed record EmployeeDirectoryItem(
    Guid Id,
    string FullName,
    string Role,
    string Designation,
    string Department,
    bool IsActive);

public sealed record EmployeeDetailsResponse(
    Guid Id,
    Guid UserId,
    string EmployeeNumber,
    string FullName,
    Guid DepartmentId,
    string Department,
    string Designation,
    string EmploymentType,
    DateOnly JoiningDate,
    string? WorkEmail,
    DateTime? WorkEmailVerifiedAtUtc,
    bool IsActive,
    string RowVersion);

public sealed record EmployeeDirectoryResponse(
    IReadOnlyList<object> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record EmploymentHistoryItem(
    Guid Id,
    string ChangeType,
    string? PreviousValuesJson,
    string? NewValuesJson,
    string Reason,
    DateTime ChangedAtUtc);

public sealed record EmploymentHistoryResponse(
    IReadOnlyList<EmploymentHistoryItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record WorkEmailVerificationResponse(
    Guid VerificationId,
    DateTime ExpiresAtUtc);

public sealed record EmployeeOperationResult<T>(
    bool Success,
    string Code,
    string Message,
    T? Data = default);
