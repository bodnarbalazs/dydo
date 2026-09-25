namespace DynaDocs.Services.Map;

using System.Text;

/// <summary>
/// The viewer's built files. `DynaDocs.csproj` embeds `viewer/dist/**` under its repository-relative
/// path when `viewer/dist/index.html` existed at build time; without it, `/` serves a short page
/// saying the viewer was not built.
/// </summary>
internal sealed class ViewerBundle(Func<string, byte[]?> read)
{
    private const string Html = "text/html; charset=utf-8";

    private static readonly byte[] NotBuiltPage = Encoding.UTF8.GetBytes(
        "<!doctype html><title>dydo map</title><p>The dydo map viewer was not built into this dydo " +
        "binary. Build it with <code>pnpm -C viewer run build</code>, then rebuild dydo.</p>");

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = Html,
        [".js"] = "text/javascript; charset=utf-8",
        [".mjs"] = "text/javascript; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".map"] = "application/json; charset=utf-8",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".ico"] = "image/x-icon",
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        [".txt"] = "text/plain; charset=utf-8",
    };

    public static ViewerBundle Embedded { get; } = new(Resources("viewer/dist/"));

    public bool IsBuilt => read("index.html") != null;

    /// <summary>The file for a request path, `/` meaning `index.html`; null when there is none.</summary>
    public (string ContentType, byte[] Body)? Find(string urlPath)
    {
        var name = urlPath == "/" ? "index.html" : urlPath.TrimStart('/');
        if (read(name) is { } body)
            return (ContentType(name), body);
        return urlPath == "/" ? (Html, NotBuiltPage) : null;
    }

    internal static string ContentType(string name) =>
        ContentTypes.GetValueOrDefault(Path.GetExtension(name), "application/octet-stream");

    /// <summary>Reads this assembly's embedded resources named <paramref name="root"/> + path.</summary>
    internal static Func<string, byte[]?> Resources(string root) => name =>
    {
        using var stream = typeof(ViewerBundle).Assembly.GetManifestResourceStream(root + name);
        if (stream == null)
            return null;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    };
}
