using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace DrakesItemShop;

/// <summary>How a weapon sits on a weapon rack.</summary>
public enum RackStyle
{
    /// <summary>As the vanilla item stand shows it (point / head up), grip on the fork.</summary>
    AsPosed,
    /// <summary>Hilt down, blade up, grip on the fork (daggers, swords).</summary>
    PointUp,
    /// <summary>Head up, resting on the fork, handle hanging through it (axe, hammer, mace, farm tools).</summary>
    HeadOnHook,
    /// <summary>Head down, handle up (found from the mesh when readable).</summary>
    HeadDown,
    /// <summary>Butt on the floor, leaning through the fork (spear, atgeir, bow). Weapon stands only.</summary>
    Floor,
    /// <summary>Stands over the pegs, centred on the fork, covering them.</summary>
    Shield,
}

/// <summary>Weapon types that get their own pose settings.</summary>
public enum RackGroup
{
    Sword, Dagger, Axe, Pickaxe, Hammer, Club, FarmTool, Spear, Polearm, Bow, Shield, Other,
}

/// <summary>How a mod binds the pose settings: synced from the server, and who may change them.</summary>
public interface IRackPoseBinder
{
    ConfigEntry<T> Bind<T>(ConfigFile config, string section, string key, T defaultValue, string description, AcceptableValueBase? range);

    /// <summary>True on the side whose values everyone gets (host, server or single player): the only one that can change them.</summary>
    bool CanEdit { get; }
}

/// <summary>Position, rotation and size adjustments for one weapon type or item.</summary>
public sealed class RackPose
{
    public RackStyle Style;
    /// <summary>Metres, in the item's own frame: X across the rack, Y up, Z out of the side it hangs on.</summary>
    public Vector3 Offset;
    /// <summary>Degrees about the same axes, applied before it is fitted and placed.</summary>
    public Vector3 Rotation;
    /// <summary>Size multiplier.</summary>
    public float Scale = 1f;
    /// <summary>Shrink it if it can't fit its slot (off = always its real size, times <see cref="Scale"/>).</summary>
    public bool Fit = true;
}

/// <summary>
/// Weapon rack poses, tuned live: one settings block per weapon type (BepInEx config, so any config UI can edit it), and
/// optional per-item overrides by prefab name in a YAML file next to the config. Both apply as soon as they change; racks
/// redraw themselves. Item overrides win over the type's settings, field by field.
/// </summary>
public static class RackPoseSettings
{
    private const string SectionPrefix = "04 Rack pose - ";

    private sealed class Entries
    {
        public ConfigEntry<RackStyle> Style = default!;
        public ConfigEntry<float> X = default!, Y = default!, Z = default!;
        public ConfigEntry<float> RotX = default!, RotY = default!, RotZ = default!;
        public ConfigEntry<float> Scale = default!;
        public ConfigEntry<bool> Fit = default!;
    }

    private static readonly Dictionary<RackGroup, Entries> Groups = new();
    private static readonly Dictionary<string, Dictionary<string, string>> Items = new(StringComparer.OrdinalIgnoreCase);
    private static string _yamlPath = "";
    private static IRackPoseBinder _binder = default!;
    private static ConfigEntry<string> _itemEntry = default!;
    private static DateTime _yamlStamp;
    private static float _nextPoll;

    /// <summary>Bumped whenever any pose setting changes; racks redraw when it moves.</summary>
    public static int Version { get; private set; }

    /// <summary>The style a weapon type gets unless configured otherwise.</summary>
    private static RackStyle DefaultStyle(RackGroup group) => group switch
    {
        RackGroup.Sword or RackGroup.Dagger => RackStyle.PointUp,
        RackGroup.Axe or RackGroup.Pickaxe or RackGroup.Hammer or RackGroup.Club or RackGroup.FarmTool => RackStyle.HeadOnHook,
        RackGroup.Spear or RackGroup.Polearm or RackGroup.Bow => RackStyle.Floor,
        RackGroup.Shield => RackStyle.Shield,
        _ => RackStyle.AsPosed,
    };

