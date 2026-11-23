namespace NexaHR.Application.DTOs.Accounts;

public sealed class AccountListResponse
{
    public IReadOnlyList<AccountResponse> Items { get; init; }
        = [];

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }
}
