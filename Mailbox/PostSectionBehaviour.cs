using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrakesItemShop.Mail;

/// <summary>
/// A post wall section. When it is first placed, its owner's client fills every cell with a real PO box (its own piece, chest,
/// label and flag) and tags the boxes with the section's wall id. The boxes can't be hammered off on their own; removing the
/// section takes them with it, and the section can't be removed while any of its boxes still holds mail.
/// </summary>
public class PostSectionBehaviour : MonoBehaviour
{
    public Vector3[] m_cells = Array.Empty<Vector3>();

    public const string BoxPrefab = "Drake_POBox";
    /// <summary>On a section: its wall id. On a PO box: the id of the section it belongs to (0 = a loose box).</summary>
    internal static readonly int WallIdKey = "drake_post_wall".GetStableHashCode();
    private static readonly int FilledKey = "drake_post_filled".GetStableHashCode();

    internal static readonly List<PostSectionBehaviour> Live = new();

    private ZNetView? _nview;

    public long WallId => _nview != null && _nview.IsValid() ? _nview.GetZDO().GetLong(WallIdKey) : 0L;

    private void Awake() => _nview = GetComponent<ZNetView>();

    private void OnEnable() => Live.Add(this);

    private void OnDisable() => Live.Remove(this);

    private void Start()
    {
        // The build ghost has no valid ZNetView, so only a placed section fills itself, and only once.
        if (_nview == null || !_nview.IsValid() || !_nview.IsOwner())
            return;
        var zdo = _nview.GetZDO();
        if (zdo.GetInt(FilledKey) != 0)
            return;

        var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(BoxPrefab) : null;
        if (prefab == null)
        {
            ItemShopPlugin.LogSource?.LogError($"[PostWall] {name}: prefab {BoxPrefab} is missing, the section stays empty.");
            return;
        }

        var wallId = zdo.GetLong(WallIdKey);
        if (wallId == 0)
        {
            wallId = ((long)UnityEngine.Random.Range(1, int.MaxValue) << 31) ^ DateTime.UtcNow.Ticks;
            zdo.Set(WallIdKey, wallId);
        }
        var creator = GetComponent<Piece>() is { } piece ? piece.GetCreator() : 0L;

        foreach (var cell in m_cells)
        {
            var box = Instantiate(prefab, transform.TransformPoint(cell), transform.rotation);
            if (box.GetComponent<ZNetView>() is { } boxView && boxView.IsValid())
                boxView.GetZDO().Set(WallIdKey, wallId);
            // The section's builder is the boxes' builder (the postmaster).
            if (creator != 0 && box.GetComponent<Piece>() is { } boxPiece)
                SetCreator(boxPiece, creator);
        }
        zdo.Set(FilledKey, 1);
        ItemShopPlugin.LogSource?.LogInfo($"[PostWall] {name}: filled {m_cells.Length} PO box(es), wall {wallId}.");
    }

    private static readonly System.Reflection.MethodInfo? SetCreatorMethod = typeof(Piece).GetMethod("SetCreator");

    /// <summary>Piece.SetCreator gained a platform user id on live Valheim (the stubs we build against have one argument): fill the rest with defaults.</summary>
    private static void SetCreator(Piece piece, long creator)
    {
        if (SetCreatorMethod == null)
            return;
        var parameters = SetCreatorMethod.GetParameters();
        var args = new object?[parameters.Length];
        args[0] = creator;
        for (var i = 1; i < args.Length; i++)
            args[i] = parameters[i].ParameterType.IsValueType ? Activator.CreateInstance(parameters[i].ParameterType) : null;
        SetCreatorMethod.Invoke(piece, args);
    }

    /// <summary>The PO boxes that belong to this section (only those loaded nearby, which is all of them: they share its spot).</summary>
    public IEnumerable<MailboxBehaviour> Boxes()
    {
        var id = WallId;
        if (id == 0)
            yield break;
        foreach (var box in MailboxBehaviour.Live)
            if (box != null && box.WallId == id)
                yield return box;
    }

    public static PostSectionBehaviour? Find(long wallId)
    {
        if (wallId == 0)
            return null;
        foreach (var section in Live)
            if (section != null && section.WallId == wallId)
                return section;
        return null;
    }
}
