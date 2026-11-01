namespace Aeroway.Services;

public static class AppVersionCompare
{
    public static bool IsNewer(string remote, string local)
    {
        if (Version.TryParse(Normalize(remote), out var remoteVersion) &&
            Version.TryParse(Normalize(local), out var localVersion))
            return remoteVersion > localVersion;

        return !string.Equals(remote, local, StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string version)
    {
        var parts = version.Trim().Split('.');
        var list = parts.ToList();
        while (list.Count < 3)
            list.Add("0");
        return string.Join('.', list.Take(3));
    }
}
