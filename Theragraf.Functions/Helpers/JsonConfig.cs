namespace Theragraf.Functions.Helpers;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Shared JSON serializer options for all HTTP entry-point functions.
/// Combines the Web defaults (camelCase property names, case-insensitive reads)
/// with string-based enum serialization using the exact member name (PascalCase),
/// matching the TypeScript frontend's convention.
/// </summary>
public static class JsonConfig
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web)
    {
        // Serialize enums as PascalCase member names ("OccupationalTherapy").
        // Deserializes case-insensitively via JsonSerializerDefaults.Web.
        Converters = { new JsonStringEnumConverter() }
    };
}
