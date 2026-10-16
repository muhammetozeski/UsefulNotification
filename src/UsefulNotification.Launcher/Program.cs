using System.Diagnostics;

// Resolve the payload beside the launcher, independent of the terminal working directory.
var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "AppData", "UsefulNotification.exe")) { UseShellExecute = false, WorkingDirectory = AppContext.BaseDirectory };
foreach (string argument in args) start.ArgumentList.Add(argument);
using Process? process = Process.Start(start);
