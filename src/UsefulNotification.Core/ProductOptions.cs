namespace UsefulNotification;

/// <summary>Owns executable names and portable directory names shared with the launcher.</summary>
internal sealed class ProductOptions
{
    public string Name { get; set; } = "UsefulNotification";
    public string Executable { get; set; } = "UsefulNotification.exe";
    public string CommandExecutable { get; set; } = "notify.exe";
    public string Icon { get; set; } = "app.ico";
    public string BackgroundArgument { get; set; } = "--background";
    public string PayloadDirectory { get; set; } = "AppData";
    public string AppCacheDirectory { get; set; } = "AppCache";
    public string UserDataDirectory { get; set; } = "UserData";
    public string UserCacheDirectory { get; set; } = "UserCache";
}
