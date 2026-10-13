using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace DrakesItemShop.DisplayCard;

/// <summary>The four open-crate looks a stand can lay out for its buy offers (same meshes as the hammer crates).</summary>
internal sealed class CrateKind
{
    public readonly string Prefab;
    public readonly string Label;
    /// <summary>Floor cells it covers along the stand's length (the long crate is 2 m).</summary>
    public readonly int Cells;
    /// <summary>Stacks it holds, as its hammer twin's inventory does.</summary>
    public readonly int Stacks;
    public readonly int InvW, InvH;
    public readonly float Height;

    private CrateKind(string prefab, string label, int cells, int invW, int invH, float height)
    {
        Prefab = prefab;
        Label = label;
        Cells = cells;
        InvW = invW;
        InvH = invH;
        Stacks = invW * invH;
        Height = height;
    }

    public static readonly CrateKind[] All =
    {
        new("Drake_OpenCrate", "Crate", 1, 2, 2, 1f),
        new("Drake_OpenCrateSmallLow", "Low crate", 1, 2, 2, 0.5f),
        new("Drake_OpenCrateBigLow", "Long crate", 2, 4, 2, 0.5f),
        new("Drake_OpenCrateTall", "Tall crate", 1, 2, 4, 1.5f),
    };

    public static CrateKind? Find(string prefab) => All.FirstOrDefault(k => k.Prefab == prefab);

    public Sprite? Icon =>
        Jotunn.Managers.PieceManager.Instance.GetPiece(Prefab)?.Piece is { } piece ? piece.m_icon : null;
}

/// <summary>One crate in a stand's layout: which look, which floor cell (its first cell), and which bought item it holds.</summary>
internal readonly struct CratePlan
{
    public CratePlan(CrateKind kind, int col, int row, string holds)
    {
        Kind = kind;
        Col = col;
        Row = row;
        Holds = holds ?? "";
    }

    public CrateKind Kind { get; }
    public int Col { get; }
    public int Row { get; }
    /// <summary>Prefab of the buy offer's item this crate stores ("" = not assigned yet).</summary>
    public string Holds { get; }

    public bool Covers(int col, int row) => row == Row && col >= Col && col < Col + Kind.Cells;

    public CratePlan WithHolds(string holds) => new(Kind, Col, Row, holds);
}

/// <summary>
/// The 1 m floor grid around a stand where its buy crates go, and the saved crate layout. The grid is the stand's own footprint
/// plus <see cref="Ring"/> cells on every side; column 0 is the stocker's left (the stand's +Z end, like slot 0), row 0 the store
/// (door) side, the last row the customers' side.
/// </summary>
internal static class BuyCrates
{
    public const int Ring = 2;
    private const float Cell = 1f;
    private static readonly int Key = "drake_buy_crates".GetStableHashCode();

    public readonly struct Grid
    {
        public Grid(DisplayCaseBehaviour stand)
        {
            StandCols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(stand.m_floorZ1 - stand.m_floorZ0) + 0.05f));
            StandRows = Mathf.Max(1, Mathf.CeilToInt(stand.m_maxAcross + 0.35f));
            Cols = StandCols + 2 * Ring;
            Rows = StandRows + 2 * Ring;
            CenterZ = (stand.m_floorZ0 + stand.m_floorZ1) / 2f;
            CenterX = stand.m_floorX;
        }

        public int Cols { get; }
        public int Rows { get; }
        public int StandCols { get; }
        public int StandRows { get; }
        public float CenterZ { get; }
        public float CenterX { get; }

        public bool IsStand(int col, int row) =>
            col >= Ring && col < Ring + StandCols && row >= Ring && row < Ring + StandRows;

        public bool Inside(int col, int row) => col >= 0 && row >= 0 && col < Cols && row < Rows;

        /// <summary>Stand-local floor point under the middle of a crate starting at (<paramref name="col"/>, <paramref name="row"/>).</summary>
        public Vector3 Centre(int col, int row, int cells)
        {
            var mid = col + (cells - 1) / 2f;
            return new Vector3(CenterX + (row - (Rows - 1) / 2f) * Cell, 0f, CenterZ - (mid - (Cols - 1) / 2f) * Cell);
        }
    }

    public static List<CratePlan> All(ZDO? zdo)
    {
        var result = new List<CratePlan>();
        if (zdo == null)
            return result;
        foreach (var part in (zdo.GetString(Key) ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var f = part.Split(':');
            if (f.Length < 3 || CrateKind.Find(f[0]) is not { } kind)
                continue;
            if (!int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var col)
                || !int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var row))
                continue;
            result.Add(new CratePlan(kind, col, row, f.Length >= 4 ? f[3] : ""));
        }
        return result;
    }

    public static void SetAll(ZDO zdo, IEnumerable<CratePlan> crates) =>
        zdo.Set(Key, string.Join("|", crates.Select(c =>
            $"{c.Kind.Prefab}:{c.Col.ToString(CultureInfo.InvariantCulture)}:{c.Row.ToString(CultureInfo.InvariantCulture)}:{c.Holds}")));

    public static string Fingerprint(ZDO? zdo) => zdo?.GetString(Key) ?? "";

    /// <summary>Most crates a stand may lay out: one per display slot.</summary>
    public static int Max(Container container) =>
        Mathf.Max(1, container.GetInventory()?.GetWidth() ?? container.m_width);

    /// <summary>True when (<paramref name="col"/>, <paramref name="row"/>) can take a <paramref name="kind"/> crate (ignoring <paramref name="skip"/>).</summary>
    public static bool Fits(Grid grid, IReadOnlyList<CratePlan> crates, CrateKind kind, int col, int row, int skip = -1)
    {
        for (var c = col; c < col + kind.Cells; c++)
        {
            if (!grid.Inside(c, row) || grid.IsStand(c, row))
                return false;
            for (var i = 0; i < crates.Count; i++)
                if (i != skip && crates[i].Covers(c, row))
                    return false;
        }
        return true;
    }

    public static int IndexAt(IReadOnlyList<CratePlan> crates, int col, int row)
    {
        for (var i = 0; i < crates.Count; i++)
            if (crates[i].Covers(col, row))
                return i;
        return -1;
    }

    public static bool HasCrateFor(ZDO? zdo, string prefab) => All(zdo).Any(c => c.Holds == prefab);

    /// <summary>Units of <paramref name="prefab"/> the crates holding it can take in total (stacks × stack size).</summary>
    public static int Capacity(ZDO? zdo, string prefab)
    {
        var maxStack = Math.Max(1, ObjectDB.instance?.GetItemPrefab(prefab)?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_maxStackSize ?? 1);
        return All(zdo).Where(c => c.Holds == prefab).Sum(c => c.Kind.Stacks) * maxStack;
    }
}
