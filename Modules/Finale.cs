using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace BigOrb;

// Black tower finale, one stage at a time. The front door itself is driven by the five tower hubs (HubReset).
//
// Stage "dreamdoor": NHoldFullSet has one N-hold rig per player count
// (NHoldSet2/3/4, ButtonNHoldTwo/Three/Four ×N). All held at once → NHoldSucess → one of the
// pressers gets the vision (BlackTowerDream/DreamStateSystem 0→1) → DreamDoorLogic 0→1
// (savable BlackTowerInteriorDoor) + BasicDoor DreamBlocker 0→1. Plain SetState(1) on the
// buttons trips the chain without a player in the contexts.
//   cmd finalereset key=<stage|all>         stage's states → baseline
//   cmd dreamhold val=N                     hold N of the current variant's N-hold buttons
//                                           (auto-release after 30s; leave one for a real press)
//   cmd dreamrelease                        release them now
//   panel finale                            per-stage live status
public class Finale : IOrbScript
{
    public string Name => "finale";
    private ScriptHost _h;
    private int _tick;
    private readonly List<TrackedPeckState> _held = new(); private float _releaseAt;
    private readonly List<(TrackedPeckState st, float at)> _bounces = new();
    private static Finale _instance;
    // The two ending gates (EndingGate_Door - Entry/Exit GateOpeningLogic) only animate shut on a
    // clean 1→0 transition; a 0 written right after the game re-opened them is swallowed and the
    // gate stays open (seen ). So: open, hold 3s, close. Called by HubReset when the
    // black key is pulled out of the ending plinth, and by the bell stage.
    public static void BounceEndingGates() => _instance?.BounceGates();
    // for other scripts (the cell): give one player the vision / wake just that player
    public static string DreamStatic(PlayerCharacter pc) => _instance != null ? _instance.GiveDream(pc) : "finale script not loaded";
    public static void WakeStatic(PlayerCharacter pc)
    {
        var (dc, _) = Controller(); if (dc == null || pc == null) return;
        try { pc.playerNetworking.ServerSetDream(dc, false); } catch { }
        try { if (dc.dreamPlayer == pc) dc.dreamPlayer = null; } catch { }
    }
    private void BounceGates()
    {
        int n = 0;
        foreach (var st in UnityEngine.Object.FindObjectsOfType<TrackedPeckState>(true))
        {
            if (st == null) continue;
            string path; try { path = Api.TransformPath(st.transform); } catch { continue; }
            if (path.IndexOf("EndingGate/GatePositioner/EndingGate_Door", StringComparison.OrdinalIgnoreCase) < 0 || !path.EndsWith("/GateOpeningLogic")) continue;
            try { st.SetState(1); _bounces.Add((st, Time.unscaledTime + 3f)); n++; } catch { }
        }
        if (n > 0) _h?.Event("finalereset", $"ending gates: {n} bounced open→close (3s)");
    }
    private readonly List<(string[] roots, float at)> _rechecks = new();

