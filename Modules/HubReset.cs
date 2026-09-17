using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Mirror;

namespace BigOrb;

// Hub (gourd monument) reset. Captured on IntroPavilion:
//   gourds in slots → gourdIsPinnedSystem 1 ×N, GourdMonumentRewardSystem 1, KeyStoneLogic 0→2
//   (key released), IntroCage CageLogic/Monument0State 1, map flag 1, ending indicator 1
//   cutting → each CuttingTrailStation AnimationSystem 2 + ArrowSystem 1; KeyBlank.cuts[i]=true,
//   all cut → prop group BigKeyComplete
//   key on plinth → BigKeyPlinthNetworking 1 → DrawBridgeLogic 1; save bigKeyIntro=210 (=plinth home)
// Reset = states under every listed root → baseline, key blanked + back in its keystone,
// gourds back to their own puzzles (each has a startHome) with those puzzles reset — or
// just dropped at the hub if val=1.
//   cmd resethub key=<hub root> [val=1 keep gourds at hub]
public class HubReset : IOrbScript
{
    public string Name => "hubreset";
    private ScriptHost _h;

    // roots: every hierarchy substring whose TrackedPeckStates belong to this hub's chain
    // keySave: the key's SaveablePropName (identifies the BigKeyProp)
    private static readonly (string root, string label, string keySave, string[] roots, string note)[] Hubs =
    {
        ("IntroPavilion", "Tutorial hub (intro pavilion)", "bigKeyIntro",
         new[] { "IntroPavilion", "CuttingTrails/CuttingTrailBoardwalk", "IntroCage", "Walkway-IntroExit", "MonumentMapFlag Intro", "EndingGateIndicator" },
         "4 slots → blank key → 5 cutters → drawbridge. Known: cutter plate markers + key cover meshes only refresh for connected clients on rejoin (game never re-enables them over the network); host is fixed locally"),
        ("Lookout_RedCone/RoofStuff", "Red tower hub (lookout roof)", "bigKeyRedZone",
         new[] { "Lookout_RedCone/RoofStuff", "CuttingTrails/CuttingTrailRedTower", "MonumentMapFlag Red", "UnlockLogics/Lookout0Red", "EndingGateIndicator (1)" },
         "5 slots → red key → 5 cutters → MapRoom plinth; door/lights separate (Doors panel); verified"),
        ("Lookout_GreenHourglass/RoofStuff", "Green tower hub (lookout roof)", "bigKeyGreenZone",
         new[] { "Lookout_GreenHourglass/RoofStuff", "CuttingTrails/CuttingTrailGreenTower", "MonumentMapFlag Green", "UnlockLogics/Lookout1Green", "EndingGateIndicator (2)" },
         "5 slots → green key → 5 cutters → chairlift plinth (ChairliftBend_green); door/lights separate (Doors panel); indicator index verified"),
        ("Lookout_BlueTube/RoofStuff", "Blue tower hub (lookout roof)", "bigKeyBlueZone",
         new[] { "Lookout_BlueTube/RoofStuff", "CuttingTrails/CuttingTrailBlueTower", "MonumentMapFlag Blue", "UnlockLogics/Lookout2Blue", "EndingGateIndicator (3)" },
         "5 slots → blue key → 5 cutters; door/lights separate (Doors panel); indicator index verified"),
        ("Lookout_YellowZigzag/RoofStuff", "Yellow tower hub (lookout roof)", "bigKeyYellowZone",
         new[] { "Lookout_YellowZigzag/RoofStuff", "CuttingTrails/CuttingTrailYellowTower", "MonumentMapFlag Yellow", "UnlockLogics/Lookout3Yellow", "EndingGateIndicator (4)" },
         "5 slots → yellow key → 5 cutters; door/lights separate (Doors panel); indicator index verified"),
        // finale: the monument inside the black tower. The black key needs no cutting (it is
        // already BigKeyEndingComplete); it opens the gate to the finale area. Not a tower hub:
        // the TowerShared extras (front door / lights / black flag) must not apply here.
        ("BlackTower/Positioner/GourdPlinth X6", "Black tower monument (6 gourds → black key)", "bigKeyBoss",
         new[] { "BlackTower/Positioner/GourdPlinth X6", "BlackTower/Positioner/BigKeyInStoneEnding", "BigKeyInBox Ending", "MonumentMapFlag Overflow", "Indicators/EndingGateIndicator - black", "OverflowMonument/Positioner/BasicDoor - to overflow", "OverflowMonument/Positioner/Lights/" },
         "6 slots → black key (no cutting) → ending gate plinth; completing it un-hides the overflow monument's map flag + lights its black indicator (both re-hidden on reset); reset sends the key back to its stone"),
    };
    // (root, label) for every registered hub — Progress uses it to name where a gourd was turned in
    public static IEnumerable<(string root, string label)> Table() { foreach (var h in Hubs) yield return (h.root, h.label); }
    private static bool IsTowerHub(string root) => !root.StartsWith("BlackTower", StringComparison.OrdinalIgnoreCase);
    // loose gourds this close to a hub's slots count as "at the hub" for a reset
    private const float LooseRadius = 25f;
    // Black tower front door opens when all five tower indicators are lit (verified). The game closes the door and lights again when an indicator drops, but nothing ever
    // re-hides the map flag — so every tower hub reset also puts these back to baseline.
    private static readonly string[] TowerShared = { "MonumentMapFlag Black", "BlackTower/Positioner/BasicDoor/", "BlackTower/Positioner/Lighting/" };