    /// <summary>Synced entries this binds (for the mod's expected-count check).</summary>
    public static int SyncedEntryCount => Enum.GetValues(typeof(RackGroup)).Length * 9 + 1;

    /// <param name="binder">Binds each setting as a server-synced one.</param>
    /// <param name="yamlName">File name (in the BepInEx config folder) the host edits per-item overrides in; its text is what syncs.</param>
    public static void Bind(ConfigFile config, IRackPoseBinder binder, string yamlName)
    {
        _binder = binder;
        foreach (RackGroup group in Enum.GetValues(typeof(RackGroup)))
        {
            var section = SectionPrefix + group;
            Groups[group] = new Entries
            {
                Style = binder.Bind(config, section, "Style", DefaultStyle(group), "How this weapon type sits on a weapon rack.", null),
                X = Offset(config, section, "Offset X", "Metres across the rack (positive = towards its left as you face it)."),
                Y = Offset(config, section, "Offset Y", "Metres up."),
                Z = Offset(config, section, "Offset Z", "Metres out of the side it hangs on."),
                RotX = Turn(config, section, "Rotate X", "Degrees about the across-rack axis (tip towards you or away)."),
                RotY = Turn(config, section, "Rotate Y", "Degrees about the vertical axis (turn it)."),
                RotZ = Turn(config, section, "Rotate Z", "Degrees about the axis pointing out of the rack (lean it)."),
                Scale = binder.Bind(config, section, "Scale", 1f, "Size multiplier.", new AcceptableValueRange<float>(0.2f, 2f)),
                Fit = binder.Bind(config, section, "Shrink to fit", true, "Shrink it if it can't fit its slot. Off = always its real size.", null),
            };
        }
        _itemEntry = binder.Bind(config, SectionPrefix + "Item overrides", "YAML", "",
            "Per-item pose overrides (same format as the YAML file next to the config, which the host edits; this is what clients receive). Leave it to the file.", null);
        config.SettingChanged += (_, args) =>
        {
            var setting = args.ChangedSetting;
            if (!setting.Definition.Section.StartsWith(SectionPrefix, StringComparison.Ordinal))
                return;
            if (setting == _itemEntry)
                ParseYaml(_itemEntry.Value);
            Version++;
        };

        _yamlPath = Path.Combine(Paths.ConfigPath, yamlName);
        if (!File.Exists(_yamlPath))
            File.WriteAllText(_yamlPath, ExampleYaml);
        _yamlStamp = File.GetLastWriteTimeUtc(_yamlPath);
        if (_itemEntry.Value.Length == 0 && binder.CanEdit)
            _itemEntry.Value = File.ReadAllText(_yamlPath);
        ParseYaml(_itemEntry.Value);
    }

    private static ConfigEntry<float> Offset(ConfigFile config, string section, string key, string description) =>
        _binder.Bind(config, section, key, 0f, description, new AcceptableValueRange<float>(-1.5f, 1.5f));

    private static ConfigEntry<float> Turn(ConfigFile config, string section, string key, string description) =>
        _binder.Bind(config, section, key, 0f, description, new AcceptableValueRange<float>(-180f, 180f));

    /// <summary>True when this side may change pose settings (otherwise they come from the server).</summary>
    public static bool CanEdit => _binder != null && _binder.CanEdit;

    /// <summary>Picks up edits to the YAML file (checked about once a second).</summary>
    public static void Poll()
    {
        if (Time.unscaledTime < _nextPoll || _yamlPath.Length == 0)
            return;
        _nextPoll = Time.unscaledTime + 1f;
        try
        {
            if (_binder.CanEdit && File.Exists(_yamlPath) && File.GetLastWriteTimeUtc(_yamlPath) != _yamlStamp)
            {
                _yamlStamp = File.GetLastWriteTimeUtc(_yamlPath);
                _itemEntry.Value = File.ReadAllText(_yamlPath);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ItemRack] Could not read {_yamlPath}: {e.Message}");
        }
    }

