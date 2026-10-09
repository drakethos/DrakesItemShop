using UnityEngine;

namespace DrakesItemShop.Integration;

internal static class VanillaWardAdapter
{
    public static bool IsPermitted(Player player, Vector3 worldPos, bool requirePermitted)
    {
        if (!requirePermitted)
            return true;

        var area = FindPrivateArea(worldPos);
        if (area == null)
            return true;

        return Runtime.GameAccess.IsPermitted(area, player.GetPlayerID());
    }

    private static PrivateArea? FindPrivateArea(Vector3 pos)
    {
        PrivateArea? best = null;
        float bestDist = float.MaxValue;

        var areas = Object.FindObjectsOfType<PrivateArea>();
        foreach (var area in areas)
        {
            if (area == null || !area.isActiveAndEnabled)
                continue;

            float dist = Vector3.Distance(area.transform.position, pos);
            if (dist > area.m_radius + 2f)
                continue;

            if (dist < bestDist)
            {
                bestDist = dist;
                best = area;
            }
        }

        return best;
    }
}
