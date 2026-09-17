using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Mirror;
using UnityEngine;

namespace BigOrb;

public class OrbMenu : MonoBehaviour
{
    private const string DiscordUrl = "https://discord.gg/5z3WvVhxCf";
    private const string SourceUrl = "https://github.com/RadioFreeOpportunity/bigorb";
    private const string RepoUrl = "https://github.com/AdamMady/Moderation-Improvements/";

    public OrbMenu(IntPtr ptr) : base(ptr) { }

    private readonly string[] _tabs = { "Players", "Bans & Roster", "Signs", "Logs", "Settings" };
    private bool _open, _oldCursorVisible, _oldMenuMode, _showPassword;
    private CursorLockMode _oldCursorLock;
    private int _tab;
    private float _openedAt, _nextRefresh, _scroll, _contentHeight, _y, _width, _viewHeight;
    private string _focus, _status = "", _password = "", _banId = "", _csvPath = "", _signText = "";
    private string _logKind = "alerts";
    private JsonDocument _state;
    private Action _pending;
    private string _confirmation;
    private GUIStyle _label, _heading;

    public static bool IsOpen { get; private set; }

    private JsonElement Root => _state == null ? default : _state.RootElement;
    private static JsonElement Get(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var result) ? result : default;
    private static string Str(JsonElement value, string key) => Get(value, key).ToString();
    private static bool Flag(JsonElement value, string key) => Get(value, key).ValueKind == JsonValueKind.True;
    private static IEnumerable<JsonElement> Rows(JsonElement value) =>
        value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : Enumerable.Empty<JsonElement>();

