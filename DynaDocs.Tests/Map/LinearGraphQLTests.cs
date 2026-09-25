namespace DynaDocs.Tests.Map;

using System.Net;
using DynaDocs.Services.Map;

public class LinearGraphQLTests
{
    private readonly FakeLinear _linear = new();

    private Task<System.Text.Json.JsonElement> Query() =>
        _linear.Transport().QueryAsync("query Teams { teams { nodes { id } } }", new() { ["after"] = null }, CancellationToken.None);

    [Fact]
    public async Task PostsJsonWithTheBareKey()
    {
        _linear.Serve(_ => """{"data":{"teams":{"nodes":[]}}}""");

        var data = await Query();

        var call = Assert.Single(_linear.Calls);
        Assert.Equal(HttpMethod.Post, call.Method);
        Assert.Equal("application/json", call.MediaType);
        Assert.Equal(FakeLinear.ApiKey, call.Authorization);
        Assert.Equal("Teams", call.Operation);
        Assert.True(call.Variables.ContainsKey("after"));
        Assert.Equal(0, data.GetProperty("teams").GetProperty("nodes").GetArrayLength());
    }

    [Theory]
    [InlineData(HttpStatusCode.OK,
        """{"errors":[{"message":"Field 'x' is unknown"}],"data":null}""",
        "linear_error", 502, "Field 'x' is unknown")]
    [InlineData(HttpStatusCode.OK,
        """{"errors":[{"message":"partial"}],"data":{"teams":null}}""",
        "linear_error", 502, "partial")]
    [InlineData(HttpStatusCode.BadRequest,
        """{"errors":[{"message":"Authentication required, not authenticated","extensions":{"type":"authentication error","userPresentableMessage":"You need to authenticate to access this operation."}}]}""",
        "linear_auth", 502, "Authentication required, not authenticated")]
    [InlineData(HttpStatusCode.BadRequest,
        """{"errors":[{"message":"Rate limit exceeded","extensions":{"code":"RATELIMITED"}}]}""",
        "linear_rate_limited", 503, "Rate limit exceeded")]
    [InlineData(HttpStatusCode.TooManyRequests, "slow down", "linear_rate_limited", 503, "rate limit")]
    [InlineData(HttpStatusCode.OK,
        """{"errors":[{"message":"Entity not found: Project","extensions":{"type":"invalid input"}}],"data":null}""",
        "not_found", 404, "Entity not found: Project")]
    [InlineData(HttpStatusCode.OK, """{"errors":[{"extensions":{"code":"X"}}]}""", "linear_error", 502, "Linear reported an error.")]
    [InlineData(HttpStatusCode.InternalServerError, "<html>oops</html>", "linear_error", 502, "HTTP 500")]
    [InlineData(HttpStatusCode.OK, """{"data":null}""", "linear_error", 502, "without data")]
    [InlineData(HttpStatusCode.OK, "not json", "linear_error", 502, "without data")]
    public async Task ClassifiesLinearFailures(HttpStatusCode status, string body, string code, int httpStatus, string message)
    {
        _linear.Answer = _ => (status, body);

        var error = await Assert.ThrowsAsync<MapApiException>(Query);

        Assert.Equal(code, error.Code);
        Assert.Equal(httpStatus, error.Status);
        Assert.Contains(message, error.Message);
        Assert.DoesNotContain(FakeLinear.ApiKey, error.Message);
    }

    [Fact]
    public async Task UnreachableLinear_IsLinearError()
    {
        _linear.Answer = _ => throw new HttpRequestException("connection refused");

        var error = await Assert.ThrowsAsync<MapApiException>(Query);

        Assert.Equal(("linear_error", 502), (error.Code, error.Status));
        Assert.Contains("connection refused", error.Message);
    }

    [Fact]
    public async Task SlowLinear_IsLinearError()
    {
        _linear.Answer = _ => throw new TaskCanceledException("timeout");

        var error = await Assert.ThrowsAsync<MapApiException>(Query);

        Assert.Equal(("linear_error", 502), (error.Code, error.Status));
        Assert.Contains("in time", error.Message);
    }
}
