using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BigOrb;

// Read-only run progress for the dashboard's "progress" tab: where every reward gourd is
// right now. Puzzle completion and hub state come from the puzzles/hubs panels that
// PuzzleReset/HubReset already publish; this adds the gourd census those don't cover —
// gourds that have left their puzzle but not been turned in (carried, dropped somewhere,
// sitting in some other home), and which gourds fill which hub slot.
//   panel gourds: { total, inPuzzle, turnedIn, out, list:[{name,label,root,where,detail,slotOf,heldBy,flag,saveVal,pos}] }
//     where = puzzle | slot | held | backpack | home | loose
public class Progress : IOrbScript
{
    public string Name => "progress";
    private ScriptHost _h;
    private int _tick;

    // gourd props + each puzzle's home position, indexed once a minute (props don't come and go)
    private readonly List<(Prop p, string name, string root, string label, RewardGourd rg, Vector3? home)> _gourds = new();
    private readonly List<(string label, Vector3 pos)> _homes = new();
    private float _builtAt = -999f;

    public void Load(ScriptHost host)
    {
        _h = host;
        host.Tick(() => { if (++_tick % 8 == 6 && Api.DashboardLive) Publish(); });
        host.Endpoint("progress", () => { Publish(); return _last ?? "null"; });
    }

    private void BuildIndex()
    {
        _gourds.Clear(); _homes.Clear();
        var props = Prop.allProps;
        if (props == null) return;
        for (int i = 0; i < props.Count; i++)
        {
            Prop p = null; try { p = props[i]; } catch { }
            if (p == null) continue;
            try
            {
                if (!p.MatchesGroup(PropGroup.RewardGourd)) continue;
                // root/label/RewardGourd are fixed per prop: resolving them here is what used to
                // cost a TransformPath + GetComponent per gourd every 2s
                string name = "?"; try { name = p.saveablePropName.ToString(); } catch { }
                string root = null; try { root = PuzzleReset.RootForGourd(p); } catch { }
                RewardGourd rg = null; try { rg = PuzzleReset.RewardGourdOf(p); } catch { }
                var home = p.startHome;
                _gourds.Add((p, name, root, root != null ? PuzzleReset.LabelOf(root) : name, rg, home != null ? home.transform.position : null));
                if (home != null) _homes.Add((root != null ? PuzzleReset.LabelOf(root) : home.name, home.transform.position));
            }
            catch { }
        }
        _builtAt = Time.unscaledTime;
    }

    private string _last;

