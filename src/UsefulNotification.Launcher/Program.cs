using System.Diagnostics;
using UsefulNotification;
using static UsefulNotification.ExceptionBoundary;

// Resolve the payload beside the launcher, independent of the terminal working directory.
var result = RunSafely(() =>
{
    var product = AppConstants.Product;
    var start = new ProcessStartInfo(Path.Combine(AppPaths.PayloadDirectory, product.Executable)) { UseShellExecute = false, WorkingDirectory = AppPaths.Root };
    foreach (string argument in args) start.ArgumentList.Add(argument);
    using Process? process = Process.Start(start) ?? throw new IOException("The application did not start.");
});
return result.Succeeded ? 0 : 1;
