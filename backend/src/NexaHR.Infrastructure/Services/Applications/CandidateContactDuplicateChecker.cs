using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NexaHR.Domain.Constants;
using NexaHR.Infrastructure.Identity;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Services.Applications;

// Internal lookup result.
// Never expose matching candidate identities or contacts.
internal readonly record struct CandidateContactDuplicateResult(
    bool EmailMatch,
    bool PhoneMatch);

internal static class CandidateContactDuplicateChecker
{
    // Produces fresh contact flags.
    // Caller must authorize access and own the transaction.
    internal static async Task<CandidateContactDuplicateResult>
        CheckAsync(
            NexaHRDbContext db,
            UserManager<ApplicationUser> users,
            Guid candidateUserId,
            string? email,
            string? phoneNumber,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(users);

        cancellationToken.ThrowIfCancellationRequested();

        if (candidateUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid candidate ID is required.",
                nameof(candidateUserId));
        }

        // Keep the fresh check within the same transaction
        // as the protected review operation.
        var transaction = db.Database.CurrentTransaction;

        if (transaction is null ||
            transaction.GetDbTransaction().IsolationLevel !=
                IsolationLevel.Serializable)
        {
            throw new InvalidOperationException(
                "A serializable transaction is required.");
        }

        var normalizedEmail =
            string.IsNullOrWhiteSpace(email)
                ? null
                : users.NormalizeEmail(email.Trim());

        var normalizedPhone =
            ContactNormalizer.NormalizePhone(phoneNumber);

        // Invalid non-null phone input must not silently
        // become a successful no-duplicate result.
        if (phoneNumber is not null &&
            normalizedPhone is null)
        {
            throw new ArgumentException(
                "The phone number cannot be normalized.",
                nameof(phoneNumber));
        }

        if (email is not null &&
            string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new ArgumentException(
                "The email address cannot be normalized.",
                nameof(email));
        }

        var emailMatch = false;
        var phoneMatch = false;

        if (normalizedEmail is null &&
            normalizedPhone is null)
        {
            return new CandidateContactDuplicateResult(
                false,
                false);
        }

        // Preserve the existing application-submission
        // scope: other Candidate-role accounts.
        var contacts =
            from other in db.Users.AsNoTracking()
            join membership in db.UserRoles
                on other.Id equals membership.UserId
            join role in db.Roles
                on membership.RoleId equals role.Id
            where other.Id != candidateUserId &&
                  role.Name == AppRoles.Candidate
            select new
            {
                other.NormalizedEmail,
                other.PhoneNumber
            };

        // Stream only the required contact fields.
        // Do not materialize complete identity records.
        await foreach (
            var contact in contacts
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!emailMatch)
            {
                emailMatch = MatchesEmail(
                    normalizedEmail,
                    contact.NormalizedEmail);
            }

            if (!phoneMatch)
            {
                phoneMatch = MatchesPhone(
                    normalizedPhone,
                    contact.PhoneNumber);
            }

            if ((normalizedEmail is null || emailMatch) &&
                (normalizedPhone is null || phoneMatch))
            {
                break;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        return new CandidateContactDuplicateResult(
            emailMatch,
            phoneMatch);
    }

    internal static bool MatchesEmail(
        string? normalizedEmail,
        string? otherNormalizedEmail)
    {
        return !string.IsNullOrWhiteSpace(normalizedEmail) &&
            string.Equals(
                normalizedEmail,
                otherNormalizedEmail,
                StringComparison.Ordinal);
    }

    internal static bool MatchesPhone(
        string? normalizedPhone,
        string? otherPhone)
    {
        return normalizedPhone is not null &&
            string.Equals(
                normalizedPhone,
                ContactNormalizer.NormalizePhone(otherPhone),
                StringComparison.Ordinal);
    }
}
