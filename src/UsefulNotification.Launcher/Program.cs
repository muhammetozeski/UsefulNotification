using System.Diagnostics;

// Resolve the payload beside the launcher, independent of the terminal working directory.
string root = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
var start = new ProcessStartInfo(Path.Combine(root, "AppData", "UsefulNotification.exe")) { UseShellExecute = false, WorkingDirectory = root };
foreach (string argument in args) start.ArgumentList.Add(argument);
using Process? process = Process.Start(start);
