using System.Diagnostics;
using System.Text.RegularExpressions;
using Aeroway.Models;

namespace Aeroway.Services;

/// <summary>
/// Одна проверка целей без winws. Токены совпадают со стандартным прогоном test zapret.ps1
/// (curl -I, HTTP / TLS 1.2 / TLS 1.3, пинг), чтобы сравнение со стратегией было в тех же статусах.
/// Сами стратегии по-прежнему гоняет test zapret.ps1.
/// </summary>
public static class BaselineTargetProbe
{
    private const int CurlTimeoutSeconds = 4;

    public static async Task<IReadOnlyList<TestTargetRow>> ProbeAsync(
        IReadOnlyList<TestTargetEndpoint> endpoints,
        CancellationToken cancellationToken = default)
    {
        if (endpoints.Count == 0 || !await CurlAvailableAsync(cancellationToken).ConfigureAwait(false))
            return [];

        var rows = new TestTargetRow[endpoints.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, endpoints.Count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 6,
                CancellationToken = cancellationToken
            },
            async (index, token) =>
            {
                rows[index] = await ProbeEndpointAsync(endpoints[index], token).ConfigureAwait(false);
            }).ConfigureAwait(false);

        return rows;
    }

    private static async Task<TestTargetRow> ProbeEndpointAsync(TestTargetEndpoint endpoint, CancellationToken cancellationToken)
    {
        if (endpoint.PingOnly)
        {
            return new TestTargetRow
            {
                Name = endpoint.Name,
                PingOnly = true,
                Http = "…",
                Tls12 = "…",
                Tls13 = "…",
                Ping = await PingAsync(endpoint.PingHost, cancellationToken).ConfigureAwait(false)
            };
        }

        var http = await CurlTokenAsync("HTTP", ["--http1.1"], endpoint.Url!, cancellationToken).ConfigureAwait(false);
        var tls12 = await CurlTokenAsync("TLS1.2", ["--tlsv1.2", "--tls-max", "1.2"], endpoint.Url!, cancellationToken)
            .ConfigureAwait(false);
        var tls13 = await CurlTokenAsync("TLS1.3", ["--tlsv1.3", "--tls-max", "1.3"], endpoint.Url!, cancellationToken)
            .ConfigureAwait(false);
        var ping = await PingAsync(endpoint.PingHost, cancellationToken).ConfigureAwait(false);
        return new TestTargetRow
        {
            Name = endpoint.Name,
            PingOnly = false,
            Http = http,
            Tls12 = tls12,
            Tls13 = tls13,
            Ping = ping
        };
    }

    private static async Task<string> CurlTokenAsync(
        string label,
        IReadOnlyList<string> extraArgs,
        string url,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "curl.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[]
                 {
                     "-I", "-s", "-m", CurlTimeoutSeconds.ToString(),
                     "--connect-timeout", "2", "-o", "NUL", "-w", "%{http_code}", "--show-error"
                 })
            psi.ArgumentList.Add(arg);
        foreach (var arg in extraArgs)
            psi.ArgumentList.Add(arg);
        psi.ArgumentList.Add(url);

        using var process = new Process { StartInfo = psi };
        try
        {
            if (!process.Start())
                return $"{label}:ERROR";

            var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(CurlTimeoutSeconds + 4));
            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return $"{label}:ERROR";
            }

            var stderr = await stderrTask.ConfigureAwait(false);
            _ = await stdoutTask.ConfigureAwait(false);
            return $"{label}:{BaselineCurlTokens.FromCurl(process.ExitCode, stderr)}";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return $"{label}:ERROR";
        }
        finally
        {
            TryKill(process);
        }
    }

    private static async Task<string> PingAsync(string? host, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(host))
            return "n/a";

        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = await ping.SendPingAsync(host, 1000).WaitAsync(cancellationToken).ConfigureAwait(false);
            return reply.Status == System.Net.NetworkInformation.IPStatus.Success
                ? $"{reply.RoundtripTime} ms"
                : "Timeout";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return "Timeout";
        }
    }

    private static async Task<bool> CurlAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "curl.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("--version");
            using var process = Process.Start(psi);
            if (process is null)
                return false;
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Процесс уже завершился.
        }
    }
}

internal static class BaselineCurlTokens
{
    private static readonly Regex CertificateOrDns = new(
        "Could not resolve host|certificate|SSL certificate problem|self[- ]?signed|certificate verify failed|unable to get local issuer certificate",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex Unsupported = new(
        "does not support|not supported|unsupported protocol|TLS.*not supported|Unrecognized option|Unknown option|unsupported option|unsupported feature|schannel",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string FromCurl(int exitCode, string? stderr)
    {
        var error = stderr ?? "";
        if (CertificateOrDns.IsMatch(error))
            return "SSL";
        if (exitCode == 35 || Unsupported.IsMatch(error))
            return "UNSUP";
        return exitCode == 0 ? "OK" : "ERROR";
    }
}
