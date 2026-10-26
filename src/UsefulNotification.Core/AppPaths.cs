using System.Reflection;

namespace UsefulNotification;

/// <summary>Resolves portable paths from the executing application, including an AppData payload layout.</summary>
internal static class AppPaths
{
    internal const bool IsLogEnabled = true;

    public static readonly string PayloadDirectory = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
    public static readonly string Root = string.Equals(Path.GetFileName(PayloadDirectory), AppConstants.Product.PayloadDirectory, StringComparison.OrdinalIgnoreCase)
        ? Directory.GetParent(PayloadDirectory)!.FullName
        : PayloadDirectory;

    public static readonly string AppCache = Path.Combine(Root, AppConstants.Product.AppCacheDirectory);
    public static readonly string UserData = Path.Combine(Root, AppConstants.Product.UserDataDirectory);
    public static readonly string UserCache = Path.Combine(Root, AppConstants.Product.UserCacheDirectory);
    public static readonly string PayloadVersion = (Assembly.GetEntryAssembly() ?? typeof(AppPaths).Assembly)
        .GetName().Version?.ToString() ?? "1.0.0.0";
}