    private static HubReset _instance;
    public static void ResetStatic(string hubRoot, bool keepGourds = false) => _instance?.Reset(hubRoot, keepGourds);

    public void Load(ScriptHost host)
    {
        _h = host; _instance = this;
        host.Command("resethub", (id, key, val, text) => Reset(string.IsNullOrEmpty(key) ? Hubs[0].root : key, val == 1));
        // cheat/test: cmd hubfill key=<hub root> [val=N]: pin up to N (default: all empty slots)
        // registered puzzle gourds into the hub's empty slots, taking them from their puzzles
        host.Command("hubfill", (id, key, val, text) =>
        {
            int hi = Array.FindIndex(Hubs, h => string.Equals(h.root, key, StringComparison.OrdinalIgnoreCase));
            if (hi < 0) { host.Event("hubfill", $"unknown hub {key}"); return; }
            var hub = Hubs[hi];
            var empty = new List<PropHome>();
            var homes = PropHome.allPropHomes;
            if (homes != null)
                for (int i = 0; i < homes.Count; i++)
                {
                    PropHome h = null; try { h = homes[i]; } catch { }
                    if (h == null) continue;
                    try { if (h.pinGroup == PropGroup.RewardGourd && h.pinnedProp == null && Under(Api.TransformPath(h.transform), hub.root)) empty.Add(h); } catch { }
                }
            int want = val > 0 ? Math.Min(val, empty.Count) : empty.Count;
            var used = new List<string>(); int k = 0;
            var props = Prop.allProps;
            if (props != null)
                for (int i = 0; i < props.Count && k < want; i++)
                {
                    Prop g = null; try { g = props[i]; } catch { }
                    if (g == null) continue;
                    try
                    {
                        if (!g.MatchesGroup(PropGroup.RewardGourd) || g.startHome == null || g.currentHome != g.startHome) continue;
                        if (Under(Api.TransformPath(g.startHome.transform), hub.root)) continue; // a hub's own gourd
                        if (PuzzleReset.RootForGourd(g) == null) continue;                          // only table puzzles
                        g.ServerSetUnpinned(); g.ServerSetPinned(empty[k]);
                        used.Add(g.saveablePropName.ToString()); k++;
                    }
                    catch (Exception e) { host.Warn("hubfill: " + e.Message); }
                }
            host.Event("hubfill", $"{hub.root}: {k}/{empty.Count} empty slot(s) filled — {string.Join(", ", used)}");
        });
        // cheat/test: cmd hubkey key=<hub root> text=<plinth path substring>: fully cut the hub's
        // key and pin it into that plinth (the plinth checks the cut data, so a blank key is rejected)
        host.Command("hubkey", (id, key, val, text) =>
        {
            int hi = Array.FindIndex(Hubs, h => string.Equals(h.root, key, StringComparison.OrdinalIgnoreCase));
            if (hi < 0 || string.IsNullOrEmpty(text)) { host.Event("hubkey", "need key=<hub root> text=<plinth substring>"); return; }
            var hub = Hubs[hi];
            Prop keyProp = null; var props = Prop.allProps;
            for (int i = 0; props != null && i < props.Count; i++) { Prop p = null; try { p = props[i]; } catch { } if (p == null) continue; try { if (p.saveablePropName.ToString() == hub.keySave) { keyProp = p; break; } } catch { } }
            if (keyProp == null) { host.Event("hubkey", "key not found: " + hub.keySave); return; }
            PropHome plinth = null; var homes = PropHome.allPropHomes;
            for (int i = 0; homes != null && i < homes.Count; i++) { PropHome h = null; try { h = homes[i]; } catch { } if (h == null) continue; try { if (Api.TransformPath(h.transform).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) { plinth = h; break; } } catch { } }
            if (plinth == null) { host.Event("hubkey", "no PropHome matching " + text); return; }
            var notes = new List<string>();
            try
            {
                var kb = KeyBlankOf(keyProp);
                if (kb != null) { var cuts = kb.cuts; int n = cuts != null ? cuts.Count : 0; for (int i = 0; i < n; i++) cuts[i] = true; kb.RefreshPropGroup(); notes.Add($"{n} cuts set, group {(keyProp.MatchesGroup(kb.finishedPropGroup) ? "complete" : "NOT complete")}"); }
                else notes.Add("no KeyBlank");
                DropIfHeld(keyProp);
                keyProp.ServerSetUnpinned(); keyProp.ServerSetPinned(plinth);
                notes.Add("pinned in " + plinth.name + (keyProp.currentHome == plinth ? "" : " (REJECTED — currentHome is " + (keyProp.currentHome != null ? keyProp.currentHome.name : "none") + ")"));
            }
            catch (Exception e) { notes.Add(e.Message); }
            host.Event("hubkey", $"{hub.keySave}: {string.Join("; ", notes)}");
        });
        // cmd keygroups key=<keySave> [text=<PropGroup to add>]: list a key's groups (+ what its
        // plinths want); with text, add that group back (a reset once stripped the black key's)
        host.Command("keygroups", (id, key, val, text) =>
        {
            Prop keyProp = null; var props = Prop.allProps;
            for (int i = 0; props != null && i < props.Count; i++) { Prop p = null; try { p = props[i]; } catch { } if (p == null) continue; try { if (p.saveablePropName.ToString() == key) { keyProp = p; break; } } catch { } }
            if (keyProp == null) { host.Event("keygroups", "no key " + key); return; }
            var groups = new List<string>(); try { foreach (var g in keyProp.propGroups) groups.Add(g.ToString()); } catch { }
            if (!string.IsNullOrEmpty(text) && Enum.TryParse<PropGroup>(text, true, out var add))
            {
                try { if (!keyProp.MatchesGroup(add)) { keyProp.propGroups.Add(add); groups.Add(add + " (added)"); } } catch (Exception e) { groups.Add("add failed: " + e.Message); }
            }
            var plinths = new List<string>(); var homes = PropHome.allPropHomes;
            for (int i = 0; homes != null && i < homes.Count; i++) { PropHome h = null; try { h = homes[i]; } catch { } if (h == null) continue; try { var hp = Api.TransformPath(h.transform); if (hp.IndexOf("BigKeyPlinth", StringComparison.OrdinalIgnoreCase) >= 0) plinths.Add(hp.Split('/')[^2] + " wants " + h.pinGroup); } catch { } }
            host.Event("keygroups", $"{key}: [{string.Join(", ", groups)}] · plinths: {string.Join("; ", plinths)}");
        });
        host.Command("respawnkey", (id, key, val, text) =>
        {
            var props = Prop.allProps; var notes = new List<string>(); int n = 0;
            for (int i = 0; i < props.Count; i++) { Prop p = null; try { p = props[i]; } catch { } if (p == null) continue; try { if (p.saveablePropName == SaveablePropName.bigKeyIntro) { n = RespawnForRemotes(p.netIdentity, notes); break; } } catch { } }
            host.Event("respawnkey", $"respawned for {n} client(s){(notes.Count > 0 ? " — " + string.Join("; ", notes) : "")}");
        });
        host.Tick(() =>
        {
            if (++_tick % 8 == 4 && Api.DashboardLive) PublishStatus();
            if (_recheck.Count > 0 && Time.unscaledTime >= _recheckAt)
            {
                foreach (var pr in _recheck) { try { PuzzleReset.ResetStatic(pr, 0); } catch { } }
                _recheck.Clear();
            }
        });
        host.Endpoint("stationdebug", () =>
        {
            var sb = new StringBuilder("[");
            bool first = true;
            foreach (var st in UnityEngine.Object.FindObjectsOfType<UnlockTrailStation>(true))
            {
                if (st == null) continue;
                if (!first) sb.Append(','); first = false;
                sb.Append("{\"path\":").Append(Api.Json(Api.TransformPath(st.transform))).Append(",\"index\":").Append(st.stationIndex);
                try { var a = st.arrowTransform; sb.Append(",\"arrow\":").Append(a != null ? Api.Json(a.name + " localRot=" + a.localRotation.eulerAngles.ToString("0.#") + " localPos=" + a.localPosition.ToString("0.##") + " active=" + a.gameObject.activeSelf) : "null"); } catch { }
                try { var t = st.peckTween; sb.Append(",\"tween\":").Append(t != null ? Api.Json(Api.TransformPath(t.transform) + " dur=" + t.duration) : "null"); } catch { }
                var comps = new List<string>();
                try { foreach (var c in st.GetComponentsInChildren<Component>(true)) { try { var n = c.GetIl2CppType().Name; if (n.StartsWith("PeckEffect") || n.Contains("Tween") || n.Contains("Toggle") || n == "TrackedPeckState") comps.Add(n + "@" + c.gameObject.name); } catch { } } } catch { }
                sb.Append(",\"effects\":").Append(Api.Json(string.Join(" | ", comps))).Append('}');
            }
            return sb.Append(']').ToString();
        });
        host.Endpoint("keytree", () =>
        {
            var props = Prop.allProps; Prop key = null;
            for (int i = 0; i < props.Count; i++) { Prop p = null; try { p = props[i]; } catch { } if (p == null) continue; try { if (p.saveablePropName == SaveablePropName.bigKeyIntro) { key = p; break; } } catch { } }
            if (key == null) return "{\"err\":\"no key\"}";
            var sb = new StringBuilder("[");
            bool first = true;
            foreach (var t in key.GetComponentsInChildren<Transform>(true))
            {
                if (!first) sb.Append(','); first = false;
                var comps = new List<string>(); foreach (var c in t.GetComponents<Component>()) { try { var n = c.GetIl2CppType().Name; if (n != "Transform") comps.Add(n); } catch { } }
                var r = t.GetComponent<Renderer>();
                sb.Append("{\"path\":").Append(Api.Json(Api.TransformPath(t).Replace(Api.TransformPath(key.transform), "KEY"))).Append(",\"active\":").Append(t.gameObject.activeSelf ? "true" : "false")
                  .Append(",\"renderer\":").Append(r != null ? (r.enabled ? "\"on\"" : "\"off\"") : "null").Append(",\"comps\":").Append(Api.Json(string.Join(",", comps))).Append('}');
            }
            var kb = KeyBlankOf(key);
            if (kb != null && kb.covers != null)
                foreach (var cv in kb.covers)
                {
                    if (cv == null) continue;
                    sb.Append(",{\"cover\":").Append(Api.Json(cv.name)).Append(",\"stage\":").Append(cv.currentStage).Append(",\"stages\":").Append(Api.Json(StageNames(cv))).Append('}');
                }
            return sb.Append(']').ToString();
        });
        host.Endpoint("keydebug", () =>
        {
            var props = Prop.allProps; var sb = new StringBuilder("{");
            for (int i = 0; i < props.Count; i++)
            {
                Prop p = null; try { p = props[i]; } catch { }
                if (p == null) continue;
                try { if (p.saveablePropName != SaveablePropName.bigKeyIntro) continue; } catch { continue; }
                sb.Append("\"prop\":").Append(Api.Json(p.name));
                var kb = KeyBlankOf(p);
                sb.Append(",\"kb\":").Append(kb != null ? "true" : "false");
                if (kb != null)
                {
                    object cuts = null; string err = null; int count = -1; string items = "";
                    try { cuts = kb.cuts; } catch (Exception e) { err = "cuts: " + e.Message; }
                    try { if (kb.cuts != null) { count = kb.cuts.Count; for (int c = 0; c < count; c++) items += (kb.cuts[c] ? "1" : "0"); } } catch (Exception e) { err = (err ?? "") + " count: " + e.Message; }
                    var comps = p.GetComponents<Component>(); var names = new List<string>(); foreach (var c in comps) { try { names.Add(c.GetIl2CppType().Name); } catch { } }
                    sb.Append(",\"cutsNull\":").Append(cuts == null ? "true" : "false").Append(",\"count\":").Append(count).Append(",\"items\":").Append(Api.Json(items)).Append(",\"err\":").Append(Api.Json(err))
                      .Append(",\"covers\":").Append(kb.covers != null ? kb.covers.Length : -1).Append(",\"finishedGroup\":").Append(Api.Json(kb.finishedPropGroup.ToString()))
                      .Append(",\"components\":").Append(Api.Json(string.Join(",", names)));
                }
                break;
            }
            return sb.Append('}').ToString();
        });
    }
    private int _tick; private readonly List<string> _recheck = new(); private float _recheckAt;

