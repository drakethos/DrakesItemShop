using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DrakesItemShop.Apron;

/// <summary>Every player gets a watcher: when the apron is their shoulder item, the apron is built on their body.</summary>
[HarmonyPatch(typeof(Player), "Awake")]
internal static class ApronPlayerPatch
{
    [HarmonyPostfix]
    private static void Postfix(Player __instance)
    {
        if (__instance.GetComponent<ApronWatcher>() == null)
            __instance.gameObject.AddComponent<ApronWatcher>();
    }
}

/// <summary>
/// Watches what a player wears (the synced equipment hashes VisEquipment keeps, so other players' aprons show too) and builds or
/// removes the apron on their body; rebuilds it when the fitting numbers change.
/// </summary>
internal sealed class ApronWatcher : MonoBehaviour
{
    private static readonly FieldInfo ShoulderHash = AccessTools.Field(typeof(VisEquipment), "m_currentShoulderItemHash");

    private VisEquipment? _vis;
    private GameObject? _apron;
    private int _builtMesh, _builtVersion = -1, _builtHash;
    private float _next;

    private void Update()
    {
        if (Time.time < _next)
            return;
        _next = Time.time + 0.15f;
        if (ZNet.instance != null && ZNet.instance.IsDedicated())
            return;
        try
        {
            Refresh();
        }
        catch (Exception ex)
        {
            ItemShopPlugin.LogSource?.LogWarning($"[Apron] {ex}");
            _next = Time.time + 5f;
        }
    }

    /// <summary>Rebuilds at once if the numbers moved (automated fitting runs measure straight after a change).</summary>
    internal void RefreshNow() => Refresh();

    internal GameObject? Current => _apron;

    private void Refresh()
    {
        _vis ??= GetComponent<VisEquipment>() ?? GetComponentInChildren<VisEquipment>();
        if (_vis == null || ShoulderHash == null)
            return;
        var hash = (int)ShoulderHash.GetValue(_vis);
        if (!ApronStyles.ByHash.TryGetValue(hash, out var style))
        {
            if (_apron != null)
                Destroy(_apron);
            _apron = null;
            return;
        }

        var body = _vis.m_bodyModel;
        if (body == null || body.sharedMesh == null)
            return;
        var meshId = body.sharedMesh.GetInstanceID();
        if (_apron != null && hash == _builtHash && meshId == _builtMesh && _builtVersion == ApronTuning.Version)
            return;

        if (_apron != null)
            Destroy(_apron);
        _apron = ApronBody.Build(transform, _vis, body, style);
        _builtHash = hash;
        _builtMesh = meshId;
        _builtVersion = ApronTuning.Version;
    }

    private void OnDestroy()
    {
        if (_apron != null)
            Destroy(_apron);
    }
}

/// <summary>
/// Builds the apron on the live skeleton. The panel (bib and skirt), a belt and two crossed straps are laid out upright from
/// the numbers in <see cref="ApronTuning"/>, then bent onto the skeleton's own axes (<see cref="ApronFrame"/>: the bib along the
/// torso, the skirt along the legs, facing the way the hips face, which is not necessarily the way the player object faces) and
/// each part is fixed to one bone.
/// </summary>
internal static class ApronBody
{
    // Body proportions as fractions of a standing height of 1.76 m.
    internal const float StandingHeight = 1.76f;
    internal const float BibTop = 0.735f, Waist = 0.585f, Hem = 0.335f;

