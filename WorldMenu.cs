using System;
using System.Text.Json;
using Mirror;
using UnityEngine;

namespace BigOrb;

public partial class OrbMenu
{
    private string _worldKey = "", _worldValue = "0", _worldText = "", _worldId = "";
    private string _worldReport = "";
    private JsonElement Panels => Get(Get(Root, "world"), "panels");

    private bool WorldHost()
    {
        if (NetworkServer.active) return true;
        Text("World tools require hosting this lobby.");
        return false;
    }

    private void WorldAction(string label, string action, string key = null, int val = 0, string id = null, string text = null)
    {
        _confirmation = label + "? This changes the hosted world. Save progress before large resets.";
        _pending = () => Send(action, id, key, val, text);
    }

    private void Puzzles()
    {
        if (!WorldHost()) return;
        Text("Puzzle repair", true);
        Text("Reset restores captured world state. Individual puzzle helpers appear beside each puzzle.");
        foreach (var p in Rows(Get(Panels, "puzzles")))
        {
            var key = Str(p, "root");
            Text(Str(p, "label") + "  |  gourds home " + Str(p, "gourdsHome") + "/" + Str(p, "gourds"), true);
            Text(Str(p, "note"));
            Buttons(("Reset puzzle", () => WorldAction("Reset " + Str(p, "label"), "resetpuzzle", key)),
                ("Reset states", () => WorldAction("Reset states", "resetpuzzle", key, 1)),
                ("Return puzzle props", () => WorldAction("Return puzzle props", "resetpuzzle", key, 2)),
                ("Bring puzzle gourds", () => WorldAction("Bring puzzle gourds", "bringgourds", key)));
            var helper = Str(p, "cheat");
            if (!string.IsNullOrEmpty(helper))
                Buttons(("Run puzzle helper: " + helper, () => WorldAction("Run " + helper, helper, key)));
        }
    }

    private void HubsAndFinale()
    {
        if (!WorldHost()) return;
        Text("Hubs", true);
        foreach (var h in Rows(Get(Panels, "hubs")))
        {
            var key = Str(h, "root");
            Text(Str(h, "label") + "  |  slots " + Str(h, "filled") + "/" + Str(h, "slots"), true);
            Text(Str(h, "note") + "  Key: " + Str(h, "keyWhere"));
            Buttons(("Reset hub", () => WorldAction("Reset hub", "resethub", key)),
                ("Reset, keep gourds", () => WorldAction("Reset hub, keep gourds", "resethub", key, 1)),
                ("Fill hub", () => WorldAction("Fill hub", "hubfill", key)),
                ("Bring hub key", () => WorldAction("Bring hub key", "bring", Str(h, "save"))));
        }
        Text("Black tower finale", true);
        Buttons(("Reset whole finale", () => WorldAction("Reset whole finale", "finalereset", "all")));
        foreach (var stage in Rows(Get(Get(Panels, "finale"), "stages")))
        {
            var key = Str(stage, "key");
            Text(Str(stage, "label") + (Flag(stage, "done") ? "  [complete]" : ""));
            Text(Str(stage, "note"));
            Buttons(("Reset stage", () => WorldAction("Reset " + Str(stage, "label"), "finalereset", key)));
        }
    }

    private void ItemsAndProgress()
    {
        if (!WorldHost()) return;
        Text("Item restoration", true);
        Text("Uses fresh-world baseline. Capturing a replacement baseline records current positions.");
        Buttons(("Reset nearby items (20m)", () => WorldAction("Reset nearby items", "propreset", "near", 20)),
            ("Reset all items", () => WorldAction("Reset all items", "propreset", "all")),
            ("Capture current item baseline", () => WorldAction("Replace item baseline", "posebaseline")));
        var gourds = Get(Panels, "gourds");
        Text("Gourd progress", true);
        Text("Total: " + Str(gourds, "total") + "  Turned in: " + Str(gourds, "turnedIn") + "  Loose / held: " + Str(gourds, "out"));
        foreach (var row in Rows(Get(gourds, "list")))
        {
            Text(Str(row, "label") + "  " + Str(row, "where") + "  " + Str(row, "detail"));
            var net = Str(row, "net");
            if (string.IsNullOrEmpty(net)) net = Str(row, "netId");
            if (!string.IsNullOrEmpty(net))
                Buttons(("Bring this gourd", () => WorldAction("Bring gourd", "bringnet", net)));
        }
    }

    private void SolveFeed()
    {
        if (!WorldHost()) return;
        var solves = Get(Panels, "solves");
        Text("Puzzle solve feed", true);
        Buttons((Flag(solves, "chime") ? "Solve chime: on" : "Solve chime: off", () => Send("solvechime", val: Flag(solves, "chime") ? 0 : 1)),
            ("Clear solve feed", () => Send("solveclear")));
        foreach (var row in Rows(Get(solves, "recent")))
            Text(Str(row, "t") + "  " + Str(row, "what") + "  " + Str(row, "who"));
    }

    private void AdvancedWorldTools()
    {
        if (!WorldHost()) return;
        Text("All Big Orb module commands", true);
        Text("Use named puzzle/hub/stage roots shown in other tabs. Commands run directly in game; no web server required.");
        InputField("worldKey", "Target root / key", ref _worldKey);
        InputField("worldValue", "Numeric value", ref _worldValue);
        InputField("worldId", "Player identifier (optional)", ref _worldId);
        InputField("worldText", "Text / additional argument (optional)", ref _worldText);
        var mines = Get(Panels, "mines");
        if (mines.ValueKind == JsonValueKind.Object) { Text("Revealed mine state", true); Details(mines); }
        foreach (var module in Rows(Get(Get(Root, "world"), "scripts")))
        {
            Text(Str(module, "name"), true);
            foreach (var command in Rows(Get(module, "commands")))
            {
                var action = command.ToString();
                Buttons((action, () =>
                {
                    if (!int.TryParse(_worldValue, out var value)) { _status = "Numeric value must be a whole number."; return; }
                    WorldAction("Run " + action, action, _worldKey, value, string.IsNullOrEmpty(_worldId) ? null : _worldId,
                        string.IsNullOrEmpty(_worldText) ? null : _worldText);
                }));
            }
        }
        Text("Inspect world data", true);
        foreach (var name in Scripting.EndpointNames)
        {
            var endpoint = name;
            Buttons(("Inspect " + endpoint, () => OrbState.MainQueue.Enqueue(() =>
            {
                try
                {
                    using var document = JsonDocument.Parse(Scripting.EndpointOnMain(endpoint));
                    _worldReport = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
                }
                catch (Exception error) { _worldReport = "Inspection failed: " + error.Message; }
            })));
        }
        if (!string.IsNullOrEmpty(_worldReport)) Text(_worldReport);
    }
}
