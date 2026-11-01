using Aeroway.Services;

namespace Aeroway.Helpers;

/// <summary>
/// Translates test zapret.ps1 console output when UI language is Russian.
/// Parsing still uses English patterns from the script.
/// </summary>
internal static class TestOutputLocalizer
{
    private static readonly (string From, string To)[] LineReplacements =
    [
        ("ZAPRET CONFIG TESTS", "ТЕСТЫ КОНФИГОВ ZAPRET"),
        ("=== ANALYTICS ===", "=== АНАЛИТИКА ==="),
        ("All tests finished.", "Все тесты завершены."),
        ("Best config:", "Лучший конфиг:"),
        ("Best strategy:", "Лучшая стратегия:"),
        ("Results saved to", "Результаты сохранены в"),
        ("Could not read arguments of previous winws instance", "Не удалось прочитать аргументы предыдущего процесса winws"),
        ("Failed to restore previous winws instance", "Не удалось восстановить предыдущий процесс winws"),
        ("skipping restore.", "пропуск восстановления."),
        ("Press any key to close...", "Нажмите любую клавишу для выхода..."),
        ("Press any key to exit...", "Нажмите любую клавишу для выхода..."),
        ("Select test type:", "Выберите тип теста:"),
        ("Select test run mode:", "Выберите режим запуска:"),
        ("  [2] Selected configs", "  [2] Выбранные конфиги"),
        ("  [1] All configs", "  [1] Все конфиги"),
        ("Selected configs", "Выбранные конфиги"),
        ("Enter 1 or 2", "Введите 1 или 2"),
        ("Enter numbers (e.g. 1,3,5) , ranges (e.g. 2-7), or mixed (e.g. 1,5-10,12). '0' for all",
            "Введите номера (напр. 1,3,5), диапазоны (напр. 2-7) или комбинации (напр. 1,5-10,12). «0» — все"),
        ("Standard tests (HTTP/ping)", "Стандартные тесты (HTTP/пинг)"),
        ("DPI checkers (TCP 16-20 freeze)", "Проверка DPI (заморозка TCP 16–20)"),
        ("All configs", "Все конфиги"),
        ("Selected configs:", "Выбранные конфиги:"),
        ("Available configs:", "Доступные конфиги:"),
        ("> Starting config...", "> Запуск конфигурации..."),
        ("> Running tests...", "> Выполнение тестов..."),
        ("> Running DPI checkers...", "> Проверка DPI..."),
        ("Total configs:", "Всего конфигов:"),
        ("Mode: STANDARD", "Режим: СТАНДАРТ"),
        ("Mode: DPI", "Режим: DPI"),
        ("Tests may take several minutes to complete. Please wait...", "Тесты могут занять несколько минут. Подождите..."),
        ("Administrator rights detected", "Права администратора подтверждены"),
        ("Run as Administrator to execute tests", "Запустите от имени администратора"),
        ("curl.exe not found", "curl.exe не найден"),
        ("Install curl or add it to PATH", "Установите curl или добавьте в PATH"),
        ("curl.exe found", "curl.exe найден"),
        ("No general*.bat files found", "Файлы general*.bat не найдены"),
        ("Fix the errors above and rerun.", "Исправьте ошибки выше и запустите снова."),
        ("Incorrect input. Please try again.", "Неверный ввод. Попробуйте снова."),
        ("Invalid input format. Use numbers, ranges (1-5), or combinations (1,3-7,10). Try again.",
            "Неверный формат. Используйте номера, диапазоны (1-5) или комбинации (1,3-7,10)."),
        ("No valid configs selected. Try again.", "Конфиги не выбраны. Попробуйте снова."),
        ("Some entries were skipped due to errors (see warnings above).",
            "Часть пунктов пропущена из-за ошибок (см. предупреждения выше)."),
        ("Loaded targets from targets.txt", "Цели загружены из targets.txt"),
        ("Targets loaded:", "Загружено целей:"),
        ("targets.txt missing or empty. Using defaults.", "targets.txt отсутствует или пуст. Используются значения по умолчанию."),
        ("Restoring original ipset mode...", "Восстановление исходного режима ipset..."),
        ("Restoring previously running winws instances...", "Восстановление ранее запущенных winws..."),
        ("Script interrupted. Restoring ipset...", "Скрипт прерван. Восстановление ipset..."),
        ("An error occurred during tests. Restoring ipset...", "Ошибка во время тестов. Восстановление ipset..."),
        ("Detected leftover ipset switch flag. Restoring ipset...", "Обнаружен флаг ipset. Восстановление ipset..."),
        ("Current ipset status:", "Текущий статус ipset:"),
        ("Ipset will be switched to 'any' for accurate DPI tests.", "Ipset будет переключён в «any» для точных DPI-тестов."),
        ("If you close the window with the X button, ipset will NOT restore immediately.",
            "При закрытии окна крестиком ipset не восстановится сразу."),
        ("It will be restored automatically on the next script run.",
            "Восстановление произойдёт при следующем запуске скрипта."),
        ("Windows service 'zapret' is installed", "Установлена служба Windows «zapret»"),
        ("Remove the service before running tests", "Удалите службу перед тестами"),
        ("Open service.bat and choose 'Remove Services'", "Откройте service.bat и выберите «Remove Services»"),
        ("Ipset is in '", "Ipset в режиме «"),
        ("' mode. Switching to 'any' for accurate DPI tests...", "». Переключение в «any» для DPI-тестов..."),
        ("[WARNING]", "[ВНИМАНИЕ]"),
        ("[ERROR]", "[ОШИБКА]"),
        ("[WARN]", "[ПРЕД]"),
        ("[INFO]", "[ИНФО]"),
        ("[OK]", "[ОК]"),
        ("| Ping:", "| Пинг:"),
        (" Ping:", " Пинг:"),
    ];

