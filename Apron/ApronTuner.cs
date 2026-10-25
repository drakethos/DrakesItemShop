using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace DrakesItemShop.Apron;

/// <summary>
/// The apron fitting room. F8 opens it: your character stands still in the middle of the screen, the camera circles them
/// (right mouse drag turns, wheel zooms, or use the Front / Side / Back buttons) and a panel on the left has one tab per part of the
/// apron with a slider for every number, an Auto-fit button that measures your body, and Save. The apron on your body rebuilds as
/// you move a slider. Arrow keys still work: Up / Down pick, Left / Right change (Shift x5), PageUp / PageDown change tab.
/// </summary>
internal static class ApronTuner
{
    private static bool _open;
    private static int _tab;
    private static int _selected;
    private static float _nextRepeat;
    private static string _status = "";
    private static float _statusUntil;
    private static GUIStyle? _label, _bold, _small, _tabOn, _tabOff, _button;
    private static Texture2D? _panel;

    // camera
    private static float _yaw, _pitch = 8f, _dist = 2.3f, _focus = 1.0f;
    private static float _focusNow = 1.0f;
    private static bool _spin;
    private static Vector3 _lastMouse;

    internal static bool IsOpen => _open;

    // Where the camera looks (height up the body) and how far away it stands, per tab.
    private static readonly float[] TabFocus = { 1.0f, 1.3f, 0.8f, 0.95f, 1.05f, 1.45f, 1.3f };
    private static readonly float[] TabDistance = { 2.4f, 1.5f, 2.0f, 1.8f, 1.7f, 1.1f, 1.7f };
    private static Vector2 _scroll;

    private static string[] Tabs => ApronTuning.Tabs;

    private static IEnumerable<int> TabParams(int tab)
    {
        var name = Tabs[Mathf.Clamp(tab, 0, Tabs.Length - 1)];
        for (var i = 0; i < ApronTuning.All.Length; i++)
            if (ApronTuning.All[i].Group == name)
                yield return i;
    }

    private static void Toggle()
    {
        _open = !_open;
        if (!_open)
            return;
        var player = Player.m_localPlayer;
        _yaw = player != null ? player.transform.eulerAngles.y : 0f;
        _pitch = 8f;
        _dist = 2.3f;
        _spin = false;
        _lastMouse = Input.mousePosition;
        _focusNow = _focus = 1.0f;
        SelectTab(_tab);
    }

    /// <summary>For automated fitting runs (see ApronTestHooks): opens or closes the room and puts the camera at a set view.</summary>
    internal static void SetView(bool open, float yawOffset, float focus, float dist, float pitch, int tab)
    {
        var player = Player.m_localPlayer;
        _open = open && player != null;
        if (!_open)
            return;
        SelectTab(tab);
        _yaw = (player != null ? player.transform.eulerAngles.y : 0f) + yawOffset;
        _pitch = pitch;
        _dist = dist;
        _focus = _focusNow = focus;
        _spin = false;
    }

    private static void SelectTab(int tab)
    {
        _tab = (tab % Tabs.Length + Tabs.Length) % Tabs.Length;
        _selected = TabParams(_tab).FirstOrDefault();
        _focus = _tab < TabFocus.Length ? TabFocus[_tab] : 1.0f;
        _dist = _tab < TabDistance.Length ? TabDistance[_tab] : _dist;
        _scroll = Vector2.zero;
    }

