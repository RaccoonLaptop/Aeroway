namespace Aeroway.Tests;

internal static class TestPaths
{
    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Aeroway.csproj")))
            dir = dir.Parent;
        if (dir is null)
            throw new InvalidOperationException("Aeroway.csproj was not found above the test output.");
        return dir.FullName;
    }
}

internal sealed class TempTree : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "aeroway-tests-" + Guid.NewGuid().ToString("N"));

    public TempTree() => Directory.CreateDirectory(Root);

    public string Combine(params string[] parts)
    {
        var path = Root;
        foreach (var part in parts)
            path = Path.Combine(path, part);
        return path;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
        catch
        {
            /* a test may still be holding a file */
        }
    }
}