    /// <summary>Which settings block a weapon uses, from what it is (the item it is shown as, for reskins).</summary>
    public static RackGroup GroupOf(string prefabName, ItemDrop.ItemData.SharedData shared)
    {
        if (shared.m_itemType == ItemDrop.ItemData.ItemType.Shield)
            return RackGroup.Shield;
        switch (shared.m_skillType)
        {
            case Skills.SkillType.Knives: return RackGroup.Dagger;
            case Skills.SkillType.Swords: return RackGroup.Sword;
            case Skills.SkillType.Axes: return RackGroup.Axe;
            case Skills.SkillType.Pickaxes: return RackGroup.Pickaxe;
            case Skills.SkillType.Clubs: return prefabName.Contains("ammer") ? RackGroup.Hammer : RackGroup.Club;
            case Skills.SkillType.Spears: return RackGroup.Spear;
            case Skills.SkillType.Polearms: return RackGroup.Polearm;
            case Skills.SkillType.Bows:
            case Skills.SkillType.Crossbows: return RackGroup.Bow;
        }
        return shared.m_itemType == ItemDrop.ItemData.ItemType.Tool && shared.m_skillType != Skills.SkillType.Fishing
            ? RackGroup.FarmTool : RackGroup.Other;
    }

    /// <summary>The pose for an item: its weapon type's settings, then its YAML overrides on top.</summary>
    public static RackPose Resolve(string prefabName, RackGroup group)
    {
        var pose = new RackPose { Style = DefaultStyle(group) };
        if (Groups.TryGetValue(group, out var e))
        {
            pose.Style = e.Style.Value;
            pose.Offset = new Vector3(e.X.Value, e.Y.Value, e.Z.Value);
            pose.Rotation = new Vector3(e.RotX.Value, e.RotY.Value, e.RotZ.Value);
            pose.Scale = e.Scale.Value;
            pose.Fit = e.Fit.Value;
        }

        if (Defaults.TryGetValue(prefabName, out var builtIn))
            Apply(pose, builtIn);
        if (Items.TryGetValue(prefabName, out var fields))
            Apply(pose, fields);
        return pose;
    }

    private static void Apply(RackPose pose, Dictionary<string, string> fields)
    {
        if (fields.TryGetValue("style", out var style) && Enum.TryParse(style, true, out RackStyle parsed))
            pose.Style = parsed;
        if (fields.TryGetValue("offset", out var offset) && Vec(offset) is { } o)
            pose.Offset = o;
        if (fields.TryGetValue("rotation", out var rotation) && Vec(rotation) is { } r)
            pose.Rotation = r;
        if (fields.TryGetValue("scale", out var scale) && Num(scale) is { } s)
            pose.Scale = s;
        if (fields.TryGetValue("fit", out var fit) && bool.TryParse(fit, out var f))
            pose.Fit = f;
    }

    // ---- live editing (used by the rack pose debug helper) ----

    /// <summary>The adjustable fields, in the order the debug helper cycles them.</summary>
    public static readonly string[] FieldNames =
        { "Style", "Offset X", "Offset Y", "Offset Z", "Rotate X", "Rotate Y", "Rotate Z", "Scale", "Shrink to fit" };

    private static readonly float[] FieldSteps = { 0f, 0.01f, 0.01f, 0.01f, 5f, 5f, 5f, 0.05f, 0f };

    /// <summary>The field's current value for an item (<paramref name="itemTarget"/>: its resolved pose) or for its weapon type.</summary>
    public static string ValueText(string prefabName, RackGroup group, bool itemTarget, int field)
    {
        var pose = itemTarget ? Resolve(prefabName, group) : TypePose(group);
        return field switch
        {
            0 => pose.Style.ToString(),
            1 => pose.Offset.x.ToString("0.00", CultureInfo.InvariantCulture),
            2 => pose.Offset.y.ToString("0.00", CultureInfo.InvariantCulture),
            3 => pose.Offset.z.ToString("0.00", CultureInfo.InvariantCulture),
            4 => pose.Rotation.x.ToString("0", CultureInfo.InvariantCulture),
            5 => pose.Rotation.y.ToString("0", CultureInfo.InvariantCulture),
            6 => pose.Rotation.z.ToString("0", CultureInfo.InvariantCulture),
            7 => pose.Scale.ToString("0.00", CultureInfo.InvariantCulture),
            _ => pose.Fit ? "on" : "off",
        };
    }

