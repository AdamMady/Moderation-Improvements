using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BigOrb;

// Generic puzzle reset. A puzzle is a hierarchy subtree (e.g. "TallButtonChallenge");
// under it live its TrackedPeckStates (button, vice victorySystem, launcher, skip aid)
// and PropHomes (the vice the gourd starts in). Reset =
//   1. every state under the root back to its baseline value (baseline = values
//      captured by PuzzleProbe's statesnap; falls back to the state's own initialState)
//   2. every prop whose startHome is under the root is dropped/unpinned and re-pinned
//      into that home (the gourd goes back in the vice)
//   3. optional: the gourd's save key reverted (cmd puzzlesave)
//   cmd resetpuzzle key=<root substring> [val=1 states only | 2 props only]
//   cmd savekey key=<name> val=<n>        write a SaveManager int
//   GET /api/s/saves                      every SaveablePropName key's current int value
public class PuzzleReset : IOrbScript
{
    public string Name => "puzzlereset";
    private ScriptHost _h;
    private static PuzzleReset _instance;
    /// <summary>Reset a puzzle by root from another script (e.g. hub reset returning gourds).</summary>
    public static void ResetStatic(string root, int part) => _instance?.Reset(root, part);

    public void Load(ScriptHost host)
    {
        _h = host; _instance = this;
        host.Command("resetpuzzle", (id, key, val, text) => Reset(key, val)); // val: 0 both, 1 states only, 2 props only
        host.Command("savekey", (id, key, val, text) =>
        {
            SaveManager.SetIntValue(key, val);
            SaveManager.WriteCurrentSaveData();
            host.Event("savekey", $"{key} = {val} (written)");
        });
        host.Endpoint("saves", Saves);
        // solo cheat for simultaneous-press puzzles: peck every SimPressSwitchPressSystem
        // under the root in the same frame, as the host. cmd simpress key=<root>
        host.Command("simpress", (id, key, val, text) =>
        {
            var root = string.IsNullOrEmpty(key) ? "SmallSimPressChallenge" : key;
            var me = Api.Local();
            if (me == null) { host.Event("simpress", "no local player"); return; }
            int n = 0;
            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<TrackedPeckState> all;
            try { all = UnityEngine.Object.FindObjectsOfType<TrackedPeckState>(true); } catch (Exception e) { host.Event("simpress", "scan failed: " + e.Message); return; }
            for (int i = 0; i < all.Length; i++)
            {
                TrackedPeckState st = null; try { st = all[i]; } catch { }
                if (st == null) continue;
                string path; try { path = Api.TransformPath(st.transform); } catch { continue; }
                if (path.IndexOf(root, StringComparison.OrdinalIgnoreCase) < 0 || !path.EndsWith("SimPressSwitchPressSystem")) continue;
                try { st.SetState(new PeckContext(me, null)); n++; } catch (Exception e) { host.Warn($"simpress {path}: {e.Message}"); }
            }
            host.Event("simpress", $"{root}: pecked {n} switch(es) in one frame");
        });
        // cmd bringgourds key=<root,root,...> (blank = every puzzle in the table): unpin those
        // puzzles' gourds and drop them in front of the host, spread out
        host.Command("bringgourds", (id, key, val, text) =>
        {
            var me = Api.Local();
            if (me == null) { host.Event("bringgourds", "no local player"); return; }
            var roots = string.IsNullOrEmpty(key) ? Array.ConvertAll(Puzzles, x => x.root) : key.Split(',');
            var props = Prop.allProps; int n = 0;
            if (props != null)
                for (int i = 0; i < props.Count; i++)
                {
                    Prop p = null; try { p = props[i]; } catch { }
                    if (p == null) continue;
                    try
                    {
                        if (!p.MatchesGroup(PropGroup.RewardGourd) || p.startHome == null) continue;
                        var hp = Api.TransformPath(p.startHome.transform);
                        bool hit = false; foreach (var r in roots) if (hp.IndexOf(r.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) { hit = true; break; }
                        if (!hit) continue;
                        if (!DropIfHeld(p)) continue;
                        try { p.ServerSetUnpinned(); } catch { }
                        var dest = me.transform.position + me.transform.forward * 1.5f + me.transform.right * (n - 1.5f) * 0.6f + Vector3.up * 0.6f;
                        p.transform.position = dest;
                        var rb = p.GetComponent<Rigidbody>();
                        if (rb != null) { rb.position = dest; rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                        n++;
                    }
                    catch (Exception e) { host.Warn($"bringgourds {p.name}: {e.Message}"); }
                }
            host.Event("bringgourds", $"{n} gourd(s) brought to host");
        });
        // cmd progress key=<root> val=<n>: set requiredIncrements on every ProgressTracker under
        // root (PointersParadise: sequence length). val=0 reads without changing.
        host.Command("progress", (id, key, val, text) =>
        {
            int n = 0; var report = new List<string>();
            foreach (var pt in UnityEngine.Object.FindObjectsOfType<ProgressTracker>(true))
            {
                if (pt == null) continue;
                string path; try { path = Api.TransformPath(pt.transform); } catch { continue; }
                if (!string.IsNullOrEmpty(key) && path.IndexOf(key, StringComparison.OrdinalIgnoreCase) < 0) continue;
                int old = pt.requiredIncrements;
                if (val > 0) pt.requiredIncrements = val;
                report.Add($"{path.Split('/')[2]}: {old}{(val > 0 ? " → " + val : "")} (now {pt.GetCurrentValue()})");
                n++;
            }
            host.Event("progress", n == 0 ? $"no ProgressTracker under {key}" : string.Join("; ", report));
        });
        // cmd bringall key=<PropGroup or name substring> val=<radius from host>: every matching prop
        // within radius comes to the host in a grid (EggHunt: 36 pegs)
        host.Command("bringall", (id, key, val, text) =>
        {
            var me = Api.Local(); if (me == null || string.IsNullOrEmpty(key)) { host.Event("bringall", "need key + local player"); return; }
            PropGroup grp; bool byGroup = Enum.TryParse(key, true, out grp);
            float r = val > 0 ? val : 80f; var here = me.transform.position; int n = 0;
            var props = Prop.allProps;
            for (int i = 0; i < props.Count; i++)
            {
                Prop p = null; try { p = props[i]; } catch { }
                if (p == null) continue;
                try
                {
                    if (!(byGroup ? p.MatchesGroup(grp) : p.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                    if ((p.transform.position - here).magnitude > r) continue;
                    DropIfHeld(p);
                    try { p.ServerSetUnpinned(); } catch { }
                    var dest = here + me.transform.forward * 1.5f + me.transform.right * ((n % 6) - 2.5f) * 0.45f + me.transform.forward * (n / 6) * 0.45f + Vector3.up * 0.6f;
                    p.transform.position = dest;
                    var rb = p.GetComponent<Rigidbody>(); if (rb != null) { rb.position = dest; rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                    n++;
                }
                catch (Exception e) { host.Warn($"bringall {p.name}: {e.Message}"); }
            }
            host.Event("bringall", $"{n} prop(s) matching {key} within {r:0}m brought to host");
        });
        // cmd bringnet key=<netId> [id=<player>]: bring one specific prop by network id to the host (or a player)
        host.Command("bringnet", (id, key, val, text) =>
        {
            // id=<player> delivers to them instead of the host
            var me = string.IsNullOrEmpty(id) ? Api.Local() : Api.ById(id); if (me == null || !uint.TryParse(key, out var net)) { host.Event("bringnet", "need key=<netId> (+ known player)"); return; }
            var props = Prop.allProps;
            for (int i = 0; i < props.Count; i++)
            {
                Prop p = null; try { p = props[i]; } catch { }
                if (p == null || p.netId != net) continue;
                try
                {
                    DropIfHeld(p);
                    try { p.ServerSetUnpinned(); } catch { }
                    var dest = me.transform.position + me.transform.forward * 2f + me.transform.right * (val * 0.6f) + Vector3.up * 0.8f;
                    p.transform.position = dest;
                    var rb = p.GetComponent<Rigidbody>(); if (rb != null) { rb.position = dest; rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                    host.Event("bringnet", $"{p.name} #{net} brought to {Api.Display(me.playerNetworking)}");
                }
                catch (Exception e) { host.Event("bringnet", e.Message); }
                return;
            }
            host.Event("bringnet", $"no prop #{net}");
        });
        // cmd bring key=<PropGroup, prop name or SaveablePropName e.g. bigKeyIntro>: unpin the first matching prop and drop it in front of the host
        host.Command("bring", (id, key, val, text) =>
        {
            var me = Api.Local(); if (me == null || string.IsNullOrEmpty(key)) { host.Event("bring", "need key + local player"); return; }
            PropGroup grp; bool byGroup = Enum.TryParse(key, true, out grp);
            var props = Prop.allProps;
            for (int i = 0; i < props.Count; i++)
            {
                Prop p = null; try { p = props[i]; } catch { }
                if (p == null) continue;
                try
                {
                    bool hit = byGroup ? p.MatchesGroup(grp) : p.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!hit) { try { hit = string.Equals(p.saveablePropName.ToString(), key, StringComparison.OrdinalIgnoreCase); } catch { } }   // bring key=bigKeyIntro
                    if (!hit) continue;
                    DropIfHeld(p);
                    try { p.ServerSetUnpinned(); } catch { }
                    var dest = me.transform.position + me.transform.forward * 2f + Vector3.up * 0.8f;
                    p.transform.position = dest;
                    var rb = p.GetComponent<Rigidbody>(); if (rb != null) { rb.position = dest; rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                    host.Event("bring", $"{p.name} brought to host");
                    return;
                }
                catch (Exception e) { host.Event("bring", $"{p.name}: {e.Message}"); return; }
            }
            host.Event("bring", $"no prop matching {key}");
        });
        // cmd bringrandom key=<PropGroup or prop name> [val=count, default 1] [id=player]: val random
        // matching props (skipping held ones) dropped in a row in front of the host / player
        host.Command("bringrandom", (id, key, val, text) =>
        {
            var me = string.IsNullOrEmpty(id) ? Api.Local() : Api.ById(id); if (me == null || string.IsNullOrEmpty(key)) { host.Event("bring", "need key + player"); return; }
            PropGroup grp; bool byGroup = Enum.TryParse(key, true, out grp);
            var pool = new List<Prop>(); var props = Prop.allProps;
            for (int i = 0; i < props.Count; i++)
            {
                Prop p = null; try { p = props[i]; } catch { }
                if (p == null) continue;
                try { if (byGroup ? p.MatchesGroup(grp) : p.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) pool.Add(p); } catch { }
            }
            int want = Math.Max(1, val), n = 0; var rng = new System.Random();
            while (n < want && pool.Count > 0)
            {
                var p = pool[rng.Next(pool.Count)]; pool.Remove(p);
                try
                {
                    DropIfHeld(p);
                    try { p.ServerSetUnpinned(); } catch { }
                    var dest = me.transform.position + me.transform.forward * 2f + me.transform.right * ((n - (want - 1) * 0.5f) * 0.8f) + Vector3.up * 0.8f;
                    p.transform.position = dest;
                    var rb = p.GetComponent<Rigidbody>(); if (rb != null) { rb.position = dest; rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                    host.Event("bring", $"{p.name} #{p.netId} brought to {Api.Display(me.playerNetworking)}");
                    n++;
                }
                catch (Exception e) { host.Event("bring", $"{p.name}: {e.Message}"); }
            }
            if (n == 0) host.Event("bring", $"no prop matching {key}");
        });
        // cheat for counting puzzles: PulseGenerator.total is the answer; write it into the
        // CountingMachine's valueStorage state. cmd countcheat key=<root> [val=1 reveal only]
        host.Command("countcheat", (id, key, val, text) =>
        {
            var root = string.IsNullOrEmpty(key) ? "PerspectiveCounting" : key;
            PulseGenerator gen = null;
            foreach (var g in UnityEngine.Object.FindObjectsOfType<PulseGenerator>(true))
            {
                if (g == null) continue;
                try { if (Api.TransformPath(g.transform).IndexOf(root, StringComparison.OrdinalIgnoreCase) >= 0) { gen = g; break; } } catch { }
            }
            if (gen == null) { host.Event("countcheat", $"no PulseGenerator under {root}"); return; }
            int total = gen.total;
            if (val == 1) { host.Event("countcheat", $"{root}: answer is {total} (not written)"); return; }
            TrackedPeckState store = null;
            foreach (var st in UnityEngine.Object.FindObjectsOfType<TrackedPeckState>(true))
            {
                if (st == null) continue;
                string path; try { path = Api.TransformPath(st.transform); } catch { continue; }
                if (path.IndexOf(root, StringComparison.OrdinalIgnoreCase) >= 0 && path.EndsWith("CountingMachine/valueStorage")) { store = st; break; }
            }
            if (store == null) { host.Event("countcheat", $"{root}: answer {total}, but no valueStorage state found"); return; }
            store.SetState(total);
            host.Event("countcheat", $"{root}: counter set to {total} — press validate");
        });
        // cheat for the memory/mines puzzle: SweeperBrain.bombs (private list of input indices)
        // says which capsules are bombs. val=1: also press every safe mine's button (as the
        // host), 0.4s apart. cmd minecheat key=<root>
        host.Command("minecheat", (id, key, val, text) =>
        {
            var root = string.IsNullOrEmpty(key) ? "MemoryBombs" : key;
            SweeperBrain brain = null;
            foreach (var b in UnityEngine.Object.FindObjectsOfType<SweeperBrain>(true))
            {
                if (b == null) continue;
                try { if (Api.TransformPath(b.transform).IndexOf(root, StringComparison.OrdinalIgnoreCase) >= 0) { brain = b; break; } } catch { }
            }
            if (brain == null) { host.Event("minecheat", $"no SweeperBrain under {root}"); return; }
            var bombIdx = new HashSet<int>();
            try { var bl = brain.bombs; if (bl != null) for (int i = 0; i < bl.Count; i++) bombIdx.Add(bl[i]); } catch (Exception e) { host.Event("minecheat", "bombs unreadable: " + e.Message); return; }
            var safe = new List<string>(); var bombs = new List<string>(); var safeButtons = new List<TrackedPeckState>();
            var inputs = brain.inputs;
            for (int i = 0; inputs != null && i < inputs.Length; i++)
            {
                var cb = inputs[i]; if (cb == null) continue;
                string mine = "#" + i;
                try { var ms = cb.mineSystem; if (ms != null) { var path = Api.TransformPath(ms.transform); int k = path.IndexOf("/Mines/"); if (k >= 0) mine = path.Substring(k + 7).Split('/')[0]; } } catch { }
                if (bombIdx.Contains(i)) bombs.Add(mine);
                else
                {
                    safe.Add(mine);
                }
            }
            // buttons: the PokeButtonPeckLogic under each safe mine
            var buttons = new Dictionary<string, TrackedPeckState>();
            foreach (var st in UnityEngine.Object.FindObjectsOfType<TrackedPeckState>(true))
            {
                if (st == null) continue;
                string path; try { path = Api.TransformPath(st.transform); } catch { continue; }
                if (path.IndexOf(root, StringComparison.OrdinalIgnoreCase) < 0 || !path.EndsWith("PokeButtonPeckLogic")) continue;
                int k = path.IndexOf("/Mines/"); if (k < 0) continue;
                buttons[path.Substring(k + 7).Split('/')[0]] = st;
            }
            _h.Panel("mines", "{\"safe\":[" + string.Join(",", safe.ConvertAll(Api.Json)) + "],\"bombs\":[" + string.Join(",", bombs.ConvertAll(Api.Json)) + "],\"bombCount\":" + brain.bombCount + "}");
            host.Event("minecheat", $"{root}: {safe.Count} safe, {bombs.Count} bombs (bombCount {brain.bombCount}) — bombs: {string.Join(", ", bombs)}");
            if (val == 1)
            {
                _minePressQueue.Clear();
                foreach (var m in safe) if (buttons.TryGetValue(m, out var b)) _minePressQueue.Enqueue(b);
                _minePressAt = Time.unscaledTime;
                host.Event("minecheat", $"pressing {_minePressQueue.Count} safe mine(s)…");
            }
        });
        // cmd press key=<path substring ending the state's path>: plain SetState(1) on one button state
        host.Command("press", (id, key, val, text) =>
        {
            if (string.IsNullOrEmpty(key)) { host.Event("press", "need key"); return; }
            foreach (var st in UnityEngine.Object.FindObjectsOfType<TrackedPeckState>(true))
            {
                if (st == null) continue;
                string path; try { path = Api.TransformPath(st.transform); } catch { continue; }
                if (!path.EndsWith(key, StringComparison.OrdinalIgnoreCase)) continue;
                int v = text == "0" ? 0 : (val > 0 ? val : 1);
                try { st.SetState(v); host.Event("press", $"{path.Split('/')[^1]} → {v}"); } catch (Exception e) { host.Event("press", e.Message); }
                return;
            }
            host.Event("press", $"no state path ending in {key}");
        });
        // cheat for N-hold rigs (TrapRoom start, every SkipAid): press every ButtonNHold*/ButtonSystem
        // under the root in one frame, release them all ~1.5s later. cmd nhold key=<root>
        host.Command("nhold", (id, key, val, text) => NHold(key, val));
        LoadTail(host);
    }

    // for other modules: trip a puzzle's N-hold rig
    public static void NHoldStatic(string root) => _instance?.NHold(root, 0);
    private void NHold(string key, int val)
    {
        var host = _h;
        {
            if (string.IsNullOrEmpty(key)) { host.Event("nhold", "need key=<root>"); return; }
            _nholdRelease.Clear();
            var me = Api.Local();
            foreach (var st in UnityEngine.Object.FindObjectsOfType<TrackedPeckState>(true))
            {
                if (st == null) continue;
                string path; try { path = Api.TransformPath(st.transform); } catch { continue; }
                if (path.IndexOf(key, StringComparison.OrdinalIgnoreCase) < 0 || !path.EndsWith("/ButtonSystem") || path.IndexOf("ButtonNHold", StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (val != 9 && path.IndexOf("SkipAid", StringComparison.OrdinalIgnoreCase) >= 0) continue; // the puzzle's own rig only, never the skip-aid (val=9 = player-context test, includes both)
                // holding all four TrapRoom buttons with the host's PeckContext hard-crashed
                // the game (native, no log) — the N-hold logic likely indexes per holder. Default to a
                // plain state write; val=9 uses the player context again for deliberate testing.
                try { if (val == 9 && me != null) st.SetState(new PeckContext(me, null)); else st.SetState(1); _nholdRelease.Add(st); } catch (Exception e) { host.Warn($"nhold {path}: {e.Message}"); }
            }
            _nholdReleaseAt = Time.unscaledTime + 1.5f;
            host.Event("nhold", $"{key}: holding {_nholdRelease.Count} button(s), releasing in 1.5s");
        }
    }

    private void LoadTail(ScriptHost host)
    {
        host.Tick(() =>
        {
            if (++_tick % 8 == 0) PublishStatus();
            // per-puzzle queues: a hub reset fires several puzzle resets in one frame, so a
            // single slot would drop all but the last bounce/recheck
            for (int i = _bounces.Count - 1; i >= 0; i--)
                if (Time.unscaledTime >= _bounces[i].at) { try { _bounces[i].st.SetState(0); } catch { } _bounces.RemoveAt(i); }
            for (int i = _rechecks.Count - 1; i >= 0; i--)
            {
                var (r, at, full, settleUntil) = _rechecks[i];
                if (Time.unscaledTime < at) continue;
                _rechecks.RemoveAt(i);
                _quiet = true; try { Reset(r, full ? 0 : 1); } finally { _quiet = false; }
                if (Time.unscaledTime < settleUntil) _rechecks.Add((r, Time.unscaledTime + 2f, true, settleUntil));
            }
            if (_minePressQueue.Count > 0 && Time.unscaledTime >= _minePressAt)
            {
                var b = _minePressQueue.Dequeue(); _minePressAt = Time.unscaledTime + 0.4f;
                // plain state write: SetState(new PeckContext(player, null)) on buttons has hard-crashed the game twice
                try { b.SetState(1); } catch (Exception e) { _h.Warn("mine press: " + e.Message); }
            }
            if (_nholdRelease.Count > 0 && Time.unscaledTime >= _nholdReleaseAt)
            {
                foreach (var st in _nholdRelease) { try { st.SetState(0); } catch { } }
                _nholdRelease.Clear();
            }
        });
    }

    // ---- known puzzles (root = hierarchy substring; save = SaveablePropName key) ----
    // verified TallButtonChallenge solve→reset round-trip on host
    // radius: loose props (peg tiles etc.) within this distance of the vice go back to
    // their session-start pose; 0 = gourd-only puzzles
    // groups: PropGroup names whose props (world-wide) go back to their baseline pose on reset
    // post: "bounce:<path suffix>" — after the state reset, set that state 1 then 0 a moment later
    // (CountingController puzzles only clear their validator screen on a Blocked→Idle transition)
    private static readonly (string root, string label, string save, float radius, string note, string cheat, bool gourdOnly, string groups, string post)[] Puzzles =
    {
        ("TallButtonChallenge", "Tall button (crane)", "gourdHighButton", 0f, "verified incl. client replication", null, false, null, null),
        ("TellerWindow", "Teller window (blue house, peg tiles)", "gourdTellerWindow", 12f, "9 tiles back on rack; verified incl. client replication", null, false, null, null),
        ("FixedTelescopeToGourd", "Telescope → gourd box", "gourdTelescopeToBox", 0f, "box (no vice): boxLogic + platform button; unverified", null, false, null, null),
        ("SmallSimPressChallenge", "Sim-press (N buttons at once)", "gourdEasySimPress", 0f, "cheat presses every switch in one frame", "simpress", false, null, null),
        ("BasketballChallenge", "Basketball (anything through the hoop)", "gourdBasketball", 15f, "progress tracker; thrown props within 15m go home; verified on host", null, false, null, null),
        ("Fielding", "Fielding (cannon → receptacle)", "gourdFielding", 15f, "cannon/receptacle/ball self-reset; verified on host", null, false, null, null),
        ("CoordinatesHolding", "Single coordinate box (button → box)", "gourdCoordinatesHolding", 0f, "box = home, like the telescope one; verified on host", null, false, null, null),
        ("Labyrinth", "Cheese maze (labyrinth, pomodoro → receptacle)", "gourdWindowLabyrinth", 0f, "gourd/vice only; dispenser+receptacle left to the game; verified on host", null, true, null, null),
        ("CannonBallCarry", "Heavy golf (cannonball up the hill)", "gourdCannonBall", 0f, "cannonball back to its start; verified on host", null, false, "CannonBall", null),
        ("CannonBallCommute", "Purple heavy golf goal (cannonball across the map)", "gourdCannonballCommute", 0f, "bonus (purple) puzzle; cannonball back to its start; verified on host", null, false, "CannonBall", null),
        ("TrapRoom", "Trap house (4-hold start, peg tile case)", "gourdTrapRoom", 12f, "cheat = 4-hold start (plain states; player-context variant CRASHES the game); verified on host", "nhold", false, null, null),
        ("PerspectiveCounting", "Counting lights (perspective counting)", "gourdPerspectiveCounting", 0f, "cheat writes the pulse total into the counter (val=1 just reveals it); verified on host", "countcheat", false, null, "bounce:IgnitionPedestal/BasicKnob/KnobPeckLogic"),
        ("MemoryBombs", "Memory game (mines: safe vs bomb)", "gourdMemoryBombs", 0f, "cheat reveals bombs (SweeperBrain.bombs) + presses safe mines with plain states; verified on host", "minecheat", false, null, null),
        ("SignalFlags", "Signal flags (count flashes → peg board → telescope)", "gourdSignalFlags", 14f, "9 tiles back on rack; verified on host", "countcheat", false, null, "bounce:Dresser/Door/BasicDoorSoundproof/BasicDoorPeckLogic"),
        ("BlindfoldFishTrap", "Headless chicken (blindfold fish trap, peg board)", "gourdBlindfoldFishtrap", 20f, "9 tiles + helmet back; verified on host", null, false, null, null),
        ("InvisibleInkChallenge", "Invisible ink (telescope, peg board)", "gourdInvisibleInk", 12f, "9 tiles back on rack; door bounced to re-scramble the answer; verified on host", null, false, null, "bounce:InvisibleInkChallenge/Positioner/BasicDoor/BasicDoorPeckLogic"),
        ("RingRoomChallenge", "Telephone game (ring rooms, 4 chambers)", "gourdRingRoom", 20f, "36 tiles back on racks; per-player-count chambers; nhold cheat works; verified on host", null, false, null, null),
        ("ObservationRoom", "Interrogation (observation room, peg board)", "gourdObservationRoom", 15f, "9 tiles back on rack; door bounced to re-scramble; verified on host", null, false, null, "bounce:ObservationRoom/Positioner/BasicDoorSoundproof/BasicDoorPeckLogic"),
        ("EggHunt", "Hide and seek (egg hunt, 36 hidden pegs)", "gourdEggHunt", 60f, "36 pegs back to their hiding spots (each has a startHome); verified on host", null, false, null, null),
        ("Carousel", "Carousel chair (departing tiles → arriving validator)", "gourdCarousel", 60f, "tiles ride the carousel; rack at Departing, validator at Arriving; verified on host", null, false, null, null),
        ("ConductorConcert", "Auditorium (conductor concert, press in order)", "gourdConcert", 15f, "like Pointers' Paradise; chairs back; verified on host", null, false, null, null),
        ("KickUpPits", "Box pit (parcel up the pit → opener socket)", "gourdKickUpPits", 0f, "gourd back inside the parcel, parcel back to the pit floor; verified on host", null, false, "GourdParcel", null),
        ("SingerAndSelecter", "Red music (singer & selecter, intercoms, peg board)", "gourdSingerAndSelecter", 16f, "9 speaker tiles back; door bounced to re-scramble; verified on host", null, false, null, "bounce:SelecterBuilding/Door/BasicDoorSoundproof/BasicDoorPeckLogic"),
        ("MicrophoneArray", "Six microphones (directional mics, press in order)", "gourdMicrophoneArray", 0f, "press-in-order family; verified on host", null, false, null, null),
        ("ScoutBombs", "Six kettles (scout bombs: kettles, motion sensors, 12 B-tiles)", "gourdScoutBombs", 20f, "6 kettles + 12 tiles back; door bounced to re-scramble; verified on host", null, false, null, "bounce:DoorGroup - tile room to outside/BasicDoorSoundproof - tile room to outside (2)/BasicDoorPeckLogic"),
        ("CoordinatesSimPress", "Dual camo box (two sim-press switches → box)", "gourdCoordinates", 0f, "box = home; permosign re-hooked (whiteboard + coordinate tracker left alone, shared); cheat presses A+B together; box re-closed on second pass; verified on host", "simpress", false, null, "recheck"),
        ("SemaphoreRooms", "North warehouse (semaphore rooms, D-tiles)", "gourdIndoorSemaphore", 22f, "9 D-tiles back; door bounced to re-scramble; verified on host", null, false, null, "bounce:SemaphoreBuilding/BasicDoorSoundproof_L/BasicDoorPeckLogic"),
        ("BlindfoldCatwalk", "Blind obstacle course (catwalk, blindfold helmet)", "gourdBlindfoldCatwalk", 0f, "helmet re-posed by guid (vice is 40m from the start); end button is a hair-trigger (any write = victory) so it is re-primed FIRST (re-fires victory), launch timer cancelled, gourd/vice re-settled 1.2s later; verified on host", null, false, "44bba8d0-aa6b-4132-ab9e-24b4b6eac497", "first:BasicPokeButton/PokeButtonPeckLogic;recheck:1.2"),
        ("CabinFever/Positioner", "5-minute house (cabin fever, N-hold → 5 min timer)", "gourdCabinFever", 0f, "cheat = N-hold start; timer is PeckEffectTimerNetworked (timerend shortens it live); verified on host", "nhold", false, null, null),
        ("CenturionSong", "100 buttons (centurion song: signal room → input room)", "gourdCenturonSong", 0f, "press-in-order ×100 across two rooms; verified on host", null, false, null, null),
        ("BreadcrumbLoop", "Long telescope (breadcrumb loop, 4 stations)", "gourdBreadcrumbLoop", 0f, "N sim-press switches across the map (one per station, per player count); cheat presses all at once; verified on host", "simpress", false, null, null),
        ("FlareRun", "Flare run (rocket timer → camo box)", "gourdFlareRun", 0f, "box = home, 40s networked timer at the rocket; verified on host", null, false, null, "recheck"),
        ("MediumSimPressChallenge", "Medium sim-press (4 switches)", "gourdMediumSimPress", 0f, "cheat presses every switch in one frame; verified on host", "simpress", false, null, null),
        ("MusicalHoliday", "Musical holiday (box)", "gourdMusicalHoliday", 0f, "box = home; its BroadcastStation (FM) is left alone; verified on host", null, false, null, "recheck;skip:BroadcastStation/PokeButtonPeckLogic;skip:PokeButtonPeckLogic/AnimationFinishedSystem;skip:FmRadioKernal/FmRadioLogic"),
        ("Contents/Obby/", "Obby (obstacle course: button door, sim-press door, pomodoro finish)", "gourdObby", 0f, "floors 1/3/5; pomodoro back in dispenser; verified on host", "simpress", false, null, null),
        ("OpticalTelegraph", "Optical telegraph (5 telescopes, D-tile board)", "gourdOpticalTelegraph", 12f, "9 D-tiles back on rack; verified on host", null, false, null, null),
        ("PoetAndPreist", "Poet & priest (one-way turnstiles, priest tile board)", "gourdPoetAndPreist", 14f, "one-way turnstile locks swap on the win (savable PoetAndPriestDoors); 9 priest tiles back; verified on host", null, false, null, null),
        ("CabinFeverLong/Positioner", "30-minute house (cabin fever long, purple)", "gourdCabinFeverLong", 0f, "cheat = N-hold start; 1800s networked timer (timerend shortens it); verified on host", "nhold", false, null, null),
        ("CenturionSeance", "100 buttons hard (centurion seance, purple)", "gourdCenturionSeance", 0f, "press-in-order ×100 hard variant; verified on host", null, false, null, null),
        ("CharadesRooms", "Charades rooms (purple; semaphore building, A-tiles)", "gourdCharadesRooms", 18f, "9 tiles back; door bounced to re-scramble; verified on host", null, false, null, "bounce:SemaphoreBuilding/BasicDoorSoundproof_L/BasicDoorPeckLogic"),
        ("DancerAndSelecter", "Dancer & selecter (purple; hard music, speaker tiles)", "gourdDancerAndSelecter", 16f, "9 speaker tiles back; door bounced to re-scramble; verified on host", null, false, null, "bounce:SelecterBuilding/Door/BasicDoorSoundproof/BasicDoorPeckLogic"),
        ("PoetAndPontiff", "Poet & pontiff (purple; one-way turnstiles, priest tile board)", "gourdPoetAndPontiff", 14f, "savable PoetAndPontiffDoors; 36 priest tiles back; verified on host", null, false, null, null),
        ("SpeedObby", "Speed obby (purple; hard obstacle course)", "gourdSpeedObby", 0f, "doors/switches self-reset; verified on host", "simpress", false, null, null),
        ("TileThief", "Tile thief (tiles stolen from other racks)", "gourdTileThief", 15f, "every tile within 15m goes back to ITS OWN rack (pose baseline by guid); verified on host with 4 stolen tiles", null, false, null, null),
        ("PointersParadise", "Pointers' paradise (green silent buttons, press in order)", "gourdPointersParadise", 0f, "re-arms itself when the door closes (normal). Length tweak: host-authoritative; guests see bar/sound complete at vanilla 8. verified", null, false, null, null),
    };
    // (root, label) for every registered puzzle, and each one's live solved flag (same rule as
    // the dashboard: vice open, or — box puzzles with no vice — states off baseline and the
    // gourd gone from its home). Refreshed by PublishStatus every ~2s; read by Solves.
    public static IEnumerable<(string root, string label)> Table() { foreach (var p in Puzzles) yield return (p.root, p.label); }
    public static readonly Dictionary<string, bool> SolvedNow = new(StringComparer.OrdinalIgnoreCase);
    public static string LabelOf(string root) { foreach (var p in Puzzles) if (string.Equals(p.root, root, StringComparison.OrdinalIgnoreCase)) return p.label; return root; }
    // Which table row owns a gourd: its SaveablePropName matches the row's save key; failing
    // that, the longest root that is a substring of its startHome path (so CabinFever/Positioner
    // never claims CabinFeverLong, Contents/Obby/ never claims SpeedObby). null = not registered.
    public static string RootForGourd(Prop g)
    {
        string save = null; try { save = g.saveablePropName.ToString(); } catch { }
        if (!string.IsNullOrEmpty(save)) foreach (var p in Puzzles) if (string.Equals(p.save, save, StringComparison.OrdinalIgnoreCase)) return p.root;
        string hp = null; try { hp = g.startHome != null ? Api.TransformPath(g.startHome.transform) : null; } catch { }
        string best = null;
        if (hp != null) foreach (var p in Puzzles) if (hp.IndexOf(p.root, StringComparison.OrdinalIgnoreCase) >= 0 && (best == null || p.root.Length > best.Length)) best = p.root;
        return best;
    }
    private static float RadiusFor(string root) { foreach (var p in Puzzles) if (string.Equals(p.root, root, StringComparison.OrdinalIgnoreCase)) return p.radius; return 0f; }
    private static string GroupsFor(string root) { foreach (var p in Puzzles) if (string.Equals(p.root, root, StringComparison.OrdinalIgnoreCase)) return p.groups; return null; }
    private static string PostFor(string root) { foreach (var p in Puzzles) if (string.Equals(p.root, root, StringComparison.OrdinalIgnoreCase)) return p.post; return null; }
    private static bool GourdOnly(string root) { foreach (var p in Puzzles) if (string.Equals(p.root, root, StringComparison.OrdinalIgnoreCase)) return p.gourdOnly; return false; }
    private int _tick;
    private readonly List<TrackedPeckState> _nholdRelease = new(); private float _nholdReleaseAt;
    private readonly Queue<TrackedPeckState> _minePressQueue = new(); private float _minePressAt;
    private readonly List<(TrackedPeckState st, float at)> _bounces = new();
    private readonly List<(string root, float at, bool full, float settleUntil)> _rechecks = new();
    private bool _quiet;

    // Status is published every 2s, so it must not sweep the world each time: the states
    // and props under each root are indexed once (they don't move in the hierarchy) and
    // only their current values are read per tick. Index rebuilds every 60s in case the
    // world streamed in late.
    private class Idx { public List<(TrackedPeckState s, string key, string lbl)> States = new(); public List<Prop> Props = new(); }
    private readonly Dictionary<string, Idx> _idx = new();
    private float _idxBuiltAt = -999f;

    private void BuildIndex()
    {
        _idx.Clear();
        foreach (var pz in Puzzles) _idx[pz.root] = new Idx();
        Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<TrackedPeckState> all = null;
        try { all = UnityEngine.Object.FindObjectsOfType<TrackedPeckState>(true); } catch { }
        if (all != null)
            for (int i = 0; i < all.Length; i++)
            {
                TrackedPeckState st = null; try { st = all[i]; } catch { }
                if (st == null) continue;
                string path; try { path = Api.TransformPath(st.transform); } catch { continue; }
                if (path.Contains("PlayerCharacter")) continue;
                foreach (var pz in Puzzles)
                    if (path.IndexOf(pz.root, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string lbl = ""; try { lbl = st.label ?? ""; } catch { }
                        _idx[pz.root].States.Add((st, PuzzleProbeBaseline.Key(path, lbl), lbl));
                    }
            }
        var props = Prop.allProps;
        if (props != null)
            for (int i = 0; i < props.Count; i++)
            {
                Prop p = null; try { p = props[i]; } catch { }
                if (p == null) continue;
                try
                {
                    var home = p.startHome; if (home == null) continue;
                    var hp = Api.TransformPath(home.transform);
                    foreach (var pz in Puzzles) if (hp.IndexOf(pz.root, StringComparison.OrdinalIgnoreCase) >= 0) _idx[pz.root].Props.Add(p);
                }
                catch { }
            }
        _idxBuiltAt = Time.unscaledTime;
    }

    private void PublishStatus()
    {
        if (Time.unscaledTime - _idxBuiltAt > 60f) BuildIndex();
        var sb = new StringBuilder("[");
        for (int k = 0; k < Puzzles.Length; k++)
        {
            var (root, label, save, radius, note, cheat, gourdOnly, groups, post) = Puzzles[k];
            if (!_idx.TryGetValue(root, out var ix)) continue;
            int vice = int.MinValue, changed = 0, seen = 0;
            foreach (var (st, key, lbl) in ix.States)
            {
                seen++;
                int cur = int.MinValue; try { var c = st.currentPeckContext; if (c != null) cur = c.state; } catch { continue; }
                if (lbl == "victorySystem") vice = cur;
                if (PuzzleProbeBaseline.TryGet(key, out var b) && b != cur) changed++;
            }
            int gourdsHome = 0, gourdsTotal = 0; string gourdWhere = null;
            foreach (var p in ix.Props)
            {
                try
                {
                    gourdsTotal++;
                    if (p.currentHome == p.startHome) gourdsHome++;
                    else { var h = p.currentHome; gourdWhere = h != null ? "in " + h.name : "loose at " + p.transform.position.ToString("0"); }
                }
                catch { }
            }
            int saveVal = int.MinValue; try { saveVal = SaveManager.GetIntValue(save, int.MinValue); } catch { }
            if (seen > 0) lock (SolvedNow) SolvedNow[root] = vice == 0 || (vice == int.MinValue && changed > 0 && gourdsHome < gourdsTotal);
            if (k > 0) sb.Append(',');
            sb.Append("{\"root\":").Append(Api.Json(root)).Append(",\"label\":").Append(Api.Json(label)).Append(",\"note\":").Append(Api.Json(note)).Append(",\"cheat\":").Append(Api.Json(cheat))
              .Append(",\"save\":").Append(Api.Json(save)).Append(",\"saveVal\":").Append(saveVal == int.MinValue ? "null" : saveVal.ToString())
              .Append(",\"vice\":").Append(vice == int.MinValue ? "null" : vice.ToString())
              .Append(",\"changed\":").Append(changed).Append(",\"states\":").Append(seen)
              .Append(",\"gourdsHome\":").Append(gourdsHome).Append(",\"gourds\":").Append(gourdsTotal).Append(",\"gourdWhere\":").Append(Api.Json(gourdWhere))
              .Append('}');
        }
        _h.Panel("puzzles", sb.Append(']').ToString());
    }

    private static string Saves()
    {
        var sb = new StringBuilder("{");
        bool first = true;
        foreach (SaveablePropName n in Enum.GetValues(typeof(SaveablePropName)))
        {
            if (n == SaveablePropName.notSavable) continue;
            int v = int.MinValue; try { v = SaveManager.GetIntValue(n.ToString(), int.MinValue); } catch { }
            if (!first) sb.Append(','); first = false;
            sb.Append(Api.Json(n.ToString())).Append(':').Append(v == int.MinValue ? "null" : v.ToString());
        }
        return sb.Append('}').ToString();
    }

    // part: 0 both, 1 states only, 2 props only
    private void Reset(string root, int part)
    {
        if (string.IsNullOrEmpty(root)) { _h.Event("resetpuzzle", "no root given"); return; }
        int statesReset = 0, statesSeen = 0, propsReset = 0, propsSeen = 0, held = 0;
        var notes = new List<string>();

        // find the puzzle centre (the vice) and all states under root in one pass
        Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<TrackedPeckState> all = null;
        try { all = UnityEngine.Object.FindObjectsOfType<TrackedPeckState>(true); } catch (Exception e) { notes.Add("states: " + e.Message); }
        var under = new List<(TrackedPeckState s, string path, string lbl)>();
        Vector3? center = null; Vector3 sum = Vector3.zero; int sumN = 0;
        if (all != null)
            for (int i = 0; i < all.Length; i++)
            {
                TrackedPeckState st = null; try { st = all[i]; } catch { }
                if (st == null) continue;
                string path; try { path = Api.TransformPath(st.transform); } catch { continue; }
                if (path.IndexOf(root, StringComparison.OrdinalIgnoreCase) < 0 || path.Contains("PlayerCharacter")) continue;
                string lbl = ""; try { lbl = st.label ?? ""; } catch { }
                under.Add((st, path, lbl));
                try { var pos = st.transform.position; sum += pos; sumN++; if (lbl == "victorySystem") center = pos; } catch { }
            }
        if (center == null && sumN > 0) center = sum / sumN;

        // 0. post "first:<suffix>" — states that must be written BEFORE the prop passes: a
        // hair-trigger button re-primed here fires victory, and the gourd/vice steps that
        // follow undo that again (BlindfoldCatwalk)
        var postSpec0 = PostFor(root) ?? "";
        if (part != 2 && !_quiet)
            foreach (var tok in postSpec0.Split(';'))
            {
                var t = tok.Trim(); if (!t.StartsWith("first:")) continue;
                var suffix = t.Substring(6);
                foreach (var (st, path, lbl) in under)
                    if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && PuzzleProbeBaseline.TryGet(PuzzleProbeBaseline.Key(path, lbl), out var b0))
                    {
                        try { st.SetState(b0); notes.Add("first: " + suffix.Split('/')[^1] + "→" + b0); } catch (Exception e) { notes.Add("first: " + e.Message); }
                    }
            }

        // 1. loose props around the puzzle → session-start pose (tiles back on the rack)
        int around = 0, aroundMatched = 0;
        float radius = RadiusFor(root);
        bool gourdOnly = GourdOnly(root);
        if (part != 1 && radius > 0 && center != null)
        {
            var r = Api.ResetPropsAround(center.Value, radius);
            around = r.reset; aroundMatched = r.matched; held += r.held;
        }

        // 1b. named prop groups (e.g. the one cannonball) → baseline pose, wherever they are
        int grouped = 0;
        var groupList = GroupsFor(root);
        if (part != 1 && !string.IsNullOrEmpty(groupList))
            foreach (var g in groupList.Split(','))
            {
                // skip when every prop in the group is already pinned in its startHome: core's
                // pose reset compares against a session-start snapshot and would unpin/re-pin a
                // prop that is already home (the cannonball has been left unpinned that way)
                if (GroupAllHome(g.Trim())) { notes.Add("group " + g.Trim() + " already home"); continue; }
                try { Api.ResetProps(g.Trim(), 0); grouped++; } catch (Exception e) { notes.Add("group " + g + ": " + e.Message); }
            }

        // 2. props whose startHome is under root → back in it (the gourd into the vice)
        var props = part != 1 ? Prop.allProps : null;
        if (props != null)
            for (int i = 0; i < props.Count; i++)
            {
                Prop p = null; try { p = props[i]; } catch { }
                if (p == null) continue;
                try
                {
                    var home = p.startHome;
                    if (home == null) continue;
                    if (gourdOnly && !p.MatchesGroup(PropGroup.RewardGourd)) continue;
                    var hp = Api.TransformPath(home.transform);
                    if (hp.IndexOf(root, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    propsSeen++;
                    if (p.currentHome == home) continue;
                    if (!DropIfHeld(p)) { held++; continue; }
                    try { p.ServerSetUnpinned(); } catch { }
                    p.ServerSetPinned(home);
                    propsReset++;
                }
                catch (Exception e) { notes.Add($"{p.name}: {e.Message}"); }
            }

        // 2b. the gourd's save + map flag: the game writes gourdX=0 itself when a gourd leaves a
        // saveable hub slot, but a gourd re-homed straight from the floor keeps its solved code
        // (1xxx) and the 3D map keeps showing it tipped over. Match what the game does.
        string saveName = null; foreach (var pz in Puzzles) if (string.Equals(pz.root, root, StringComparison.OrdinalIgnoreCase)) { saveName = pz.save; break; }
        if (part != 1 && !string.IsNullOrEmpty(saveName))
        {
            try
            {
                int cur = SaveManager.GetIntValue(saveName, int.MinValue);
                if (cur != int.MinValue && cur != 0) { SaveManager.SetIntValue(saveName, 0); SaveManager.WriteCurrentSaveData(); notes.Add($"{saveName} {cur}→0 (saved)"); }
                // the map flag is a SyncVar on the gourd (RewardGourd.gourdState, hook → map refresh);
                // set it server-side so clients (and late joiners) get Locked too
                int flagged = 0;
                // purple (variant challenge) gourds are Hidden on the map until the post-game reveal
                foreach (var p in ix_props_for_flag(root))
                {
                    var rg = RewardGourdOf(p); if (rg == null) continue;
                    bool variant = false; try { variant = rg.isVariantChallenge; } catch { }
                    rg.ServerSetGourdState(variant ? GourdFlag.GourdState.Hidden : GourdFlag.GourdState.Locked); flagged++;
                    if (variant) notes.Add("variant gourd → Hidden");
                }
                if (flagged > 0) notes.Add($"{flagged} gourd flag(s) set");
            }
            catch (Exception e) { notes.Add("save/map: " + e.Message); }
        }

        // 2c. pending timers under root (the vice's ~20s launch countdown is a PeckEffectTimer
        // armed by victory) → cancelled, so a re-primed hair-trigger button can't launch the gourd
        int timers = 0;
        if (part != 2)
            try
            {
                foreach (var tm in UnityEngine.Object.FindObjectsOfType<PeckEffectTimer>(true))
                {
                    if (tm == null) continue;
                    string path; try { path = Api.TransformPath(tm.transform); } catch { continue; }
                    if (path.IndexOf(root, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    try { if (tm.GetTimeRemaining() > 0f) { tm.SetTimerActive(false); timers++; } } catch { }
                }
            }
            catch (Exception e) { notes.Add("timers: " + e.Message); }
        if (timers > 0) notes.Add($"{timers} pending timer(s) cancelled");

        // 3. states under root → baseline (last, so validators see the tiles already gone)
        // post "skip:<suffix>" excludes states whose write would itself trigger the puzzle
        // (BlindfoldCatwalk's end button pecks victory on ANY state write)
        var postSpec = PostFor(root) ?? "";
        var skips = new List<string>();
        foreach (var tok in postSpec.Split(';')) { var t = tok.Trim(); if (t.StartsWith("skip:")) skips.Add(t.Substring(5)); else if (t.StartsWith("first:")) skips.Add(t.Substring(6)); }
        if (part != 2)
            foreach (var (st, path, lbl) in under)
            {
                bool skip = false; foreach (var sk in skips) if (path.EndsWith(sk, StringComparison.OrdinalIgnoreCase)) { skip = true; break; }
                if (skip) continue;
                statesSeen++;
                int target;
                if (PuzzleProbeBaseline.TryGet(PuzzleProbeBaseline.Key(path, lbl), out var b)) target = b;
                else { bool hi = false; try { hi = st.hasInitialState; } catch { } if (!hi) continue; target = st.initialState; }
                int cur = int.MinValue; try { var c = st.currentPeckContext; if (c != null) cur = c.state; } catch { }
                if (cur == target) continue;
                try { st.SetState(target); statesReset++; }
                catch (Exception e) { notes.Add($"{path.Substring(path.LastIndexOf('/') + 1)}: {e.Message}"); }
            }

        if (_quiet)
        {
            if (statesReset > 0 || propsReset > 0) _h.Event("resetpuzzle", $"{root}: second pass re-applied {statesReset} state(s), {propsReset} prop(s)");
            return;
        }
        // second pass a moment later for states the game flips back in reaction to the
        // first pass (a box re-opens when its gourd is re-homed with the switches still -1)
        // (opt-in via post:"recheck" — puzzles like PointersParadise re-arm on purpose)
        var postAct = PostFor(root);
        if (part != 2 && postAct != null && postAct.Contains("recheck"))
        {
            // a 'first:' hair-trigger button re-fires victory, which queues the gourd launch
            // ~19s out; the follow-up pass has to land after that
            bool hasFirst = postAct.Contains("first:");
            float delay = 1.2f;
            foreach (var tok in postAct.Split(';')) { var t = tok.Trim(); if (t.StartsWith("recheck:") && float.TryParse(t.Substring(8), out var dv)) delay = dv; }
            // a re-primed hair-trigger re-fires victory; the vice's open animation launches the
            // gourd ~19s in. Keep re-settling (gourd + vice) until that window has passed.
            _rechecks.RemoveAll(x => string.Equals(x.root, root, StringComparison.OrdinalIgnoreCase));
            _rechecks.Add((root, Time.unscaledTime + delay, hasFirst, hasFirst ? Time.unscaledTime + 26f : 0f));
        }

        // post-step: bounce a trigger state so controllers see a real transition
        string bounceTok = null;
        if (postAct != null) foreach (var tok in postAct.Split(';')) if (tok.Trim().StartsWith("bounce:")) bounceTok = tok.Trim();
        if (part != 2 && bounceTok != null)
        {
            var suffix = bounceTok.Substring(7);
            foreach (var (st, path, lbl) in under)
                if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    try { st.SetState(1); _bounces.Add((st, Time.unscaledTime + 1f)); notes.Add("bounced " + suffix.Split('/')[^1]); } catch (Exception e) { notes.Add("bounce: " + e.Message); }
                    break;
                }
        }

        _h.Event("resetpuzzle", $"{root}: {statesReset}/{statesSeen} states reset, {propsReset}/{propsSeen} re-homed, {around}/{aroundMatched} loose props back{(grouped > 0 ? $", {grouped} group(s) re-posed" : "")}{(held > 0 ? $", {held} still held" : "")}{(notes.Count > 0 ? " — " + string.Join("; ", notes) : "")}");
    }

    // true when every prop in the named PropGroup has a startHome and is pinned in it
    private static bool GroupAllHome(string groupName)
    {
        if (!Enum.TryParse<PropGroup>(groupName, true, out var group)) return false;
        var props = Prop.allProps; if (props == null) return false;
        int n = 0;
        for (int i = 0; i < props.Count; i++)
        {
            Prop p = null; try { p = props[i]; } catch { }
            if (p == null) continue;
            try
            {
                if (!p.MatchesGroup(group)) continue;
                n++;
                if (p.startHome == null || p.currentHome != p.startHome) return false;
            }
            catch { return false; }
        }
        return n > 0;
    }

    // gourds whose startHome is under root (the puzzle's reward gourd(s))
    private static IEnumerable<Prop> ix_props_for_flag(string root)
    {
        var props = Prop.allProps; if (props == null) yield break;
        for (int i = 0; i < props.Count; i++)
        {
            Prop p = null; try { p = props[i]; } catch { }
            if (p == null) continue;
            bool hit = false;
            try { hit = p.MatchesGroup(PropGroup.RewardGourd) && p.startHome != null && Api.TransformPath(p.startHome.transform).IndexOf(root, StringComparison.OrdinalIgnoreCase) >= 0; } catch { }
            if (hit) yield return p;
        }
    }

    public static RewardGourd RewardGourdOf(Prop p)
    {
        try { var rg = p.GetComponent<RewardGourd>(); if (rg != null) return rg; } catch { }
        try { var rg = p.GetComponentInChildren<RewardGourd>(true); if (rg != null) return rg; } catch { }
        try { foreach (var rg in UnityEngine.Object.FindObjectsOfType<RewardGourd>(true)) if (rg != null && rg.prop == p) return rg; } catch { }
        return null;
    }

    private static bool DropIfHeld(Prop p)
    {
        foreach (var pc in Api.Players())
        {
            try
            {
                var h = pc.playerNetworking.playerHeldInformation;
                if (h != null && h.hasProp && h.GetProp() == p) { pc.playerNetworking.ServerDropPropAutomatic(false); return true; }
            }
            catch { return false; }
        }
        return true;
    }
}

// Baseline of every TrackedPeckState's value in a fresh world, keyed by hierarchy
// path + label. Persisted to statebaseline.tsv so it survives script reloads and
// game restarts; PuzzleProbe's statesnap writes it, PuzzleReset reads it.
public static class PuzzleProbeBaseline
{
    private static readonly Dictionary<string, int> _values = new();
    private static bool _loaded;
    private static string PathOnDisk => System.IO.Path.Combine(Api.DataDir, "statebaseline.tsv");

    public static string Key(string path, string label) => path + "|" + label;

    public static void Set(string key, int value) { lock (_values) _values[key] = value; }
    public static void Clear() { lock (_values) _values.Clear(); }
    public static bool TryGet(string key, out int value) { Load(); lock (_values) return _values.TryGetValue(key, out value); }
    public static int Count { get { Load(); lock (_values) return _values.Count; } }

    public static void Save()
    {
        try
        {
            var sb = new StringBuilder();
            lock (_values) foreach (var kv in _values) sb.Append(kv.Value).Append('\t').Append(kv.Key.Replace('\t', ' ')).Append('\n');
            System.IO.File.WriteAllText(PathOnDisk, sb.ToString());
        }
        catch (Exception e) { Api.Event("baseline", "script", "save failed: " + e.Message); }
    }

    public static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            if (!System.IO.File.Exists(PathOnDisk)) return;
            lock (_values)
                foreach (var line in System.IO.File.ReadAllLines(PathOnDisk))
                {
                    int tab = line.IndexOf('\t');
                    if (tab < 0) continue;
                    if (int.TryParse(line.Substring(0, tab), out var v)) _values[line.Substring(tab + 1)] = v;
                }
        }
        catch (Exception e) { Api.Event("baseline", "script", "load failed: " + e.Message); }
    }
}
