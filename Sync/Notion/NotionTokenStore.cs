namespace DynaDocs.Sync.Notion;

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

/// <summary>
/// The local-only Notion token secret store (Decision 027 §3). Persists a single line at
/// <c>dydo/_system/.local/notion.secret</c> (gitignored). On Windows the token is DPAPI-protected under
/// the current-user key and stored as <c>dpapi:&lt;base64&gt;</c>; on other platforms it is stored as
/// <c>plain:&lt;token&gt;</c> with <c>0600</c> permissions — the honest cross-platform floor. No key ever
/// lives next to the ciphertext (DPAPI's key is OS-managed), so this store adds no self-rolled crypto and
/// no dependency. The token is never logged. Vault mode (committed ciphertext) is Slice B.
/// </summary>
public static class NotionTokenStore
{
    public const string LocalMode = "local";
    public const string VaultMode = "vault";
    public const string SecretFileName = "notion.secret";

    public const string VaultNotImplementedMessage =
        "notion: 'vault' token storage is not implemented yet (Slice B). Set notion.tokenStorage to \"local\".";

    private const string DpapiPrefix = "dpapi:";
    private const string PlainPrefix = "plain:";

    /// <summary>The secret file path for a given dydo root (e.g. <c>&lt;dydoRoot&gt;/_system/.local/notion.secret</c>).</summary>
    public static string PathFor(string dydoRoot) =>
        Path.Combine(dydoRoot, "_system", ".local", SecretFileName);

    public static bool Exists(string secretFilePath) => File.Exists(secretFilePath);

    public static void Delete(string secretFilePath)
    {
        if (File.Exists(secretFilePath))
            File.Delete(secretFilePath);
    }

    public static void Write(string secretFilePath, string token)
    {
        var dir = Path.GetDirectoryName(secretFilePath)!;
        Directory.CreateDirectory(dir);
        EnsureLocalGitignore(dir);

        if (OperatingSystem.IsWindows())
        {
            var line = DpapiPrefix + Convert.ToBase64String(Protect(Encoding.UTF8.GetBytes(token)));
            File.WriteAllText(secretFilePath, line);
            return;
        }

        // Non-Windows plaintext floor: create the file owner-only (0600) AT open() time via UnixCreateMode,
        // so the bearer token is never briefly world-readable in the window between write and chmod (TOCTOU).
        // The trailing SetUnixFileMode also tightens a pre-existing, loosely-permissioned file on overwrite.
        var options = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
        };
        using (var stream = new FileStream(secretFilePath, options))
        using (var writer = new StreamWriter(stream))
        {
            writer.Write(PlainPrefix + token);
        }

        File.SetUnixFileMode(secretFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    /// <summary>Belt-and-suspenders against Decision 027's permanent-public-repo-leak threat: drop a
    /// <c>.gitignore</c> containing <c>*</c> inside the secret's own directory, so the plaintext token can
    /// never be staged even if the project-root <c>.gitignore</c> is missing the <c>.local/</c> entry.</summary>
    private static void EnsureLocalGitignore(string dir)
    {
        var ignorePath = Path.Combine(dir, ".gitignore");
        if (!File.Exists(ignorePath))
            File.WriteAllText(ignorePath, "*\n");
    }

    /// <summary>
    /// Returns the stored token, or <c>null</c> if the file is missing, malformed, or — on Windows — was
    /// protected under a different user/machine (DPAPI unprotect fails → cannot decrypt → null, never throws).
    /// </summary>
    public static string? Read(string secretFilePath)
    {
        if (!File.Exists(secretFilePath))
            return null;

        var content = File.ReadAllText(secretFilePath).Trim();

        if (content.StartsWith(PlainPrefix, StringComparison.Ordinal))
            return content[PlainPrefix.Length..];

        if (content.StartsWith(DpapiPrefix, StringComparison.Ordinal))
        {
            if (!OperatingSystem.IsWindows())
                return null;

            byte[] cipher;
            try
            {
                cipher = Convert.FromBase64String(content[DpapiPrefix.Length..]);
            }
            catch (FormatException)
            {
                return null;
            }

            var plain = Unprotect(cipher);
            return plain == null ? null : Encoding.UTF8.GetString(plain);
        }

        return null;
    }

    [SupportedOSPlatform("windows")]
    private static byte[] Protect(byte[] data)
    {
        var input = new DataBlob();
        var output = new DataBlob();
        var pData = Marshal.AllocHGlobal(data.Length);
        try
        {
            Marshal.Copy(data, 0, pData, data.Length);
            input.cbData = data.Length;
            input.pbData = pData;

            if (!CryptProtectData(ref input, nint.Zero, nint.Zero, nint.Zero, nint.Zero, CryptprotectUiForbidden, out output))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            var result = new byte[output.cbData];
            Marshal.Copy(output.pbData, result, 0, output.cbData);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(pData);
            if (output.pbData != nint.Zero)
                LocalFree(output.pbData);
        }
    }

    [SupportedOSPlatform("windows")]
    private static byte[]? Unprotect(byte[] data)
    {
        var input = new DataBlob();
        var output = new DataBlob();
        var pData = Marshal.AllocHGlobal(data.Length);
        try
        {
            Marshal.Copy(data, 0, pData, data.Length);
            input.cbData = data.Length;
            input.pbData = pData;

            if (!CryptUnprotectData(ref input, nint.Zero, nint.Zero, nint.Zero, nint.Zero, CryptprotectUiForbidden, out output))
                return null;

            var result = new byte[output.cbData];
            Marshal.Copy(output.pbData, result, 0, output.cbData);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(pData);
            if (output.pbData != nint.Zero)
                LocalFree(output.pbData);
        }
    }

    private const int CryptprotectUiForbidden = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int cbData;
        public nint pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob pDataIn, nint szDataDescr, nint pOptionalEntropy,
        nint pvReserved, nint pPromptStruct, int dwFlags, out DataBlob pDataOut);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob pDataIn, nint ppszDataDescr, nint pOptionalEntropy,
        nint pvReserved, nint pPromptStruct, int dwFlags, out DataBlob pDataOut);

    [DllImport("kernel32.dll")]
    private static extern nint LocalFree(nint hMem);
}
