using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using DrakesItemShop.Apron;
using UnityEngine;

namespace DrakesItemShop.Testing;

/// <summary>
/// Test hooks that fit the apron to the worn body from a DrakesTestLab run: measure how far the apron sits off the body surface,
/// move the fitting numbers one pass at a time (coordinate descent, kept near the saved fitting), and write every accepted
/// change to a trace file. The test takes the screenshots between passes. Results go to
/// <c>config/DrakesTestLab/results/apron-calibration/</c>; the saved fitting file is never touched.
/// </summary>
internal static class ApronTestHooks
{
    private const float TargetGap = 0.025f;   // metres of cloth held off the skin
    private const float FloatCap = 0.15f;     // a vertex further than this is "floating", not "close"

    // Fit numbers the search may move, and how far from the starting fitting each may drift (share of its range).
    private static string[] Keys =
    {
        "RotPitch", "Flare", "SkirtBelly",
        "Curve", "SkirtCurve", "BibPitch", "SkirtPitch", "TopForward",
    };
    private const float Drift = 0.12f;
    private static bool _band;               // also score the waist tie against the body
    private const float BandGap = 0.012f;    // a tie lies close to the skin

    private static Dictionary<string, float>? _start;
    private static int _pass;
    private static int _evals;

    private static string Dir
    {
        get
        {
            var dir = Path.Combine(Paths.ConfigPath, "DrakesTestLab", "results", "apron-calibration");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static Player P => Player.m_localPlayer ?? throw new InvalidOperationException("no local player");

    private static ApronWatcher Watcher()
    {
        var w = P.GetComponent<ApronWatcher>() ?? throw new InvalidOperationException("no apron watcher on the player");
        w.RefreshNow();
        if (w.Current == null)
            throw new InvalidOperationException("the apron is not built: is one equipped as the shoulder item?");
        return w;
    }

    private sealed class Body
    {
        public readonly List<Vector3> Pos = new();
        public readonly List<Vector3> Normal = new();
        public readonly Dictionary<Vector3Int, List<int>> Grid = new();
        public const float Cell = 0.06f;

        public static Vector3Int Key(Vector3 p) =>
            new(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell), Mathf.FloorToInt(p.z / Cell));
    }

    private static Body BakeBody(Player player)
    {
        var body = new Body();
        foreach (var smr in player.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (smr.sharedMesh == null || smr.name.IndexOf("apron", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            var mesh = new Mesh();
            smr.BakeMesh(mesh);
            var matrix = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            var verts = mesh.vertices;
            var normals = mesh.normals;
            for (var i = 0; i < verts.Length; i++)
            {
                body.Pos.Add(matrix.MultiplyPoint3x4(verts[i]));
                body.Normal.Add(i < normals.Length ? matrix.MultiplyVector(normals[i]).normalized : Vector3.up);
            }
            UnityEngine.Object.Destroy(mesh);
        }
        for (var i = 0; i < body.Pos.Count; i++)
        {
            var key = Body.Key(body.Pos[i]);
            if (!body.Grid.TryGetValue(key, out var list))
                body.Grid[key] = list = new List<int>();
            list.Add(i);
        }
        return body;
    }

    internal sealed class Fit
    {
        public float Cost, MeanGap, Inside, Floating;
        public int Samples;
        public override string ToString() =>
            $"cost {Cost:0.000000} meanGap {MeanGap * 100f:0.0}cm inside {Inside * 100f:0}% floating {Floating * 100f:0}% ({Samples} pts)";
    }

    /// <summary>Distance of the apron's cloth and belt from the body surface, signed by the surface normal there.</summary>
    private static Fit Measure(ApronWatcher watcher, Body body)
    {
        var fit = new Fit();
        var apron = watcher.Current!;
        double total = 0, gapSum = 0;
        int inside = 0, floating = 0, n = 0;
        var parts = apron.GetComponent<ApronParts>();
        if (parts == null)
            throw new InvalidOperationException("the apron has no ApronParts");
        var cloth = new List<(Vector3[] verts, Func<Vector3, Vector3> world, float target)>();
        foreach (var go in parts.Parts.Where(p => p != null))
        {
            // Cloth only: the belt sits over the cloth and the straps stand off the body.
            if (go.name == "drake_apron_cloth" && go.GetComponent<SkinnedMeshRenderer>() is { } smr)
            {
                var baked = new Mesh();
                smr.BakeMesh(baked);
                var matrix = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                cloth.Add((baked.vertices, v => matrix.MultiplyPoint3x4(v), TargetGap));
                UnityEngine.Object.Destroy(baked);
            }
            else if (_band && go.name == "drake_apron_band" && go.GetComponent<MeshFilter>()?.sharedMesh is { } bm)
            {
                var bt = go.transform;
                cloth.Add((bm.vertices, v => bt.TransformPoint(v), BandGap));
            }
            else if ((go.name == "drake_apron_skirt" || go.name == "drake_apron_bib") && go.GetComponent<MeshFilter>()?.sharedMesh is { } m)
            {
                var t = go.transform;
                cloth.Add((m.vertices, v => t.TransformPoint(v), TargetGap));
            }
        }
        foreach (var (verts, world, target) in cloth)
        {
            for (var i = 0; i < verts.Length; i += 2)
            {
                var p = world(verts[i]);
                var c = Body.Key(p);
                var best = float.MaxValue;
                var bi = -1;
                for (var dx = -1; dx <= 1; dx++)
                for (var dy = -1; dy <= 1; dy++)
                for (var dz = -1; dz <= 1; dz++)
                {
                    if (!body.Grid.TryGetValue(new Vector3Int(c.x + dx, c.y + dy, c.z + dz), out var list))
                        continue;
                    foreach (var j in list)
                    {
                        var d = (body.Pos[j] - p).sqrMagnitude;
                        if (d < best)
                        {
                            best = d;
                            bi = j;
                        }
                    }
                }
                n++;
                if (bi < 0)
                {
                    floating++;
                    total += (FloatCap - target) * (FloatCap - target);
                    gapSum += FloatCap;
                    continue;
                }
                var s = Vector3.Dot(p - body.Pos[bi], body.Normal[bi]);
                if (s < 0f)
                    inside++;
                if (s > FloatCap)
                    floating++;
                s = Mathf.Min(s, FloatCap);
                var r = s - target;
                total += r < 0f ? 4f * r * r : r * r;   // cloth through the skin counts four times as much as cloth too far off
                gapSum += s;
            }
        }
        if (n == 0)
            throw new InvalidOperationException("the apron has no cloth mesh to measure");
        fit.Samples = n;
        fit.Cost = (float)(total / n);
        fit.MeanGap = (float)(gapSum / n);
        fit.Inside = inside / (float)n;
        fit.Floating = floating / (float)n;
        return fit;
    }

    private static Fit Now(out ApronWatcher watcher, out Body body)
    {
        watcher = Watcher();
        body = BakeBody(P);
        return Measure(watcher, body);
    }

    private static void Trace(string kind, string key, float from, float to, Fit fit)
    {
        var line = new StringBuilder();
        line.Append("{\"pass\":").Append(_pass).Append(",\"kind\":\"").Append(kind).Append('"');
        if (key.Length > 0)
            line.Append(",\"param\":\"").Append(key).Append("\",\"from\":").Append(from.ToString("0.####", CultureInfo.InvariantCulture))
                .Append(",\"to\":").Append(to.ToString("0.####", CultureInfo.InvariantCulture));
        line.Append(",\"cost\":").Append(fit.Cost.ToString("0.000000", CultureInfo.InvariantCulture))
            .Append(",\"meanGapCm\":").Append((fit.MeanGap * 100f).ToString("0.00", CultureInfo.InvariantCulture))
            .Append(",\"insidePct\":").Append((fit.Inside * 100f).ToString("0.0", CultureInfo.InvariantCulture))
            .Append(",\"floatingPct\":").Append((fit.Floating * 100f).ToString("0.0", CultureInfo.InvariantCulture)).Append('}');
        File.AppendAllText(Path.Combine(Dir, "trace.jsonl"), line + Environment.NewLine);
    }

    /// <summary>Forget earlier passes: the fitting as it is now is the starting point. Optional <c>file</c> loads numbers first.</summary>
    [TestHook("apron.begin")]
    private static string Begin(string file = "")
    {
        if (file.Length > 0)
            ApronTuning.LoadFrom(file);
        _start = ApronTuning.All.ToDictionary(p => p.Key, p => ApronTuning.Get(p.Key));
        _pass = 0;
        _evals = 0;
        File.WriteAllText(Path.Combine(Dir, "trace.jsonl"), "");
        var fit = Now(out _, out _);
        Trace("start", "", 0f, 0f, fit);
        return fit.ToString();
    }

    /// <summary>Choose which numbers the passes may move (comma list) and whether the waist tie is scored too.</summary>
    [TestHook("apron.use")]
    private static string Use(string keys, bool band = false)
    {
        Keys = keys.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(k => k.Trim()).ToArray();
        _band = band;
        foreach (var k in Keys)
            if (Array.FindIndex(ApronTuning.All, p => p.Key == k) < 0)
                throw new ArgumentException("no fitting number named " + k);
        return string.Join(",", Keys);
    }

    [TestHook("apron.measure")]
    private static float MeasureHook()
    {
        var fit = Now(out _, out _);
        ItemShopPlugin.LogSource?.LogInfo("[ApronCalib] " + fit);
        return fit.Cost;
    }

    [TestHook("apron.report")]
    private static string Report() => Now(out _, out _).ToString();

    [TestHook("apron.insidePct")]
    private static float InsidePct() => Now(out _, out _).Inside * 100f;

    [TestHook("apron.floatingPct")]
    private static float FloatingPct() => Now(out _, out _).Floating * 100f;

    [TestHook("apron.meanGapCm")]
    private static float MeanGapCm() => Now(out _, out _).MeanGap * 100f;

    /// <summary>One sweep over the fit numbers: try a step each way, keep what lowers the cost. Returns the cost afterwards.</summary>
    [TestHook("apron.pass")]
    private static float PassHook(float stepScale = 4f)
    {
        _start ??= ApronTuning.All.ToDictionary(p => p.Key, p => ApronTuning.Get(p.Key));
        _pass++;
        var fit = Now(out var watcher, out var body);
        var accepted = 0;
        foreach (var key in Keys)
        {
            var index = Array.FindIndex(ApronTuning.All, p => p.Key == key);
            var param = ApronTuning.All[index];
            var origin = _start[key];
            var reach = param.Unit == "m" ? 0.03f : param.Unit == "x" ? 0.3f : Drift * (param.Max - param.Min);   // lengths stay within 3 cm of the saved fitting
            var lo = Mathf.Max(param.Min, origin - reach);
            var hi = Mathf.Min(param.Max, origin + reach);
            foreach (var dir in new[] { 1f, -1f })
            {
                var from = ApronTuning.Get(index);
                var to = Mathf.Clamp(from + dir * stepScale * param.Step, lo, hi);
                if (Mathf.Approximately(to, from))
                    continue;
                // Keep going the same way while it keeps helping.
                var moved = false;
                for (var guard = 0; guard < 6; guard++)
                {
                    from = ApronTuning.Get(index);
                    to = Mathf.Clamp(from + dir * stepScale * param.Step, lo, hi);
                    if (Mathf.Approximately(to, from))
                        break;
                    ApronTuning.Set(index, to);
                    watcher.RefreshNow();
                    var trial = Measure(watcher, body = BakeBody(P));
                    _evals++;
                    if (trial.Cost < fit.Cost - 1e-8f)
                    {
                        Trace("accept", key, from, ApronTuning.Get(index), trial);
                        fit = trial;
                        accepted++;
                        moved = true;
                    }
                    else
                    {
                        ApronTuning.Set(index, from);
                        watcher.RefreshNow();
                        break;
                    }
                }
                if (moved)
                    break;
            }
        }
        Trace("pass", "", 0f, 0f, fit);
        ItemShopPlugin.LogSource?.LogInfo($"[ApronCalib] pass {_pass} (step x{stepScale}): {accepted} changes, {_evals} measurements so far; {fit}");
        return fit.Cost;
    }

    [TestHook("apron.set")]
    private static string SetHook(string key, float value)
    {
        ApronTuning.Set(key, value);
        Watcher();
        return $"{key} = {ApronTuning.Get(key)}";
    }

    /// <summary>Writes the current numbers to a file next to the traces (and, with <c>name</c>, a copy beside the saved fitting).</summary>
    [TestHook("apron.dump")]
    private static string Dump(string name = "calibrated")
    {
        var path = Path.Combine(Dir, name + ".json");
        ApronTuning.SaveTo(path);
        return path;
    }

    /// <summary>Opens the fitting room's camera at a view so the next <c>screenshot</c> step shows it (<c>open</c> false closes it).</summary>
    [TestHook("apron.view")]
    private static bool View(float yaw = 0f, float focus = 1.0f, float dist = 2.3f, float pitch = 8f, int tab = 0, bool open = true)
    {
        ApronTuner.SetView(open, yaw, focus, dist, pitch, tab);
        return ApronTuner.IsOpen == open;
    }

    // ---- the apron's carry weight and stamina bonuses ----

    private static SE_ShopkeeperApron Effect()
    {
        var se = P.GetSEMan().GetStatusEffect(ApronItem.EffectName.GetStableHashCode()) as SE_ShopkeeperApron;
        return se ?? throw new InvalidOperationException("the apron's status effect is not on the player: is an apron worn?");
    }

    /// <summary>Re-checks the condition now (the effect does it about once a second) and returns the carry bonus it is giving.</summary>
    [TestHook("apron.refresh")]
    private static float Refresh()
    {
        var se = Effect();
        se.UpdateStatusEffect(1.1f);
        return se.m_addMaxCarryWeight;
    }

    [TestHook("apron.carryBonus")]
    private static float CarryBonus() => Effect().m_addMaxCarryWeight;

    /// <summary>The run drain modifier (-0.1 = 10% less stamina).</summary>
    [TestHook("apron.staminaMod")]
    private static float StaminaMod() => Effect().m_runStaminaDrainModifier;

    [TestHook("apron.jumpMod")]
    private static float JumpMod() => Effect().m_jumpStaminaUseModifier;

    [TestHook("apron.tooltip")]
    private static string Tooltip() => Effect().GetTooltipString();

    /// <summary>Is the player where this condition applies (Always, UnderRoof, UnderRoofNotDungeon)?</summary>
    [TestHook("apron.covered")]
    private static bool Covered(string condition) =>
        SE_ShopkeeperApron.Covered(P, (ApronCondition)Enum.Parse(typeof(ApronCondition), condition, true));

    [TestHook("apron.sheltered")]
    private static bool Sheltered() => SE_ShopkeeperApron.Covered(P, ApronCondition.UnderRoof);

    /// <summary>The rule alone: condition, sheltered, dungeon.</summary>
    [TestHook("apron.decide")]
    private static bool Decide(string condition, bool sheltered, bool dungeon) =>
        SE_ShopkeeperApron.Decide((ApronCondition)Enum.Parse(typeof(ApronCondition), condition, true), sheltered, dungeon);

    /// <summary>Where the player stands and what the roof check says there (for working out why a test is not sheltered).</summary>
    [TestHook("apron.probe")]
    private static string Probe()
    {
        var p = P.transform.position;
        Cover.GetCoverForPoint(P.GetCenterPoint(), out var cover, out var under);
        var shelter = (bool?)HarmonyLib.AccessTools.Method(typeof(Player), "InShelter")?.Invoke(P, null);
        var msg = $"pos {p.x:0.0},{p.y:0.0},{p.z:0.0} cover {cover:0.00} underRoof {under} InShelter {shelter}";
        ItemShopPlugin.LogSource?.LogInfo("[ApronProbe] " + msg);
        return msg;
    }

    /// <summary>Walks the player to a spot under a real roof (the test house) by sampling the roof check round the nearest station.</summary>
    [TestHook("apron.goUnderRoof")]
    private static string GoUnderRoof()
    {
        CraftingStation? station = null;
        var best = 1e9f;
        foreach (var s in UnityEngine.Object.FindObjectsOfType<CraftingStation>())
        {
            var d = (s.transform.position - P.transform.position).sqrMagnitude;
            if (d < best) { best = d; station = s; }
        }
        if (station == null)
            throw new InvalidOperationException("no crafting station near (is the test house built?)");
        var origin = station.transform.position;
        Vector3? spot = null;
        for (var r = 0f; r <= 8f && spot == null; r += 0.5f)
            for (var a = 0; a < 360 && spot == null; a += 30)
            {
                var x = origin + Quaternion.Euler(0f, a, 0f) * Vector3.forward * r;
                var p = new Vector3(x.x, origin.y + 1f, x.z);
                Cover.GetCoverForPoint(p, out var cover, out var under);
                if (under && cover >= 0.9f)
                    spot = new Vector3(x.x, origin.y + 0.1f, x.z);
            }
        if (spot == null)
            throw new InvalidOperationException("no roofed spot within 8 m of " + station.name);
        P.transform.position = spot.Value;
        Physics.SyncTransforms();
        return $"{spot.Value.x:0.0},{spot.Value.y:0.0},{spot.Value.z:0.0}";
    }
}