    internal static GameObject? Build(Transform player, VisEquipment vis, SkinnedMeshRenderer body, ApronStyle style)
    {
        var bones = body.bones;
        Transform? Find(params string[] names) =>
            bones.FirstOrDefault(b => b != null && names.Any(n => string.Equals(b.name, n, StringComparison.OrdinalIgnoreCase)));
        var hips = Find("Hips", "Pelvis");
        var spine1 = Find("Spine1") ?? Find("Spine") ?? hips;
        var spine2 = Find("Spine2") ?? spine1;
        var legL = Find("LeftUpLeg", "LeftUpperLeg") ?? hips;
        var legR = Find("RightUpLeg", "RightUpperLeg") ?? hips;
        if (hips == null || spine1 == null || spine2 == null || legL == null || legR == null)
        {
            ItemShopPlugin.LogSource?.LogWarning("[Apron] the body has no spine / hip bones I can use: " + string.Join(",", bones.Select(b => b.name).Take(20)));
            return null;
        }

        float T(string key) => ApronTuning.Get(key);
        float depth = T("Depth") + (style.Bib ? 0f : T("HalfDepth")), bulge = T("Bulge"), flare = T("Flare") + (style.Bib ? 0f : T("HalfFlare")), curve = T("Curve"), width = T("Width");
        float topForward = T("TopForward"), topZone = T("TopZone"), topWrap = T("TopWrapExtra");
        float skirtCurve = T("SkirtCurve"), belly = T("SkirtBelly"), foldAmp = T("FoldAmp"), foldCount = T("FoldCount"), puff = T("PocketPuff");
        float clearance = T("Clearance");

        // Which way the hips face in the player's space: the toes point there.
        var toe = Find("LeftToeBase", "LeftToes", "LeftToe");
        var ankle = Find("LeftFoot");
        var hint = Vector3.forward;
        if (toe != null && ankle != null)
        {
            var d = player.InverseTransformPoint(toe.position) - player.InverseTransformPoint(ankle.position);
            d.y = 0f;
            if (d.sqrMagnitude > 1e-4f)
                hint = d.normalized;
        }

        var height = StandingHeight;
        var s = height / 1.85f;
        var waist = Waist * height + T("Lift");
        var top = style.Bib ? BibTop * height + T("Lift") + T("TopExtra") : waist - 0.004f;
        var hem = Hem * height + T("Lift") - T("HemExtra") - style.HemExtra;

        var kneeL = Find("LeftLeg", "LeftLowerLeg");
        var kneeR = Find("RightLeg", "RightLowerLeg");
        var frame = ApronFrame.Make(player, hips, spine2, legL, legR, kneeL, kneeR, waist, hint, T("BibPitch"), T("SkirtPitch"), T("SideShift"), T("RotYaw"), T("RotPitch"), T("RotRoll"), T("Size"), T("SeamBlend"));

        // How far forward of the spine line the panel stands at (x, y). The upright layout has its axis at z = 0.
        float Forward(float x, float y)
        {
            var f = depth;
            float wrap;
            if (y > waist)
            {
                var along = Mathf.InverseLerp(waist, top, y);
                f += bulge * Mathf.SmoothStep(0f, 1f, along);
                // The top of the bib can be pulled back towards the neck (or pushed out) on its own.
                var zone = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - topZone, 1f, along));
                f += topForward * zone;
                wrap = curve + topWrap * zone;
            }
            else
            {
                var t = Mathf.InverseLerp(waist, hem, y);
                f += flare * Mathf.SmoothStep(0f, 1f, t) + belly * Mathf.Sin(Mathf.PI * t);
                wrap = skirtCurve;
            }
            return f * (1f - wrap * Mathf.Pow(Mathf.Clamp01(Mathf.Abs(x) / 0.3f), 2f));
        }

        // The cloth's own relief: soft vertical folds (deeper towards the hem) on top of the base shape.
        float Folds(float x, float y)
        {
            if (foldAmp <= 0f)
                return 0f;
            var t = Mathf.Clamp01(Mathf.InverseLerp(top, hem, y));
            var phase = Mathf.PI * foldCount * x / Mathf.Max(0.05f, 0.25f * s * width) + 0.7f * Mathf.Sin(y * 5f);
            return foldAmp * (0.25f + 0.75f * t) * Mathf.Sin(phase);
        }

        var zFront = Forward(0f, waist) + clearance;
        var zBack = -depth * 0.85f - clearance - T("BackDepth");

        var clothVerts = new List<Vector3>();
        var clothUv = new List<Vector2>();
        var clothNormals = new List<Vector3>();
        var leatherVerts = new List<Vector3>();
        var leatherUv = new List<Vector2>();
        var leatherNormals = new List<Vector3>();
        var leatherTris = new List<int>();

        // ---- panel: bib and skirt ----
        const int cols = 28, rows = 36;
        for (var r = 0; r <= rows; r++)
        {
            var y = Mathf.Lerp(hem, top, r / (float)rows);
            var half = HalfWidth(y, hem, waist, top, s * width, T("BibTopWidth"));
            for (var c = 0; c <= cols; c++)
            {
                var u = c / (float)cols;
                var x = Mathf.Lerp(-half, half, u);
                var v = (y - hem) / (top - hem);
                // The patch pocket stands out from the cloth (its outline in the texture is u .27-.73, v .16-.38).
                var pocket = Mathf.Clamp01(Mathf.Min(Mathf.Min(u - 0.27f, 0.73f - u), Mathf.Min(v - 0.16f, 0.38f - v)) / 0.04f);
                clothVerts.Add(new Vector3(x, y, Forward(x, y) + clearance + Folds(x, y) + puff * Mathf.SmoothStep(0f, 1f, pocket)));
                clothNormals.Add(new Vector3(x * 1.5f, 0f, 1f).normalized);
                clothUv.Add(new Vector2(u, v));
            }
        }

        // ---- waist ties: they leave the apron's two side edges at the waist and run round the body to the back ----
        var edgeHalf = HalfWidth(waist, hem, waist, top, s * width, T("BibTopWidth"));
        var beltA = style.Bib ? Mathf.Max(edgeHalf * 1.03f, 0.215f * s * width * T("BandWidth"))
            : Mathf.Max(edgeHalf, 0.215f * s * width * T("HalfBandWidth"));
        var beltCz = (zFront + zBack) * 0.5f;
        var beltB = (zFront - zBack) * 0.5f + 0.004f;
        {
            var halfBand = (style.Bib ? T("BandHeight") : T("HalfBandHeight")) * 0.5f;
            // Where the ellipse round the torso is as wide as the apron: the ties start there, not across the front of the cloth.
            var start = Mathf.Asin(Mathf.Clamp(edgeHalf / beltA, 0.2f, 0.98f));
            const int seg = 40, blend = 5;
            var baseIndex = leatherVerts.Count;
            for (var i = 0; i <= seg; i++)
            {
                var th = Mathf.Lerp(start, Mathf.PI * 2f - start, i / (float)seg);
                var onBody = new Vector3(beltA * Mathf.Sin(th), waist, beltCz + beltB * Mathf.Cos(th));
                // The first and last few steps ease from the cloth's edge onto the ellipse so the tie grows out of the apron.
                var k = Mathf.Min(i, seg - i);
                if (k < blend)
                {
                    var side = th < Mathf.PI ? 1f : -1f;
                    var edge = new Vector3(side * edgeHalf, waist, Forward(side * edgeHalf, waist) + clearance);
                    onBody = Vector3.Lerp(edge, onBody, Mathf.SmoothStep(0f, 1f, k / (float)blend));
                }
                var normal = new Vector3(Mathf.Sin(th) / beltA, 0f, Mathf.Cos(th) / beltB).normalized;
                foreach (var dy in new[] { -halfBand, halfBand })
                {
                    leatherVerts.Add(new Vector3(onBody.x, waist + dy, onBody.z));
                    leatherNormals.Add(normal);
                    leatherUv.Add(new Vector2(i / (float)seg * 5f, dy > 0 ? 1f : 0f));
                }
            }
            for (var i = 0; i < seg; i++)
            {
                var q = baseIndex + i * 2;
                AddQuad(leatherTris, q, q + 2, q + 3, q + 1);
            }
        }
        // ---- tied belt: a knot at the back with two tails hanging from it ----
        if (T("BeltTie") >= 0.5f)
        {
            var cz = beltCz;
            var b = beltB;
            var a = beltA;
            var th = Mathf.PI + T("TieSide");
            var knotAt = new Vector3(a * Mathf.Sin(th), waist, cz + b * Mathf.Cos(th));
            var outward = new Vector3(Mathf.Sin(th) / a, 0f, Mathf.Cos(th) / b).normalized;
            var along = new Vector3(outward.z, 0f, -outward.x);
            var k = T("TieKnot") * 0.5f;
            AddBox(leatherVerts, leatherNormals, leatherUv, leatherTris, knotAt + outward * (k * 0.5f), outward, along, k * 0.8f, k, k * 0.9f);
            foreach (var side in new[] { -1f, 1f })
            {
                var start = knotAt + outward * (k * 0.9f);
                var path = new List<Vector3>();
                const int steps = 8;
                for (var i = 0; i <= steps; i++)
                {
                    var tt = i / (float)steps;
                    path.Add(start + outward * (0.008f + 0.03f * tt) + along * (side * 0.05f * tt) + Vector3.down * (T("TieTail") * tt));
                }
                var baseIndex = leatherVerts.Count;
                for (var i = 0; i < path.Count; i++)
                {
                    var half = 0.02f + 0.006f * (i / (float)(path.Count - 1));
                    leatherVerts.Add(path[i] - along * half);
                    leatherVerts.Add(path[i] + along * half);
                    leatherNormals.Add(outward);
                    leatherNormals.Add(outward);
                    leatherUv.Add(new Vector2(i * 0.5f, 0f));
                    leatherUv.Add(new Vector2(i * 0.5f, 1f));
                }
                for (var i = 0; i < path.Count - 1; i++)
                {
                    var q = baseIndex + i * 2;
                    AddQuad(leatherTris, q, q + 2, q + 3, q + 1);
                }
            }
        }

        var bandVertexCount = leatherVerts.Count;

        // ---- crossed straps (the older style): from each bib corner over the shoulder, crossing down the back ----
        if (style.Bib && T("StrapStyle") >= 0.5f)
        {
            var cz = (zFront + zBack) * 0.5f;
            var spread = T("StrapSpread");
            var halfWidth = T("StrapWidth") * 0.5f;
            var shoulderX = T("StrapShoulder");
            var rise = T("StrapRise");
            var shoulderZ = cz + T("StrapShoulderDepth");
            var backHeight = T("StrapBackHeight");
            var backSpread = T("StrapBackSpread");
            var cross = T("StrapCross");
            foreach (var side in new[] { -1f, 1f })
            {
                var xs = side * spread;
                var shoulderY = top + rise;
                var k0 = new Vector3(xs, top - 0.01f, Forward(xs, top) + clearance + 0.004f);
                var k1 = new Vector3(side * shoulderX, shoulderY, shoulderZ);
                var k2 = new Vector3(Mathf.Lerp(side * backSpread * 0.5f, -side * backSpread * 0.4f, cross), top + backHeight, zBack - 0.004f);
                var k3 = new Vector3(Mathf.Lerp(side * backSpread, -side * backSpread, cross), waist, zBack - 0.004f);
                var path = Spline(new[] { k0, k0, k1, k2, k3, k3 }, 7);
                var baseIndex = leatherVerts.Count;
                for (var i = 0; i < path.Count; i++)
                {
                    var p = path[i];
                    var tangent = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                    var radial = (p - new Vector3(0f, Mathf.Min(p.y, shoulderY - 0.2f), cz)).normalized;
                    var across = Vector3.Cross(tangent, radial).normalized * halfWidth;
                    leatherVerts.Add(p - across);
                    leatherVerts.Add(p + across);
                    leatherNormals.Add(radial);
                    leatherNormals.Add(radial);
                    leatherUv.Add(new Vector2(i * 0.5f, 0f));
                    leatherUv.Add(new Vector2(i * 0.5f, 1f));
                }
                for (var i = 0; i < path.Count - 1; i++)
                {
                    var q = baseIndex + i * 2;
                    AddQuad(leatherTris, q, q + 2, q + 3, q + 1);
                }
            }
        }

        // ---- neck loop: a collar round the neck with a strap down each side to the bib corners ----
        if (style.Bib && T("StrapStyle") < 0.5f)
        {
            var neck = Find("Neck");
            var halfWidth = T("StrapWidth") * 0.5f;
            var r = T("NeckRadius");
            var c = neck != null ? frame.ToIdeal(player.InverseTransformPoint(neck.position)) : new Vector3(0f, top + 0.1f, (zFront + zBack) * 0.5f);
            c += new Vector3(0f, T("NeckLift"), T("NeckForward"));
            const int seg = 28;
            var baseIndex = leatherVerts.Count;
            for (var i = 0; i <= seg; i++)
            {
                var th = i / (float)seg * Mathf.PI * 2f;
                var x = c.x + r * Mathf.Sin(th);
                var z = c.z + r * 0.9f * Mathf.Cos(th);
                foreach (var dy in new[] { -halfWidth, halfWidth })
                {
                    leatherVerts.Add(new Vector3(x, c.y + dy, z));
                    leatherNormals.Add(new Vector3(Mathf.Sin(th) / r, 0f, Mathf.Cos(th) / (r * 0.9f)).normalized);
                    leatherUv.Add(new Vector2(i / (float)seg * 3f, dy > 0 ? 1f : 0f));
                }
            }
            for (var i = 0; i < seg; i++)
            {
                var q = baseIndex + i * 2;
                AddQuad(leatherTris, q, q + 2, q + 3, q + 1);
            }

            var bibTop = T("BibTopWidth");
            foreach (var side in new[] { -1f, 1f })
            {
                var k0 = new Vector3(side * bibTop, top - 0.01f, Forward(side * bibTop, top) + clearance + 0.004f);
                var angle = T("NeckAngle") * side;
                var k1 = new Vector3(c.x + r * Mathf.Sin(angle), c.y, c.z + r * 0.9f * Mathf.Cos(angle));
                var path = Spline(new[] { k0, k0, k1, k1 }, 8);
                var strapBase = leatherVerts.Count;
                for (var i = 0; i < path.Count; i++)
                {
                    var p = path[i];
                    var tangent = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                    var radial = (p - new Vector3(c.x, Mathf.Min(p.y, c.y - 0.1f), c.z)).normalized;
                    var across = Vector3.Cross(tangent, radial).normalized * halfWidth;
                    leatherVerts.Add(p - across);
                    leatherVerts.Add(p + across);
                    leatherNormals.Add(radial);
                    leatherNormals.Add(radial);
                    leatherUv.Add(new Vector2(i * 0.5f, 0f));
                    leatherUv.Add(new Vector2(i * 0.5f, 1f));
                }
                for (var i = 0; i < path.Count - 1; i++)
                {
                    var q = strapBase + i * 2;
                    AddQuad(leatherTris, q, q + 2, q + 3, q + 1);
                }
            }
        }

        var clothIdealY = clothVerts.Select(v => v.y).ToList();

        // ---- bend everything onto the skeleton ----
        for (var i = 0; i < clothVerts.Count; i++)
        {
            clothNormals[i] = frame.Normal(clothVerts[i], clothNormals[i]);
            clothVerts[i] = frame.Point(clothVerts[i]);
        }
        for (var i = 0; i < leatherVerts.Count; i++)
        {
            leatherNormals[i] = frame.Normal(leatherVerts[i], leatherNormals[i]);
            leatherVerts[i] = frame.Point(leatherVerts[i]);
        }

        // ---- parts, each fixed to one bone ----
        var holder = new GameObject("drake_apron") { layer = body.gameObject.layer };
        holder.transform.SetParent(player, false);
        var parts = holder.AddComponent<ApronParts>();
        var clothMaterial = Cloth(ApronArt.Cloth(style));
        var leatherMaterial = Cloth(ApronArt.Leather(style));

        // The panel's rows split at the waist: the bib hangs from the chest, the skirt from the hips.
        var waistRow = Mathf.Clamp(Mathf.RoundToInt((waist - hem) / (top - hem) * rows), 1, rows - 1);
        var skirtTris = new List<int>();
        var bibTris = new List<int>();
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var a0 = r * (cols + 1) + c;
                AddQuad(r < waistRow ? skirtTris : bibTris, a0, a0 + 1, a0 + cols + 2, a0 + cols + 1);
            }
        }
        var bandTris = new List<int>();
        var strapTris = new List<int>();
        for (var i = 0; i < leatherTris.Count; i++)
            (leatherTris[i] < bandVertexCount ? bandTris : strapTris).Add(leatherTris[i]);

        var layer = body.gameObject.layer;
        var stretch = style.Bib ? T("SeamStretch") : 0f;
        if (stretch > 0.001f)
        {
            // One piece of cloth whose waist is skinned across hips, belly and chest: it bends and stretches there instead of
            // splitting into a bib and a skirt that slide over each other.
            var cloth = new List<int>(skirtTris);
            cloth.AddRange(bibTris);
            parts.Add(SkinnedCloth("drake_apron_cloth", holder.transform, new[] { hips, spine1, spine2 }, clothVerts, clothUv, clothNormals,
                cloth, clothMaterial, layer, waist, stretch, clothIdealY));
        }
        else
        {
            parts.Add(Part("drake_apron_skirt", hips, player, clothVerts, clothUv, clothNormals, skirtTris, clothMaterial, layer));
            parts.Add(Part("drake_apron_bib", spine2, player, clothVerts, clothUv, clothNormals, bibTris, clothMaterial, layer));
        }
        parts.Add(Part("drake_apron_band", spine1, player, leatherVerts, leatherUv, leatherNormals, bandTris, leatherMaterial, layer));
        parts.Add(Part("drake_apron_straps", spine2, player, leatherVerts, leatherUv, leatherNormals, strapTris, leatherMaterial, layer));
        return holder;
    }

    private static GameObject Part(string name, Transform bone, Transform player, List<Vector3> verts, List<Vector2> uv,
        List<Vector3> normals, List<int> tris, Material material, int layer)
    {
        var go = new GameObject(name) { layer = layer };
        go.transform.SetParent(bone, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        var local = new List<Vector3>(verts.Count);
        var localNormals = new List<Vector3>(verts.Count);
        for (var i = 0; i < verts.Count; i++)
        {
            local.Add(bone.InverseTransformPoint(player.TransformPoint(verts[i])));
            localNormals.Add(bone.InverseTransformDirection(player.TransformDirection(normals[i])).normalized);
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(local);
        mesh.SetUVs(0, uv);
        mesh.SetNormals(localNormals);
        mesh.SetTriangles(Doubled(tris), 0);
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        return go;
    }

    /// <summary>
    /// The cloth as one skinned mesh. Vertices are in the player's space (the holder sits at the player's origin). Below the waist
    /// they follow the hips, above it the chest, and across <paramref name="stretch"/> metres either side of the waist the weight
    /// slides hips, then belly, then chest, so the cloth bends with the body instead of tearing at the seam.
    /// </summary>
    private static GameObject SkinnedCloth(string name, Transform holder, Transform[] bones, List<Vector3> verts, List<Vector2> uv,
        List<Vector3> normals, List<int> tris, Material material, int layer, float waist, float stretch, List<float> idealY)
    {
        var go = new GameObject(name) { layer = layer };
        go.transform.SetParent(holder, false);
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uv);
        mesh.SetNormals(normals);
        mesh.SetTriangles(Doubled(tris), 0);

        // The panel was laid out upright (y = height) before it was bent onto the skeleton, so the weight follows that height.
        var weights = new BoneWeight[verts.Count];
        for (var i = 0; i < verts.Count; i++)
        {
            var y = idealY[i];
            var t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(waist - stretch, waist + stretch, y));
            var w0 = Mathf.Clamp01(1f - 2f * t);
            var w2 = Mathf.Clamp01(2f * t - 1f);
            weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = w0, boneIndex1 = 1, weight1 = 1f - w0 - w2, boneIndex2 = 2, weight2 = w2 };
        }
        mesh.boneWeights = weights;
        var bind = new Matrix4x4[bones.Length];
        for (var i = 0; i < bones.Length; i++)
            bind[i] = bones[i].worldToLocalMatrix * go.transform.localToWorldMatrix;
        mesh.bindposes = bind;
        mesh.RecalculateBounds();

        var renderer = go.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = mesh;
        renderer.sharedMaterial = material;
        renderer.bones = bones;
        renderer.rootBone = bones[0];
        renderer.updateWhenOffscreen = true;
        return go;
    }

    private static Material Cloth(Texture texture)
    {
        var template = ApronItem.ClothMaterial;
        var material = template != null ? new Material(template) : new Material(Shader.Find("Standard") ?? Shader.Find("Sprites/Default"));
        material.name = "drake_apron";
        material.color = Color.white;
        material.mainTexture = texture;
        // No inherited normal map, gloss map or glow from the borrowed material.
        foreach (var property in new[] { "_BumpMap", "_MetallicGlossMap", "_EmissionMap", "_NormalTex", "_MetalGlossTex" })
            if (material.HasProperty(property))
                material.SetTexture(property, null);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", Color.black);
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", 0.05f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        return material;
    }

    private static float HalfWidth(float y, float hem, float waist, float top, float s, float bibTop)
    {
        if (y >= waist)
        {
            var t = Mathf.Clamp01((y - waist) / Mathf.Max(0.01f, top - waist));
            return Mathf.Lerp(s * 0.205f, bibTop, Mathf.SmoothStep(0f, 1f, t));
        }
        var d = Mathf.Clamp01((waist - y) / Mathf.Max(0.01f, waist - hem));
        return s * Mathf.Lerp(0.205f, 0.245f, d);
    }

    /// <summary>A small box (the belt knot) with flat faces. <paramref name="forward"/> and <paramref name="right"/> are its horizontal axes.</summary>
    private static void AddBox(List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris, Vector3 center,
        Vector3 forward, Vector3 right, float halfForward, float halfUp, float halfRight)
    {
        var up = Vector3.up;
        var axes = new[] { (forward, halfForward), (-forward, halfForward), (right, halfRight), (-right, halfRight), (up, halfUp), (-up, halfUp) };
        foreach (var (normal, extent) in axes)
        {
            // Two axes across the face.
            var u = Mathf.Abs(Vector3.Dot(normal, up)) > 0.9f ? forward : up;
            var v = Vector3.Cross(normal, u).normalized;
            u = Vector3.Cross(v, normal).normalized;
            var faceCenter = center + normal * extent;
            var du = u * (Mathf.Abs(Vector3.Dot(u, up)) > 0.9f ? halfUp : Mathf.Abs(Vector3.Dot(u, forward)) > 0.9f ? halfForward : halfRight);
            var dv = v * (Mathf.Abs(Vector3.Dot(v, up)) > 0.9f ? halfUp : Mathf.Abs(Vector3.Dot(v, forward)) > 0.9f ? halfForward : halfRight);
            var baseIndex = verts.Count;
            verts.Add(faceCenter - du - dv);
            verts.Add(faceCenter + du - dv);
            verts.Add(faceCenter + du + dv);
            verts.Add(faceCenter - du + dv);
            for (var i = 0; i < 4; i++)
                normals.Add(normal);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(0f, 1f));
            AddQuad(tris, baseIndex, baseIndex + 1, baseIndex + 2, baseIndex + 3);
        }
    }

    private static void AddQuad(List<int> tris, int a, int b, int c, int d)
    {
        tris.Add(a); tris.Add(b); tris.Add(c);
        tris.Add(a); tris.Add(c); tris.Add(d);
    }

    /// <summary>The triangles plus their reverses, so the cloth shows from both sides (edges, the back of the straps).</summary>
    private static List<int> Doubled(List<int> tris)
    {
        var all = new List<int>(tris.Count * 2);
        for (var i = 0; i < tris.Count; i += 3)
        {
            all.Add(tris[i]);
            all.Add(tris[i + 1]);
            all.Add(tris[i + 2]);
        }
        for (var i = 0; i < tris.Count; i += 3)
        {
            all.Add(tris[i]);
            all.Add(tris[i + 2]);
            all.Add(tris[i + 1]);
        }
        return all;
    }

    private static List<Vector3> Spline(Vector3[] knots, int perSegment)
    {
        var points = new List<Vector3>();
        for (var i = 1; i < knots.Length - 2; i++)
        {
            for (var step = 0; step < perSegment; step++)
            {
                var t = step / (float)perSegment;
                points.Add(CatmullRom(knots[i - 1], knots[i], knots[i + 1], knots[i + 2], t));
            }
        }
        points.Add(knots[knots.Length - 2]);
        return points;
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }
}