    private void Publish()
    {
        if (Time.unscaledTime - _builtAt > 60f) BuildIndex();

        // who holds what, by prop netId
        var holders = new Dictionary<uint, string>();
        foreach (var pc in Api.Players())
        {
            try { var h = pc.playerNetworking.playerHeldInformation; if (h != null && h.hasProp) { var hp = h.GetProp(); if (hp != null) holders[hp.netId] = Api.Display(pc.playerNetworking); } } catch { }
        }
        var hubs = new List<(string root, string label)>(HubReset.Table());

        int inPuzzle = 0, turnedIn = 0, outCount = 0;
        var sb = new StringBuilder();
        bool first = true;
        foreach (var (g, name, root, label, rg, homePos) in _gourds)
        {
            if (g == null) continue;
            string where, detail = "", slotOf = null, heldBy = null, flag = null;
            int saveVal = int.MinValue; Vector3 pos = Vector3.zero;
            try { pos = g.transform.position; } catch { }
            try { saveVal = SaveManager.GetIntValue(name, int.MinValue); } catch { }
            try { if (rg != null) flag = rg.gourdState.ToString(); } catch { }

            PropHome cur = null, start = null;
            try { cur = g.currentHome; start = g.startHome; } catch { }
            if (holders.TryGetValue(SafeNetId(g), out heldBy))
            {
                where = "held"; detail = "carried by " + heldBy; outCount++;
            }
            else if (cur != null && start != null && cur == start)
            {
                where = "puzzle"; detail = "in its vice"; inPuzzle++;
            }
            else if (cur != null && IsSlot(cur))
            {
                where = "slot";
                string path = ""; try { path = Api.TransformPath(cur.transform); } catch { }
                foreach (var (hr, hl) in hubs) if (path.IndexOf(hr, StringComparison.OrdinalIgnoreCase) >= 0) { slotOf = hl; break; }
                if (slotOf == null) slotOf = path.IndexOf("Overflow", StringComparison.OrdinalIgnoreCase) >= 0 ? "Overflow monument" : Landmark(path) ?? cur.name;
                detail = "turned in at " + slotOf; turnedIn++;
            }
            else if (cur != null && Carrier(cur) is PlayerCharacter pc)
            {
                where = "backpack"; heldBy = Api.Display(pc.playerNetworking); detail = "in " + heldBy + "'s backpack"; outCount++;
            }
            else if (cur != null)
            {
                where = "home"; detail = "in " + cur.name + Near(pos); outCount++;
            }
            else
            {
                where = "loose"; detail = "loose" + Near(pos); outCount++;
            }

            if (!first) sb.Append(','); first = false;
            sb.Append("{\"net\":").Append(SafeNetId(g)).Append(",\"name\":").Append(Api.Json(name)).Append(",\"label\":").Append(Api.Json(label)).Append(",\"root\":").Append(Api.Json(root))
              .Append(",\"where\":").Append(Api.Json(where)).Append(",\"detail\":").Append(Api.Json(detail)).Append(",\"slotOf\":").Append(Api.Json(slotOf))
              .Append(",\"heldBy\":").Append(Api.Json(heldBy)).Append(",\"flag\":").Append(Api.Json(flag))
              .Append(",\"saveVal\":").Append(saveVal == int.MinValue ? "null" : saveVal.ToString())
              .Append(",\"pos\":[").Append((int)pos.x).Append(',').Append((int)pos.y).Append(',').Append((int)pos.z).Append(']');
            // the puzzle's own spot (gourd vice) for the admin map: fixed, so it doubles as the map's puzzle marker
            if (homePos is Vector3 hp) sb.Append(",\"home\":[").Append((int)hp.x).Append(',').Append((int)hp.y).Append(',').Append((int)hp.z).Append("],\"homeUv\":").Append(Api.MapUv(hp));
            sb.Append('}');
        }
        _last = "{\"total\":" + _gourds.Count + ",\"inPuzzle\":" + inPuzzle + ",\"turnedIn\":" + turnedIn + ",\"out\":" + outCount + ",\"list\":[" + sb + "]}";
        _h.Panel("gourds", _last);
    }

    // a home hanging off a player (GoesInBackpackHome etc.) = riding in that player's backpack
    private static PlayerCharacter Carrier(PropHome h) { try { return h.GetComponentInParent<PlayerCharacter>(); } catch { return null; } }

    private static uint SafeNetId(Prop p) { try { return p.netId; } catch { return 0; } }

    // a home that takes gourds and is not the gourd's own vice = a monument slot
    private static bool IsSlot(PropHome h) { try { return h.pinGroup == PropGroup.RewardGourd; } catch { return false; } }

    private static string Landmark(string path)
    {
        var parts = path.Split('/');
        if (parts.Length < 2 || !parts[0].StartsWith("Landmarks")) return null;
        int i = parts[1] == "Contents" ? 2 : 1;
        return i < parts.Length ? parts[i] : null;
    }

    // " · 12m from Teller window" — nearest puzzle home, so a dropped gourd is findable
    private string Near(Vector3 pos)
    {
        string best = null; float bd = float.MaxValue;
        foreach (var (label, hp) in _homes) { var d = (hp - pos).sqrMagnitude; if (d < bd) { bd = d; best = label; } }
        return best != null ? $" · {Mathf.Sqrt(bd):0}m from {best} · at {pos:0}" : $" · at {pos:0}";
    }
}
