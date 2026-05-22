using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DrakesItemShop.Runtime;

internal static class FailureLogWriter
{
    public static void WriteAll(IEnumerable<ValidationFailure> failures)
    {
        ItemShopPaths.EnsureDirectories();
        var sb = new StringBuilder();

        foreach (var f in failures)
        {
            sb.AppendLine($"[{f.FileName}]");
            sb.AppendLine();
            sb.AppendLine("ERROR:");
            sb.AppendLine(f.Error);
            if (!string.IsNullOrEmpty(f.Field))
            {
                sb.AppendLine();
                sb.AppendLine("FIELD:");
                sb.AppendLine(f.Field);
            }

            if (!string.IsNullOrEmpty(f.Suggestion))
            {
                sb.AppendLine();
                sb.AppendLine("SUGGESTION:");
                sb.AppendLine(f.Suggestion);
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        File.WriteAllText(ItemShopPaths.FailureLogFile, sb.ToString());
    }

    public static void Clear()
    {
        ItemShopPaths.EnsureDirectories();
        File.WriteAllText(ItemShopPaths.FailureLogFile, "");
    }
}
