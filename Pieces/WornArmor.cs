using System.Collections.Generic;
using UnityEngine;

namespace DrakesItemShop;

/// <summary>
/// Armor as an armor stand shows it (worn), baked once per item into plain static meshes so racks can hang it
/// cheaply: no mannequin, animator or skinned mesh per hook. Imitates what VisEquipment does when it equips armor
/// (body textures for chest and legs, "attach_skin" parts skinned to the body, "attach_&lt;joint&gt;" and helmet parts
/// on their joints) on one hidden, mesh-only copy of the vanilla armor stand's skeleton.
/// </summary>
internal static class WornArmor
{
    internal sealed class Part
    {
        public Mesh Mesh = null!;
        public Material[] Materials = System.Array.Empty<Material>();
        /// <summary>Relative to the stand's root (its feet).</summary>
        public Matrix4x4 Pose;
    }

    internal sealed class Look
    {
        public readonly List<Part> Parts = new();
        /// <summary>The head's attach point, relative to the stand's root.</summary>
        public Vector3 Head;
    }

    private static readonly Dictionary<string, Look?> Cache = new();
    private static GameObject? _stand;
    private static SkinnedMeshRenderer? _body;
    private static Transform? _helmet;
    private static Mesh? _bodyMesh;

    /// <summary>The item's worn look, baked on first use; null when it can't be (no armor stand, nothing to show).</summary>
    public static Look? Get(ItemDrop.ItemData item)
    {
        if (item.m_dropPrefab == null)
            return null;
        var key = item.m_dropPrefab.name + "|" + item.m_variant;
        if (Cache.TryGetValue(key, out var look))
            return look;
        look = EnsureStand() ? Bake(item) : null;
        Cache[key] = look;
        return look;
    }

    private static Look? Bake(ItemDrop.ItemData item)
    {
        var root = _stand!.transform;
        var look = new Look { Head = root.InverseTransformPoint(_helmet!.position) };
        var prefab = item.m_dropPrefab.transform;
        var type = item.m_shared.m_itemType;

        // Chest and legs paint the body (VisEquipment.SetChestEquipped / SetLegEquipped).
        if (type is ItemDrop.ItemData.ItemType.Chest or ItemDrop.ItemData.ItemType.Legs && item.m_shared.m_armorMaterial is { } armor)
        {
            var materials = _body!.sharedMaterials;
            var skin = new Material(materials[0]);
            var part = type == ItemDrop.ItemData.ItemType.Chest ? "Chest" : "Legs";
            foreach (var suffix in new[] { "Tex", "BumpMap", "Metal" })
            {
                var property = $"_{part}{suffix}";
                if (skin.HasProperty(property) && armor.HasProperty(property))
                    skin.SetTexture(property, armor.GetTexture(property));
            }
            materials[0] = skin;
            look.Parts.Add(new Part { Mesh = BodyMesh(), Materials = materials, Pose = Unscaled(root, _body.transform) });
        }

        if (type == ItemDrop.ItemData.ItemType.Helmet)
        {
            // VisEquipment.AttachItem on the helmet joint, with the item's "equipoffset".
            var attach = prefab.Find("attach") ?? prefab.Find("attach_skin");
            if (attach != null)
            {
                var offset = prefab.Find("equipoffset");
                Add(look, attach.gameObject, _helmet, offset != null ? offset.position : Vector3.zero,
                    offset != null ? offset.rotation : Quaternion.identity);
            }
        }
        else
        {
            // VisEquipment.AttachArmor: every "attach_*" child; "attach_skin" goes on the body, the rest on that joint.
            foreach (Transform child in prefab)
            {
                if (!child.name.StartsWith("attach_"))
                    continue;
                var joint = child.name == "attach_skin" ? _body!.transform.parent : FindChild(root, child.name.Substring(7));
                if (joint != null)
                    Add(look, child.gameObject, joint, Vector3.zero, Quaternion.identity);
            }
        }
        return look.Parts.Count > 0 ? look : null;
    }

