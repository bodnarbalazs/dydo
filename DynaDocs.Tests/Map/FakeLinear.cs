namespace DynaDocs.Tests.Map;

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DynaDocs.Services.Map;

/// <summary>
/// A fake Linear GraphQL endpoint: it records every call and answers from <see cref="Answer"/>,
/// which tests set to hand-written GraphQL JSON per operation and cursor.
/// </summary>
internal sealed partial class FakeLinear : HttpMessageHandler
{
    public const string ApiKey = "lin_api_fake_key_for_tests";

    public List<LinearCall> Calls { get; } = [];

    public Func<LinearCall, (HttpStatusCode Status, string Body)> Answer { get; set; } =
        call => throw new InvalidOperationException($"No answer for {call.Operation}");

    public HttpClient Client => new(this);

    public LinearReader Reader() => new(Transport());

    public LinearGraphQL Transport() => new(Client, new Uri("http://fake-linear.test/graphql"), ApiKey);

    /// <summary>Answers each operation with a fixed body and HTTP 200.</summary>
    public void Serve(Func<LinearCall, string> body) => Answer = call => (HttpStatusCode.OK, body(call));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
        var query = body.RootElement.GetProperty("query").GetString()!;
        var variables = body.RootElement.GetProperty("variables").EnumerateObject()
            .ToDictionary(variable => variable.Name, variable => variable.Value.GetString());
        var call = new LinearCall(
            OperationName().Match(query).Groups[1].Value,
            query,
            variables,
            request.Headers.TryGetValues("Authorization", out var auth) ? auth.Single() : null,
            request.Method,
            request.Content.Headers.ContentType?.MediaType);
        Calls.Add(call);

        var (status, text) = Answer(call);
        return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
    }

    public int CallsTo(string operation) => Calls.Count(call => call.Operation == operation);

    [GeneratedRegex(@"query (\w+)")]
    private static partial Regex OperationName();
}