    private static readonly (string From, string To)[] TokenReplacements =
    [
        ("TLS1.3:UNSUP", "TLS1.3:НЕПОДД"),
        ("TLS1.3:ERROR", "TLS1.3:ОШИБКА"),
        ("TLS1.3:OK", "TLS1.3:ОК"),
        ("TLS1.2:UNSUP", "TLS1.2:НЕПОДД"),
        ("TLS1.2:ERROR", "TLS1.2:ОШИБКА"),
        ("TLS1.2:OK", "TLS1.2:ОК"),
        ("HTTP:UNSUP", "HTTP:НЕПОДД"),
        ("HTTP:ERROR", "HTTP:ОШИБКА"),
        ("HTTP:OK", "HTTP:ОК"),
        ("TLS1.2:BLOCKED", "TLS1.2:БЛОК"),
        ("TLS1.3:BLOCKED", "TLS1.3:БЛОК"),
        ("HTTP:BLOCKED", "HTTP:БЛОК"),
    ];

    public static bool IsActive => !LocalizationService.IsEnglish;

    public static string TranslateLine(string line)
    {
        if (!IsActive || string.IsNullOrEmpty(line)) return line;

        var result = line;
        foreach (var (from, to) in LineReplacements.OrderByDescending(p => p.From.Length))
            result = result.Replace(from, to, StringComparison.Ordinal);

        return result;
    }

    public static string TranslateToken(string? token)
    {
        if (!IsActive || string.IsNullOrEmpty(token)) return token ?? string.Empty;

        var result = token;
        foreach (var (from, to) in TokenReplacements)
            result = result.Replace(from, to, StringComparison.Ordinal);

        return result;
    }

    public static string TranslatePing(string ping)
    {
        if (!IsActive) return ping;

        return ping
            .Replace("Timeout", "Таймаут", StringComparison.OrdinalIgnoreCase)
            .Replace(" ms", " мс", StringComparison.Ordinal);
    }

    public static string TranslateAnalyticsCell(string cell)
    {
        if (!IsActive) return cell;

        return cell
            .Replace("HTTP OK:", "HTTP ОК:", StringComparison.Ordinal)
            .Replace("Ping OK:", "Пинг ОК:", StringComparison.Ordinal)
            .Replace("ERR:", "ОШ:", StringComparison.Ordinal)
            .Replace("UNSUP:", "НЕПОДД:", StringComparison.Ordinal)
            .Replace("Fail:", "Сбой:", StringComparison.Ordinal);
    }

    public static TestTableRow ToDisplayRow(TestTableRow row) =>
        new()
        {
            Name = row.Name,
            Http = row.Http is null ? null : TranslateToken(row.Http),
            Tls12 = row.Tls12 is null ? null : TranslateToken(row.Tls12),
            Tls13 = row.Tls13 is null ? null : TranslateToken(row.Tls13),
            Ping = TranslatePing(row.Ping)
        };

    public static AnalyticsTableRow ToDisplayRow(AnalyticsTableRow row) =>
        new()
        {
            Config = row.Config,
            HttpOk = TranslateAnalyticsCell(row.HttpOk),
            Err = TranslateAnalyticsCell(row.Err),
            Unsup = TranslateAnalyticsCell(row.Unsup),
            PingOk = TranslateAnalyticsCell(row.PingOk),
            Fail = TranslateAnalyticsCell(row.Fail)
        };
}
