using System;
using System.Reflection;
using UnityEngine;

namespace DrakesItemShop.Integration;

/// <summary>Soft adapter — no hard dependency on Better Wards.</summary>
internal static class BetterWardsAdapter
{
    private static bool _checked;
    private static MethodInfo? _isPermitted;

    public static bool TryIsPermitted(Player player, Vector3 worldPos)
    {
        if (!EnsureMethod())
            return false;

        try
        {
            var result = _isPermitted!.Invoke(null, new object[] { player, worldPos });
            return result is bool b && b;
        }
        catch
        {
            return false;
        }
    }

    private static bool EnsureMethod()
    {
        if (_checked)
            return _isPermitted != null;
        _checked = true;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (asm.GetName().Name?.IndexOf("BetterWards", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var type = asm.GetType("BetterWards.BetterWards") ?? asm.GetType("BetterWards.API");
            if (type == null)
                continue;

            _isPermitted = type.GetMethod("IsPermitted", BindingFlags.Public | BindingFlags.Static)
                           ?? type.GetMethod("CheckAccess", BindingFlags.Public | BindingFlags.Static);
            break;
        }

        return _isPermitted != null;
    }
}
