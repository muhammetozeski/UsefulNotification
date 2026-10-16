using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Diagnostics.ToolHelp;
namespace UsefulNotification;

/// <summary>Identifies the process that invoked the command-line sender.</summary>
internal static class SenderIdentity
{
    /// <summary>Reads the parent process name from the Windows process snapshot.</summary>
    /// <returns>The calling program name, or the product name if the process has already exited.</returns>
    public static unsafe string Parent()
    {
        var result = RunSafely(() =>
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10)) return AppConstants.Product.Name;
            using var snapshot = new Microsoft.Win32.SafeHandles.SafeFileHandle((nint)PInvoke.CreateToolhelp32Snapshot(CREATE_TOOLHELP_SNAPSHOT_FLAGS.TH32CS_SNAPPROCESS, 0).Value, true);
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (PInvoke.Process32First(snapshot, ref entry))
                do
                {
                    if (entry.th32ProcessID != Environment.ProcessId) continue;
                    using Process parent = Process.GetProcessById((int)entry.th32ParentProcessID);
                    return parent.ProcessName;
                } while (PInvoke.Process32Next(snapshot, ref entry));
            return AppConstants.Product.Name;
        });
        return result.Succeeded ? result.Value! : AppConstants.Product.Name;
    }
}
