using System.ComponentModel.DataAnnotations;

namespace NexaHR.Application.DTOs.CvDocuments;

public sealed class CvDocumentQuery
{
    public Guid? OwnerUserId { get; set; }

    [Range(1, 100000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
