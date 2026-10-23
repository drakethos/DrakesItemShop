using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DrakesItemShop.Apron;

/// <summary>
/// Measures the worn body (and whatever is worn over it) and sets the fitting numbers that follow its surface: depth, chest
/// bulge, hem flare, side wrap, width and sideways centring. It works in the apron's own frame (<see cref="ApronFrame"/>), so a
/// hip turned away from the player object doesn't throw it off. It is a starting point: the rest is done by hand.
/// </summary>
internal static class ApronAutoFit
{
    internal static string Run(Player player)
    {
        var root = player.transform;
        var vis = player.GetComponent<VisEquipment>() ?? player.GetComponentInChildren<VisEquipment>();
        var body = vis != null ? vis.m_bodyModel : null;
        if (vis == null || body == null || body.sharedMesh == null)
            return "Auto-fit: no body found.";

        var bones = body.bones;
        Transform? Find(params string[] names) =>
            bones.FirstOrDefault(b => b != null && names.Any(n => string.Equals(b.name, n, StringComparison.OrdinalIgnoreCase)));
        var hips = Find("Hips", "Pelvis");
        var spine2 = Find("Spine2") ?? Find("Spine1") ?? hips;
        var legL = Find("LeftUpLeg", "LeftUpperLeg") ?? hips;
        var legR = Find("RightUpLeg", "RightUpperLeg") ?? hips;
        if (hips == null || spine2 == null || legL == null || legR == null)
            return "Auto-fit: no spine / hip bones.";

        // The body and whatever is worn over it, as baked now, in the player's space.
        var verts = new List<Vector3>();
        foreach (var smr in player.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (smr.sharedMesh == null || smr.name.IndexOf("apron", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            var mesh = new Mesh();
            smr.BakeMesh(mesh);
            var matrix = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            foreach (var v in mesh.vertices)
                verts.Add(root.InverseTransformPoint(matrix.MultiplyPoint3x4(v)));
            UnityEngine.Object.Destroy(mesh);
        }
        if (verts.Count == 0)
            return "Auto-fit: the body has no mesh.";

        // The baked body might be turned half way round about the player's up axis: keep the way round that puts the hands, feet
        // and head bones nearest the surface.
        var points = bones.Where(b => b != null && (b.name.EndsWith("Hand") || b.name.EndsWith("ToeBase") || b.name == "Head"))
            .Select(b => root.InverseTransformPoint(b.position)).ToList();
        float Score(float sign)
        {
            var total = 0f;
            foreach (var point in points)
            {
                var best = float.MaxValue;
                foreach (var v in verts)
                {
                    var dx = v.x * sign - point.x;
                    var dy = v.y - point.y;
                    var dz = v.z * sign - point.z;
                    best = Mathf.Min(best, dx * dx + dy * dy + dz * dz);
                }
                total += Mathf.Sqrt(best);
            }
            return total / Mathf.Max(1, points.Count);
        }
        var asBaked = Score(1f);
        var turned = Score(-1f);
        var sign = asBaked <= turned ? 1f : -1f;
        ItemShopPlugin.LogSource?.LogInfo($"[Apron] auto-fit: extremity bones {asBaked:F3} m from the body as baked, {turned:F3} m turned half way; using {(sign > 0 ? "as baked" : "turned")}");
        if (sign < 0f)
            for (var i = 0; i < verts.Count; i++)
                verts[i] = new Vector3(-verts[i].x, verts[i].y, -verts[i].z);

        var toe = Find("LeftToeBase", "LeftToes", "LeftToe");
        var ankle = Find("LeftFoot");
        var hint = Vector3.forward;
        if (toe != null && ankle != null)
        {
            var d = root.InverseTransformPoint(toe.position) - root.InverseTransformPoint(ankle.position);
            d.y = 0f;
            if (d.sqrMagnitude > 1e-4f)
                hint = d.normalized;
        }

        float T(string key) => ApronTuning.Get(key);
        const float height = ApronBody.StandingHeight;
        var s = height / 1.85f;
        var top = ApronBody.BibTop * height + T("Lift") + T("TopExtra");
        var waist = ApronBody.Waist * height + T("Lift");
        var hem = ApronBody.Hem * height + T("Lift") - T("HemExtra");
        var frame = ApronFrame.Make(root, hips, spine2, legL, legR, Find("LeftLeg", "LeftLowerLeg"), Find("RightLeg", "RightLowerLeg"),
            waist, hint, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f);

        // Every vertex as (across, height, forward) in the apron frame.
        var local = new List<Vector3>(verts.Count);
        foreach (var v in verts)
        {
            var d = v - frame.O;
            var forward = v.y >= waist ? Vector3.Dot(d, frame.FT) : Vector3.Dot(d, frame.FS);
            local.Add(new Vector3(Vector3.Dot(d, frame.RT), v.y, forward));
        }

        // Front surface: the most forward point within a small window, or NaN where there is no body.
        float Probe(float x, float y)
        {
            var best = float.NaN;
            foreach (var v in local)
            {
                if (Mathf.Abs(v.x - x) < 0.04f && Mathf.Abs(v.y - y) < 0.04f && (float.IsNaN(best) || v.z > best))
                    best = v.z;
            }
            return best;
        }

        var front = Probe(0f, waist);
        if (float.IsNaN(front))
            return "Auto-fit: couldn't find the body at the waist.";

        var report = new List<string>();
        void Apply(string key, float value)
        {
            var index = Array.FindIndex(ApronTuning.All, p => p.Key == key);
            ApronTuning.Set(index, value);
            report.Add($"{key} {ApronTuning.Get(key):0.###}");
        }

        Apply("Depth", front);
        var fTop = Probe(0f, top - 0.03f);
        Apply("Bulge", float.IsNaN(fTop) ? T("Bulge") : fTop - front);
        var fHem = Probe(0f, hem + 0.05f);
        Apply("Flare", float.IsNaN(fHem) ? T("Flare") : fHem - front);
        Apply("SkirtBelly", 0f);

        var chest = Probe(0f, waist + 0.12f);
        var chestSide = Probe(0.15f, waist + 0.12f);
        if (!float.IsNaN(chest) && !float.IsNaN(chestSide) && chest > 0.02f)
            Apply("Curve", Mathf.Clamp((1f - chestSide / chest) / 0.25f, 0f, 3f));
        var thigh = Probe(0f, waist - 0.2f);
        var thighSide = Probe(0.15f, waist - 0.2f);
        if (!float.IsNaN(thigh) && !float.IsNaN(thighSide) && thigh > 0.02f)
            Apply("SkirtCurve", Mathf.Clamp((1f - thighSide / thigh) / 0.25f, 0f, 3f));

        // Edges of the torso at the waist, where the front surface falls away: gives the width and which way it is off-centre.
        float edgeRight = 0.2f, edgeLeft = -0.2f;
        for (var x = 0.08f; x < 0.45f; x += 0.02f)
        {
            var f = Probe(x, waist);
            if (float.IsNaN(f) || f < 0.25f * front)
                break;
            edgeRight = x;
        }
        for (var x = -0.08f; x > -0.45f; x -= 0.02f)
        {
            var f = Probe(x, waist);
            if (float.IsNaN(f) || f < 0.25f * front)
                break;
            edgeLeft = x;
        }
        var half = (edgeRight - edgeLeft) * 0.5f;
        Apply("Width", Mathf.Clamp(half * 0.95f / (0.205f * s), 0.4f, 3f));
        Apply("SideShift", (edgeRight + edgeLeft) * 0.5f);
        Apply("Clearance", 0.012f);

        // Bib up to just under the neck (BibToNeck): the loop and the bib top then meet.
        var neckBone = Find("Neck");
        if (neckBone != null)
        {
            var neckY = frame.ToIdeal(root.InverseTransformPoint(neckBone.position)).y;
            Apply("TopExtra", neckY - 0.07f - (ApronBody.BibTop * height + T("Lift")));
        }

        var text = "Auto-fit: " + string.Join(", ", report);
        ItemShopPlugin.LogSource?.LogInfo("[Apron] " + text);
        return text;
    }
}