    // roots: hierarchy substrings whose states belong to the stage; probe: (path suffix → label) shown live
    private static readonly (string key, string label, string[] roots, (string suffix, string label)[] probe, string note, string anchor, float radius, string[] bounce, string[] props)[] Stages =
    {
        ("dreamdoor", "Dream door (N-hold → vision → interior door)",
         new[] { "BlackTower/Positioner/NHoldFullSet", "BlackTower/Positioner/DreamDoorLogic", "BlackTower/BlackTowerDream", "BlackTower/Positioner/BasicDoor DreamBlocker" },
         new[] { ("/DreamDoorLogic", "door"), ("/DreamStateSystem", "dream"), ("/NHoldSucess", "success"), ("DreamBlocker/BasicDoorPeckLogic", "blocker") },
         "hold all N buttons (N = player count) at once; one presser gets the vision, door unlocks", null, 0f, null, null),
        // verified 4 holds → SpeechlessFieldLogic 1→2 (crushing, ~14s) → 0 and the exit gate opens.
        // The entry gate + plinth are the black key's business (HubReset follows the key into the plinth).
        ("bell", "Bell room (4 holds → bell crushed → exit gate)",
         new[] { "EndingGate/GatePositioner/SecondGate", "EndingGate/GatePositioner/EndingGate_Door - Exit" },
         new[] { ("SpeechlessField/SpeechlessFieldLogic", "field"), ("EndingGate_Door - Exit/AnimatingPartsEndingGate/GateOpeningLogic", "exit"), ("NHoldPlinth0/ButtonNHoldFour/ButtonSystem", "hold0") },
         "field 1 = bell up, 2 = crushing, 0 = crushed; cheat: nhold key=EndingGate/GatePositioner/SecondGate/NHolds", null, 0f, null, null),
        // ---- the orb: the silent gauntlet, one level per stage (in order) ----
        // Level 0: 9-tile peg board → validator button → ChallengeCompletedSystem (GauntletChamber0)
        // → "Challenge Complete" gate; a separate "All Hold" gate on the stairs needs everyone holding.
        ("gauntlet0", "Gauntlet 0 — peg board (9 tiles)",
         new[] { "Positioner/Level0/" },
         new[] { ("Level0/GauntletChamber00PegBoard/GauntletHallway/GauntletChamberNetworkObjects/ChallengeCompletedSystem", "complete"), ("Level0/GourdTower_Stairs/GourdTower_Gate Challenge Complete/GourdTowerGateNetworking", "gate"), ("Level0/GourdTower_Stairs/GourdTower_Gate All Hold/GourdTowerGateNetworking", "holdgate") },
         "9 tiles back on the rack (by guid — wherever they were carried), gates closed, completion cleared, room doors bounced to re-scramble", null, 0f,
         new[] { "Level0/GauntletChamber00PegBoard/Room Always/BasicDoor Always/BasicDoorPeckLogic", "Room Teller1/BasicDoor Teller1/BasicDoorPeckLogic", "Room Teller2/BasicDoor Teller2/BasicDoorPeckLogic" },
         new[] { "fc58b531-2bfa-46ca-9ecf-fb53bc5cf37f", "a2d05ea0-6cbd-433a-84af-a194a05d6536", "5eec232c-c20f-432f-95dc-1c8d6b32e159", "1690f6b6-2f55-404f-b578-dd7bf7ca3d47", "987b4ef6-7b9d-483f-9eba-4bcf1e0643f0", "556ba962-5281-47ac-9ecf-b7ba73b4ef15", "dceb4dd3-5f79-423c-b83c-39cca9e1bae2", "c02dde13-6886-411c-9b8c-b558473b1316", "8bde95f3-e638-4ddf-95c8-e30613cd9e42" }),
        // Level 1: press-in-order (Pointers' Paradise family) — progress tracker + indicator lights, no props; re-arms on door close
        Gauntlet(1, "GauntletChamber01Pointers", "Gauntlet 1 — pointers (press in order)", "tracker + lights to baseline, doors bounced to re-arm", null, 0f,
                 new[] { "Level1/GauntletChamber01Pointers/DoorGroup/BasicDoor/BasicDoorPeckLogic", "DoorGroup/BasicDoor 3PlayerAndUp/BasicDoorPeckLogic", "DoorGroup/BasicDoor4Player/BasicDoorPeckLogic" }, null),
        // Level 2: kick the pomodoro (dispenser → turnstiles/ramps → receptacle pedestal); the pomodoro is a prop → pose reset around the pedestal
        Gauntlet(2, "GauntletKick02ToKick", "Gauntlet 2 — kick to kick (pomodoro → pedestal)", "receptacle/doors/turnstiles to baseline, pomodoro back to the dispenser (pose reset 40m)", "Level2/GauntletKick02ToKick/BasicReceptical", 40f, null, null),
        // Level 3: four sim-press switches (self-resetting) — only the completion + gates matter
        Gauntlet(3, "GauntletChamber03SimPress", "Gauntlet 3 — sim-press (4 switches)", "switches self-reset; completion + gates to baseline", null, 0f, null, null),
        // Level 4: press-in-order (volleyball) — the special red kettle is a homeless prop, re-posed by guid
        Gauntlet(4, "GauntletChamberVolleyBall", "Gauntlet 4 — volleyball (press in order, red kettle)", "tracker + lights to baseline, kettle back by guid, doors bounced", null, 0f,
                 new[] { "Level4/GauntletChamberVolleyBall/Doors/BasicDoor 1 (primary)/BasicDoorPeckLogic", "Doors/BasicDoor 2/BasicDoorPeckLogic", "Doors/BasicDoor 3/BasicDoorPeckLogic", "Doors/BasicDoor 4/BasicDoorPeckLogic" },
                 new[] { "33494b4b-6d5e-4ac4-bcbb-fb43ddeb6bf2" }),
        // Level 5: invisible-ink peg board (telescope + nonverbal comms) — 9 tiles back on the rack, doors bounced to re-scramble
        Gauntlet(5, "GauntletChamberInvisi", "Gauntlet 5 — invisible ink (peg board, telescope)", "9 tiles back on the rack (by guid), screen/completion/gates cleared, doors bounced to re-scramble", null, 0f,
                 new[] { "Level5/GauntletChamberInvisi/Door1/BasicDoor/BasicDoorPeckLogic", "Level5/GauntletChamberInvisi/Door2/BasicDoor/BasicDoorPeckLogic", "3PlayerAndUp/Door3/", "4PlayerOnly/Door4/" },
                 new[] { "3016c28d-21d9-40fd-9875-2d3470c19efe", "abbcb967-6c81-4cae-b246-c809f364fbe0", "a3088495-97ef-4a27-bedf-98f3bedb606a", "cee0452e-f813-46b2-adbc-b2580379555c", "c7aa5a25-c784-455d-aadd-ad3b89a6b208", "e007cc19-9f5b-4d45-af25-1d722c50b74d", "7211c43f-4efd-4c8b-ae62-01fbd8a62308", "90dc6ef0-92e4-4637-821c-307a9fc97a64", "1207f056-b89d-4280-9ebf-a848b95db57e" }),
        // Level 6: sculptures press-in-order (nonverbal) — tracker + lights to baseline, four doors bounced
        Gauntlet(6, "GauntletChamber06Sculptures", "Gauntlet 6 — sculptures (press in order)", "tracker + lights to baseline, doors bounced to re-arm", null, 0f,
                 new[] { "Level6/GauntletChamber06Sculptures/Doors/BasicDoor 1 (primary)/", "Doors/BasicDoor 2 /", "Doors/BasicDoor 3/", "Doors/BasicDoor 4/" }),
        // Level 7 (top of the orb): 4-hold → final bell crushed (SpeechlessField 1→2→0) → skylight door + GauntletComplete save flag
        ("gauntlet7", "Gauntlet 7 — final bell (4-hold → skylight)",
         new[] { "Positioner/Level7/", "CreditsTheatre/Positioner/CreditsScreen" }, // credits: screen off + button primed, nothing persistent
         new[] { ("Level7/SpeechlessField/SpeechlessFieldLogic", "field"), ("Level7/GauntletEndingLogicGroup/SkylightDoorSystem", "skylight"), ("Level7/GauntletEndingLogicGroup/GauntletIsCompletedSystem", "complete") },
         "field 1 = bell up; reset puts the bell back, closes the skylight, clears GauntletComplete; cheat: nhold key=Level7/NHoldAnchor", null, 0f, null, null),
    };

