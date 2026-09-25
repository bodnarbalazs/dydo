namespace DynaDocs.Services.Map;

using System.Text.Json;
using DynaDocs.Serialization;

/// <summary>
/// A failure the map API reports to the viewer as its error envelope. The factories pin each code to
/// its HTTP status, as the plan's API contract fixes them.
/// </summary>
internal sealed class MapApiException(string code, int status, string message) : Exception(message)
{
    public string Code { get; } = code;

    public int Status { get; } = status;

    public static MapApiException MissingParameter(string name) =>
        new("missing_parameter", 400, $"Missing query parameter: {name}.");

    public static MapApiException NotFound(string message) => new("not_found", 404, message);

    public static MapApiException LinearAuth(string message) => new("linear_auth", 502, message);

    public static MapApiException LinearRateLimited(string message) => new("linear_rate_limited", 503, message);

    public static MapApiException LinearError(string message) => new("linear_error", 502, message);

    public byte[] Envelope() => JsonSerializer.SerializeToUtf8Bytes(
        new Dictionary<string, MapError> { ["error"] = new(Code, Message) },
        MapJsonContext.Default.DictionaryStringMapError);
}