    // KeyBlank lives somewhere in the key's hierarchy, not on the Prop's own object; it
    // points back at the Prop, so match on that
    private static KeyBlank KeyBlankOf(Prop p)
    {
        try { var kb = p.GetComponentInChildren<KeyBlank>(true); if (kb != null) return kb; } catch { }
        try { var kb = p.GetComponentInParent<KeyBlank>(); if (kb != null) return kb; } catch { }
        try
        {
            foreach (var kb in UnityEngine.Object.FindObjectsOfType<KeyBlank>(true))
                if (kb != null && kb.prop == p) return kb;
        }
        catch { }
        return null;
    }

    // "rejoin for one object": hide then show the identity for every remote client, so
    // they destroy and re-spawn their copy from current server state (OnStartClient runs
    // again → visuals rebuilt from synced data). Host's own copy is untouched.
    public static int RespawnForRemotes(NetworkIdentity id, List<string> notes)
    {
        if (id == null) return 0;
        int n = 0;
        try
        {
            foreach (var kv in NetworkServer.connections)
            {
                var conn = kv.Value;
                if (conn == null || conn == NetworkServer.localConnection) continue;
                try { NetworkServer.HideForConnection(id, conn); NetworkServer.ShowForConnection(id, conn); n++; }
                catch (Exception e) { notes?.Add("respawn conn " + kv.Key + ": " + e.Message); }
            }
        }
        catch (Exception e) { notes?.Add("respawn: " + e.Message); }
        return n;
    }