    // one gauntlet level = everything under Level<n>/ (chamber + the two stair gates + all-hold set)
    private static (string, string, string[], (string, string)[], string, string, float, string[], string[]) Gauntlet(int n, string chamber, string label, string note, string anchor, float radius, string[] bounce, string[] props = null) =>
        ("gauntlet" + n, label, new[] { "Positioner/Level" + n + "/" },
         new[] { ("Level" + n + "/" + chamber + "/GauntletHallway/GauntletChamberNetworkObjects/ChallengeCompletedSystem", "complete"), ("Level" + n + "/GourdTower_Stairs/GourdTower_Gate Challenge Complete/GourdTowerGateNetworking", "gate"), ("Level" + n + "/GourdTower_Stairs/GourdTower_Gate All Hold/GourdTowerGateNetworking", "holdgate") },
         note, anchor, radius, bounce, props);

    public void Load(ScriptHost host)
    {
        _h = host; _instance = this;
        // key=all = the whole finale: every stage here + the black monument (HubReset row, which
        // also pulls the key out of the ending plinth and resets the gate + bell behind it)
        host.Command("finalereset", (id, key, val, text) =>
        {
            int n = 0;
            if (string.Equals(key, "all", StringComparison.OrdinalIgnoreCase)) { try { HubReset.ResetStatic("BlackTower/Positioner/GourdPlinth X6"); } catch (Exception e) { host.Warn("monument: " + e.Message); } }
            // the orb re-opens every gate below the highest player (verified). Warn, don't refuse.
            if (key.StartsWith("gauntlet", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "all", StringComparison.OrdinalIgnoreCase))
            {
                var inside = new List<string>();
                foreach (var pc in Api.Players())
                {
                    try { var q = pc.transform.position; if (q.x > -800 && q.x < -680 && q.z > 640 && q.z < 740 && q.y > 40) inside.Add($"{Api.Display(pc.playerNetworking)} (y {q.y:0})"); } catch { }
                }
                if (inside.Count > 0) host.Event("finalereset", "⚠ players inside the orb above the entrance — the tower will re-open the gates below them: " + string.Join(", ", inside));
            }
            foreach (var s in Stages)
            {
                if (!string.Equals(key, "all", StringComparison.OrdinalIgnoreCase) && !string.Equals(key, s.key, StringComparison.OrdinalIgnoreCase)) continue;
                var notes = new List<string>();
                if (s.key == "dreamdoor") { Release(); if (_trapped.Count > 0) { notes.Add("traps dropped: " + string.Join(", ", _trapped.Values)); _trapped.Clear(); } StopDream(notes); }
                if (s.key == "bell") BounceGates();
                int props = 0;
                if (!string.IsNullOrEmpty(s.anchor) && s.radius > 0)
                {
                    var at = Anchor(s.anchor);
                    if (at == null) notes.Add("anchor not found: " + s.anchor);
                    else { try { var r = Api.ResetPropsAround(at.Value, s.radius); props = r.reset; notes.Add($"{r.reset}/{r.matched} prop(s) re-posed{(r.held > 0 ? $", {r.held} held" : "")}"); } catch (Exception e) { notes.Add("props: " + e.Message); } }
                }
                if (s.props != null) foreach (var pk in s.props) { try { Api.ResetProps(pk, 0); notes.Add("re-posed " + pk.Substring(0, Math.Min(8, pk.Length))); } catch (Exception e) { notes.Add("prop " + pk + ": " + e.Message); } }
                int states = HubReset.ResetStatesUnder(s.roots, notes);
                if (s.bounce != null)
                    foreach (var st in UnityEngine.Object.FindObjectsOfType<TrackedPeckState>(true))
                    {
                        if (st == null) continue;
                        string path; try { path = Api.TransformPath(st.transform); } catch { continue; }
                        // bounce entries are substrings; the state must also sit under one of the stage's roots and be a door
                        bool inStage = false; foreach (var r in s.roots) if (path.IndexOf(r, StringComparison.OrdinalIgnoreCase) >= 0) { inStage = true; break; }
                        if (!inStage || !path.EndsWith("/BasicDoorPeckLogic", StringComparison.OrdinalIgnoreCase)) continue;
                        foreach (var b in s.bounce)
                            if (path.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0) { try { st.SetState(1); _bounces.Add((st, Time.unscaledTime + 1f)); notes.Add("bounced " + b.TrimEnd('/').Split('/')[^1]); } catch (Exception e) { notes.Add("bounce: " + e.Message); } break; }
                    }
                // the bounce lights doorway LiveSignals that the close never turns off → state pass again after it settles
                if (s.bounce != null) _rechecks.Add((s.roots, Time.unscaledTime + 2.5f));
                host.Event("finalereset", $"{s.key}: {states} state(s) → baseline{(notes.Count > 0 ? " — " + string.Join("; ", notes) : "")}");
                n++;
            }
            if (n == 0) host.Event("finalereset", $"unknown stage {key}; stages: {string.Join(", ", Array.ConvertAll(Stages, s => s.key))}, all");
            Publish();
        });
        host.Command("dreamhold", (id, key, val, text) =>
        {
            Release();
            int variant = 4; try { variant = Api.WorldVariant(); } catch { }
            string set = "NHoldFullSet/NHoldSet" + variant + "/";
            int want = val > 0 ? val : variant - 1; // default: leave one for a real press
            foreach (var st in UnityEngine.Object.FindObjectsOfType<TrackedPeckState>(true))
            {
                if (st == null || _held.Count >= want) continue;
                string path; try { path = Api.TransformPath(st.transform); } catch { continue; }
                if (path.IndexOf(set, StringComparison.OrdinalIgnoreCase) < 0 || !path.EndsWith("/ButtonSystem")) continue;
                try { st.SetState(1); _held.Add(st); } catch (Exception e) { host.Warn("dreamhold: " + e.Message); }
            }
            _releaseAt = Time.unscaledTime + 30f;
            host.Event("dreamhold", $"holding {_held.Count} of the {variant}-hold buttons for 30s — press the rest for real");
        });
        // cmd gauntletopen key=N: force a level's challenge — VictoryLogic → 1 (completion + challenge gate)
        // cmd gauntlethold key=N: press the stairs' all-hold set (the gate everyone must hold for) — kept separate on purpose
        host.Command("gauntletopen", (id, key, val, text) => GauntletPress(key, true, false));
        host.Command("gauntlethold", (id, key, val, text) => GauntletPress(key, false, true));
        host.Command("dreamrelease", (id, key, val, text) => { int n = _held.Count; Release(); host.Event("dreamhold", $"released {n}"); });
        // the vision-forcing tools (dream / dreamtrap / dreamfree) are not part of this build
        // generic: cmd hold key=<path substring> val=N — hold N ButtonSystems under the path for
        // 30s (plain state: every held button reads as ONE presser, so leave the rest to people)
        host.Command("hold", (id, key, val, text) =>
        {
            Release();
            int want = val > 0 ? val : 1;
            foreach (var st in UnityEngine.Object.FindObjectsOfType<TrackedPeckState>(true))
            {
                if (st == null || _held.Count >= want) continue;
                string path; try { path = Api.TransformPath(st.transform); } catch { continue; }
                if (path.IndexOf(key ?? "", StringComparison.OrdinalIgnoreCase) < 0 || !path.EndsWith("/ButtonSystem")) continue;
                try { st.SetState(1); _held.Add(st); } catch (Exception e) { host.Warn("hold: " + e.Message); }
            }
            _releaseAt = Time.unscaledTime + 30f;
            host.Event("hold", $"holding {_held.Count} button(s) under {key} for 30s");
        });
        // cmd dreamstop: end the dream for everyone (states untouched)
        host.Command("dreamstop", (id, key, val, text) => { var notes = new List<string>(); StopDream(notes); host.Event("dream", string.Join("; ", notes)); });
        try
        {
            var m = typeof(HouseHouse.Dream.DreamController).GetMethod("OnStartDreamPeck", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            host.Harmony.Patch(m, prefix: new HarmonyMethod(typeof(Finale).GetMethod(nameof(StartDreamPrefix), BindingFlags.Static | BindingFlags.Public)));
            _patched = true;
        }
        catch (Exception e) { host.Warn("patch OnStartDreamPeck: " + e.Message); }
        host.Tick(() =>
        {
            if (_held.Count > 0 && Time.unscaledTime >= _releaseAt) { int n = _held.Count; Release(); host.Event("dreamhold", $"auto-released {n}"); }
            TrapTick();
            for (int i = _bounces.Count - 1; i >= 0; i--) if (Time.unscaledTime >= _bounces[i].at) { try { _bounces[i].st.SetState(0); } catch { } _bounces.RemoveAt(i); }
            for (int i = _rechecks.Count - 1; i >= 0; i--) if (Time.unscaledTime >= _rechecks[i].at) { var n = HubReset.ResetStatesUnder(_rechecks[i].roots, null); if (n > 0) _h.Event("finalereset", $"settle pass: {n} state(s) re-applied"); _rechecks.RemoveAt(i); }
            if (++_tick % 8 == 6 && Api.DashboardLive) Publish();
        });
        Publish();
    }

    private static float _suppressUntil; private static bool _patched;
    // skip DreamController.OnStartDreamPeck for a few seconds around our own state write (auto-unpatched on reload)
    public static bool StartDreamPrefix() => Time.unscaledTime >= _suppressUntil;

    private readonly Dictionary<IntPtr, string> _trapped = new();
    private float _nextTrapCheck;

    // give one player the vision: DreamStateSystem → 1 with the game's random pick suppressed
    // (it remembers earlier pressers and would dream a second player), then the same RPC the
    // game makes. Returns an error string or null.
    private string GiveDream(PlayerCharacter pc)
    {
        var (dc, st) = Controller();
        if (dc == null) return "no DreamController (BlackTowerDream/DreamStateSystem)";
        try
        {
            // the game's random pick (OnStartDreamPeck) can land a frame or more after the state
            // write, so suppress it for a window, not just the call — otherwise a random past
            // presser of the dream-door buttons gets the vision too (seen in tpall)
            _suppressUntil = Time.unscaledTime + 3f;
            int cur = -1; try { var c = st.currentPeckContext; if (c != null) cur = c.state; } catch { }
            if (cur != 1) { try { st.SetState(1); } catch (Exception e) { _h.Warn("dream state: " + e.Message); } }
            dc.dreamPlayer = pc;
            pc.playerNetworking.ServerSetDream(dc, true);
            return null;
        }
        catch (Exception e) { return "failed: " + e.Message; }
    }

    // re-arm trapped players whose dream has ended (the DreamOver peck stops dc.dreamPlayer)
    private void TrapTick()
    {
        if (_trapped.Count == 0 || Time.unscaledTime < _nextTrapCheck) return;
        _nextTrapCheck = Time.unscaledTime + 1f;
        var gone = new List<IntPtr>();
        foreach (var kv in _trapped)
        {
            PlayerCharacter pc = null;
            foreach (var p in Api.Players()) if (p.Pointer == kv.Key) { pc = p; break; }
            if (pc == null) { gone.Add(kv.Key); continue; } // left the lobby
            bool dreaming = true; try { dreaming = pc.dreamer.isDreaming; } catch { }
            if (!dreaming) { var err = GiveDream(pc); if (err != null) _h.Warn("dreamtrap " + kv.Value + ": " + err); }
        }
        foreach (var k in gone) { _h.Event("dreamtrap", _trapped[k] + " left — trap dropped"); _trapped.Remove(k); }
    }

    private void Release()
    {
        foreach (var st in _held) { try { st.SetState(0); } catch { } }
        _held.Clear();
    }

    private static PlayerCharacter Pick(string id, string text)
    {
        if (!string.IsNullOrEmpty(id)) { try { var p = Api.ById(id); if (p != null) return p; } catch { } }
        if (!string.IsNullOrEmpty(text))
            foreach (var p in Api.Players())
            {
                try { if (Api.Display(p.playerNetworking).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) return p; } catch { }
            }
        return null;
    }

    // id: a player id, "all", "others" (everyone but the host); text: name substring; defaultHost: empty → host
    private static List<PlayerCharacter> Targets(string id, string text, bool defaultHost)
    {
        var list = new List<PlayerCharacter>();
        bool all = string.Equals(id, "all", StringComparison.OrdinalIgnoreCase), others = string.Equals(id, "others", StringComparison.OrdinalIgnoreCase);
        if (all || others)
        {
            var me = Api.Local();
            foreach (var p in Api.Players()) if (p != null && !(others && p == me)) list.Add(p);
            return list;
        }
        var one = Pick(id, text) ?? (defaultHost && string.IsNullOrEmpty(id) && string.IsNullOrEmpty(text) ? Api.Local() : null);
        if (one != null) list.Add(one);
        return list;
    }

    private void GauntletPress(string key, bool victory, bool hold)
    {
        int n = 0; if (!int.TryParse(key, out n)) { _h.Event("gauntlet", "need key=<level>"); return; }
        int variant = 4; try { variant = Api.WorldVariant(); } catch { }
        int hit = 0;
        foreach (var st in UnityEngine.Object.FindObjectsOfType<TrackedPeckState>(true))
        {
            if (st == null) continue;
            string path; try { path = Api.TransformPath(st.transform); } catch { continue; }
            if (path.IndexOf("/Level" + n + "/", StringComparison.OrdinalIgnoreCase) < 0) continue;
            bool isV = victory && path.EndsWith("/GauntletChamberNetworkObjects/VictoryLogic", StringComparison.OrdinalIgnoreCase);
            bool isH = hold && path.IndexOf("GourdTower_Stairs/NHoldFullSet/NHoldSet" + variant + "/", StringComparison.OrdinalIgnoreCase) >= 0 && path.EndsWith("/ButtonSystem");
            if (!isV && !isH) continue;
            try { st.SetState(1); hit++; if (isH) _held.Add(st); } catch (Exception e) { _h.Warn("gauntlet: " + e.Message); }
        }
        if (hold) _releaseAt = Time.unscaledTime + 1.5f;
        _h.Event(victory ? "gauntletopen" : "gauntlethold", $"level {n}: {hit} state(s) pressed ({(victory ? "victory" : variant + "-hold")})");
    }

    private static Vector3? Anchor(string suffix)
    {
        foreach (var st in UnityEngine.Object.FindObjectsOfType<TrackedPeckState>(true))
        {
            if (st == null) continue;
            string path; try { path = Api.TransformPath(st.transform); } catch { continue; }
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return st.transform.position;
        }
        return null;
    }

    private static (HouseHouse.Dream.DreamController dc, TrackedPeckState st) Controller()
    {
        foreach (var st in UnityEngine.Object.FindObjectsOfType<TrackedPeckState>(true))
        {
            if (st == null) continue;
            string path; try { path = Api.TransformPath(st.transform); } catch { continue; }
            if (!path.EndsWith("BlackTowerDream/DreamStateSystem", StringComparison.OrdinalIgnoreCase)) continue;
            var dc = st.GetComponent<HouseHouse.Dream.DreamController>();
            return (dc, st);
        }
        return (null, null);
    }

    // stop an active dream before the states go back (ServerSetDream(false) is what the
    // DreamOver peck does; a dreaming player left mid-dream would otherwise be stuck in it)
    // Stops it for EVERY player, not just dc.dreamPlayer: the game's own pick and a forced one
    // can both be dreaming (a host got stuck that way), and ServerSetDream(false) on a player
    // who isn't dreaming is harmless.
    private void StopDream(List<string> notes)
    {
        var (dc, _) = Controller(); if (dc == null) { notes?.Add("no DreamController"); return; }
        int n = 0;
        foreach (var p in Api.Players())
        {
            try { p.playerNetworking.ServerSetDream(dc, false); n++; } catch (Exception e) { notes?.Add("stop " + Api.Display(p.playerNetworking) + ": " + e.Message); }
        }
        try { dc.dreamPlayer = null; } catch { }
        notes?.Add($"dream stopped for {n} player(s)");
    }

    // the probed states are static world logic: sweep + path-match once a minute, then only
    // read their current values per publish (this used to walk every TrackedPeckState every 2s)
    private readonly List<(TrackedPeckState st, string key)> _probeIdx = new();
    private float _probeIdxAt = -999f;
    private void IndexProbes()
    {
        _probeIdx.Clear(); _probeIdxAt = Time.unscaledTime;
        foreach (var st in UnityEngine.Object.FindObjectsOfType<TrackedPeckState>(true))
        {
            if (st == null) continue;
            string path; try { path = Api.TransformPath(st.transform); } catch { continue; }
            if (path.IndexOf("BlackTower", StringComparison.OrdinalIgnoreCase) < 0 && path.IndexOf("EndingGate", StringComparison.OrdinalIgnoreCase) < 0 && path.IndexOf("Gauntlet", StringComparison.OrdinalIgnoreCase) < 0) continue;
            foreach (var s in Stages) foreach (var (suffix, label) in s.probe)
                if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) _probeIdx.Add((st, s.key + "/" + label));
        }
    }

