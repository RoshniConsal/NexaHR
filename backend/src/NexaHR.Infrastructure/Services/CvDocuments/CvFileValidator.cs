using System.IO.Compression;
using System.Text;
using NexaHR.Application.Interfaces.CvDocuments;

namespace NexaHR.Infrastructure.Services.CvDocuments;

public sealed class CvFileValidator : ICvFileValidator
{
    private const long MaxFileSize =
        5 * 1024 * 1024;

    private const long MaxExpandedSize =
        50 * 1024 * 1024;

    private const int MaxZipEntries = 1000;

    private const string PdfContentType =
        "application/pdf";

    private const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public CvFileValidationResult Validate(
        string filePath,
        string originalFileName)
    {
        if (string.IsNullOrWhiteSpace(originalFileName) ||
            originalFileName.Length > 255 ||
            originalFileName.Any(char.IsControl) ||
            originalFileName.Contains('/') ||
            originalFileName.Contains('\\') ||
            originalFileName.Contains(':'))
        {
            return Invalid("invalid_file_name");
        }

        var safeFileName = originalFileName.Trim();

        var extension = Path.GetExtension(
            safeFileName).ToLowerInvariant();

        if (extension != ".pdf" &&
            extension != ".docx")
        {
            return Invalid("unsupported_file_type");
        }

        try
        {
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            if (stream.Length == 0)
            {
                return Invalid("empty_file");
            }

            if (stream.Length > MaxFileSize)
            {
                return Invalid("file_too_large");
            }

            var valid = extension == ".pdf"
                ? IsPdf(stream)
                : IsDocx(stream);

            if (!valid)
            {
                return Invalid("invalid_file_content");
            }

            return new CvFileValidationResult
            {
                IsValid = true,
                Code = "valid_file",
                SafeFileName = safeFileName,
                ContentType = extension == ".pdf"
                    ? PdfContentType
                    : DocxContentType
            };
        }
        catch (InvalidDataException)
        {
            return Invalid("invalid_file_content");
        }
        catch (IOException)
        {
            return Invalid("file_read_failed");
        }
        catch (UnauthorizedAccessException)
        {
            return Invalid("file_read_failed");
        }
    }

    private static bool IsPdf(Stream stream)
    {
        if (stream.Length < 12)
            return false;

        Span<byte> header = stackalloc byte[5];

        stream.ReadExactly(header);

        if (!header.SequenceEqual("%PDF-"u8))
            return false;

        var tailLength = (int)Math.Min(
            stream.Length, 2048);

        stream.Seek(
            -tailLength,
            SeekOrigin.End);

        var tail = new byte[tailLength];

        stream.ReadExactly(tail);

        return Encoding.ASCII
            .GetString(tail)
            .TrimEnd(' ', '\r', '\n', '\t')
            .EndsWith(
                "%%EOF",
                StringComparison.Ordinal);
    }

    private static bool IsDocx(Stream stream)
    {
        if (stream.Length < 4)
            return false;

        Span<byte> header = stackalloc byte[4];

        stream.ReadExactly(header);

        if (header[0] != 0x50 ||
            header[1] != 0x4B ||
            header[2] != 0x03 ||
            header[3] != 0x04)
        {
            return false;
        }

        stream.Position = 0;

        using var archive = new ZipArchive(
            stream,
            ZipArchiveMode.Read,
            leaveOpen: true);

        if (archive.Entries.Count == 0 ||
            archive.Entries.Count > MaxZipEntries)
        {
            return false;
        }

        long expandedSize = 0;

        var names = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName;

            if (string.IsNullOrWhiteSpace(name) ||
                name.StartsWith('/') ||
                name.Contains('\\') ||
                name.Contains(':') ||
                name.Split('/').Any(
                    p => p == ".." || p == ".") ||
                !names.Add(name))
            {
                return false;
            }

            if (name.Contains(
                    "vbaProject",
                    StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(
                    "word/activeX/",
                    StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(
                    "word/embeddings/",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (entry.Length >
                MaxExpandedSize - expandedSize)
            {
                return false;
            }

            expandedSize += entry.Length;
        }

        var contentTypes = archive.GetEntry(
            "[Content_Types].xml");

        var relationships = archive.GetEntry(
            "_rels/.rels");

        var document = archive.GetEntry(
            "word/document.xml");

        if (contentTypes is null ||
            relationships is null ||
            document is null ||
            contentTypes.Length == 0 ||
            relationships.Length == 0 ||
            document.Length == 0 ||
            contentTypes.Length > 1024 * 1024)
        {
            return false;
        }

        using var reader = System.Xml.XmlReader.Create(
            contentTypes.Open(),
            new System.Xml.XmlReaderSettings
            {
                DtdProcessing =
                    System.Xml.DtdProcessing.Prohibit,

                XmlResolver = null,

                MaxCharactersInDocument =
                    1024 * 1024
            });

        var hasWordDocument = false;

        try
        {
            while (reader.Read())
            {
                if (reader.NodeType !=
                    System.Xml.XmlNodeType.Element)
                {
                    continue;
                }

                var contentType =
                    reader.GetAttribute("ContentType");

                if (contentType is not null &&
                    (contentType.Contains(
                        "macroEnabled",
                        StringComparison.OrdinalIgnoreCase) ||
                     contentType.Contains(
                        "vbaProject",
                        StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                if (reader.LocalName == "Override" &&
                    reader.NamespaceURI ==
                        "http://schemas.openxmlformats.org/package/2006/content-types" &&
                    reader.GetAttribute("PartName") ==
                        "/word/document.xml" &&
                    contentType ==
                        "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml")
                {
                    hasWordDocument = true;
                }
            }
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }

        return hasWordDocument;
    }

    private static CvFileValidationResult Invalid(
        string code)
    {
        return new CvFileValidationResult
        {
            IsValid = false,
            Code = code
        };
    }
}
