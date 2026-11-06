using Aeroway.Helpers;

namespace Aeroway.Services;

/// <summary>
/// Подменяет активную пару WinDivert в bin на пакет bol-van (служба Monkey)
/// или возвращает обычные WinDivert.dll и WinDivert64.sys.
/// </summary>
public static class WinDivertHideService
{
    public const string DllFileName = "WinDivert.dll";
    public const string StockDriverFileName = "WinDivert64.sys";
    public const string HideDriverFileName = "Monkey64.sys";
    public const string HideServiceName = "Monkey";

    public static Task ApplyAsync(ZapretPaths paths, bool hide, CancellationToken cancellationToken = default)
    {
        var sets = ResolveSetsDirectory(paths.Root)
                   ?? throw new InvalidOperationException(Loc.T("service.windivert_hide_missing_pack"));
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            ApplyCore(paths, hide, sets);
            if (NeedsApply(paths.Bin, sets, hide))
                throw new InvalidOperationException(Loc.T("service.windivert_hide_failed"));
        }, cancellationToken);
    }

    /// <summary>
    /// Если файлы в bin не совпадают с сохранённой настройкой, приводит их к ней.
    /// Совпадающий комплект не трогает и не останавливает обход.
    /// </summary>
    public static void TryReconcile(ZapretPaths paths, bool hide)
    {
        try
        {
            if (!paths.IsValid)
                return;

            var sets = ResolveSetsDirectory(paths.Root);
            if (!NeedsApply(paths.Bin, sets, hide))
                return;

            if (sets is null)
            {
                ConsoleLog.Instance.Write(Loc.T("service.windivert_hide_missing_pack"));
                return;
            }

            ApplyCore(paths, hide, sets);
        }
        catch (Exception ex)
        {
            ConsoleLog.Instance.Write(ex.Message);
        }
    }

    internal static void ApplyCore(ZapretPaths paths, bool hide, string setsDirectory)
    {
        if (!hide && CanDropHideDriverOnly(paths.Bin, setsDirectory))
        {
            SwapBinaries(paths.Bin, setsDirectory, hide: false);
            return;
        }

        ZapretShutdownService.PrepareForDriverFileSwap();
        SwapBinaries(paths.Bin, setsDirectory, hide);
    }

    internal static void SwapBinaries(string binDirectory, string setsDirectory, bool hide)
    {
        Directory.CreateDirectory(binDirectory);
        if (!hide && CanDropHideDriverOnly(binDirectory, setsDirectory))
        {
            DeleteIfExists(Path.Combine(binDirectory, HideDriverFileName));
            return;
        }

        var source = Path.Combine(setsDirectory, hide ? "hide" : "stock");
        if (hide)
        {
            CopyOverwrite(Path.Combine(source, HideDriverFileName), Path.Combine(binDirectory, HideDriverFileName));
            CopyOverwrite(Path.Combine(source, DllFileName), Path.Combine(binDirectory, DllFileName));
            DeleteIfExists(Path.Combine(binDirectory, StockDriverFileName));
            return;
        }

        CopyOverwrite(Path.Combine(source, StockDriverFileName), Path.Combine(binDirectory, StockDriverFileName));
        CopyOverwrite(Path.Combine(source, DllFileName), Path.Combine(binDirectory, DllFileName));
        DeleteIfExists(Path.Combine(binDirectory, HideDriverFileName));
    }

    /// <summary>
    /// Обычный режим не перезаписывает более новый stock-DLL из обновления Flowseal,
    /// если рядом лишь лишний Monkey64.sys. Выключение настоящего hide-комплекта
    /// по-прежнему восстанавливает пару stock целиком.
    /// </summary>
    internal static bool CanDropHideDriverOnly(string binDirectory, string setsDirectory)
    {
        var dll = Path.Combine(binDirectory, DllFileName);
        var stock = Path.Combine(binDirectory, StockDriverFileName);
        var hideDll = Path.Combine(setsDirectory, "hide", DllFileName);
        return File.Exists(dll)
               && File.Exists(stock)
               && File.Exists(hideDll)
               && !FileEquals(dll, hideDll);
    }

    internal static bool NeedsApply(string binDirectory, string? setsDirectory, bool hide)
    {
        var dllPath = Path.Combine(binDirectory, DllFileName);
        var stockSys = Path.Combine(binDirectory, StockDriverFileName);
        var hideSys = Path.Combine(binDirectory, HideDriverFileName);
        var hasDll = File.Exists(dllPath);
        var hasStock = File.Exists(stockSys);
        var hasHide = File.Exists(hideSys);

        if (hide)
        {
            if (!hasDll || !hasHide || hasStock)
                return true;
            if (setsDirectory is null)
                return false;
            var hideFolder = Path.Combine(setsDirectory, "hide");
            return !FileEquals(dllPath, Path.Combine(hideFolder, DllFileName))
                   || !FileEquals(hideSys, Path.Combine(hideFolder, HideDriverFileName));
        }

        if (!hasDll || !hasStock || hasHide)
            return true;
        if (setsDirectory is null)
            return false;

        var hideDll = Path.Combine(setsDirectory, "hide", DllFileName);
        return File.Exists(hideDll) && FileEquals(dllPath, hideDll);
    }

    internal static string? ResolveSetsDirectory(string zapretRoot)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in EnumerateCandidates(zapretRoot))
        {
            string full;
            try
            {
                full = Path.GetFullPath(candidate);
            }
            catch
            {
                continue;
            }

            if (!seen.Add(full))
                continue;
            if (IsCompleteSetsDirectory(full))
                return full;
        }

        return null;
    }

    internal static bool IsCompleteSetsDirectory(string directory) =>
        File.Exists(Path.Combine(directory, "stock", DllFileName))
        && File.Exists(Path.Combine(directory, "stock", StockDriverFileName))
        && File.Exists(Path.Combine(directory, "hide", DllFileName))
        && File.Exists(Path.Combine(directory, "hide", HideDriverFileName));

    private static IEnumerable<string> EnumerateCandidates(string zapretRoot)
    {
        var parent = Directory.GetParent(zapretRoot)?.FullName;
        if (parent is not null)
            yield return Path.Combine(parent, "windivert");

        var dir = AppContext.BaseDirectory.TrimEnd('\\', '/');
        for (var i = 0; i < 8; i++)
        {
            yield return Path.Combine(dir, "windivert");
            yield return Path.Combine(dir, "packaging", "windivert");
            var next = Directory.GetParent(dir)?.FullName;
            if (next is null)
                break;
            dir = next;
        }
    }

    internal static bool FileEquals(string left, string right)
    {
        var a = new FileInfo(left);
        var b = new FileInfo(right);
        if (!a.Exists || !b.Exists || a.Length != b.Length)
            return false;

        return File.ReadAllBytes(left).AsSpan().SequenceEqual(File.ReadAllBytes(right));
    }

    private static void CopyOverwrite(string source, string destination)
    {
        if (!File.Exists(source))
            throw new FileNotFoundException(Loc.T("service.windivert_hide_missing_pack"), source);

        for (var attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                File.Copy(source, destination, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 7)
            {
                Thread.Sleep(250);
            }
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (!File.Exists(path))
            return;

        for (var attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < 7)
            {
                Thread.Sleep(250);
            }
        }
    }
}
