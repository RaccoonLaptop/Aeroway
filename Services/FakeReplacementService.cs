using System.IO;
using System.Security.Cryptography;
using Aeroway.Helpers;

namespace Aeroway.Services;

public sealed class FakeReplacementService
{
    private const string DiscordActive = "ACTIVE_DISCORD_UDP.bin";
    private const string GameActive = "ACTIVE_GAME_UDP.bin";

    private readonly ZapretPaths _paths;

    public FakeReplacementService(ZapretPaths paths) => _paths = paths;

    public FakeReplacementStatus GetStatus()
    {
        var binDir = Path.Combine(_paths.Root, "bin");
        if (!Directory.Exists(binDir))
            return new FakeReplacementStatus { Error = Loc.T("fake.bin_missing") };

        var files = Directory.GetFiles(binDir, "*.bin")
            .Select(Path.GetFileName)
            .Where(name => name is not null && !name.StartsWith("ACTIVE_", StringComparison.OrdinalIgnoreCase))
            .Cast<string>()
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var discordHash = GetFileHash(Path.Combine(binDir, DiscordActive));
        var gameHash = GetFileHash(Path.Combine(binDir, GameActive));

        return new FakeReplacementStatus
        {
            AvailableFiles = files,
            CurrentDiscordFake = ResolveName(files, binDir, discordHash),
            CurrentGameFake = ResolveName(files, binDir, gameHash)
        };
    }

    public void Replace(FakeTarget target, string sourceFileName)
    {
        var binDir = Path.Combine(_paths.Root, "bin");
        var sourcePath = Path.Combine(binDir, sourceFileName);
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException(Loc.T("fake.file_not_found"), sourceFileName);

        var activeName = target == FakeTarget.DiscordUdp ? DiscordActive : GameActive;
        var activePath = Path.Combine(binDir, activeName);
        File.Copy(sourcePath, activePath, true);
    }

    private static string? ResolveName(IReadOnlyList<string> files, string binDir, string? activeHash)
    {
        if (string.IsNullOrWhiteSpace(activeHash))
            return null;

        foreach (var file in files)
        {
            if (string.Equals(GetFileHash(Path.Combine(binDir, file)), activeHash, StringComparison.OrdinalIgnoreCase))
                return Path.GetFileNameWithoutExtension(file);
        }

        return null;
    }

    private static string? GetFileHash(string path)
    {
        if (!File.Exists(path))
            return null;

        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }
}

public enum FakeTarget
{
    DiscordUdp,
    GameFilterUdp
}

public sealed class FakeReplacementStatus
{
    public IReadOnlyList<string> AvailableFiles { get; init; } = [];
    public string? CurrentDiscordFake { get; init; }
    public string? CurrentGameFake { get; init; }
    public string? Error { get; init; }
}
