using UnityEngine;

namespace DrakesItemShop.DisplayCard;

/// <summary>Marks a spawned buy-card visual so the crosshair can hover/sell against it.</summary>
public class BuyCardTag : MonoBehaviour
{
    public int OfferIndex;
    public Container? Shop;

    private const float MaxDistance = 8f;
    private const float Padding = 0.04f;

    /// <summary>The buy card under the crosshair among cards parented under <paramref name="root"/>.</summary>
    public static BuyCardTag? UnderCrosshair(Transform root)
    {
        var camera = GameCamera.instance != null ? GameCamera.instance.GetComponent<Camera>() : Camera.main;
        if (camera == null)
            return null;
        var ray = new Ray(camera.transform.position, camera.transform.forward);

        BuyCardTag? best = null;
        var bestDistance = MaxDistance;
        foreach (var tag in root.GetComponentsInChildren<BuyCardTag>(true))
        {
            foreach (var renderer in tag.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled)
                    continue;
                var bounds = renderer.bounds;
                bounds.Expand(Padding);
                if (bounds.IntersectRay(ray, out var distance) && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = tag;
                }
            }
        }
        return best;
    }
}