    /// <summary>True when the item has its own YAML override for the field.</summary>
    public static bool HasItemOverride(string prefabName, int field) =>
        (Items.TryGetValue(prefabName, out var fields) && fields.ContainsKey(FieldKey(field)))
        || (Defaults.TryGetValue(prefabName, out var builtIn) && builtIn.ContainsKey(FieldKey(field)));

    private static string FieldKey(int field) => field switch
    {
        0 => "style",
        >= 1 and <= 3 => "offset",
        >= 4 and <= 6 => "rotation",
        7 => "scale",
        _ => "fit",
    };

    /// <summary>Settings of a weapon type alone (no item overrides).</summary>
    private static RackPose TypePose(RackGroup group) => Resolve("", group);

    /// <summary>Step a field up (+1) or down (-1) by its step times <paramref name="multiplier"/>; for a type or one item.</summary>
    public static void Adjust(string prefabName, RackGroup group, bool itemTarget, int field, int direction, float multiplier)
    {
        if (!CanEdit)
            return;
        var pose = itemTarget ? Resolve(prefabName, group) : TypePose(group);
        var step = FieldSteps[field] * multiplier * direction;
        switch (field)
        {
            case 0:
                var count = Enum.GetValues(typeof(RackStyle)).Length;
                pose.Style = (RackStyle)(((int)pose.Style + direction + count) % count);
                break;
            case 1: pose.Offset.x = Mathf.Clamp(pose.Offset.x + step, -1.5f, 1.5f); break;
            case 2: pose.Offset.y = Mathf.Clamp(pose.Offset.y + step, -1.5f, 1.5f); break;
            case 3: pose.Offset.z = Mathf.Clamp(pose.Offset.z + step, -1.5f, 1.5f); break;
            case 4: pose.Rotation.x = Mathf.Clamp(pose.Rotation.x + step, -180f, 180f); break;
            case 5: pose.Rotation.y = Mathf.Clamp(pose.Rotation.y + step, -180f, 180f); break;
            case 6: pose.Rotation.z = Mathf.Clamp(pose.Rotation.z + step, -180f, 180f); break;
            case 7: pose.Scale = Mathf.Clamp(pose.Scale + step, 0.2f, 2f); break;
            default: pose.Fit = !pose.Fit; break;
        }

        if (!itemTarget)
        {
            var e = Groups[group];
            e.Style.Value = pose.Style;
            e.X.Value = pose.Offset.x; e.Y.Value = pose.Offset.y; e.Z.Value = pose.Offset.z;
            e.RotX.Value = pose.Rotation.x; e.RotY.Value = pose.Rotation.y; e.RotZ.Value = pose.Rotation.z;
            e.Scale.Value = pose.Scale;
            e.Fit.Value = pose.Fit;
            return;
        }

        if (!Items.TryGetValue(prefabName, out var fields))
            Items[prefabName] = fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var key = FieldKey(field);
        fields[key] = key switch
        {
            "style" => pose.Style.ToString(),
            "offset" => Format(pose.Offset, "0.###"),
            "rotation" => Format(pose.Rotation, "0.#"),
            "scale" => pose.Scale.ToString("0.###", CultureInfo.InvariantCulture),
            _ => pose.Fit ? "true" : "false",
        };
        SaveYaml();
    }

    /// <summary>Put a field back to its default: for an item, drop its override (the type's value shows again).</summary>
    public static void Reset(string prefabName, RackGroup group, bool itemTarget, int field)
    {
        if (!CanEdit)
            return;
        if (!itemTarget)
        {
            var e = Groups[group];
            ConfigEntryBase[] entries = field switch
            {
                0 => new ConfigEntryBase[] { e.Style },
                1 => new ConfigEntryBase[] { e.X },
                2 => new ConfigEntryBase[] { e.Y },
                3 => new ConfigEntryBase[] { e.Z },
                4 => new ConfigEntryBase[] { e.RotX },
                5 => new ConfigEntryBase[] { e.RotY },
                6 => new ConfigEntryBase[] { e.RotZ },
                7 => new ConfigEntryBase[] { e.Scale },
                _ => new ConfigEntryBase[] { e.Fit },
            };
            foreach (var entry in entries)
                entry.BoxedValue = entry.DefaultValue;
            return;
        }

        if (!Items.TryGetValue(prefabName, out var fields) || !fields.Remove(FieldKey(field)))
            return;
        if (fields.Count == 0)
            Items.Remove(prefabName);
        SaveYaml();
    }