    private void Publish()
    {
        if (Time.unscaledTime - _probeIdxAt > 60f || _probeIdx.Count == 0) IndexProbes();
        var vals = new Dictionary<string, int>();
        foreach (var (st, key) in _probeIdx)
        {
            if (st == null) continue;
            int cur = int.MinValue; try { var c = st.currentPeckContext; if (c != null) cur = c.state; } catch { }
            vals[key] = cur;
        }
        var sb = new StringBuilder("{\"held\":").Append(_held.Count).Append(",\"trapped\":[");
        bool ft = true; foreach (var w in _trapped.Values) { if (!ft) sb.Append(','); ft = false; sb.Append(Api.Json(w)); }
        sb.Append("],\"stages\":[");
        for (int i = 0; i < Stages.Length; i++)
        {
            var s = Stages[i];
            if (i > 0) sb.Append(',');
            bool done = false; try { done = s.key == "dreamdoor" ? vals.TryGetValue("dreamdoor/door", out var dv) && dv == 1 : s.key == "bell" ? vals.TryGetValue("bell/exit", out var ev) && ev == 1 : s.key == "gauntlet7" ? vals.TryGetValue("gauntlet7/skylight", out var sv) && sv == 1 : vals.TryGetValue(s.key + "/gate", out var gv) && gv == 1; } catch { }
            sb.Append("{\"key\":").Append(Api.Json(s.key)).Append(",\"label\":").Append(Api.Json(s.label)).Append(",\"note\":").Append(Api.Json(s.note)).Append(",\"done\":").Append(done ? "true" : "false").Append(",\"probe\":{");
            bool first = true;
            foreach (var (suffix, label) in s.probe)
            {
                if (!first) sb.Append(','); first = false;
                sb.Append(Api.Json(label)).Append(':').Append(vals.TryGetValue(s.key + "/" + label, out var v) && v != int.MinValue ? v.ToString() : "null");
            }
            sb.Append("}}");
        }
        _h.Panel("finale", sb.Append("]}").ToString());
    }

    public void Unload() { Release(); _trapped.Clear(); }
}
