using BepInEx.Configuration;

namespace DrakesItemShop.Storefront;

/// <summary>Local (per player) settings for the shopfront pieces.</summary>
internal static class StorefrontSettings
{
    private const string Section = "08 Storefront";

    private static ConfigEntry<float> _doorRange = null!;
    private static ConfigEntry<float> _alertRange = null!;
    private static ConfigEntry<bool> _doorMessage = null!;

    /// <summary>How far from a door bell a door counts as its door (metres).</summary>
    internal static float DoorRange => _doorRange?.Value ?? 7f;

    /// <summary>How far from a door bell you still get "A customer has arrived" (metres).</summary>
    internal static float AlertRange => _alertRange?.Value ?? 30f;

    /// <summary>Show the "A customer has arrived" card (the jingle always plays).</summary>
    internal static bool DoorMessage => _doorMessage?.Value ?? true;

    internal static void Bind(ConfigFile config)
    {
        _doorRange = config.Bind(Section, "Door bell door range", 7f,
            new ConfigDescription("A door this close to a door bell (metres) makes it ring when it opens.", new AcceptableValueRange<float>(2f, 20f)));
        _alertRange = config.Bind(Section, "Door bell message range", 30f,
            new ConfigDescription("How close to the door bell you must be to get the \"A customer has arrived\" message (metres).", new AcceptableValueRange<float>(5f, 80f)));
        _doorMessage = config.Bind(Section, "Door bell message", true,
            "Show \"A customer has arrived\" when a door near a door bell opens (you never get it for a door you are opening yourself). The jingle plays either way.");
    }
}
