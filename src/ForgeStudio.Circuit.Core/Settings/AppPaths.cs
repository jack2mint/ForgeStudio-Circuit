namespace ForgeStudio.Circuit.Core.Settings;

public static class AppPaths
{
    public static string UserDataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StaticTechGroup",
        "ForgeStudio Circuit");

    public static string Logs => Path.Combine(UserDataRoot, "Logs");
    public static string Projects => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ForgeStudio Circuit", "Projects");
    public static string Backups => Path.Combine(UserDataRoot, "Backups");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(UserDataRoot);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Projects);
        Directory.CreateDirectory(Backups);
    }
}
