using NexaHR.Application.DTOs.Employees;

namespace NexaHR.Application.Interfaces.Employees;

public interface IEmployeeService
{
    // API-051: Employee directory
    Task<EmployeeOperationResult<EmployeeDirectoryResponse>>
        GetEmployeesAsync(
            Guid actorId,
            EmployeeDirectoryQuery query,
            CancellationToken cancellationToken = default);

    // API-052: Employee details
    Task<EmployeeOperationResult<object>>
        GetEmployeeAsync(
            Guid actorId,
            Guid employeeId,
            CancellationToken cancellationToken = default);

    // API-053: Update employee
    Task<EmployeeOperationResult<EmployeeDetailsResponse>>
        UpdateEmployeeAsync(
            Guid actorId,
            Guid employeeId,
            UpdateEmployeeRequest request,
            CancellationToken cancellationToken = default);

    // API-054: Request work email verification
    Task<EmployeeOperationResult<WorkEmailVerificationResponse>>
        RequestWorkEmailVerificationAsync(
            Guid actorId,
            Guid employeeId,
            RequestWorkEmailVerification request,
            CancellationToken cancellationToken = default);

    // API-055: Confirm work email verification
    Task<EmployeeOperationResult<bool>>
        ConfirmWorkEmailVerificationAsync(
            ConfirmWorkEmailVerification request,
            CancellationToken cancellationToken = default);

    // API-056: Employment history
    Task<EmployeeOperationResult<EmploymentHistoryResponse>>
        GetEmploymentHistoryAsync(
            Guid actorId,
            Guid employeeId,
            EmploymentHistoryQuery query,
            CancellationToken cancellationToken = default);
}