/// <summary>
/// Bends an upright panel (x across, y up, z forward from the spine line; y in the player's height) onto the skeleton: above the
/// waist along the torso's own axis, below it along the legs.
/// </summary>
internal sealed class ApronFrame
{
    public Vector3 O, UT, RT, FT, DL, FS;
    public float Waist, UTy, DLy, Size = 1f, Seam;
    public Quaternion Turn = Quaternion.identity;

    public static ApronFrame Make(Transform player, Transform hips, Transform spine2, Transform legL, Transform legR,
        Transform? kneeL, Transform? kneeR, float waist, Vector3 forwardHint, float bibPitch, float skirtPitch, float sideShift,
        float yaw, float pitch, float roll, float size, float seam)
    {
        var hipsP = player.InverseTransformPoint(hips.position);
        var chestP = player.InverseTransformPoint(spine2.position);
        var line = (chestP - hipsP).normalized;
        var ut = line;
        if (ut.y < 0.5f)
            ut = Vector3.up;
        var rt = player.InverseTransformPoint(legR.position) - player.InverseTransformPoint(legL.position);
        rt -= Vector3.Dot(rt, ut) * ut;
        rt = rt.sqrMagnitude < 1e-6f ? Vector3.right : rt.normalized;
        var ft = Vector3.Cross(rt, ut);
        if (Vector3.Dot(ft, forwardHint) < 0f)
            ft = -ft;

        var dl = Vector3.down;
        if (kneeL != null && kneeR != null)
        {
            dl = ((player.InverseTransformPoint(kneeL.position) - player.InverseTransformPoint(legL.position))
                  + (player.InverseTransformPoint(kneeR.position) - player.InverseTransformPoint(legR.position))).normalized;
            if (dl.y > -0.5f)
                dl = Vector3.down;
        }
        var fs = Vector3.Cross(rt, dl);
        if (Vector3.Dot(fs, forwardHint) < 0f)
            fs = -fs;

        // Tilts: swing the bib's top / the skirt's hem forward (+) or back (-) about the across axis.
        var bibTurn = Quaternion.AngleAxis(-bibPitch, rt);
        ut = bibTurn * ut;
        ft = bibTurn * ft;
        var skirtTurn = Quaternion.AngleAxis(skirtPitch, rt);
        dl = skirtTurn * dl;
        fs = skirtTurn * fs;

        var t = line.y > 0.1f ? (waist - hipsP.y) / line.y : 0f;
        var origin = hipsP + line * t + rt * sideShift;
        return new ApronFrame
        {
            O = origin, UT = ut, RT = rt, FT = ft, DL = dl, FS = fs, Waist = waist,
            UTy = Mathf.Max(0.3f, ut.y), DLy = Mathf.Max(0.3f, -dl.y),
            Turn = Quaternion.AngleAxis(yaw, ut) * Quaternion.AngleAxis(pitch, rt) * Quaternion.AngleAxis(roll, ft), Size = size, Seam = seam,
        };
    }

