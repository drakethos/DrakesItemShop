using System;
using System.Reflection;
using UnityEngine;

namespace DrakesItemShop.Integration;

/// <summary>Soft adapter — no hard dependency on WardIsLove.</summary>
internal static class WardIsLoveAdapter
{
    private static bool _checked;
    private static MethodInfo? _checkAccess;

    public static bool TryIsPermitted(Player player, Vector3 worldPos)
    {
        if (!EnsureMethod())
            return false;

        try
        {
            var result = _checkAccess!.Invoke(null, new object[] { player, worldPos });
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
            return _checkAccess != null;
        _checked = true;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = asm.GetName().Name ?? "";
            if (name.IndexOf("WardIsLove", StringComparison.OrdinalIgnoreCase) < 0 &&
                name.IndexOf("WardIsLoveLite", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var type = asm.GetType("WardIsLove.API") ?? asm.GetType("WardIsLoveLite.API");
            if (type == null)
                continue;

            _checkAccess = type.GetMethod("CheckAccess", BindingFlags.Public | BindingFlags.Static)
                           ?? type.GetMethod("IsPermitted", BindingFlags.Public | BindingFlags.Static);
            break;
        }

        return _checkAccess != null;
    }
}
