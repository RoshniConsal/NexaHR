using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using NexaHR.Application.DTOs.CvDocuments;
using NexaHR.Application.Interfaces.CvDocuments;
using NexaHR.Domain.Constants;

namespace NexaHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/cv-documents")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
public sealed class CvDocumentsController : ControllerBase
{
    private const long MaxFileSize =
        5 * 1024 * 1024;

    private readonly ICvDocumentService _service;

    public CvDocumentsController(
        ICvDocumentService service)
    {
        _service = service;
    }

    // API-057: Upload a new candidate CV version.
    [HttpPost]
    [Authorize(Roles = AppRoles.Candidate)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxFileSize + 65536)]
    [RequestFormLimits(
        MultipartBodyLengthLimit = MaxFileSize)]
    public async Task<IActionResult> Upload(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorUserId))
            return Unauthorized();

        if (file is null || file.Length == 0)
        {
            return BadRequest(new
            {
                code = "invalid_file",
                message = "A CV file is required."
            });
        }

        if (file.Length > MaxFileSize)
        {
            return StatusCode(413, new
            {
                code = "file_too_large",
                message = "CV must not exceed 5 MB."
            });
        }

        await using var stream =
            file.OpenReadStream();

        var result = await _service.UploadAsync(
            actorUserId,
            file.FileName,
            stream,
            cancellationToken);

        if (!result.Success)
        {
            return result.Code switch
            {
                "forbidden" =>
                    StatusCode(403, result),

                "scanner_busy" =>
                    StatusCode(503, result),

                "cv_save_failed" or
                "cv_database_error" or
                "cv_storage_error" or
                "file_read_failed" =>
                    StatusCode(503, result),

                _ => BadRequest(result)
            };
        }

        return CreatedAtAction(
            nameof(GetDetails),
            new
            {
                documentId =
                    result.Document!.DocumentId
            },
            result);
    }

    // API-058: List accessible CV versions.
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] CvDocumentQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorUserId))
            return Unauthorized();

        var result = await _service.GetListAsync(
            actorUserId,
            query,
            cancellationToken);

        if (result is null)
            return Forbid();

        return Ok(result);
    }

    // API-059: Get private CV metadata.
    [HttpGet("{documentId:guid}")]
    public async Task<IActionResult> GetDetails(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorUserId))
            return Unauthorized();

        var result = await _service.GetDetailsAsync(
            actorUserId,
            documentId,
            cancellationToken);

        if (result is null)
        {
            return NotFound(new
            {
                code = "cv_not_found"
            });
        }

        return Ok(result);
    }

    // API-060: Authenticated CV preview/download.
    [HttpGet("{documentId:guid}/content")]
    public async Task<IActionResult> GetContent(
        Guid documentId,
        CancellationToken cancellationToken,
        [FromQuery] string disposition = "attachment")
    {
        if (!TryGetActorId(out var actorUserId))
            return Unauthorized();

        if (disposition != "inline" &&
            disposition != "attachment")
        {
            return BadRequest(new
            {
                code = "invalid_disposition",
                message =
                    "Use inline or attachment."
            });
        }

        var result = await _service.GetContentAsync(
            actorUserId,
            documentId,
            cancellationToken);

        if (!result.Success || result.Content is null)
        {
            return result.Code switch
            {
                "cv_access_blocked" =>
                    StatusCode(403, new
                    {
                        code = result.Code
                    }),

                "cv_integrity_failed" or
                "cv_content_unavailable" =>
                    StatusCode(503, new
                    {
                        code = "cv_content_unavailable"
                    }),

                _ => NotFound(new
                {
                    code = "cv_not_found"
                })
            };
        }

        // DOCX is always downloaded.
        // Only PDF supports inline browser preview.
        var actualDisposition =
            disposition == "inline" &&
            result.ContentType == "application/pdf"
                ? "inline"
                : "attachment";

        var contentDisposition =
            new ContentDispositionHeaderValue(
                actualDisposition);

        contentDisposition.SetHttpFileName(
            result.FileName);

        Response.Headers["Content-Disposition"] =
            contentDisposition.ToString();

        Response.Headers["X-Content-Type-Options"] =
            "nosniff";

        Response.Headers["Cache-Control"] =
            "private, no-store";

        Response.Headers["Content-Security-Policy"] =
            "sandbox; default-src 'none'";

        return File(
            result.Content,
            result.ContentType,
            enableRangeProcessing: false);
    }

    private bool TryGetActorId(out Guid actorUserId)
    {
        return Guid.TryParse(
            User.FindFirstValue(
                ClaimTypes.NameIdentifier),
            out actorUserId)
            && actorUserId != Guid.Empty;
    }
}