    private void Update()
    {
        var lPressed = Input.GetKeyDown(Plugin.MenuKey.Value);
        if (!_open)
        {
            if (lPressed) SetOpen(true);
            return;
        }

        if (Time.unscaledTime - _openedAt > .15f &&
            (lPressed || Input.GetKeyDown(KeyCode.Escape)))
        {
            SetOpen(false);
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + .5f;
        RefreshState();
    }

    private void RefreshState()
    {
        OrbState.Polled();
        try
        {
            var json = "{\"snap\":" + OrbState.SnapshotJson +
                ",\"bans\":" + OrbState.BansJson() +
                ",\"roster\":" + OrbState.RosterJson() +
                ",\"signlocks\":" + OrbState.SignLocksJson() +
                ",\"signs\":" + OrbState.Tail("signs") +
                ",\"chat\":" + OrbState.Tail("chat") +
                ",\"alerts\":" + OrbState.Tail("alerts") +
                ",\"events\":" + OrbState.Tail("events") +
                ",\"identities\":" + Guard.Eos.Json() + "}";
            var replacement = JsonDocument.Parse(json);
            _state?.Dispose();
            _state = replacement;
        }
        catch (Exception e) { _status = "Could not refresh: " + e.Message; }
    }

    private void SetOpen(bool value)
    {
        if (_open == value) return;
        _open = value;
        IsOpen = value;
        _focus = null;
        _pending = null;
        if (_open)
        {
            CloseMapMenu();
            _openedAt = Time.unscaledTime;
            _oldCursorLock = Cursor.lockState;
            _oldCursorVisible = Cursor.visible;
            _oldMenuMode = ControlsManager.menuModeActive;
            ControlsManager.SetMenuMode(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _nextRefresh = 0;
            RefreshState();
        }
        else
        {
            ControlsManager.SetMenuMode(_oldMenuMode);
            Cursor.lockState = _oldCursorLock;
            Cursor.visible = _oldCursorVisible;
        }
    }

    private static void CloseMapMenu()
    {
        try
        {
            var type = Type.GetType("AdamMady.Minimap.MapController, AdamMady_Minimap", false);
            type?.GetMethod("CloseMenu", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
        }
        catch { }
    }

    public static void CloseMenu()
    {
        var menus = UnityEngine.Object.FindObjectsOfType<OrbMenu>(true);
        for (int i = 0; i < menus.Length; i++) menus[i]?.SetOpen(false);
    }

    private void OnDisable() => SetOpen(false);
    private void OnDestroy() { SetOpen(false); _state?.Dispose(); }

    private void OnGUI()
    {
        if (!_open) return;
        var ev = Event.current;
        if (ev.type == EventType.KeyDown && Time.unscaledTime - _openedAt > .15f && ev.keyCode == KeyCode.Escape)
        {
            ev.Use();
            SetOpen(false);
            return;
        }

        if (_label == null)
        {
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            _heading = new GUIStyle(_label) { fontSize = 23, fontStyle = FontStyle.Bold };
        }

        GUI.depth = -1000;
        var oldColor = GUI.color;
        GUI.color = new Color(.055f, .065f, .09f, 1f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = oldColor;

        GUI.Label(new Rect(24, 16, Screen.width - 500, 36),
            "MODERATION IMPROVEMENTS  /  " + (NetworkServer.active ? OrbState.SessionName : "Not hosting"), _heading);
        if (GUI.Button(new Rect(Screen.width - 306, 18, 142, 32), "Join The Discord!")) Application.OpenURL(DiscordUrl);
        if (GUI.Button(new Rect(Screen.width - 156, 18, 132, 32), "Close [" + Plugin.MenuKey.Value + "]")) { SetOpen(false); return; }

        var tabWidth = (Screen.width - 48f) / _tabs.Length;
        for (int i = 0; i < _tabs.Length; i++)
        {
            GUI.backgroundColor = i == _tab ? new Color(.35f, .7f, 1f) : Color.white;
            if (GUI.Button(new Rect(24 + i * tabWidth, 64, tabWidth - 4, 38), _tabs[i]))
            { _tab = i; _scroll = 0; _focus = null; }
        }
        GUI.backgroundColor = Color.white;
        GUI.Label(new Rect(24, Screen.height - 35, Screen.width - 48, 30), _status, _label);

        var viewport = new Rect(24, 114, Screen.width - 48, Math.Max(50, Screen.height - 160));
        _viewHeight = viewport.height;
        _width = viewport.width - 22;
        if (_pending == null && ev.type == EventType.ScrollWheel)
        { _scroll = Mathf.Clamp(_scroll + ev.delta.y * 35, 0, Math.Max(0, _contentHeight - _viewHeight)); ev.Use(); }
        if (_pending == null && ev.type == EventType.KeyDown)
        {
            if (ev.keyCode == KeyCode.PageDown || ev.keyCode == KeyCode.DownArrow)
            { _scroll = Mathf.Clamp(_scroll + (ev.keyCode == KeyCode.PageDown ? _viewHeight * .8f : 42f), 0, Math.Max(0, _contentHeight - _viewHeight)); ev.Use(); }
            else if (ev.keyCode == KeyCode.PageUp || ev.keyCode == KeyCode.UpArrow)
            { _scroll = Mathf.Clamp(_scroll - (ev.keyCode == KeyCode.PageUp ? _viewHeight * .8f : 42f), 0, Math.Max(0, _contentHeight - _viewHeight)); ev.Use(); }
        }
        if (_contentHeight > _viewHeight)
            _scroll = GUI.VerticalScrollbar(new Rect(viewport.xMax - 16, viewport.y, 16, _viewHeight),
                _scroll, _viewHeight, 0, _contentHeight);

        var enabled = GUI.enabled;
        GUI.enabled = _pending == null;
        GUI.BeginGroup(viewport);
        _y = 0;
        try
        {
            switch (_tab)
            {
                case 0: Players(); break;
                case 1: Bans(); break;
                case 2: Signs(); break;
                case 3: Logs(); break;
                case 4: Settings(); break;
            }
            _contentHeight = _y;
        }
        finally { GUI.EndGroup(); GUI.enabled = enabled; }
        _scroll = Mathf.Clamp(_scroll, 0, Math.Max(0, _contentHeight - _viewHeight));

        if (_pending != null)
        {
            var box = new Rect((Screen.width - 520) / 2f, (Screen.height - 190) / 2f, 520, 190);
            GUI.Box(box, "Confirm action");
            GUI.Label(new Rect(box.x + 20, box.y + 35, 480, 90), _confirmation, _label);
            if (GUI.Button(new Rect(box.x + 20, box.y + 140, 230, 32), "Cancel")) _pending = null;
            if (GUI.Button(new Rect(box.x + 270, box.y + 140, 230, 32), "Confirm"))
            { var action = _pending; _pending = null; action?.Invoke(); }
        }

        if (ev.type == EventType.MouseDown) _focus = null;
        if (ev.type == EventType.KeyDown || ev.type == EventType.KeyUp || ev.type == EventType.ScrollWheel) ev.Use();
    }

    private Rect Line(float height = 34)
    {
        var result = new Rect(0, _y - _scroll, _width, height);
        _y += height + 6;
        return result;
    }

    private bool Visible(Rect rect) => rect.yMax >= 0 && rect.y < _viewHeight;

    private void Text(string text, bool title = false)
    {
        var style = title ? _heading : _label;
        var height = Math.Max(title ? 34 : 24, style.CalcHeight(new GUIContent(text), _width));
        var rect = Line(height);
        if (Visible(rect)) GUI.Label(rect, text, style);
    }

    private void Buttons(params (string label, Action action)[] buttons)
    {
        var columns = Math.Max(1, (int)(_width / 185));
        for (int start = 0; start < buttons.Length; start += columns)
        {
            var rect = Line();
            var count = Math.Min(columns, buttons.Length - start);
            var buttonWidth = _width / count;
            if (!Visible(rect)) continue;
            for (int i = 0; i < count; i++)
                if (GUI.Button(new Rect(i * buttonWidth, rect.y, buttonWidth - 6, rect.height), buttons[start + i].label))
                { _focus = null; buttons[start + i].action(); }
        }
    }

    private void InputField(string key, string caption, ref string value)
    {
        Text(caption);
        var rect = Line();
        if (!Visible(rect)) return;
        GUI.Box(rect, value + (_focus == key ? " |" : ""), GUI.skin.textField);
        var ev = Event.current;
        if (GUI.enabled && ev.type == EventType.MouseDown && rect.Contains(ev.mousePosition))
        { _focus = key; ev.Use(); }
        if (!GUI.enabled || _focus != key || ev.type != EventType.KeyDown) return;
        if ((ev.control || ev.command) && ev.keyCode == KeyCode.V) value += GUIUtility.systemCopyBuffer;
        else if (ev.keyCode == KeyCode.Backspace && value.Length > 0) value = value.Substring(0, value.Length - 1);
        else if (ev.keyCode == KeyCode.Delete) value = "";
        else if (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter || ev.keyCode == KeyCode.Tab) _focus = null;
        else if (!ev.control && !ev.alt && !char.IsControl(ev.character)) value += ev.character;
        ev.Use();
    }

    private void Send(string action, string id = null, string key = null, int val = 0, string text = null, bool confirm = false)
    {
        bool localAction = action == "nametags" || action == "ban" || action == "unban";
        if (!NetworkServer.active && !localAction) { _status = "Host a lobby to use this control."; return; }
        if (confirm)
        {
            _confirmation = action + " - " + (text ?? key ?? id ?? "selected target") + "?";
            _pending = () => Send(action, id, key, val, text);
            return;
        }
        OrbBehaviour.Cmd(action, id, key, val, text);
        _status = !NetworkServer.active && (action == "ban" || action == "unban")
            ? "Local ban list updated for future hosted lobbies."
            : "Queued: " + action + ". Check Events for outcome.";
    }

    private void Details(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) { Text(value.ToString()); return; }
        Text(string.Join("   |   ", value.EnumerateObject()
            .Where(p => p.Value.ValueKind != JsonValueKind.Array && p.Value.ValueKind != JsonValueKind.Object)
            .Select(p => p.Name + ": " + p.Value)));
    }

    private void Players()
    {
        var snap = Get(Root, "snap");
        Text("Join code: " + Str(snap, "code"));
        Buttons(("Copy join code", () => GUIUtility.systemCopyBuffer = Str(snap, "code")),
            ("Name tags: " + OrbBehaviour.NametagsOn, () => Send("nametags", val: OrbBehaviour.NametagsOn ? 0 : 1)));

        foreach (var player in Rows(Get(snap, "players")))
        {
            Text(Str(player, "name") + (Flag(player, "local") ? " (you)" : "") + " - " + Str(player, "platform"), true);
            var colors = Line(16);
            int index = 0;
            foreach (var colorValue in Rows(Get(player, "colors")))
            {
                if (Visible(colors) && ColorUtility.TryParseHtmlString(colorValue.GetString(), out var color))
                {
                    GUI.color = color;
                    GUI.DrawTexture(new Rect(index * 32, colors.y, 26, 16), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                index++;
            }
            Text("ID: " + Str(player, "id") + "   Position: " + Get(player, "pos") + "   Speed: " + Str(player, "speed") + " m/s");
            Text("Username: " + Str(player, "username") + "   Moderation name: " + Str(player, "modName"));
            if (!Flag(player, "local") && !Flag(player, "isHost"))
            {
                var id = Str(player, "id");
                if (NetworkServer.active)
                    Buttons(("Kick", () => Send("kick", id, confirm: true)),
                        ("Ban", () => Send("ban", id, confirm: true)),
                        ("Lookup identity", () => Send("eoslookup", id)));
                else
                    Buttons(("Ban for future hosted lobbies", () => Send("ban", id, confirm: true)));
            }
        }

        InputField("password", "New session password (empty removes password)", ref _password);
        Text("Current password: " + (_showPassword ? Str(snap, "password") : "[hidden]"));
        Buttons(("Set password", () => Send("setpassword", text: _password)),
            (_showPassword ? "Hide password" : "Show password", () => _showPassword = !_showPassword));
    }

    private void Bans()
    {
        InputField("ban", "Ban identifier or connection address", ref _banId);
        Buttons(("Ban identifier", () => { if (!string.IsNullOrWhiteSpace(_banId)) Send("ban", _banId.Trim(), confirm: true); }),
            ("Ban address", () => { if (!string.IsNullOrWhiteSpace(_banId)) Send("banaddr", key: _banId.Trim(), text: "Manual ban", confirm: true); }));
        InputField("csv", "CSV file path (empty uses config/ModerationImprovements/bans-export.csv)", ref _csvPath);
        Buttons(("Export bans CSV", () => Csv(false)), ("Import bans CSV", () => Csv(true)));

        Text("Banned players", true);
        foreach (var ban in Rows(Get(Root, "bans")))
        {
            Details(ban);
            var id = Str(ban, "id");
            Buttons(("Unban", () => Send("unban", id)));
        }

        Text("Everyone this session", true);
        foreach (var player in Rows(Get(Root, "roster")))
        {
            Details(player);
            var id = Str(player, "id");
            var banned = Flag(player, "banned");
            var local = OrbBehaviour.Local()?.playerNetworking;
            var isHost = local != null && id == local.identifier;
            if (!isHost)
                Buttons((banned ? "Unban" : "Ban", () => Send(banned ? "unban" : "ban", id, confirm: !banned)));
        }
    }

    private void Csv(bool import)
    {
        try
        {
            var path = string.IsNullOrWhiteSpace(_csvPath)
                ? Path.Combine(OrbState.DataDir, "bans-export.csv") : _csvPath.Trim();
            if (import) Send("banimport", text: File.ReadAllText(path));
            else { File.WriteAllText(path, OrbState.BansCsv()); _status = "Exported: " + path; }
        }
        catch (Exception e) { _status = "CSV: " + e.Message; }
    }

    private void Signs()
    {
        if (!NetworkServer.active)
        {
            Text("Whiteboard controls are available only while hosting.");
            return;
        }

        InputField("sign", "Replacement sign text (used by Set text)", ref _signText);
        Text("Locked signs", true);
        foreach (var sign in Rows(Get(Root, "signlocks")))
        {
            Details(sign);
            var key = Str(sign, "net");
            Buttons(("Unlock", () => Send("signunlock", key: key)));
        }

        Text("Sign edit history", true);
        foreach (var sign in Rows(Get(Root, "signs")).Reverse())
        {
            Details(sign);
            var key = Str(sign, "net");
            var original = Str(sign, "text");
            if (string.IsNullOrEmpty(key) || key == "0") continue;
            Buttons(("Erase", () => Send("signset", key: key, text: "")),
                ("Restore this text", () => Send("signset", key: key, text: original)),
                ("Lock this text", () => Send("signlock", key: key, text: original)),
                ("Set text", () => Send("signset", key: key, text: _signText)));
        }
    }

    private void Logs()
    {
        Buttons(("Alerts", () => { _logKind = "alerts"; _scroll = 0; }),
            ("Chat", () => { _logKind = "chat"; _scroll = 0; }),
            ("Events", () => { _logKind = "events"; _scroll = 0; }),
            ("Identity lookups", () => { _logKind = "identities"; _scroll = 0; }));
        Text(_logKind, true);
        var entries = Get(Root, _logKind);
        if (entries.ValueKind == JsonValueKind.Array)
            foreach (var row in Rows(entries).Reverse()) Details(row);
        else if (entries.ValueKind == JsonValueKind.Object)
            foreach (var row in entries.EnumerateObject()) { Text(row.Name); Details(row.Value); }
    }

    private void Settings()
    {
        Text("Moderation settings", true);
        Buttons(("Join/leave chime: " + Plugin.ChimeEnabled.Value, () => Chime.SetEnabled(!Plugin.ChimeEnabled.Value)));
        Slider("Chime volume", Plugin.ChimeVolume, 0, 1);
        Buttons(("Auto-kick speed/fly: " + Plugin.FlyAutoKick.Value, () => Plugin.FlyAutoKick.Value = !Plugin.FlyAutoKick.Value),
            ("Guard auto-ban: " + Plugin.GuardAutoBan.Value, () => Plugin.GuardAutoBan.Value = !Plugin.GuardAutoBan.Value),
            ("Anonymous login guard: " + Plugin.GuardBanAnonymous.Value, () => Plugin.GuardBanAnonymous.Value = !Plugin.GuardBanAnonymous.Value),
            ("Filter fake system chat: " + Plugin.GuardChatFilter.Value, () => Plugin.GuardChatFilter.Value = !Plugin.GuardChatFilter.Value));
        Text("Speed/fly flags are estimates. Carrying, launches, and lag can trigger them.");
        Slider("Speed limit (m/s)", Plugin.FlyMaxSpeed, 1, 100);
        Slider("Climbing time limit (seconds)", Plugin.FlyMaxAirSeconds, 1, 60);
        Text("Voice packets / second: " + Plugin.GuardVoiceLimit.Value + " (0 disables limit)");
        Buttons(("-10 packets/s", () => Plugin.GuardVoiceLimit.Value = Math.Max(0, Plugin.GuardVoiceLimit.Value - 10)),
            ("+10 packets/s", () => Plugin.GuardVoiceLimit.Value += 10));
        Text("Press " + Plugin.MenuKey.Value + " or Escape to close.");
        Text("Moderation code taken from Big Orb by RadioFreeOpportunity. AdamMady made this in-game menu.");
        Buttons(("Open Original Big Orb Repository", () => Application.OpenURL(SourceUrl)),
            ("Moderation Improvements Repository", () => Application.OpenURL(RepoUrl)));
    }

    private void Slider(string caption, BepInEx.Configuration.ConfigEntry<float> config, float min, float max)
    {
        Text(caption + ": " + config.Value.ToString("0.00"));
        var rect = Line(22);
        if (!Visible(rect)) return;
        var value = GUI.HorizontalSlider(rect, config.Value, min, max);
        if (Math.Abs(value - config.Value) > .001f) config.Value = value;
    }
}
