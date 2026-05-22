using System.IO;
using BepInEx;

namespace DrakesItemShop.Runtime;

internal static class ItemShopPaths
{
    public const string DataFolderName = "ItemShop";

    public static string DataRoot =>
        Path.Combine(Paths.ConfigPath, ItemShopPlugin.GUID, DataFolderName);

    public static string ShopsDirectory => Path.Combine(DataRoot, "shops");
    public static string CatalogsDirectory => Path.Combine(DataRoot, "catalogs");
    public static string NpcsDirectory => Path.Combine(DataRoot, "npcs");
    public static string LogsDirectory => Path.Combine(DataRoot, "logs");
    public static string ExamplesDirectory => Path.Combine(DataRoot, "examples");

    public static string FailureLogFile => Path.Combine(LogsDirectory, "failed_configs.txt");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(ShopsDirectory);
        Directory.CreateDirectory(CatalogsDirectory);
        Directory.CreateDirectory(NpcsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(ExamplesDirectory);
    }
}
