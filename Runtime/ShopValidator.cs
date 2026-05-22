using System.Collections.Generic;
using System.Linq;
using DrakesItemShop.Schema;

namespace DrakesItemShop.Runtime;

internal static class ShopValidator
{
    public static (List<CatalogDto> Valid, List<ValidationFailure> Failures) ValidateCatalogs(IReadOnlyList<CatalogDto> catalogs)
    {
        var valid = new List<CatalogDto>();
        var failures = new List<ValidationFailure>();
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        foreach (var c in catalogs)
        {
            if (string.IsNullOrWhiteSpace(c.Id))
            {
                failures.Add(new ValidationFailure
                {
                    FileName = c.SourceFile,
                    Error = "Catalog id is required.",
                    Field = "id",
                });
                continue;
            }

            if (!seen.Add(c.Id.Trim()))
            {
                failures.Add(new ValidationFailure
                {
                    FileName = c.SourceFile,
                    Error = $"Duplicate catalog id '{c.Id}'.",
                    Field = "id",
                });
                continue;
            }

            if (c.Entries == null || c.Entries.Count == 0)
            {
                failures.Add(new ValidationFailure
                {
                    FileName = c.SourceFile,
                    Error = "Catalog must have at least one entry.",
                    Field = "entries",
                });
                continue;
            }

            bool entryOk = true;
            foreach (var e in c.Entries)
            {
                if (string.IsNullOrWhiteSpace(e.Item))
                {
                    failures.Add(new ValidationFailure
                    {
                        FileName = c.SourceFile,
                        Error = "Catalog entry item is required.",
                        Field = "entries.item",
                    });
                    entryOk = false;
                    break;
                }
            }

            if (entryOk)
                valid.Add(c);
        }

        return (valid, failures);
    }

    public static (List<ShopDefinitionDto> Valid, List<ValidationFailure> Failures) ValidateShops(
        IReadOnlyList<ShopDefinitionDto> shops,
        IReadOnlyDictionary<string, CatalogDto> catalogs)
    {
        var valid = new List<ShopDefinitionDto>();
        var failures = new List<ValidationFailure>();
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        foreach (var s in shops)
        {
            if (string.IsNullOrWhiteSpace(s.Id))
            {
                failures.Add(new ValidationFailure
                {
                    FileName = s.SourceFile,
                    Error = "Shop id is required.",
                    Field = "id",
                });
                continue;
            }

            if (!seen.Add(s.Id.Trim()))
            {
                failures.Add(new ValidationFailure
                {
                    FileName = s.SourceFile,
                    Error = $"Duplicate shop id '{s.Id}'.",
                    Field = "id",
                });
                continue;
            }

            if (string.IsNullOrWhiteSpace(s.CatalogId) || !catalogs.ContainsKey(s.CatalogId))
            {
                failures.Add(new ValidationFailure
                {
                    FileName = s.SourceFile,
                    Error = $"Unknown catalogId '{s.CatalogId}'.",
                    Field = "catalogId",
                    Suggestion = "Create a matching file in catalogs/ first.",
                });
                continue;
            }

            valid.Add(s);
        }

        return (valid, failures);
    }

    public static (List<NpcDefinitionDto> Valid, List<ValidationFailure> Failures) ValidateNpcs(
        IReadOnlyList<NpcDefinitionDto> npcs,
        IReadOnlyDictionary<string, CatalogDto> catalogs,
        IReadOnlyDictionary<string, ShopDefinitionDto> shops)
    {
        var valid = new List<NpcDefinitionDto>();
        var failures = new List<ValidationFailure>();
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        foreach (var n in npcs)
        {
            if (string.IsNullOrWhiteSpace(n.Id))
            {
                failures.Add(new ValidationFailure
                {
                    FileName = n.SourceFile,
                    Error = "NPC id is required.",
                    Field = "id",
                });
                continue;
            }

            if (!seen.Add(n.Id.Trim()))
            {
                failures.Add(new ValidationFailure
                {
                    FileName = n.SourceFile,
                    Error = $"Duplicate NPC id '{n.Id}'.",
                    Field = "id",
                });
                continue;
            }

            if (!string.IsNullOrWhiteSpace(n.ShopId) && !shops.ContainsKey(n.ShopId))
            {
                failures.Add(new ValidationFailure
                {
                    FileName = n.SourceFile,
                    Error = $"Unknown shopId '{n.ShopId}'.",
                    Field = "shopId",
                });
                continue;
            }

            if (!string.IsNullOrWhiteSpace(n.CatalogId) && !catalogs.ContainsKey(n.CatalogId))
            {
                failures.Add(new ValidationFailure
                {
                    FileName = n.SourceFile,
                    Error = $"Unknown catalogId '{n.CatalogId}'.",
                    Field = "catalogId",
                });
                continue;
            }

            if (string.IsNullOrWhiteSpace(n.CatalogId) && string.IsNullOrWhiteSpace(n.ShopId))
            {
                failures.Add(new ValidationFailure
                {
                    FileName = n.SourceFile,
                    Error = "NPC requires catalogId or shopId.",
                    Field = "catalogId",
                });
                continue;
            }

            if (string.IsNullOrWhiteSpace(n.ClonePrefab))
            {
                failures.Add(new ValidationFailure
                {
                    FileName = n.SourceFile,
                    Error = "clonePrefab is required (e.g. Haldor).",
                    Field = "clonePrefab",
                });
                continue;
            }

            valid.Add(n);
        }

        return (valid, failures);
    }
}
