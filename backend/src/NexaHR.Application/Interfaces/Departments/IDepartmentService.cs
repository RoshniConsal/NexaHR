using NexaHR.Application.DTOs.Departments;

namespace NexaHR.Application.Interfaces.Departments;

public interface IDepartmentService
{
    Task<DepartmentListResponse?> GetDepartmentsAsync(
        Guid actorId,
        DepartmentQuery query);

    Task<DepartmentOperationResponse> CreateAsync(
        Guid actorId,
        CreateDepartmentRequest request);

    Task<DepartmentOperationResponse> UpdateAsync(
        Guid actorId,
        Guid departmentId,
        UpdateDepartmentRequest request);

    Task<DepartmentOperationResponse> ArchiveAsync(
        Guid actorId,
        Guid departmentId,
        ArchiveDepartmentRequest request);
}