    // Above the seam the apron follows the torso, below it the legs; across the seam the two are mixed so the waist doesn't crease.
    private float LegWeight(float y) =>
        Seam <= 0.001f ? (y >= Waist ? 0f : 1f) : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Waist + Seam, Waist - Seam, y));

    public Vector3 Point(Vector3 p)
    {
        var torso = O + RT * p.x + UT * ((p.y - Waist) / UTy) + FT * p.z;
        var leg = O + RT * p.x + DL * ((Waist - p.y) / DLy) + FS * p.z;
        var at = Vector3.Lerp(torso, leg, LegWeight(p.y));
        // The whole apron turns and scales about the waist point on the spine line.
        return O + Turn * ((at - O) * Size);
    }

    public Vector3 Normal(Vector3 p, Vector3 n)
    {
        var torso = RT * n.x + UT * n.y + FT * n.z;
        var leg = RT * n.x - DL * n.y + FS * n.z;
        return (Turn * Vector3.Lerp(torso, leg, LegWeight(p.y))).normalized;
    }

    /// <summary>Where a point in the player's space sits in the upright layout (the reverse of <see cref="Point"/> above the seam).</summary>
    public Vector3 ToIdeal(Vector3 playerSpace)
    {
        var d = Quaternion.Inverse(Turn) * (playerSpace - O) / Mathf.Max(0.01f, Size);
        return new Vector3(Vector3.Dot(d, RT), Waist + Vector3.Dot(d, UT) * UTy, Vector3.Dot(d, FT));
    }
}

/// <summary>Owns the apron's rigid parts (they sit under bones, not under this object) and removes them with it.</summary>
internal sealed class ApronParts : MonoBehaviour
{
    private readonly List<GameObject> _parts = new();

    public void Add(GameObject part) => _parts.Add(part);

    internal IReadOnlyList<GameObject> Parts => _parts;

    private void OnDestroy()
    {
        foreach (var part in _parts)
            if (part != null)
                Destroy(part);
    }
}
