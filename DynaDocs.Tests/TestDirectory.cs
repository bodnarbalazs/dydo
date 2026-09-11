namespace DynaDocs.Tests;

internal static class TestDirectory
{
    public static void Delete(string path)
    {
        if (!Directory.Exists(path))
            return;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Directory.Delete(path, true);
                return;
            }
            catch (IOException) when (attempt < 2)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
        }
    }
}
