using System.Collections.Generic;
using UnityEngine;

namespace DrakesItemShop.OpenCrate;

/// <summary>
/// Floor + four wall BoxColliders for an open crate (not a mesh collider).
/// Outer size is W×D×H with origin at bottom-center; walls ~0.075 m thick.
/// Snaps on 1 m footprint corners and 0.5 m height steps (gridfinity tiling).
/// </summary>
internal static class OpenCrateShell
{
    private const float Wall = 0.075f;
    private const float FloorThick = 0.08f;
    private const float HeightStep = 0.5f;

    /// <summary>
    /// Disable existing solid colliders on the chest clone, then add floor + walls matching
    /// <paramref name="width"/> × <paramref name="depth"/> × <paramref name="height"/>.
    /// </summary>
    public static void Apply(GameObject prefab, float width, float depth, float height)
    {
        var layer = prefab.layer;
        foreach (var col in prefab.GetComponentsInChildren<Collider>(true))
        {
            if (col != null && !col.isTrigger)
                col.enabled = false;
        }

        var old = prefab.transform.Find("drake_crate_shell");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        var shell = new GameObject("drake_crate_shell");
        shell.transform.SetParent(prefab.transform, false);
        shell.layer = layer;

        AddBox(shell, "floor", new Vector3(0f, FloorThick * 0.5f, 0f),
            new Vector3(width, FloorThick, depth), layer);

        var wallH = Mathf.Max(0.05f, height - FloorThick * 0.5f);
        var wallY = FloorThick * 0.5f + wallH * 0.5f;
        var innerW = Mathf.Max(0.1f, width - 2f * Wall);

        AddBox(shell, "wall_x+", new Vector3((width - Wall) * 0.5f, wallY, 0f),
            new Vector3(Wall, wallH, depth), layer);
        AddBox(shell, "wall_x-", new Vector3(-(width - Wall) * 0.5f, wallY, 0f),
            new Vector3(Wall, wallH, depth), layer);
        AddBox(shell, "wall_z+", new Vector3(0f, wallY, (depth - Wall) * 0.5f),
            new Vector3(innerW, wallH, Wall), layer);
        AddBox(shell, "wall_z-", new Vector3(0f, wallY, -(depth - Wall) * 0.5f),
            new Vector3(innerW, wallH, Wall), layer);
    }

    /// <summary>1 m footprint corners × 0.5 m height steps so crates tile gridfinity-style.</summary>
    public static void SnapGrid(GameObject prefab, float width, float depth, float height)
    {
        var hw = width * 0.5f;
        var hd = depth * 0.5f;
        var points = new List<Vector3>();
        for (var y = 0f; y <= height + 0.001f; y += HeightStep)
        {
            points.Add(new Vector3(-hw, y, -hd));
            points.Add(new Vector3(hw, y, -hd));
            points.Add(new Vector3(hw, y, hd));
            points.Add(new Vector3(-hw, y, hd));
        }
        DrakeModsLibs.Forge.ForgeLook.SnapPoints(prefab, true, points.ToArray());
    }

    /// <summary>Interior usable area ≈ outer minus ~0.15 m per side.</summary>
    public static void InnerBounds(float width, float depth, out float x0, out float x1, out float z0, out float z1)
    {
        const float inset = 0.15f;
        x0 = -width * 0.5f + inset;
        x1 = width * 0.5f - inset;
        z0 = -depth * 0.5f + inset;
        z1 = depth * 0.5f - inset;
    }

    private static void AddBox(GameObject parent, string name, Vector3 center, Vector3 size, int layer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.layer = layer;
        var box = go.AddComponent<BoxCollider>();
        box.center = center;
        box.size = size;
    }
}
