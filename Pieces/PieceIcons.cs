using Jotunn.Managers;
using UnityEngine;

namespace DrakesItemShop.Pieces;

/// <summary>
/// Hammer icons rendered from the dressed piece itself (Jotunn's RenderManager), so cloned pieces don't show the
/// chest they were made from.
/// </summary>
internal static class PieceIcons
{
    /// <summary>
    /// Renders <paramref name="piece"/> and makes it its icon. <paramref name="yaw"/> turns it before the camera, which
    /// looks at the piece's +Z side; it's tipped forward a little so the top shows too.
    /// </summary>
    public static void Render(Piece piece, float yaw)
    {
        // RenderManager skips inactive targets, and Jotunn keeps its prefabs under an inactive container: render a
        // mesh-only copy instead (made under an inactive parent, so nothing on it ever wakes up).
        var target = MeshOnlyCopy(piece.gameObject);
        try
        {
            var request = new RenderManager.RenderRequest(target)
            {
                Width = 128,
                Height = 128,
                Rotation = Quaternion.Euler(18f, 0f, 0f) * Quaternion.Euler(0f, yaw, 0f),
            };
            if (RenderManager.Instance.Render(request) is { } sprite)
                piece.m_icon = sprite;
            else
                ItemShopPlugin.LogSource?.LogWarning($"Couldn't render an icon for {piece.name}.");
        }
        finally
        {
            Object.DestroyImmediate(target);
        }
    }

    private static GameObject MeshOnlyCopy(GameObject source)
    {
        var parent = new GameObject("drake_icon_parent");
        parent.SetActive(false);
        var copy = Object.Instantiate(source, parent.transform);
        // Scripts first, then whatever they depended on; renderers, mesh filters and transforms stay.
        foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
            Object.DestroyImmediate(behaviour);
        foreach (var joint in copy.GetComponentsInChildren<Joint>(true))
            Object.DestroyImmediate(joint);
        foreach (var body in copy.GetComponentsInChildren<Rigidbody>(true))
            Object.DestroyImmediate(body);
        foreach (var component in copy.GetComponentsInChildren<Component>(true))
            if (component is not (Transform or MeshFilter or MeshRenderer or SkinnedMeshRenderer))
                Object.DestroyImmediate(component);
        copy.transform.SetParent(null, false);
        copy.transform.position = new Vector3(0f, -5000f, 0f);
        Object.DestroyImmediate(parent);
        copy.SetActive(true);
        return copy;
    }
}