    private static string StageNames(KeyBlankCover cv)
    {
        var st = cv.stages; if (st == null) return "";
        var parts = new List<string>();
        for (int i = 0; i < st.Length; i++) { var x = st[i]; parts.Add(x == null ? "null" : x.name + (x.gameObject.activeSelf ? "*" : "")); }
        return string.Join("|", parts);
    }

    private static bool Under(string path, string root) => path.IndexOf(root, StringComparison.OrdinalIgnoreCase) >= 0;

    // Every TrackedPeckState whose path contains one of the roots → its clean-save baseline
    // value (statebaseline.tsv). Two passes: a map flag's UnhideSystem must drop before the
    // flag itself can go back to hidden (writing the flag first gets overridden while the
    // unhide is still 1). Returns the number of states written. Shared with Finale.
    public static int ResetStatesUnder(IEnumerable<string> roots, List<string> notes)
    {
        int states = 0;
        Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<TrackedPeckState> all = null;
        try { all = UnityEngine.Object.FindObjectsOfType<TrackedPeckState>(true); } catch (Exception e) { notes?.Add("states: " + e.Message); return 0; }
        if (all == null) return 0;
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < all.Length; i++)
            {
                TrackedPeckState st = null; try { st = all[i]; } catch { }
                if (st == null) continue;
                string path; try { path = Api.TransformPath(st.transform); } catch { continue; }
                if (path.Contains("PlayerCharacter")) continue;
                if (path.EndsWith("/UnhideSystem") != (pass == 0)) continue;
                bool hit = false; foreach (var r in roots) if (Under(path, r)) { hit = true; break; }
                if (!hit) continue;
                string lbl = ""; try { lbl = st.label ?? ""; } catch { }
                if (!PuzzleProbeBaseline.TryGet(PuzzleProbeBaseline.Key(path, lbl), out var target)) continue;
                int cur = int.MinValue; try { var c = st.currentPeckContext; if (c != null) cur = c.state; } catch { }
                if (cur == target) continue;
                try { st.SetState(target); states++; } catch (Exception e) { notes?.Add(path.Substring(path.LastIndexOf('/') + 1) + ": " + e.Message); }
            }
        return states;
    }

    private void Reset(string hubRoot, bool keepGourds)
    {
        int hi = Array.FindIndex(Hubs, h => string.Equals(h.root, hubRoot, StringComparison.OrdinalIgnoreCase));
        if (hi < 0) { _h.Event("resethub", $"unknown hub {hubRoot}"); return; }
        var hub = Hubs[hi];
        var notes = new List<string>();
        int gourdsBack = 0, puzzlesReset = 0, states = 0;

        // 1. gourds: everything pinned in the hub's slots, plus (unless keeping) any puzzle gourd
        //    lying loose or being carried within LooseRadius of the slots. Each goes back to its
        //    startHome and its puzzle (looked up in the puzzle table) is reset in step 4.
        var homes = PropHome.allPropHomes;
        var puzzleRoots = new HashSet<string>();
        var handled = new HashSet<IntPtr>();
        var slotPos = new List<Vector3>();
        if (homes != null)
            for (int i = 0; i < homes.Count; i++)
            {
                PropHome h = null; try { h = homes[i]; } catch { }
                if (h == null) continue;
                try
                {
                    if (!Under(Api.TransformPath(h.transform), hub.root)) continue;
                    if (h.pinGroup == PropGroup.RewardGourd) slotPos.Add(h.transform.position);
                    var g = h.pinnedProp;
                    if (g == null || !g.MatchesGroup(PropGroup.RewardGourd)) continue;
                    handled.Add(g.Pointer);
                    try { g.ServerSetUnpinned(); } catch { }
                    if (!keepGourds && g.startHome != null) { g.ServerSetPinned(g.startHome); AddPuzzle(g, puzzleRoots, notes); }
                    else
                    {
                        if (!keepGourds) notes.Add(g.name + ": no startHome, dropped at hub");
                        var dest = h.transform.position + Vector3.up * 0.5f + UnityEngine.Random.insideUnitSphere * 0.5f;
                        g.transform.position = dest;
                        var rb = g.GetComponent<Rigidbody>(); if (rb != null) { rb.position = dest; rb.velocity = Vector3.zero; }
                        // 3D map flag: nothing in the game maps a state back from Stashed, so tell the map ourselves
                        try { var rg = PuzzleReset.RewardGourdOf(g); if (rg != null) rg.ServerSetGourdState(GourdFlag.GourdState.Loose); } catch { }
                    }
                    gourdsBack++;
                }
                catch (Exception e) { notes.Add("gourd: " + e.Message); }
            }
        if (!keepGourds && slotPos.Count > 0)
        {
            var centre = Vector3.zero; foreach (var p in slotPos) centre += p; centre /= slotPos.Count;
            var props0 = Prop.allProps;
            if (props0 != null)
                for (int i = 0; i < props0.Count; i++)
                {
                    Prop g = null; try { g = props0[i]; } catch { }
                    if (g == null || handled.Contains(g.Pointer)) continue;
                    try
                    {
                        if (!g.MatchesGroup(PropGroup.RewardGourd) || g.startHome == null || g.currentHome == g.startHome) continue;
                        bool held = HeldBy(g) != null;
                        if (!held && (g.transform.position - centre).magnitude > LooseRadius) continue;
                        DropIfHeld(g);
                        try { g.ServerSetUnpinned(); } catch { }
                        g.ServerSetPinned(g.startHome);
                        AddPuzzle(g, puzzleRoots, notes);
                        notes.Add(g.name + (held ? " taken from a player" : " picked up loose") + " → home");
                        gourdsBack++;
                    }
                    catch (Exception e) { notes.Add("loose gourd: " + e.Message); }
                }
        }

        // 2. the key: blank it, back in the keystone
        Prop keyProp = null;
        var props = Prop.allProps;
        if (props != null)
            for (int i = 0; i < props.Count; i++)
            {
                Prop p = null; try { p = props[i]; } catch { }
                if (p == null) continue;
                try { if (p.saveablePropName.ToString() == hub.keySave) { keyProp = p; break; } } catch { }
            }
        // a cut key works in ANY tower's plinth, so whatever landmark the key is currently
        // sitting in gets its states reset too (drawbridge, ski lift, ...), not just this hub's list
        var roots = new List<string>(hub.roots);
        if (IsTowerHub(hub.root)) roots.AddRange(TowerShared);
        if (keyProp == null) notes.Add("key not found (" + hub.keySave + ")");
        else
        {
            try
            {
                var ch = keyProp.currentHome;
                if (ch != null && ch != keyProp.startHome)
                {
                    var lm = PlinthScopeOf(Api.TransformPath(ch.transform));
                    if (lm != null) foreach (var one in lm.Split('|')) if (!roots.Contains(one)) { roots.Add(one); notes.Add("key was in " + ch.name + " → also resetting " + one); }
                }
            }
            catch { }
            try
            {
                DropIfHeld(keyProp);
                var kb = KeyBlankOf(keyProp);
                if (kb != null)
                {
                    var cuts = kb.cuts; int n = cuts != null ? cuts.Count : 0, cleared = 0;
                    for (int i = 0; i < n; i++) if (cuts[i]) { cuts[i] = false; cleared++; }
                    try { kb.RefreshPropGroup(); } catch (Exception e) { notes.Add("refresh group: " + e.Message); }
                    // covers are host-local visuals (clients rebuild theirs from `cuts` on join):
                    // OnBite does currentStage++ and stages[i].SetActive(i == currentStage), so rewind to 0
                    try
                    {
                        var covers = kb.covers; int rewound = 0;
                        if (covers != null)
                            foreach (var cv in covers)
                            {
                                if (cv == null) continue;
                                cv.currentStage = 0;
                                cv.gameObject.SetActive(true); // a cut segment's cover is disabled outright; that's the "cut" look
                                var stages = cv.stages;
                                if (stages != null) for (int i = 0; i < stages.Length; i++) if (stages[i] != null) stages[i].gameObject.SetActive(i == 0);
                                rewound++;
                            }
                        notes.Add($"{rewound} cover(s) rewound");
                    }
                    catch (Exception e) { notes.Add("covers: " + e.Message); }
                    // RefreshPropGroup only ever adds the finished group; take it off ourselves
                    try
                    {
                        var pg = keyProp.propGroups;
                        if (n > 0 && pg != null && keyProp.MatchesGroup(kb.finishedPropGroup)) { pg.Remove(kb.finishedPropGroup); notes.Add("finished group removed"); }
                        else if (n == 0) notes.Add("uncut key, finished group kept");
                    }
                    catch (Exception e) { notes.Add("group strip: " + e.Message); }
                    notes.Add($"key: {cleared}/{n} cuts cleared");
                }
                else notes.Add("key has no KeyBlank");
                try { keyProp.ServerSetUnpinned(); } catch { }
                if (keyProp.startHome != null) keyProp.ServerSetPinned(keyProp.startHome); else notes.Add("key has no startHome");
                // (tried NetworkServer.Hide/ShowForConnection to force clients to rebuild the key:
                // scene objects are only toggled inactive/active client-side, visuals survive. No-op.)
            }
            catch (Exception e) { notes.Add("key: " + e.Message); }
        }

        // 3. every state under the hub's roots → baseline
        states = ResetStatesUnder(roots, notes);

        // 3a. the ending gates need a deliberate open→close to animate shut (Finale owns the timing)
        if (roots.Exists(r => r.StartsWith("EndingGate/GatePositioner", StringComparison.OrdinalIgnoreCase))) { try { Finale.BounceEndingGates(); } catch (Exception e) { notes.Add("gates: " + e.Message); } }

        // 3b. cutter arrow markers: the ArrowSystem's PeckEffectTween only runs forward on a
        // state match, so a backwards state change leaves the arrow pointing at the next
        // station. Drive the tween back to t=0 ourselves.
        int arrows = 0;
        try
        {
            foreach (var st in UnityEngine.Object.FindObjectsOfType<UnlockTrailStation>(true))
            {
                if (st == null) continue;
                string path; try { path = Api.TransformPath(st.transform); } catch { continue; }
                bool hit = false; foreach (var r in roots) if (Under(path, r)) { hit = true; break; }
                if (!hit) continue;
                try { var tw = st.peckTween; if (tw != null) { tw.UpdateTween(0f); arrows++; } } catch (Exception e) { notes.Add("arrow: " + e.Message); }
            }
        }
        catch (Exception e) { notes.Add("arrows: " + e.Message); }

        // 3c. black monument: its map flag goes back to 0 (incomplete) rather than the clean-save
        //     hidden=2 — the tower door is still open, so the flag stays revealed
        if (!IsTowerHub(hub.root))
            try
            {
                foreach (var st in UnityEngine.Object.FindObjectsOfType<TrackedPeckState>(true))
                {
                    if (st == null) continue;
                    string path; try { path = Api.TransformPath(st.transform); } catch { continue; }
                    if (!path.EndsWith("MonumentMapFlag Black/MonumentMapFlagLogic", StringComparison.OrdinalIgnoreCase)) continue;
                    int cur = int.MinValue; try { var c = st.currentPeckContext; if (c != null) cur = c.state; } catch { }
                    if (cur != 0) { st.SetState(0); notes.Add("black map flag → incomplete"); }
                    break;
                }
            }
            catch (Exception e) { notes.Add("black flag: " + e.Message); }

        // 4. the puzzles the gourds came from: vice closed, buttons primed — and again 2s later,
        //    because after a burst of hub resets a re-posed prop (the cannonball) has been seen
        //    left unpinned; the second pass is a no-op when the first one stuck
        foreach (var pr in puzzleRoots) { try { PuzzleReset.ResetStatic(pr, 0); puzzlesReset++; } catch (Exception e) { notes.Add(pr + ": " + e.Message); } }
        if (puzzleRoots.Count > 0) { _recheck.AddRange(puzzleRoots); _recheckAt = Time.unscaledTime + 2f; }

        _h.Event("resethub", $"{hub.root}: {gourdsBack} gourd(s) {(keepGourds ? "dropped at hub" : "returned")}, {puzzlesReset} puzzle(s) reset, {states} state(s) reset, {arrows} arrow(s) rewound{(notes.Count > 0 ? " — " + string.Join("; ", notes) : "")}");
    }

    // The enclosure a key plinth lives in — what the key activated. Path up to the
    // "BigKeyPlinth …" object, minus any "…Plinth" grouping folder:
    //   …/Walkway-IntroExit/BigKeyPlinth Walkway/…            → Walkway-IntroExit
    //   …/MapRoom/Positioner/MapRoom_entrance/Door and Plinth/BigKeyPlinth MapRoom/… → MapRoom/Positioner/MapRoom_entrance
    // (the landmark alone would be too broad: MapRoom holds every tower's map flag)
    // plinths whose effect lives outside their own folder: plinth object name → scope to reset
    private static readonly (string plinth, string scope)[] PlinthScopes =
    {
        ("BigKeyPlinth Chairlift", "ChairLift"),   // ChairLift/ChairliftEngineSystem + terminus lights, not just Poles/ChairliftBend_green
        ("BigKeyPlinth Tunnels", "TunnelSystem"),  // plinth is at the yellow outlet; the gates are at every outlet
        ("BigKeyPlinth Ending", "EndingGate/GatePositioner"), // key out → entry gate closed, bell back up, exit gate closed (whole finale gate)
    };

    private static string PlinthScopeOf(string homePath)
    {
        foreach (var (plinth, scope) in PlinthScopes) if (homePath.IndexOf(plinth, StringComparison.OrdinalIgnoreCase) >= 0) return scope;
        var parts = new List<string>(homePath.Split('/'));
        int i = parts.FindIndex(x => x.StartsWith("BigKeyPlinth"));
        if (i <= 0) return LandmarkOf(homePath);
        parts = parts.GetRange(0, i);
        while (parts.Count > 1 && parts[parts.Count - 1].IndexOf("Plinth", StringComparison.OrdinalIgnoreCase) >= 0) parts.RemoveAt(parts.Count - 1);
        // drop the Landmarks* prefix so it matches as a substring like the other roots
        if (parts.Count > 1 && parts[0].StartsWith("Landmarks")) parts.RemoveAt(0);
        if (parts.Count > 1 && parts[0] == "Contents") parts.RemoveAt(0);
        return string.Join("/", parts);
    }

    // "LandmarksNonChallenge/Walkway-IntroExit/BigKeyPlinth Walkway/…" → "Walkway-IntroExit"
    // (the landmark is the first segment after Landmarks*, skipping a "Contents" wrapper)
    private static string LandmarkOf(string path)
    {
        var parts = path.Split('/');
        if (parts.Length < 2 || !parts[0].StartsWith("Landmarks")) return null;
        int i = parts[1] == "Contents" ? 2 : 1;
        return i < parts.Length ? parts[i] : null;
    }

    // the puzzle a gourd belongs to, via the puzzle table (save key, else longest root under its startHome)
    private static void AddPuzzle(Prop g, HashSet<string> roots, List<string> notes)
    {
        var pr = PuzzleReset.RootForGourd(g);
        if (pr != null) roots.Add(pr);
        else { string s = "?"; try { s = g.saveablePropName.ToString(); } catch { } notes.Add($"{g.name} ({s}) not in the puzzle table — gourd homed, puzzle NOT reset"); }
    }

    private static PlayerCharacter HeldBy(Prop p)
    {
        foreach (var pc in Api.Players())
        {
            try { var h = pc.playerNetworking.playerHeldInformation; if (h != null && h.hasProp && h.GetProp() == p) return pc; } catch { }
        }
        return null;
    }

    private static bool DropIfHeld(Prop p)
    {
        var pc = HeldBy(p);
        if (pc == null) return true;
        try { pc.playerNetworking.ServerDropPropAutomatic(false); return true; } catch { return false; }
    }

    // indexed once per minute like PuzzleReset: slot homes and the key prop per hub
    private class HubIdx { public List<PropHome> Slots = new(); public Prop Key; public KeyBlank Blank; }
    private readonly Dictionary<string, HubIdx> _idx = new();
    private float _idxBuiltAt = -999f;

    private void BuildIndex()
    {
        _idx.Clear();
        foreach (var hub in Hubs)
        {
            var ix = new HubIdx();
            var homes = PropHome.allPropHomes;
            if (homes != null)
                for (int i = 0; i < homes.Count; i++)
                {
                    PropHome h = null; try { h = homes[i]; } catch { }
                    if (h == null) continue;
                    try { if (h.pinGroup == PropGroup.RewardGourd && Under(Api.TransformPath(h.transform), hub.root)) ix.Slots.Add(h); } catch { }
                }
            var props = Prop.allProps;
            if (props != null)
                for (int i = 0; i < props.Count; i++)
                {
                    Prop p = null; try { p = props[i]; } catch { }
                    if (p == null) continue;
                    try { if (p.saveablePropName.ToString() == hub.keySave) { ix.Key = p; ix.Blank = KeyBlankOf(p); break; } } catch { }
                }
            _idx[hub.root] = ix;
        }
        _idxBuiltAt = Time.unscaledTime;
    }

    private void PublishStatus()
    {
        if (Time.unscaledTime - _idxBuiltAt > 60f) BuildIndex();
        var sb = new StringBuilder("[");
        for (int k = 0; k < Hubs.Length; k++)
        {
            var hub = Hubs[k];
            if (!_idx.TryGetValue(hub.root, out var ix)) continue;
            int slots = ix.Slots.Count, filled = 0; string keyWhere = "?"; int cuts = 0, cutsN = 0; bool complete = false;
            foreach (var h in ix.Slots) { try { if (h.pinnedProp != null) filled++; } catch { } }
            if (ix.Key != null)
            {
                try { var h = ix.Key.currentHome; keyWhere = h != null ? h.name : (HeldBy(ix.Key) is PlayerCharacter kpc ? "carried by " + Api.Display(kpc.playerNetworking) : "loose"); complete = ix.Key.MatchesGroup(PropGroup.BigKeyComplete); } catch { }
                try { if (ix.Blank != null && ix.Blank.cuts != null) { cutsN = ix.Blank.cuts.Count; for (int c = 0; c < cutsN; c++) if (ix.Blank.cuts[c]) cuts++; } } catch { }
            }
            int saveVal = int.MinValue; try { saveVal = SaveManager.GetIntValue(hub.keySave, int.MinValue); } catch { }
            if (k > 0) sb.Append(',');
            sb.Append("{\"root\":").Append(Api.Json(hub.root)).Append(",\"label\":").Append(Api.Json(hub.label)).Append(",\"note\":").Append(Api.Json(hub.note))
              .Append(",\"slots\":").Append(slots).Append(",\"filled\":").Append(filled)
              .Append(",\"keyWhere\":").Append(Api.Json(keyWhere)).Append(",\"cuts\":").Append(cuts).Append(",\"cutsN\":").Append(cutsN).Append(",\"complete\":").Append(complete ? "true" : "false")
              .Append(",\"save\":").Append(Api.Json(hub.keySave)).Append(",\"saveVal\":").Append(saveVal == int.MinValue ? "null" : saveVal.ToString())
              .Append('}');
        }
        _h.Panel("hubs", sb.Append(']').ToString());
    }
}
