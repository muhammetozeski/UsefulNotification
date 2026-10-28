using Microsoft.Win32;

namespace UsefulNotification;

/// <summary>Records the launched portable host under the current Windows account and resolves it for clients in other folders.</summary>
internal static class HostDiscovery
{
    const bool IsLogEnabled = true;

    /// <summary>Refreshes the account's launcher reference after a successful host initialization, including after relocation.</summary>
    public static void Register()
    {
        if (!OperatingSystem.IsWindows()) return;
        var registration = AppConstants.Transport.Registration;
        string executable = Path.Combine(AppPaths.Root, AppConstants.Product.Executable);
        if (!File.Exists(executable)) executable = Path.Combine(AppPaths.PayloadDirectory, AppConstants.Product.Executable);
        if (!File.Exists(executable)) throw new FileNotFoundException("The portable host executable is missing.", executable);
        using var key = Registry.CurrentUser.CreateSubKey(registration.RegistryPath);
        key.SetValue(registration.ExecutableValue, executable, RegistryValueKind.String);
        Log($"Host registered; executable={executable}", isRun: IsLogEnabled);
    }

    /// <summary>Finds the launched host from account registration, then a colocated payload; an explicit client Application takes precedence at the caller.</summary>
    /// <returns>An existing host executable, or an actionable missing-installation error.</returns>
    public static FileInfo Resolve()
    {
        if (OperatingSystem.IsWindows())
        {
            var registration = AppConstants.Transport.Registration;
            using var key = Registry.CurrentUser.OpenSubKey(registration.RegistryPath);
            if (key?.GetValue(registration.ExecutableValue) is string path && File.Exists(path)) return new(path);
        }
        var adjacent = new FileInfo(Path.Combine(AppPaths.PayloadDirectory, AppConstants.Product.Executable));
        return adjacent.Exists ? adjacent : throw new FileNotFoundException("Launch UsefulNotification once from its portable folder, or configure NotificationClient.Options.Application.");
    }
}
