using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DrakesItemShop.Storefront;

/// <summary>glTF (right-handed, what Blender exports) to Unity: Forge flips Z when it reads a .glb, so Z flips here too.</summary>
internal static class Gl
{
    public static Vector3 P(float x, float y, float z) => new(x, y, -z);
}

/// <summary>
/// Helpers for the Storefront pieces. Forge's .glb loader bakes every node into one flat mesh per primitive named
/// "glb{i}_{mesh name}" (no hierarchy, no pivots), so moving parts are found by that mesh name and turned about a
/// hinge point given in the piece's own space (see <see cref="HingedParts"/>).
/// </summary>
internal static class StorefrontModel
{
    private static readonly Regex PartName = new(@"^glb\d+_(.+)$", RegexOptions.Compiled);

    /// <summary>The .glb mesh name behind a Forge renderer ("glb7_sign_board" gives "sign_board"), or null if it isn't one.</summary>
    internal static string? MeshName(string rendererName)
    {
        var match = PartName.Match(rendererName);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>Switches the base chest's solid colliders off and returns the layer they were on (so ours match: placing, hover and hits).</summary>
    internal static int DisableBaseSolids(GameObject prefab)
    {
        var solids = prefab.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger && c.enabled).ToList();
        var layer = solids.Count > 0 ? solids[0].gameObject.layer : prefab.layer;
        foreach (var solid in solids)
            solid.enabled = false;
        return layer;
    }

    /// <summary>One box collider in its own child object.</summary>
    internal static GameObject AddBox(GameObject prefab, string name, Vector3 center, Vector3 size, int layer)
    {
        var holder = new GameObject(name) { layer = layer };
        holder.transform.SetParent(prefab.transform, false);
        var box = holder.AddComponent<BoxCollider>();
        box.center = center;
        box.size = size;
        return holder;
    }

    /// <summary>
    /// The model's COL_* meshes are collider proxies with no material: hide them and put a box of the same bounds on each one,
    /// so a proxy that belongs to a moving part (the sign board) carries its collider along.
    /// </summary>
    internal static int ProxiesToColliders(GameObject prefab, int layer)
    {
        var count = 0;
        foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mesh = MeshName(renderer.name);
            if (mesh == null || !mesh.StartsWith("COL_", StringComparison.Ordinal))
                continue;
            renderer.enabled = false;
            if (renderer.GetComponent<BoxCollider>() != null || renderer.GetComponent<MeshFilter>()?.sharedMesh is not { } sharedMesh)
                continue;
            renderer.gameObject.layer = layer;
            var box = renderer.gameObject.AddComponent<BoxCollider>();
            box.center = sharedMesh.bounds.center;
            box.size = sharedMesh.bounds.size;
            count++;
        }
        return count;
    }

    /// <summary>The chest's ordinary lit piece material (Custom/Piece), found before the model replaces its meshes.</summary>
    internal static Material? LitTemplate(GameObject prefab) =>
        prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
            .FirstOrDefault(m => m != null && m.shader != null && m.shader.name == "Custom/Piece");

    /// <summary>Gives every renderer using the glb material <paramref name="target"/> a plain coloured copy of <paramref name="template"/>.</summary>
    internal static void Solid(GameObject prefab, string target, Material? template, Color color, Color? emission)
    {
        if (template == null)
            return;
        var material = new Material(template) { name = target, color = color };
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", null);
        if (emission is { } glow)
            DrakeModsLibs.Forge.ForgeLook.Emission(material, glow);
        foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
        {
            var shared = renderer.sharedMaterials;
            var changed = false;
            for (var i = 0; i < shared.Length; i++)
            {
                if (shared[i] != null && shared[i].name.Replace(" (Instance)", "").Trim() == target)
                {
                    shared[i] = material;
                    changed = true;
                }
            }
            if (changed)
                renderer.sharedMaterials = shared;
        }
    }

    /// <summary>Removes the chest's inventory (these pieces aren't containers). Safe to call when it is already gone.</summary>
    internal static void StripChest(GameObject prefab)
    {
        foreach (var container in prefab.GetComponents<Container>())
            UnityEngine.Object.DestroyImmediate(container);
    }

    internal static Bounds MeshBounds(Transform root, string meshName)
    {
        var found = false;
        var bounds = new Bounds();
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!string.Equals(MeshName(renderer.name), meshName, StringComparison.OrdinalIgnoreCase)
                || renderer.GetComponent<MeshFilter>()?.sharedMesh is not { } mesh)
                continue;
            if (!found)
                bounds = mesh.bounds;
            else
                bounds.Encapsulate(mesh.bounds);
            found = true;
        }
        return bounds;
    }
}

/// <summary>A set of the model's parts (by .glb mesh name) that turn together about a hinge, working from where they started.</summary>
internal sealed class HingedParts
{
    private readonly List<(Transform Part, Vector3 Pos, Quaternion Rot)> _parts = new();

    public int Count => _parts.Count;

    /// <summary>The first part found (text and effects can be parented to it to move with the set), or null.</summary>
    public Transform? First => _parts.Count > 0 ? _parts[0].Part : null;

    public HingedParts(Transform root, params string[] meshNames)
    {
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mesh = StorefrontModel.MeshName(renderer.name);
            if (mesh == null || !meshNames.Any(n => string.Equals(n, mesh, StringComparison.OrdinalIgnoreCase)))
                continue;
            var t = renderer.transform;
            _parts.Add((t, t.localPosition, t.localRotation));
        }
    }

    /// <summary>Turn every part by <paramref name="turn"/> about <paramref name="hinge"/> (piece space).</summary>
    public void Turn(Vector3 hinge, Quaternion turn)
    {
        foreach (var (part, pos, rot) in _parts)
        {
            part.localPosition = hinge + turn * (pos - hinge);
            part.localRotation = turn * rot;
        }
    }

    /// <summary>Slide every part by <paramref name="offset"/> (piece space).</summary>
    public void Slide(Vector3 offset)
    {
        foreach (var (part, pos, rot) in _parts)
        {
            part.localPosition = pos + offset;
            part.localRotation = rot;
        }
    }
}

/// <summary>Small ZDO helpers shared by the Storefront behaviours.</summary>
internal static class StorefrontZdo
{
    internal static ZDO? Zdo(ZNetView? nview) => nview != null && nview.IsValid() ? nview.GetZDO() : null;

    internal static int Key(string name) => name.GetStableHashCode();

    /// <summary>May this player change things here? Wards decide when there is one; otherwise anyone.</summary>
    internal static bool MayUse(Component piece, bool flash = true) =>
        PrivateArea.CheckAccess(piece.transform.position, 0f, flash, false);
}
