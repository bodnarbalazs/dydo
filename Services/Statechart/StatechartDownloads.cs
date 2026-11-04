namespace DynaDocs.Services.Statechart;

using System.Security.Cryptography;
using System.Text.Json;

internal static class StatechartDownloads
{
    public static StatechartDownload[] Select(StatechartAssets assets, string rid)
    {
        using var json = JsonDocument.Parse(assets.DependencyLock());
        var root = json.RootElement;
        var platform = root.GetProperty("platforms").EnumerateArray().SingleOrDefault(row => row.GetProperty("rid").GetString() == rid);
        if (platform.ValueKind == JsonValueKind.Undefined) throw new IOException("Unsupported statechart runtime: " + rid);
        return [Sha("node", platform.GetProperty("node"), 128), Sha("java", platform.GetProperty("java"), 96),
            Sha("tlc", root.GetProperty("tlc"), 8), new StatechartDownload("pele", root.GetProperty("pele").GetProperty("url").GetString()!,
                root.GetProperty("pele").GetProperty("integrity").GetString()!, 8 * 1024 * 1024)];
    }

    public static async Task FetchAsync(StatechartDownload item, string target, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(120));
        using var handler = new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(15) };
        using var client = new HttpClient(handler);
        using var response = await client.GetAsync(item.Url, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > item.Limit) throw new IOException("Locked " + item.Name + " exceeds its compressed bound.");
        await using var source = await response.Content.ReadAsStreamAsync(deadline.Token);
        await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await CopyBoundedAsync(source, output, item.Limit, deadline.Token);
        await output.FlushAsync(deadline.Token);
    }

    public static void Verify(StatechartDownload item, string path)
    {
        if (new FileInfo(path).Length > item.Limit) throw new IOException("Oversized statechart dependency: " + item.Name);
        using var stream = File.OpenRead(path);
        var actual = item.Integrity.StartsWith("sha512-", StringComparison.Ordinal)
            ? "sha512-" + Convert.ToBase64String(SHA512.HashData(stream))
            : Convert.ToHexStringLower(SHA256.HashData(stream));
        if (actual != item.Integrity) throw new IOException("Locked statechart dependency integrity mismatch: " + item.Name);
    }

    internal static async Task CopyBoundedAsync(Stream source, Stream target, long limit, CancellationToken token)
    {
        var buffer = new byte[65536];
        long total = 0;
        int size;
        while ((size = await source.ReadAsync(buffer, token)) != 0)
        {
            total += size;
            if (total > limit) throw new IOException("Statechart download exceeds its configured byte bound.");
            await target.WriteAsync(buffer.AsMemory(0, size), token);
        }
    }

    private static StatechartDownload Sha(string name, JsonElement row, int mebibytes) =>
        new(name, row.GetProperty("url").GetString()!, row.GetProperty("sha256").GetString()!, mebibytes * 1024 * 1024);
}
