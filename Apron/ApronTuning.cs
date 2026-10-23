using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Newtonsoft.Json;
using UnityEngine;

namespace DrakesItemShop.Apron;

/// <summary>
/// Everything about how the apron sits on the body, as numbers you can change while wearing it (see <see cref="ApronTuner"/>)
/// and save to <c>BepInEx/config/DrakesItemShop.apron.json</c>. The saved numbers are what the apron uses from then on.
/// </summary>
internal static class ApronTuning
{
    internal sealed class Param
    {
        public string Group = "";
        public string Key = "";
        public string Label = "";
        public float Min, Max, Step, Default;
        public string Unit = "m";
    }

    internal static readonly Param[] All =
    {
        // Placement: where the whole apron sits on the body.
        new() { Group = "Placement", Key = "Depth", Label = "Depth at the waist", Min = 0f, Max = 0.50f, Step = 0.005f, Default = 0.145f },
        new() { Group = "Placement", Key = "Lift", Label = "Whole apron up / down", Min = -0.60f, Max = 0.60f, Step = 0.005f, Default = 0.075f },
        new() { Group = "Placement", Key = "SideShift", Label = "Shift sideways", Min = -0.30f, Max = 0.30f, Step = 0.005f, Default = -0.01f },
        new() { Group = "Placement", Key = "BackDepth", Label = "Back: further back", Min = -0.20f, Max = 0.30f, Step = 0.005f, Default = -0.01f },
        new() { Group = "Placement", Key = "Size", Label = "Whole apron: scale", Min = 0.5f, Max = 2.0f, Step = 0.02f, Default = 1f, Unit = "x" },
        new() { Group = "Placement", Key = "RotYaw", Label = "Whole apron: turn left/right", Min = -90f, Max = 90f, Step = 1f, Default = -4f, Unit = "deg" },
        new() { Group = "Placement", Key = "RotPitch", Label = "Whole apron: tilt forward/back", Min = -90f, Max = 90f, Step = 1f, Default = 3f, Unit = "deg" },
        new() { Group = "Placement", Key = "RotRoll", Label = "Whole apron: roll sideways", Min = -90f, Max = 90f, Step = 1f, Default = 0f, Unit = "deg" },

        // Shape: the panel itself.
        new() { Group = "Skirt", Key = "Width", Label = "Width", Min = 0.4f, Max = 3f, Step = 0.02f, Default = 0.98f, Unit = "x" },
        new() { Group = "Bib", Key = "BibTopWidth", Label = "Bib: width at the top (narrows towards the neck)", Min = 0.02f, Max = 0.30f, Step = 0.0025f, Default = 0.145f },
        new() { Group = "Bib", Key = "SeamBlend", Label = "Waist seam: how gently bib and skirt meet", Min = 0f, Max = 0.35f, Step = 0.005f, Default = 0.085f },
        new() { Group = "Bib", Key = "TopExtra", Label = "Bib: taller", Min = -0.40f, Max = 0.60f, Step = 0.0025f, Default = 0.015f },
        new() { Group = "Skirt", Key = "HemExtra", Label = "Hem: longer", Min = -0.50f, Max = 0.80f, Step = 0.005f, Default = -0.055f },
        new() { Group = "Bib", Key = "TopForward", Label = "Top of the bib: further forward (+) / back to the neck (-)", Min = -0.20f, Max = 0.20f, Step = 0.0025f, Default = -0.0175f },
        new() { Group = "Bib", Key = "TopZone", Label = "Top of the bib: how much of the bib that covers", Min = 0.10f, Max = 1.0f, Step = 0.02f, Default = 0.5f, Unit = "" },
        new() { Group = "Bib", Key = "TopWrapExtra", Label = "Top of the bib: wrap more / less round the chest", Min = -2f, Max = 2f, Step = 0.0025f, Default = -0.735f, Unit = "" },
        new() { Group = "Bib", Key = "Bulge", Label = "Chest: further forward", Min = -0.20f, Max = 0.40f, Step = 0.005f, Default = 0.14f },
        new() { Group = "Skirt", Key = "Flare", Label = "Hem: further forward", Min = -0.20f, Max = 0.40f, Step = 0.005f, Default = 0.07f },
        new() { Group = "Skirt", Key = "SkirtBelly", Label = "Skirt: puffs out mid-length", Min = -0.10f, Max = 0.25f, Step = 0.005f, Default = 0.05f },
        new() { Group = "Bib", Key = "Curve", Label = "Bib: wrap round the sides", Min = 0f, Max = 3f, Step = 0.05f, Default = 1.8f, Unit = "" },
        new() { Group = "Skirt", Key = "SkirtCurve", Label = "Skirt: wrap round the sides", Min = 0f, Max = 3f, Step = 0.05f, Default = 1.5f, Unit = "" },
        new() { Group = "Bib", Key = "BibPitch", Label = "Bib: tilt top forward", Min = -90f, Max = 90f, Step = 1f, Default = 17f, Unit = "deg" },
        new() { Group = "Skirt", Key = "SkirtPitch", Label = "Skirt: swing hem forward", Min = -90f, Max = 90f, Step = 1f, Default = -2f, Unit = "deg" },
        new() { Group = "Cloth", Key = "FoldAmp", Label = "Cloth: fold depth", Min = 0f, Max = 0.06f, Step = 0.002f, Default = 0.012f },
        new() { Group = "Cloth", Key = "FoldCount", Label = "Cloth: number of folds", Min = 1f, Max = 14f, Step = 1f, Default = 8f, Unit = "" },
        new() { Group = "Cloth", Key = "PocketPuff", Label = "Pocket: stands out", Min = 0f, Max = 0.06f, Step = 0.002f, Default = 0.014f },
        new() { Group = "Cloth", Key = "Clearance", Label = "Clearance off the skin", Min = 0f, Max = 0.12f, Step = 0.002f, Default = 0.044f },

        // Belt
        new() { Group = "Belt", Key = "BandHeight", Label = "Belt: height", Min = 0.01f, Max = 0.30f, Step = 0.005f, Default = 0.03f },
        new() { Group = "Belt", Key = "BandWidth", Label = "Belt: size round the body", Min = 0.6f, Max = 2.0f, Step = 0.01f, Default = 1.08f, Unit = "x" },
        new() { Group = "Belt", Key = "BeltTie", Label = "Belt: tied with a knot and tails (1) or plain (0)", Min = 0f, Max = 1f, Step = 1f, Default = 1f, Unit = "" },
        new() { Group = "Belt", Key = "TieTail", Label = "Belt: tail length", Min = 0.04f, Max = 0.50f, Step = 0.01f, Default = 0.2f },
        new() { Group = "Belt", Key = "TieKnot", Label = "Belt: knot size", Min = 0.02f, Max = 0.12f, Step = 0.005f, Default = 0.05f },
        new() { Group = "Belt", Key = "TieSide", Label = "Belt: where the knot sits (0 back, +/- round the side)", Min = -1.5f, Max = 1.5f, Step = 0.05f, Default = -0.05f, Unit = "" },

        // Straps
        new() { Group = "Neck", Key = "StrapStyle", Label = "Straps: neck loop (0) or crossed over the back (1)", Min = 0f, Max = 1f, Step = 1f, Default = 0f, Unit = "" },
        new() { Group = "Neck", Key = "NeckRadius", Label = "Neck loop: size", Min = 0.03f, Max = 0.16f, Step = 0.0025f, Default = 0.075f },
        new() { Group = "Neck", Key = "NeckLift", Label = "Neck loop: up / down", Min = -0.15f, Max = 0.25f, Step = 0.0025f, Default = 0f },
        new() { Group = "Neck", Key = "NeckForward", Label = "Neck loop: forward / back", Min = -0.15f, Max = 0.15f, Step = 0.0025f, Default = 0f },
        new() { Group = "Neck", Key = "NeckAngle", Label = "Neck loop: where the side straps meet it (round the front)", Min = 0.2f, Max = 1.6f, Step = 0.0025f, Default = 0.75f, Unit = "" },
        new() { Group = "Neck", Key = "StrapWidth", Label = "Straps: width", Min = 0.01f, Max = 0.15f, Step = 0.0025f, Default = 0.04f },
        new() { Group = "Crossed straps", Key = "StrapSpread", Label = "Straps: where they leave the bib (x)", Min = 0f, Max = 0.50f, Step = 0.005f, Default = 0.15f },
        new() { Group = "Crossed straps", Key = "StrapShoulder", Label = "Straps: where they cross the shoulder (x)", Min = 0f, Max = 0.50f, Step = 0.005f, Default = 0.185f },
        new() { Group = "Crossed straps", Key = "StrapRise", Label = "Straps: shoulder height above the bib", Min = -0.20f, Max = 0.50f, Step = 0.005f, Default = 0.07f },
        new() { Group = "Crossed straps", Key = "StrapShoulderDepth", Label = "Straps: shoulder forward/back", Min = -0.25f, Max = 0.25f, Step = 0.005f, Default = -0.045f },
        new() { Group = "Crossed straps", Key = "StrapBackHeight", Label = "Straps: height on the back", Min = -0.60f, Max = 0.20f, Step = 0.005f, Default = -0.05f },
        new() { Group = "Crossed straps", Key = "StrapBackSpread", Label = "Straps: where they meet the belt at the back (x)", Min = 0f, Max = 0.40f, Step = 0.005f, Default = 0.115f },
        new() { Group = "Crossed straps", Key = "StrapCross", Label = "Straps: crossed (1) or straight (0)", Min = 0f, Max = 1f, Step = 0.1f, Default = 1f, Unit = "" },
    };

