using Aeroway.Services;

namespace Aeroway.Helpers;

public static class Loc
{
    public static string T(string key) => LocalizationService.T(key);
    public static string F(string key, params object[] args) => LocalizationService.F(key, args);
}
