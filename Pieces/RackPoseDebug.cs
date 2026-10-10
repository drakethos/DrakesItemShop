using BepInEx.Configuration;
using UnityEngine;

namespace DrakesItemShop;

/// <summary>
/// Local, default-off helper for tuning rack poses live: while it's on, pointing at a weapon on a rack shows its pose and a few
/// keys change it for that one item (saved to the YAML overrides) or for its whole weapon type (saved to the config). Racks
/// redraw as you change things. Not synced; nothing is sent to other players.
/// </summary>
internal static class RackPoseDebug
{
    private const string Section = "05 Rack pose debug";

    private static ConfigEntry<bool> _enabled = default!;
    private static ConfigEntry<KeyCode> _previousField = default!, _nextField = default!, _increase = default!, _decrease = default!,
        _toggleTarget = default!, _reset = default!;

    private static int _field = 2;                       // Offset Y: the usual first thing to nudge
    private static bool _itemTarget = true;
    private static int _handledFrame = -1;

    internal static bool Enabled => _enabled.Value;

    internal static void Bind(ConfigFile config)
    {
        _enabled = config.Bind(Section, "Enabled", false,
            "Rack pose tuning: point at a weapon on a weapon rack to see its pose; the keys below change it live. Local to you, not synced.");
        _previousField = config.Bind(Section, "Previous field key", KeyCode.Home, "Select the previous setting.");
        _nextField = config.Bind(Section, "Next field key", KeyCode.End, "Select the next setting.");
        _increase = config.Bind(Section, "Increase key", KeyCode.PageUp, "Raise the selected setting (hold Shift for 10x, Ctrl for 0.1x).");
        _decrease = config.Bind(Section, "Decrease key", KeyCode.PageDown, "Lower the selected setting (hold Shift for 10x, Ctrl for 0.1x).");
        _toggleTarget = config.Bind(Section, "Item or type key", KeyCode.Insert, "Switch between changing this one item and its whole weapon type.");
        _reset = config.Bind(Section, "Reset key", KeyCode.Delete, "Put the selected setting back (an item's override is dropped).");
    }

    /// <summary>Handle the keys for the weapon being pointed at and return the lines to add to its hover text ("" for none).</summary>
    internal static string Update(ItemDrop.ItemData item)
    {
        if (!_enabled.Value)
            return "";
        var (prefab, shared) = ItemRackBehaviour.LookOf(item);
        if (!ItemRackBehaviour.HasPose(item))
            return "";
        var name = prefab.name;
        var group = RackPoseSettings.GroupOf(name, shared);

        if (_handledFrame != Time.frameCount)
        {
            _handledFrame = Time.frameCount;
            var fields = RackPoseSettings.FieldNames.Length;
            var multiplier = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 10f
                : Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ? 0.1f : 1f;
            if (Input.GetKeyDown(_previousField.Value))
                _field = (_field + fields - 1) % fields;
            if (Input.GetKeyDown(_nextField.Value))
                _field = (_field + 1) % fields;
            if (Input.GetKeyDown(_toggleTarget.Value))
                _itemTarget = !_itemTarget;
            if (Input.GetKeyDown(_increase.Value))
                Change(name, group, 1, multiplier);
            if (Input.GetKeyDown(_decrease.Value))
                Change(name, group, -1, multiplier);
            if (Input.GetKeyDown(_reset.Value))
                RackPoseSettings.Reset(name, group, _itemTarget, _field);
        }

        if (!RackPoseSettings.CanEdit)
            return $"\n<color=#ffcc33>Rack pose</color> {name} - {group}\nSynced from the server: only the host or server can change pose settings.";

        var value = RackPoseSettings.ValueText(name, group, _itemTarget, _field);
        var own = _itemTarget && RackPoseSettings.HasItemOverride(name, _field) ? " (item override)" : "";
        return $"\n<color=#ffcc33>Rack pose</color> {name} - {group}\n" +
               $"Changing: <b>{(_itemTarget ? "this item" : "all " + group + "s")}</b>   <b>{RackPoseSettings.FieldNames[_field]}</b> = {value}{own}\n" +
               $"<size=12>{_previousField.Value}/{_nextField.Value} setting   {_decrease.Value}/{_increase.Value} change   " +
               $"{_toggleTarget.Value} item/type   {_reset.Value} reset   (Shift x10, Ctrl x0.1)</size>";
    }

    private static void Change(string name, RackGroup group, int direction, float multiplier)
    {
        RackPoseSettings.Adjust(name, group, _itemTarget, _field, direction, multiplier);
        Debug.Log($"[ItemRack] {name} ({group}) {(_itemTarget ? "item" : "type")} {RackPoseSettings.FieldNames[_field]} = " +
                  RackPoseSettings.ValueText(name, group, _itemTarget, _field));
    }
}