    /// <summary>A stripped copy of <paramref name="source"/> at the joint; its skinned meshes use the body's bones, then everything is baked.</summary>
    private static void Add(Look look, GameObject source, Transform joint, Vector3 offset, Quaternion rotation)
    {
        var copy = Stripped(source);
        try
        {
            copy.transform.SetPositionAndRotation(joint.TransformPoint(offset), joint.rotation * rotation);
            copy.SetActive(true);
            var root = _stand!.transform;
            foreach (var renderer in copy.GetComponentsInChildren<Renderer>())
            {
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    if (source.name == "attach_skin")
                    {
                        skinned.rootBone = _body!.rootBone;
                        skinned.bones = _body.bones;
                    }
                    var mesh = new Mesh();
                    skinned.BakeMesh(mesh);
                    look.Parts.Add(new Part { Mesh = mesh, Materials = skinned.sharedMaterials, Pose = Unscaled(root, skinned.transform) });
                }
                else if (renderer is MeshRenderer && renderer.GetComponent<MeshFilter>()?.sharedMesh is { } shared)
                {
                    look.Parts.Add(new Part { Mesh = shared, Materials = renderer.sharedMaterials, Pose = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix });
                }
            }
        }
        finally
        {
            Object.Destroy(copy);
        }
    }

    /// <summary>BakeMesh output is in the renderer's space without its scale.</summary>
    private static Matrix4x4 Unscaled(Transform root, Transform renderer) =>
        root.worldToLocalMatrix * Matrix4x4.TRS(renderer.position, renderer.rotation, Vector3.one);

    private static Mesh BodyMesh()
    {
        if (_bodyMesh == null)
        {
            _bodyMesh = new Mesh();
            _body!.BakeMesh(_bodyMesh);
        }
        return _bodyMesh;
    }

    /// <summary>The hidden skeleton: a mesh-only copy of the vanilla armor stand, posed once, far below the world.</summary>
    private static bool EnsureStand()
    {
        if (_stand != null)
            return true;
        var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("ArmorStand") : null;
        var vis = prefab != null ? prefab.GetComponentInChildren<VisEquipment>(true) : null;
        if (prefab == null || vis == null || vis.m_bodyModel == null || vis.m_helmet == null)
            return false;
        var bodyPath = PathFrom(prefab.transform, vis.m_bodyModel.transform);
        var helmetPath = PathFrom(prefab.transform, vis.m_helmet);

        var stand = Stripped(prefab, keepAnimator: true);
        stand.name = "drake_rack_armor_skeleton";
        stand.transform.SetPositionAndRotation(new Vector3(0f, -5000f, 0f), Quaternion.identity);
        Object.DontDestroyOnLoad(stand);
        stand.SetActive(true);
        foreach (var renderer in stand.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = false;
        if (stand.GetComponentInChildren<Animator>() is { } animator)
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Update(0f);
        }

        _body = stand.transform.Find(bodyPath)?.GetComponent<SkinnedMeshRenderer>();
        _helmet = stand.transform.Find(helmetPath);
        if (_body == null || _helmet == null)
        {
            Object.Destroy(stand);
            return false;
        }
        _stand = stand;
        return true;
    }

    /// <summary>An inactive copy with no scripts, physics, cloth, sound or particles (made under an inactive parent, so nothing wakes up).</summary>
    private static GameObject Stripped(GameObject source, bool keepAnimator = false)
    {
        var parent = new GameObject("drake_rack_strip");
        parent.SetActive(false);
        var copy = Object.Instantiate(source, parent.transform);
        foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
            Object.DestroyImmediate(behaviour);
        foreach (var cloth in copy.GetComponentsInChildren<Cloth>(true))
            Object.DestroyImmediate(cloth);
        foreach (var joint in copy.GetComponentsInChildren<Joint>(true))
            Object.DestroyImmediate(joint);
        foreach (var body in copy.GetComponentsInChildren<Rigidbody>(true))
            Object.DestroyImmediate(body);
        foreach (var collider in copy.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(collider);
        foreach (var audio in copy.GetComponentsInChildren<AudioSource>(true))
            Object.DestroyImmediate(audio);
        foreach (var particles in copy.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (particles.GetComponent<ParticleSystemRenderer>() is { } renderer)
                Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(particles);
        }
        if (!keepAnimator)
            foreach (var animator in copy.GetComponentsInChildren<Animator>(true))
                Object.DestroyImmediate(animator);
        copy.SetActive(false);
        copy.transform.SetParent(null, false);
        Object.Destroy(parent);
        return copy;
    }

    private static string PathFrom(Transform root, Transform target)
    {
        var path = target.name;
        for (var t = target.parent; t != null && t != root; t = t.parent)
            path = t.name + "/" + path;
        return path;
    }

    private static Transform? FindChild(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name)
                return t;
        return null;
    }
}