    internal static void Tick()
    {
        if (Input.GetKeyDown(ApronSettings.TunerKey) || (_open && Input.GetKeyDown(KeyCode.Escape)))
            Toggle();
        if (!_open)
            return;
        if (Player.m_localPlayer == null)
        {
            _open = false;
            return;
        }

        // Camera: right mouse drag turns, wheel zooms.
        var mouse = Input.mousePosition;
        if (Input.GetMouseButton(1))
        {
            _yaw += (mouse.x - _lastMouse.x) * 0.45f;
            _pitch = Mathf.Clamp(_pitch - (mouse.y - _lastMouse.y) * 0.3f, -25f, 70f);
        }
        _lastMouse = mouse;
        var overPanel = mouse.x >= 16f && mouse.x <= 450f;
        if (overPanel)
            _scroll.y -= Input.mouseScrollDelta.y * 60f;
        else
            _dist = Mathf.Clamp(_dist - Input.mouseScrollDelta.y * 0.25f, 0.6f, 5f);
        if (_spin)
            _yaw += 25f * Time.unscaledDeltaTime;
        _focusNow = Mathf.Lerp(_focusNow, _focus, 6f * Time.unscaledDeltaTime);

        // Keys.
        var indexes = TabParams(_tab).ToList();
        var at = Mathf.Max(0, indexes.IndexOf(_selected));
        if (Input.GetKeyDown(KeyCode.UpArrow))
            _selected = indexes[(at - 1 + indexes.Count) % indexes.Count];
        if (Input.GetKeyDown(KeyCode.DownArrow))
            _selected = indexes[(at + 1) % indexes.Count];
        if (Input.GetKeyDown(KeyCode.PageUp))
            SelectTab(_tab - 1);
        if (Input.GetKeyDown(KeyCode.PageDown))
            SelectTab(_tab + 1);

        var dir = 0;
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            dir = Input.GetKeyDown(KeyCode.RightArrow) ? 1 : -1;
            _nextRepeat = Time.unscaledTime + 0.35f;
        }
        else if ((Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow)) && Time.unscaledTime >= _nextRepeat)
        {
            dir = Input.GetKey(KeyCode.RightArrow) ? 1 : -1;
            _nextRepeat = Time.unscaledTime + 0.06f;
        }
        if (dir != 0)
        {
            var p = ApronTuning.All[_selected];
            var steps = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 5 : 1;
            ApronTuning.Set(_selected, ApronTuning.Get(_selected) + dir * steps * p.Step);
        }
        if (Input.GetKeyDown(KeyCode.F9))
            Save();
    }

    private static void Save()
    {
        try
        {
            ApronTuning.Save();
            Say("Saved to " + ApronTuning.FilePath);
        }
        catch (Exception ex)
        {
            Say("Couldn't save: " + ex.Message);
        }
    }

    private static void Say(string text)
    {
        _status = text;
        _statusUntil = Time.unscaledTime + 6f;
    }

    /// <summary>Puts the game camera in orbit round the player (called after the game has placed it for the frame).</summary>
    internal static void PlaceCamera(Transform camera)
    {
        var player = Player.m_localPlayer;
        if (!_open || player == null)
            return;
        var target = player.transform.position + Vector3.up * _focusNow;
        var offset = Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.forward * _dist;
        camera.position = target + offset;
        camera.rotation = Quaternion.LookRotation(target - camera.position, Vector3.up);
    }

    private static void Styles()
    {
        if (_label != null)
            return;
        _panel = new Texture2D(1, 1);
        _panel.SetPixel(0, 0, new Color(0.07f, 0.05f, 0.04f, 0.88f));
        _panel.Apply();
        var cream = new Color(0.93f, 0.88f, 0.76f);
        var gold = new Color(1f, 0.82f, 0.28f);
        _label = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = cream } };
        _bold = new GUIStyle(_label) { fontStyle = FontStyle.Bold, fontSize = 16, normal = { textColor = gold } };
        _small = new GUIStyle(_label) { fontSize = 12, normal = { textColor = new Color(0.72f, 0.68f, 0.58f) } };
        _button = new GUIStyle(GUI.skin.button) { fontSize = 13 };
        _tabOff = new GUIStyle(GUI.skin.button) { fontSize = 13, normal = { textColor = cream } };
        _tabOn = new GUIStyle(_tabOff) { fontStyle = FontStyle.Bold, normal = { textColor = gold } };
    }

    internal static void Draw()
    {
        if (!_open)
            return;
        Styles();
        var tabs = Tabs;
        const float width = 430f;
        var area = new Rect(16f, 16f, width, Screen.height - 32f);
        GUI.DrawTexture(area, _panel);
        GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 10f, area.width - 24f, area.height - 20f));

        GUILayout.Label("Apron fitting room", _bold);
        GUILayout.Label("Right-drag turns you, wheel zooms (over the panel it scrolls). Arrows also work (Up/Down, Left/Right, PgUp/PgDn).", _small);

        // View buttons
        GUILayout.BeginHorizontal();
        var baseYaw = Player.m_localPlayer != null ? Player.m_localPlayer.transform.eulerAngles.y : 0f;
        if (GUILayout.Button("Front", _button)) { _yaw = baseYaw; _spin = false; }
        if (GUILayout.Button("Right", _button)) { _yaw = baseYaw + 90f; _spin = false; }
        if (GUILayout.Button("Back", _button)) { _yaw = baseYaw + 180f; _spin = false; }
        if (GUILayout.Button("Left", _button)) { _yaw = baseYaw - 90f; _spin = false; }
        if (GUILayout.Button(_spin ? "Stop spin" : "Spin", _button)) _spin = !_spin;
        GUILayout.EndHorizontal();
        GUILayout.Space(6f);

        // Tabs
        GUILayout.BeginHorizontal();
        for (var i = 0; i < tabs.Length; i++)
            if (GUILayout.Button(tabs[i], i == _tab ? _tabOn : _tabOff))
                SelectTab(i);
        GUILayout.EndHorizontal();
        GUILayout.Space(8f);

        // Sliders for this tab (scrolls when there are more than fit; the wheel over the panel scrolls it)
        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(Screen.height - 330f));
        foreach (var i in TabParams(_tab))
        {
            var p = ApronTuning.All[i];
            var value = ApronTuning.Get(i);
            var changed = !Mathf.Approximately(value, p.Default);
            GUILayout.BeginHorizontal();
            GUILayout.Label((i == _selected ? "> " : "") + p.Label + (changed ? " *" : ""), i == _selected ? _bold : _label);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{value:0.###} {p.Unit}", _label, GUILayout.Width(86f));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", _button, GUILayout.Width(26f)))
            {
                _selected = i;
                ApronTuning.Set(i, value - p.Step);
            }
            var slid = GUILayout.HorizontalSlider(value, p.Min, p.Max);
            if (!Mathf.Approximately(slid, value))
            {
                _selected = i;
                ApronTuning.Set(i, slid);
            }
            if (GUILayout.Button(">", _button, GUILayout.Width(26f)))
            {
                _selected = i;
                ApronTuning.Set(i, value + p.Step);
            }
            if (GUILayout.Button("reset", _button, GUILayout.Width(50f)))
            {
                _selected = i;
                ApronTuning.Set(i, p.Default);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(3f);
        }

        GUILayout.EndScrollView();
        GUILayout.FlexibleSpace();
        if (Time.unscaledTime < _statusUntil)
            GUILayout.Label(_status, _small);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto-fit to my body", _button, GUILayout.Height(30f)))
        {
            try
            {
                Say(ApronAutoFit.Run(Player.m_localPlayer));
            }
            catch (Exception ex)
            {
                Say("Auto-fit failed: " + ex.Message);
                ItemShopPlugin.LogSource?.LogWarning($"[Apron] auto-fit: {ex}");
            }
        }
        if (GUILayout.Button("Reset this tab", _button, GUILayout.Height(30f)))
        {
            foreach (var i in TabParams(_tab))
                ApronTuning.Set(i, ApronTuning.All[i].Default);
            Say("This tab is back to its defaults.");
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Save (F9)", _button, GUILayout.Height(30f)))
            Save();
        if (GUILayout.Button("Reset all", _button, GUILayout.Height(30f)))
        {
            ApronTuning.ResetAll();
            Say("Everything is back to its defaults.");
        }
        if (GUILayout.Button("Close (F8)", _button, GUILayout.Height(30f)))
            Toggle();
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }
}

/// <summary>While the fitting room is open: the camera circles the player, the cursor is free and the player can't move.</summary>
[HarmonyPatch(typeof(GameCamera), "LateUpdate")]
internal static class ApronRoomCameraPatch
{
    [HarmonyPostfix]
    private static void Postfix(GameCamera __instance)
    {
        if (ApronTuner.IsOpen)
            ApronTuner.PlaceCamera(__instance.transform);
    }
}

[HarmonyPatch(typeof(GameCamera), "UpdateMouseCapture")]
internal static class ApronRoomCursorPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        if (!ApronTuner.IsOpen)
            return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}

[HarmonyPatch(typeof(PlayerController), "TakeInput")]
internal static class ApronRoomInputPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref bool __result)
    {
        if (ApronTuner.IsOpen)
            __result = false;
    }
}
