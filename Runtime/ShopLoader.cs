using System;
using System.Collections.Generic;
using System.IO;
using DrakesItemShop.Schema;
using Newtonsoft.Json;

namespace DrakesItemShop.Runtime;

internal static class ShopLoader
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore,
    };

    public static (List<T> Items, List<ValidationFailure> Failures) LoadDirectory<T>(string directory)
    {
        var items = new List<T>();
        var failures = new List<ValidationFailure>();

        if (!Directory.Exists(directory))
            return (items, failures);

        foreach (var path in Directory.GetFiles(directory, "*.json"))
        {
            string fileName = Path.GetFileName(path);
            try
            {
                string json = File.ReadAllText(path);
                var dto = JsonConvert.DeserializeObject<T>(json, Settings);
                if (dto == null)
                {
                    failures.Add(new ValidationFailure
                    {
                        FileName = fileName,
                        Error = "File is empty or could not be parsed.",
                    });
                    continue;
                }

                if (dto is CatalogDto catalog)
                    catalog.SourceFile = fileName;
                else if (dto is ShopDefinitionDto shop)
                    shop.SourceFile = fileName;
                else if (dto is NpcDefinitionDto npc)
                    npc.SourceFile = fileName;

                items.Add(dto);
            }
            catch (JsonException ex)
            {
                failures.Add(new ValidationFailure
                {
                    FileName = fileName,
                    Error = $"JSON parse error: {ex.Message}",
                });
            }
            catch (Exception ex)
            {
                failures.Add(new ValidationFailure
                {
                    FileName = fileName,
                    Error = $"Failed to read file: {ex.Message}",
                });
            }
        }

        return (items, failures);
    }
}