    private static string Format(Vector3 v, string format) =>
        string.Join(", ", v.x.ToString(format, CultureInfo.InvariantCulture), v.y.ToString(format, CultureInfo.InvariantCulture),
            v.z.ToString(format, CultureInfo.InvariantCulture));

    /// <summary>Write the per-item overrides back (the file's example header stays; other hand-written comments don't).</summary>
    private static void SaveYaml()
    {
        var text = new System.Text.StringBuilder(ExampleYaml).AppendLine();
        foreach (var item in Items)
        {
            text.AppendLine(item.Key + ":");
            foreach (var field in item.Value)
                text.AppendLine("  " + field.Key + ": " + field.Value);
        }
        File.WriteAllText(_yamlPath, text.ToString());
        _yamlStamp = File.GetLastWriteTimeUtc(_yamlPath);
        _itemEntry.Value = text.ToString();
    }

    // ---- YAML: a flat, two-level file ----

    /// <summary>Per-item poses built in (the tuned values). The YAML file only needs what differs; it wins field by field.</summary>
    private const string DefaultItemYaml = @"
Hoe:
  offset: 0.02, 0.12, -0.16
  fit: false
MaceIron:
  offset: 0, 0.13, -0.07
  fit: false
PickaxeIron:
  offset: 0, 0.02, -0.06
  scale: 0.8
  fit: false
  style: HeadOnHook
AtgeirIron:
  offset: 0, -0.03, 0
";

    private static readonly Dictionary<string, Dictionary<string, string>> Defaults = ParseDefaults();

    private static Dictionary<string, Dictionary<string, string>> ParseDefaults()
    {
        var table = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        ParseInto(table, DefaultItemYaml);
        return table;
    }

    private const string ExampleYaml = @"# Weapon rack poses for single items, by prefab name (for a reskinned item, the prefab it looks like).
# Weapon types are tuned in the mod's config (section '04 Rack pose - <type>'); anything here wins over that, field by field.
# Edits apply while the game runs. Every field is optional.
#
# SwordIron:
#   style: PointUp        # AsPosed, PointUp, HeadOnHook, HeadDown, Floor or Shield
#   offset: 0, 0.05, 0    # metres: across, up, out
#   rotation: 0, 0, 0     # degrees about across, up, out
#   scale: 1.0
#   fit: true             # false = never shrink to fit its slot
";

    private static void ParseYaml(string yaml)
    {
        Items.Clear();
        ParseInto(Items, yaml);
    }

    private static void ParseInto(Dictionary<string, Dictionary<string, string>> target, string yaml)
    {
        Dictionary<string, string>? current = null;
        foreach (var raw in yaml.Replace("\r\n", "\n").Split('\n'))
        {
            var hash = raw.IndexOf('#');
            var line = (hash >= 0 ? raw.Substring(0, hash) : raw).TrimEnd();
            if (line.Trim().Length == 0)
                continue;
            var colon = line.IndexOf(':');
            if (colon < 0)
                continue;
            var key = line.Substring(0, colon).Trim();
            var value = line.Substring(colon + 1).Trim();
            if (!char.IsWhiteSpace(line[0]))
            {
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                target[key.Trim('"', '\'')] = current;
            }
            else
            {
                current?.Add(key.ToLowerInvariant(), value);
            }
        }
    }

    private static float? Num(string text) =>
        float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static Vector3? Vec(string text)
    {
        var parts = text.Trim('[', ']', ' ').Split(',');
        return parts.Length == 3 && Num(parts[0]) is { } x && Num(parts[1]) is { } y && Num(parts[2]) is { } z
            ? new Vector3(x, y, z) : null;
    }
}
