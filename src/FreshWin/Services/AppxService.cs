using System.Diagnostics;
using System.Text;
using FreshWin.Models;

namespace FreshWin.Services;

/// <summary>Lists and removes preinstalled Store apps through the Appx PowerShell cmdlets.</summary>
public static class AppxService
{
    /// <summary>Names of every Store package installed for the current user.</summary>
    public static async Task<HashSet<string>> ListInstalledAsync(CancellationToken ct)
    {
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var (exit, output) = await RunAsync(
                "Get-AppxPackage | ForEach-Object { $_.Name }", TimeSpan.FromSeconds(90), ct);

            if (exit != 0) return installed;

            foreach (var line in output.Split('\n'))
            {
                var name = line.Trim();
                if (name.Length > 0) installed.Add(name);
            }
        }
        catch
        {
            // Leaves every card in the "unknown" state, which is handled in the UI.
        }

        return installed;
    }

    public static async Task RefreshStateAsync(IEnumerable<BloatApp> apps, CancellationToken ct)
    {
        var installed = await ListInstalledAsync(ct);
        if (installed.Count == 0) return;

        foreach (var app in apps) app.IsPresent = installed.Contains(app.PackageName);
    }

    /// <summary>Removes one package for the current user, and machine-wide when elevated.</summary>
    public static async Task RemoveAsync(BloatApp app, Action<string> log, CancellationToken ct)
    {
        // Removing the provisioned copy as well stops the package coming back for new
        // user accounts. That part needs admin, so it is allowed to fail quietly.
        var script = $$"""
            $name = '{{app.PackageName}}'
            $pkg = Get-AppxPackage -Name $name -ErrorAction SilentlyContinue
            if (-not $pkg) { Write-Output 'package not present'; exit 0 }
            $pkg | Remove-AppxPackage -ErrorAction Stop
            Write-Output 'removed for current user'
            try {
                $prov = Get-AppxProvisionedPackage -Online -ErrorAction Stop |
                        Where-Object { $_.DisplayName -eq $name }
                if ($prov) {
                    $prov | Remove-AppxProvisionedPackage -Online -ErrorAction Stop | Out-Null
                    Write-Output 'removed the provisioned copy as well'
                }
            } catch {
                Write-Output 'provisioned copy left in place (needs administrator)'
            }
            """;

        var (exit, output) = await RunAsync(script, TimeSpan.FromMinutes(3), ct);

        foreach (var line in output.Split('\n'))
        {
            var text = line.TrimEnd();
            if (text.Length > 0) log("   " + text);
        }

        if (exit != 0)
            throw new InvalidOperationException(
                output.Contains("Access is denied", StringComparison.OrdinalIgnoreCase)
                    ? "needs administrator rights"
                    : "the package could not be removed");
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(
        string script, TimeSpan timeout, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("powershell")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        // No -ExecutionPolicy: it only governs script files, never -Command, and its
        // presence in a binary is one of the strongest signals antivirus heuristics use.
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(script);

        using var process = Process.Start(psi)
                            ?? throw new InvalidOperationException("could not start powershell");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        var stdout = process.StandardOutput.ReadToEndAsync(cts.Token);
        var stderr = process.StandardError.ReadToEndAsync(cts.Token);
        await process.WaitForExitAsync(cts.Token);

        return (process.ExitCode, await stdout + await stderr);
    }
}
