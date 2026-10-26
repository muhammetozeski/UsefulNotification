var start = new ProcessStartInfo("notepad.exe") { UseShellExecute = false };
var receipt = await NotificationClient.SendAsync(new() { Title = "Open Notepad", Actions = [new RunCommandAction(start) { Label = "Notepad" }], Silent = true }, cancellationToken: token);
Print(receipt);
if (receipt.Accepted) Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
