using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NexaHR.Application.DTOs.CvReviews;

namespace NexaHR.Application.Validation.CvReviews;

// Shared JSON syntax and structure validation.
// Semantic field validation is performed separately.
public static class CvReviewJsonReader
{
    public const int MaxJsonBytes = 256 * 1024;

    public const int MaxJsonDepth = 16;

    public const int SupportedSchemaVersion = 1;

    private static readonly UTF8Encoding StrictUtf8 =
        new(false, true);

    private static readonly JsonSerializerOptions Options =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,

            PropertyNameCaseInsensitive = false,

            UnmappedMemberHandling =
                JsonUnmappedMemberHandling.Disallow,

            NumberHandling = JsonNumberHandling.Strict,

            AllowTrailingCommas = false,

            ReadCommentHandling = JsonCommentHandling.Disallow,

            MaxDepth = MaxJsonDepth
        };

    public static CvStructuredValues ReadValues(string json)
    {
        return ReadCore<CvStructuredValues>(
            json,
            JsonValueKind.Object,
            root =>
            {
                if (!root.TryGetProperty(
                        "schemaVersion",
                        out var version) ||
                    version.ValueKind != JsonValueKind.Number ||
                    !version.TryGetInt32(out var number) ||
                    number != SupportedSchemaVersion)
                {
                    throw new JsonException(
                        "Unsupported CV schema version.");
                }
            });
    }

    public static IReadOnlyList<CvFieldEvidence>
        ReadEvidence(string json)
    {
        return ReadCore<List<CvFieldEvidence>>(
            json,
            JsonValueKind.Array);
    }

    public static CvDuplicateResolutionValues
        ReadDuplicateResolution(string json)
    {
        return ReadCore<CvDuplicateResolutionValues>(
            json,
            JsonValueKind.Object);
    }

    public static IReadOnlyList<string>
        ReadChangedFields(string json)
    {
        return ReadCore<List<string>>(
            json,
            JsonValueKind.Array);
    }

    private static T ReadCore<T>(
        string json,
        JsonValueKind expectedRoot,
        Action<JsonElement>? validateRoot = null)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new JsonException(
                "CV JSON payload is empty.");
        }

        // Every valid UTF-8 character requires at least
        // as many bytes as its UTF-16 code-unit count.
        if (json.Length > MaxJsonBytes)
        {
            throw new JsonException(
                "CV JSON payload exceeds the allowed size.");
        }

        try
        {
            if (StrictUtf8.GetByteCount(json) > MaxJsonBytes)
            {
                throw new JsonException(
                    "CV JSON payload exceeds the allowed size.");
            }

            using var document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,

                    CommentHandling =
                        JsonCommentHandling.Disallow,

                    MaxDepth = MaxJsonDepth
                });

            var root = document.RootElement;

            if (root.ValueKind != expectedRoot)
            {
                throw new JsonException(
                    "Unexpected CV JSON root type.");
            }

            EnsureUniqueProperties(root);

            validateRoot?.Invoke(root);

            var result = root.Deserialize<T>(Options);

            if (result is null)
            {
                throw new JsonException(
                    "CV JSON payload cannot be null.");
            }

            return result;
        }
        catch (EncoderFallbackException)
        {
            throw new JsonException(
                "CV JSON contains invalid Unicode.");
        }
        catch (InvalidOperationException)
        {
            // Malformed escaped Unicode can fail during
            // property-name or string-value decoding.
            throw new JsonException(
                "CV JSON payload is invalid.");
        }
        catch (JsonException)
        {
            // Do not propagate raw JSON, unknown field names,
            // candidate details or parser diagnostics.
            throw new JsonException(
                "CV JSON payload is invalid.");
        }
    }

    private static void EnsureUniqueProperties(
        JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var names = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (var property in
                    element.EnumerateObject())
                {
                    var name = property.Name;

                    StrictUtf8.GetByteCount(name);

                    if (!names.Add(name))
                    {
                        throw new JsonException(
                            "Duplicate CV JSON property.");
                    }

                    EnsureUniqueProperties(property.Value);
                }

                break;
            }

            case JsonValueKind.String:
            {
                // Decode all strings before deserialization
                // and verify Unicode scalar validity.
                var value = element.GetString();

                if (value is not null)
                {
                    StrictUtf8.GetByteCount(value);
                }

                break;
            }

            case JsonValueKind.Array:
            {
                foreach (var item in element.EnumerateArray())
                {
                    EnsureUniqueProperties(item);
                }

                break;
            }
        }
    }
}
