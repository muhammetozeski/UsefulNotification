string pipeName = "UsefulNotification.Demo." + Guid.NewGuid().ToString("N");
using var callback = new NamedPipeServerStream(pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
Task connected = callback.WaitForConnectionAsync(token);
var receipt = await NotificationClient.SendAsync(new() { Title = "Callback result", Actions = [new CallbackAction { Id = "confirm", Label = "Confirm" }], CallbackPipe = pipeName, Silent = true }, cancellationToken: token);
Print(receipt);
if (receipt.Accepted)
{
    Print(await NotificationClient.WaitAsync(receipt, cancellationToken: token));
    await connected;
    using var reader = new StreamReader(callback);
    string line = await reader.ReadLineAsync(token) ?? throw new EndOfStreamException();
    using var json = JsonDocument.Parse(line);
    Print(json.RootElement.Clone());
}
