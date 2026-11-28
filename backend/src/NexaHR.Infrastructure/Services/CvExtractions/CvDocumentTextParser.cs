using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace NexaHR.Infrastructure.Services.CvExtractions;

public sealed record CvTextSegment(
    string Text,
    int? PageNumber,
    int? ParagraphNumber);

public sealed class CvDocumentTextParser
{
    public const string ParserVersion = "nexahr-text-v1";

    private const int MaxTextCharacters = 250_000;
    private const int MaxDocumentBytes = 5 * 1024 * 1024;

    public async Task<IReadOnlyList<CvTextSegment>> ParseAsync(
        Stream source,
        string contentType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!source.CanRead)
            throw new InvalidDataException("Invalid CV stream.");

        using var buffer = new MemoryStream();

        var chunk = new byte[81920];

        while (true)
        {
            var count = await source.ReadAsync(
                chunk.AsMemory(),
                cancellationToken);

            if (count == 0)
                break;

            if (buffer.Length + count > MaxDocumentBytes)
                throw new InvalidDataException(
                    "CV document exceeds parser limit.");

            await buffer.WriteAsync(
                chunk.AsMemory(0, count),
                cancellationToken);
        }

        buffer.Position = 0;

        IReadOnlyList<CvTextSegment> result =
            contentType switch
            {
                "application/pdf" =>
                    await ParsePdfAsync(
                        buffer, cancellationToken),

                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" =>
                    ParseDocx(buffer, cancellationToken),

                _ => throw new InvalidDataException(
                    "Unsupported CV document format.")
            };

        if (result.Count == 0)
            throw new InvalidDataException(
                "No readable CV text.");

        return result;
    }

    private static async Task<IReadOnlyList<CvTextSegment>>
        ParsePdfAsync(
            Stream source,
            CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo
        {
            FileName = "pdftotext",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        info.ArgumentList.Add("-enc");
        info.ArgumentList.Add("UTF-8");
        info.ArgumentList.Add("-layout");
        info.ArgumentList.Add("-");
        info.ArgumentList.Add("-");

        using var process = new Process
        {
            StartInfo = info
        };

        using var timeout =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        var token = timeout.Token;

        if (!process.Start())
            throw new InvalidOperationException(
                "CV parser unavailable.");

        try
        {
            var outputTask = ReadLimitedAsync(
                process.StandardOutput, token);

            var errorTask = process.StandardError.BaseStream
                .CopyToAsync(Stream.Null, token);

            var inputTask = WriteInputAsync(
                source, process, token);

            var exitTask = process.WaitForExitAsync(token);

            // Stop immediately if bounded output reading fails.
            var first = await Task.WhenAny(
                outputTask, inputTask, exitTask);

            await first;

            var output = await outputTask;

            await Task.WhenAll(
                inputTask, errorTask, exitTask);

            if (process.ExitCode != 0)
                throw new InvalidDataException(
                    "PDF extraction failed.");

            var segments = new List<CvTextSegment>();
            var pages = output.Split('\f');

            for (var index = 0;
                 index < pages.Length;
                 index++)
            {
                var text = pages[index].Trim();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    segments.Add(new CvTextSegment(
                        text,
                        index + 1,
                        null));
                }
            }

            return segments.AsReadOnly();
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    private static async Task WriteInputAsync(
        Stream source,
        Process process,
        CancellationToken cancellationToken)
    {
        try
        {
            await source.CopyToAsync(
                process.StandardInput.BaseStream,
                cancellationToken);
        }
        finally
        {
            process.StandardInput.Close();
        }
    }

    private static async Task<string> ReadLimitedAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        var chunk = new char[4096];

        while (true)
        {
            var count = await reader.ReadAsync(
                chunk.AsMemory(),
                cancellationToken);

            if (count == 0)
                break;

            if (output.Length + count > MaxTextCharacters)
                throw new InvalidDataException(
                    "Extracted text exceeds parser limit.");

            output.Append(chunk, 0, count);
        }

        return output.ToString();
    }

    private static IReadOnlyList<CvTextSegment> ParseDocx(
        Stream source,
        CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(
            source,
            ZipArchiveMode.Read,
            leaveOpen: true);

        var entry = archive.GetEntry("word/document.xml")
            ?? throw new InvalidDataException(
                "Invalid DOCX document.");

        if (entry.Length > MaxDocumentBytes)
            throw new InvalidDataException(
                "DOCX XML exceeds parser limit.");

        using var stream = entry.Open();

        using var reader = XmlReader.Create(
            stream,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxDocumentBytes
            });

        var document = XDocument.Load(reader);

        XNamespace word =
            "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        var segments = new List<CvTextSegment>();
        var paragraphNumber = 0;
        var totalCharacters = 0;

        foreach (var paragraph in
                 document.Descendants(word + "p"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            paragraphNumber++;

            var text = string.Concat(
                paragraph.Descendants(word + "t")
                    .Select(node => node.Value)).Trim();

            if (string.IsNullOrWhiteSpace(text))
                continue;

            totalCharacters += text.Length;

            if (totalCharacters > MaxTextCharacters)
                throw new InvalidDataException(
                    "Extracted text exceeds parser limit.");

            segments.Add(new CvTextSegment(
                text,
                null,
                paragraphNumber));
        }

        return segments.AsReadOnly();
    }
}
