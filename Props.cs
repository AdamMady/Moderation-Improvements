using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BigOrb;

// Where every prop was when the world loaded, so puzzle pieces and items can be put back.
// The baseline must come from a fresh world: the save persists prop positions, so a capture
// in a played save would bless wherever things were left. A fresh-world baseline for the
// 4-player world ships inside the plugin; other variants are captured the first time they
// are hosted (do that on a fresh save) and kept in posebaseline_Np.tsv.
//   cmd propreset key=all|near|<PropGroup>|<guid> [val=radius]
//   cmd posebaseline    re-capture from the live world (fresh save only)
internal static class Props
{
    private class Pose { public Vector3 Pos; public Quaternion Rot; public PropHome Home; public string Name; }
    private static readonly Dictionary<string, Pose> _poses = new(); // main-thread only
    private static float _topUpUntil;

    private static void UnpackEmbedded(int variant)
    {
        try
        {
            using var s = typeof(Props).Assembly.GetManifestResourceStream($"BigOrb.posebaseline_{variant}p.tsv");
            if (s == null) return;
            using var f = System.IO.File.Create(PoseBaselinePath(variant));
            s.CopyTo(f);
            Plugin.Logger.LogInfo($"pose baseline: unpacked built-in {variant}p baseline");
        }
        catch (Exception e) { Plugin.Logger.LogWarning("pose baseline unpack: " + e.Message); }
    }

    // The baseline must describe a FRESH world: the save persists prop positions, so a
    // live capture in a played save would bless wherever the tiles were left. It is
    // therefore persisted per lobby-size variant (posebaseline_Np.tsv) the first time a
    // variant is seen and loaded from disk after that. cmd posebaseline re-captures live
    // (only do that on a fresh save).
    private static bool _fromDisk;
    private static string PoseBaselinePath(int variant) => System.IO.Path.Combine(OrbState.DataDir, $"posebaseline_{variant}p.tsv");

    internal static void SessionReset()
    {
        _poses.Clear();
        _fromDisk = false;
        _topUpUntil = Time.unscaledTime + 60f; // landmarks stream in after the server flips active
        var variant = OrbBehaviour.WorldVariant();
        if (variant > 0 && !System.IO.File.Exists(PoseBaselinePath(variant))) UnpackEmbedded(variant);
        if (variant > 0 && System.IO.File.Exists(PoseBaselinePath(variant))) { LoadPoseBaseline(variant); _fromDisk = true; }
        SnapshotPoses();
    }

    internal static void RecapturePoseBaseline()
    {
        _poses.Clear(); _fromDisk = false;
        int n = SnapshotPoses();
        OrbState.AddEvent("posebaseline", null, "host", $"re-captured {n} prop poses from the live world → posebaseline_{OrbBehaviour.WorldVariant()}p.tsv");
    }

    private static void LoadPoseBaseline(int variant)
    {
        try
        {
            // resolve homes by hierarchy path against the live PropHome list
            var homes = new Dictionary<string, PropHome>();
            var all = PropHome.allPropHomes;
            if (all != null)
                for (int i = 0; i < all.Count; i++)
                {
                    PropHome h = null; try { h = all[i]; } catch { }
                    if (h == null) continue;
                    try { homes[OrbBehaviour.TransformPath(h.transform)] = h; } catch { }
                }
            int n = 0, unresolved = 0;
            foreach (var line in System.IO.File.ReadAllLines(PoseBaselinePath(variant)))
            {
                var t = line.Split('\t');
                if (t.Length < 10) continue;
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                var pose = new Pose
                {
                    Name = t[1],
                    Pos = new Vector3(float.Parse(t[2], ci), float.Parse(t[3], ci), float.Parse(t[4], ci)),
                    Rot = new Quaternion(float.Parse(t[5], ci), float.Parse(t[6], ci), float.Parse(t[7], ci), float.Parse(t[8], ci)),
                };
                if (t[9].Length > 0) { if (homes.TryGetValue(t[9], out var h)) pose.Home = h; else unresolved++; }
                _poses[t[0]] = pose; n++;
            }
            Plugin.Logger.LogInfo($"pose baseline: loaded {n} from posebaseline_{variant}p.tsv ({unresolved} homes unresolved, {homes.Count} homes live)");
            OrbState.AddEvent("posebaseline", null, "host", $"loaded {n} prop poses from disk ({variant}p){(unresolved > 0 ? $", {unresolved} homes unresolved" : "")}");
        }
        catch (Exception e) { Plugin.Logger.LogError("pose baseline load: " + e.Message); }
    }

