using System.Linq;
using DrakeModsLibs.Forge;
using Jotunn.Managers;
using UnityEngine;

namespace DrakesItemShop.Pieces;

/// <summary>
/// Copies a vanilla rug's albedo (and bump when present) onto the piece's existing cloth material.
/// Avoids <c>new Material(rug)</c>, which pulls Valheim's heavy rug shader/textures onto every variant
/// and was contributing to GPU OOM crashes on Intel iGPUs at character select.
/// </summary>
internal static class RugFabric
{
    /// <summary>
    /// Retarget cloth named <paramref name="clothName"/> (e.g. <c>Fabric</c> on a fresh glb, or
    /// <c>jute_carpet</c> after a jute Dress renamed it to <c>jute_carpet_forge</c>).
    /// </summary>
    public static void Apply(GameObject prefab, string clothName, string rugPrefab, string? rugMaterial = null)
    {
        ForgeLook.Material(prefab, clothName, null, null, null, null, mat => CopyOnto(mat, rugPrefab, rugMaterial));
    }

    private static void CopyOnto(Material dest, string rugPrefab, string? rugMaterial)
    {
        var source = Find(rugPrefab, rugMaterial);
        if (source == null)
        {
            ItemShopPlugin.LogSource?.LogWarning($"[RugFabric] No material on '{rugPrefab}'" +
                (rugMaterial != null ? $" named '{rugMaterial}'" : "") + ".");
            return;
        }

        CopyTex(dest, source, "_MainTex");
        CopyTex(dest, source, "_BumpMap");
        if (dest.HasProperty("_Color") && source.HasProperty("_Color"))
            dest.SetColor("_Color", source.GetColor("_Color"));
    }

    private static void CopyTex(Material dest, Material source, string property)
    {
        if (!dest.HasProperty(property) || !source.HasProperty(property))
            return;
        var tex = source.GetTexture(property);
        if (tex != null)
            dest.SetTexture(property, tex);
    }

    private static Material? Find(string rugPrefab, string? rugMaterial)
    {
        var prefab = PrefabManager.Instance.GetPrefab(rugPrefab);
        if (prefab == null)
            return null;
        var materials = prefab.GetComponentsInChildren<Renderer>(true)
            .SelectMany(r => r.sharedMaterials)
            .Where(m => m != null);
        if (rugMaterial == null)
            return materials.FirstOrDefault();
        return materials.FirstOrDefault(m =>
        {
            var name = m!.name.Replace(" (Instance)", "").Trim();
            if (name.EndsWith("_forge", System.StringComparison.Ordinal))
                name = name.Substring(0, name.Length - "_forge".Length);
            return name == rugMaterial;
        });
    }
}
