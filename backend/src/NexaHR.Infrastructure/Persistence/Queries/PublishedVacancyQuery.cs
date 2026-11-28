using Microsoft.EntityFrameworkCore;
using NexaHR.Domain.Constants;
using NexaHR.Domain.Entities;
using NexaHR.Infrastructure.Persistence;

namespace NexaHR.Infrastructure.Persistence.Queries;

internal static class PublishedVacancyQuery
{
    internal sealed class PublicJobRecord
    {
        public Vacancy Vacancy { get; init; } = null!;

        public VacancyRevision Revision { get; init; } = null!;

        public Department Department { get; init; } = null!;
    }

    internal static IQueryable<PublicJobRecord> Query(NexaHRDbContext db)
    {
        var sriLankaTime = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById(
                "Asia/Colombo"));

        var today = DateOnly.FromDateTime(sriLankaTime);

        return
            from vacancy in db.Vacancies.AsNoTracking()
            join revision in db.VacancyRevisions.AsNoTracking()
                on vacancy.PublishedRevisionId equals (Guid?)revision.Id
            join department in db.Departments.AsNoTracking()
                on revision.DepartmentId equals department.Id
            where vacancy.Status == VacancyStatuses.Published
                && revision.VacancyId == vacancy.Id
                && vacancy.DepartmentId == revision.DepartmentId
                && vacancy.PublishedAtUtc.HasValue
                && vacancy.PublishedByUserId.HasValue
                && revision.SubmittedAtUtc.HasValue
                && revision.SubmittedByUserId.HasValue
                && revision.ClosingDate >= today
                && !department.IsArchived
                && !db.VacancyRevisions.Any(r =>
                    r.VacancyId == vacancy.Id &&
                    r.Revision > revision.Revision)
                && db.VacancyDecisions.Any(d =>
                    d.VacancyRevisionId == revision.Id &&
                    d.ApproverRole == AppRoles.CEO &&
                    d.Decision == VacancyDecisionTypes.Approved)
            select new PublicJobRecord
            {
                Vacancy = vacancy,
                Revision = revision,
                Department = department
            };
    }
}