    private static void SavePoseBaseline()
    {
        var variant = OrbBehaviour.WorldVariant();
        if (variant <= 0) return;
        try
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            foreach (var kv in _poses)
            {
                var p = kv.Value;
                string hp = ""; try { if (p.Home != null) hp = OrbBehaviour.TransformPath(p.Home.transform); } catch { }
                sb.Append(kv.Key).Append('\t').Append((p.Name ?? "").Replace('\t', ' '))
                  .Append('\t').Append(p.Pos.x.ToString("R", ci)).Append('\t').Append(p.Pos.y.ToString("R", ci)).Append('\t').Append(p.Pos.z.ToString("R", ci))
                  .Append('\t').Append(p.Rot.x.ToString("R", ci)).Append('\t').Append(p.Rot.y.ToString("R", ci)).Append('\t').Append(p.Rot.z.ToString("R", ci)).Append('\t').Append(p.Rot.w.ToString("R", ci))
                  .Append('\t').Append(hp).Append('\n');
            }
            System.IO.File.WriteAllText(PoseBaselinePath(variant), sb.ToString());
        }
        catch (Exception e) { Plugin.Logger.LogError("pose baseline save: " + e.Message); }
    }

    internal static void Tick()
    {
        if (_poses.Count > 0 && Time.unscaledTime < _topUpUntil) SnapshotPoses();
        else if (_poses.Count == 0 && Mirror.NetworkServer.active) SnapshotPoses();
    }

    private static int SnapshotPoses()
    {
        var props = Prop.allProps;
        if (props == null) return 0;
        int added = 0;
        for (int i = 0; i < props.Count; i++)
        {
            Prop p = null; try { p = props[i]; } catch { }
            if (p == null) continue;
            try
            {
                var guid = p.savablePropGuid;
                if (string.IsNullOrEmpty(guid) || _poses.ContainsKey(guid)) continue;
                _poses[guid] = new Pose { Pos = p.transform.position, Rot = p.transform.rotation, Home = p.currentHome, Name = p.name };
                added++;
            }
            catch { }
        }
        if (added > 0)
        {
            Plugin.Logger.LogInfo($"prop pose snapshot: +{added} ({_poses.Count} total){(_fromDisk ? " (top-up over disk baseline)" : "")}");
            if (!_fromDisk) SavePoseBaseline();
        }
        return added;
    }

    internal static int PoseCount => _poses.Count;

    // key: "all" | "near" (val = radius, around host) | PropGroup name | guid
    internal static void Reset(string key, int val)
    {
        var props = Prop.allProps;
        if (props == null || _poses.Count == 0) { OrbState.AddEvent("propreset", null, "host", "no pose snapshot yet"); return; }
        var me = OrbBehaviour.Local();
        var here = me != null ? me.transform.position : Vector3.zero;
        float r2 = val > 0 ? (float)val * val : 30f * 30f;
        PropGroup group = PropGroup.NoPropGroup; bool byGroup = Enum.TryParse(key, true, out group);
        int n = 0, skippedHeld = 0, checkedN = 0;
        for (int i = 0; i < props.Count; i++)
        {
            Prop p = null; try { p = props[i]; } catch { }
            if (p == null) continue;
            try
            {
                var guid = p.savablePropGuid;
                if (!_poses.TryGetValue(guid, out var pose)) continue;
                bool match = key == "all"
                          || (key == "near" && ((p.transform.position - here).sqrMagnitude <= r2 || (pose.Pos - here).sqrMagnitude <= r2))
                          || (byGroup && p.MatchesGroup(group))
                          || guid == key;
                if (!match) continue;
                checkedN++;
                // already there?
                bool samePos = (p.transform.position - pose.Pos).sqrMagnitude < 0.01f;
                bool sameHome = p.currentHome == pose.Home || (pose.Home == null && p.currentHome == null);
                if (samePos && sameHome) continue;
                // make whoever holds it drop it first
                if (!DropIfHeld(p)) { skippedHeld++; continue; }
                if (pose.Home != null)
                {
                    try { p.ServerSetUnpinned(); } catch { }
                    try { p.ServerSetPinned(pose.Home); n++; continue; } catch (Exception e) { Plugin.Logger.LogWarning($"repin {p.name}: {e.Message}"); }
                }
                p.ServerSetUnpinned();
                p.transform.position = pose.Pos;
                p.transform.rotation = pose.Rot;
                var rb = p.GetComponent<Rigidbody>();
                if (rb != null) { rb.position = pose.Pos; rb.rotation = pose.Rot; rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                n++;
            }
            catch (Exception e) { Plugin.Logger.LogWarning($"propreset {p?.name}: {e.Message}"); }
        }
        OrbState.AddEvent("propreset", null, "host", $"{key}: reset {n} of {checkedN} matched{(skippedHeld > 0 ? $", {skippedHeld} still held" : "")}");
    }

    // Return every prop whose session-start pose OR current position is within radius of
    // center to that pose (re-pinning into its original home if it had one, unpinning it
    // from any slot it's been put in otherwise). Used by puzzle resets with the puzzle's
    // own centre, so it works from the dashboard regardless of where the host stands.
    internal static (int reset, int matched, int held) ResetAround(Vector3 center, float radius)
    {
        var props = Prop.allProps;
        if (props == null || _poses.Count == 0) return (0, 0, 0);
        float r2 = radius * radius;
        int n = 0, matched = 0, held = 0;
        for (int i = 0; i < props.Count; i++)
        {
            Prop p = null; try { p = props[i]; } catch { }
            if (p == null) continue;
            try
            {
                var guid = p.savablePropGuid;
                if (!_poses.TryGetValue(guid, out var pose)) continue;
                if ((p.transform.position - center).sqrMagnitude > r2 && (pose.Pos - center).sqrMagnitude > r2) continue;
                matched++;
                bool samePos = (p.transform.position - pose.Pos).sqrMagnitude < 0.01f;
                bool sameHome = p.currentHome == pose.Home;
                if (samePos && sameHome) continue;
                if (!DropIfHeld(p)) { held++; continue; }
                if (pose.Home != null)
                {
                    try { p.ServerSetUnpinned(); } catch { }
                    try { p.ServerSetPinned(pose.Home); n++; continue; } catch (Exception e) { Plugin.Logger.LogWarning($"repin {p.name}: {e.Message}"); }
                }
                try { p.ServerSetUnpinned(); } catch { }
                p.transform.position = pose.Pos;
                p.transform.rotation = pose.Rot;
                var rb = p.GetComponent<Rigidbody>();
                if (rb != null) { rb.position = pose.Pos; rb.rotation = pose.Rot; rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                n++;
            }
            catch (Exception e) { Plugin.Logger.LogWarning($"resetaround {p?.name}: {e.Message}"); }
        }
        return (n, matched, held);
    }

    private static bool DropIfHeld(Prop p)
    {
        foreach (var pc in OrbBehaviour.Players())
        {
            try
            {
                var held = pc.playerNetworking.playerHeldInformation;
                if (held != null && held.hasProp && held.GetProp() == p) { pc.playerNetworking.ServerDropPropAutomatic(false); return true; }
            }
            catch { return false; }
        }
        return true;
    }
}