    /// <summary>The fitting room's tabs, in order.</summary>
    internal static readonly string[] Tabs = { "Placement", "Bib", "Skirt", "Cloth", "Belt", "Neck", "Crossed straps" };

    private static readonly float[] Values = new float[All.Length];
    private static bool _loaded;

    /// <summary>Bumped on every change; the apron on a body rebuilds when it moves.</summary>
    internal static int Version { get; private set; }

    internal static string FilePath => Path.Combine(Paths.ConfigPath, "DrakesItemShop.apron.json");

    internal static float Get(string key)
    {
        EnsureLoaded();
        return Values[IndexOf(key)];
    }

    internal static float Get(int index)
    {
        EnsureLoaded();
        return Values[index];
    }

    internal static void Set(int index, float value)
    {
        EnsureLoaded();
        var p = All[index];
        value = Mathf.Clamp((float)Math.Round(value / p.Step) * p.Step, p.Min, p.Max);
        if (Mathf.Approximately(Values[index], value))
            return;
        Values[index] = value;
        Version++;
    }

    internal static void ResetAll()
    {
        EnsureLoaded();
        for (var i = 0; i < All.Length; i++)
            Values[i] = All[i].Default;
        Version++;
    }

    private static int IndexOf(string key)
    {
        for (var i = 0; i < All.Length; i++)
            if (All[i].Key == key)
                return i;
        throw new ArgumentException(key);
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;
        _loaded = true;
        for (var i = 0; i < All.Length; i++)
            Values[i] = All[i].Default;
        try
        {
            if (!File.Exists(FilePath))
                return;
            var saved = JsonConvert.DeserializeObject<Dictionary<string, float>>(File.ReadAllText(FilePath));
            if (saved == null)
                return;
            for (var i = 0; i < All.Length; i++)
                if (saved.TryGetValue(All[i].Key, out var v))
                    Values[i] = Mathf.Clamp(v, All[i].Min, All[i].Max);
            ItemShopPlugin.LogSource?.LogInfo($"[Apron] loaded the saved fitting from {FilePath}");
        }
        catch (Exception ex)
        {
            ItemShopPlugin.LogSource?.LogWarning($"[Apron] couldn't read {FilePath}: {ex.Message}");
        }
    }

    internal static void Save()
    {
        EnsureLoaded();
        var saved = new Dictionary<string, float>();
        for (var i = 0; i < All.Length; i++)
            saved[All[i].Key] = (float)Math.Round(Values[i], 4);
        File.WriteAllText(FilePath, JsonConvert.SerializeObject(saved, Formatting.Indented));
        ItemShopPlugin.LogSource?.LogInfo($"[Apron] saved the fitting to {FilePath}");
    }
}
