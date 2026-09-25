namespace DynaDocs.Services.Map;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DynaDocs.Serialization;

/// <summary>
/// One read-only GraphQL call to Linear, with Linear's failures classified into the map API's error
/// codes. Linear reports errors in `errors[]` even on HTTP 200, and rate limiting as HTTP 400 with
/// `extensions.code == "RATELIMITED"`, so the body is classified before the status.
/// </summary>
internal sealed class LinearGraphQL(HttpClient http, Uri endpoint, string apiKey)
{
    public async Task<JsonElement> QueryAsync(
        string query, Dictionary<string, string?> variables, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent(new GraphQLRequest(query, variables))
        };
        // A Linear personal key is sent bare, without the Bearer scheme.
        request.Headers.TryAddWithoutValidation("Authorization", apiKey);

        using var response = await SendAsync(request, ct);
        using var document = await ParseAsync(response, ct);
        var root = document?.RootElement;

        if (root?.TryGetProperty("errors", out var errors) == true && errors.GetArrayLength() > 0)
            throw Classify(errors);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw MapApiException.LinearRateLimited("Linear rate limit reached; try again later.");
        if (!response.IsSuccessStatusCode)
            throw MapApiException.LinearError($"Linear answered HTTP {(int)response.StatusCode}.");
        if (root?.TryGetProperty("data", out var data) != true || data.ValueKind != JsonValueKind.Object)
            throw MapApiException.LinearError("Linear answered without data.");

        return data.Clone();
    }

    private static ByteArrayContent JsonContent(GraphQLRequest body)
    {
        var content = new ByteArrayContent(
            JsonSerializer.SerializeToUtf8Bytes(body, MapJsonContext.Default.GraphQLRequest));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw MapApiException.LinearError($"Could not reach Linear: {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw MapApiException.LinearError("Linear did not answer in time.");
        }
    }

    private static async Task<JsonDocument?> ParseAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static MapApiException Classify(JsonElement errors)
    {
        var all = errors.EnumerateArray().ToList();
        var first = Message(all[0]);

        if (all.Any(error => Extension(error, "type") == "authentication error"))
            return MapApiException.LinearAuth($"Linear rejected LINEAR_API_KEY: {first}");
        if (all.Any(error => Extension(error, "code") == "RATELIMITED"))
            return MapApiException.LinearRateLimited($"Linear rate limit reached: {first}");
        if (first.StartsWith("Entity not found", StringComparison.Ordinal))
            return MapApiException.NotFound(first);
        return MapApiException.LinearError(first);
    }

    private static string Message(JsonElement error) =>
        error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String
            ? message.GetString()!
            : "Linear reported an error.";

    private static string? Extension(JsonElement error, string name) =>
        error.TryGetProperty("extensions", out var extensions)
        && extensions.ValueKind == JsonValueKind.Object
        && extensions.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
