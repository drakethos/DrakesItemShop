#nullable enable
using System.Reflection;
using DrakeModsLibs.Forge;
using UnityEngine;

namespace DrakesItemShop.DisplayCard.Items;

/// <summary>
/// Paper buy-card mesh + RenameIt-style matte textures. Visual only — cases spawn these;
/// not a hammer piece.
/// </summary>
internal static class DisplayCard
{
    private static Assembly Owner => typeof(DisplayCard).Assembly;

    // The model is a folded tent card standing on its base (model units, before scale): ridge along X at the top,
    // one sloped face toward +Z and one toward -Z.
    internal const float Width = 0.2f;
    internal const float Depth = 0.22f;
    internal const float RidgeY = 0.154f;
    internal const float FootY = 0.014f;
    internal const float FootZ = 0.11f;

    /// <summary>
    /// Builds a dressed tent card under <paramref name="parent"/> (no Piece/Container), plus a non-solid hover box so the
    /// crosshair lands on it and routes to the parent's Container. <paramref name="visual"/> is the scaled mesh root.
    /// </summary>
    internal static GameObject BuildVisual(Transform parent, float scale, out Transform? visual)
    {
        var go = new GameObject("drake_buy_card");
        go.transform.SetParent(parent, false);
        visual = null;

        // ForgeLook.Model needs a material donor; an empty GO has none and Shader.Find("Standard") fails in Valheim.
        SeedMaterialDonor(go, parent);

        var model = ForgeLook.Model(Owner, go, "models/paper_mesh_card_display.glb");
        if (model == null)
        {
            ItemShopPlugin.LogSource?.LogWarning("[DisplayCard] paper_mesh_card_display.glb missing — card mesh will be empty.");
            return go;
        }
        ApplyPaperTextures(model);
        model.transform.localScale = Vector3.one * scale;
        visual = model.transform;

        var hit = new GameObject("drake_buy_card_hit");
        hit.transform.SetParent(model.transform, false);
        var layer = LayerMask.NameToLayer("piece_nonsolid");
        if (layer >= 0)
            hit.layer = layer;
        var box = hit.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, RidgeY / 2f, 0f);
        box.size = new Vector3(Width, RidgeY, Depth);
        return go;
    }

    private static void SeedMaterialDonor(GameObject host, Transform parent)
    {
        Material? template = null;
        foreach (var renderer in parent.GetComponentsInParent<MeshRenderer>(true))
        {
            if (renderer.sharedMaterial != null)
            {
                template = renderer.sharedMaterial;
                break;
            }
        }
        if (template == null)
        {
            var albedo = ForgeTextures.Load(Owner, "textures/paper_mesh_sheet.png");
            if (albedo != null)
                template = ForgeSprites.Material(albedo);
        }
        if (template == null)
            return;

        var seed = new GameObject("_mat_seed");
        seed.transform.SetParent(host.transform, false);
        seed.AddComponent<MeshFilter>().sharedMesh = ForgeSprites.Quad(0.01f, 0.01f, false);
        seed.AddComponent<MeshRenderer>().sharedMaterial = template;
        seed.SetActive(false);
    }

    /// <summary>
    /// Paper, not glass: no smoothness from any source (the Standard template can read it from the albedo's alpha, which on an
    /// opaque sheet means mirror-smooth), no specular highlight, no environment reflection.
    /// </summary>
    internal static void Matte(Material material)
    {
        foreach (var (prop, value) in new[] { ("_Glossiness", 0f), ("_GlossMapScale", 0f), ("_Metallic", 0f),
                     ("_SpecularHighlights", 0f), ("_GlossyReflections", 0f), ("_SmoothnessTextureChannel", 0f) })
            if (material.HasProperty(prop))
                material.SetFloat(prop, value);
        if (material.HasProperty("_MetallicGlossMap"))
            material.SetTexture("_MetallicGlossMap", null);
        material.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        material.DisableKeyword("_METALLICGLOSSMAP");
        material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
    }

    internal static void ApplyPaperTextures(GameObject? visual)
    {
        if (visual == null)
            return;

        var albedo = ForgeTextures.Load(Owner, "textures/paper_mesh_sheet.png");
        if (albedo == null)
            return;
        albedo.filterMode = FilterMode.Point;

        var material = new Material(ForgeSprites.Material(albedo)) { name = "drake_display_card_paper" };
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", new Color(0.80f, 0.74f, 0.62f, 1f));
        Matte(material);

        var normal = ForgeTextures.Load(Owner, "textures/paper_mesh_normal.png", normalMap: true);
        if (normal != null && material.HasProperty("_BumpMap"))
        {
            normal.filterMode = FilterMode.Point;
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 0.3f);
            material.EnableKeyword("_NORMALMAP");
        }

        foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>(true))
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
