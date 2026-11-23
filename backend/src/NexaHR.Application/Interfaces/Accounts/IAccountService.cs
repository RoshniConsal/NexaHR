using NexaHR.Application.DTOs.Accounts;

namespace NexaHR.Application.Interfaces.Accounts;

public interface IAccountService
{
    Task<AccountListResponse?> GetAccountsAsync(
        Guid actorId,
        AccountQuery query);

    Task<AccountResponse?> GetAccountAsync(
        Guid actorId,
        Guid accountId);

    Task<AccountOperationResponse> ChangeStatusAsync(
        Guid actorId,
        Guid accountId,
        ChangeAccountStatusRequest request);
}
