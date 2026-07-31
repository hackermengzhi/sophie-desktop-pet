namespace SophieDesktopPet;

internal static class AppVersion
{
    public static Version Current { get; } =
        typeof(AppVersion).Assembly.GetName().Version ?? new Version(0, 0);
}
