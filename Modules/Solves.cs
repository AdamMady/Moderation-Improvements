using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace BigOrb;

// Live solve notifications. Two sources:
//   1. puzzles — PuzzleReset's per-puzzle solved flag (vice opened / box emptied), watched
//      every tick for the at-rest → solved edge. "Who" = every player whose synced peck
//      landed under that puzzle's root in the last two minutes (the victory peck itself
//      carries no player, so the presses leading up to it are the best evidence).
//   2. hubs + keys — straight from the DoPeck postfix: GourdMonumentReward → 1 (all slots
//      filled), KeyStoneLogic → 2 (key released), BigKeyPlinthNetworking → 1 (key placed).
// Each fires an Event (kind "solve") and, when enabled, a quiet chime of its own: a fast
// three-note rising triplet (C6 E6 G6, soft mallet tone), deliberately distinct from the
// two-note join/leave bell and much quieter. Synthesised here on a 2D AudioSource.
//   cmd solvechime val=0|1     chime on solve (default on)
//   cmd solvevol val=0..100    chime level in percent (default 18); both preview the chime
//   cmd solveclear             forget the recent-solve list
//   panel solves               last 20 solves for the dashboard
public class Solves : IOrbScript
{
    public string Name => "solves";
    private static ScriptHost _h;
    private static bool _chime = true;
    private const float PresserWindow = 120f;

    // recent pecks per puzzle root: player display name → last peck time
    private static readonly Dictionary<string, Dictionary<string, float>> _pressers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, bool> _wasSolved = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<(string t, string what, string who, string kind)> _recent = new();
    private static readonly Dictionary<string, float> _debounce = new(StringComparer.OrdinalIgnoreCase);
    private static float _vol = 0.18f;
    private static GameObject _holder;
    private static AudioSource _src;
    private static AudioClip _clip;
    private int _tick;

    public void Load(ScriptHost host)
    {
        _h = host;
        _pressers.Clear(); _wasSolved.Clear(); _debounce.Clear();
        // seed so puzzles already solved at load don't fire
        lock (PuzzleReset.SolvedNow) foreach (var kv in PuzzleReset.SolvedNow) _wasSolved[kv.Key] = kv.Value;
        host.Command("solvechime", (id, key, val, text) => { _chime = val != 0; host.Event("solve", "chime " + (_chime ? "on" : "off")); if (_chime) PlayChime(); });
        host.Command("solvevol", (id, key, val, text) => { _vol = Mathf.Clamp01(val / 100f); host.Event("solve", $"chime volume {val}%"); PlayChime(); });
        host.Command("solveclear", (id, key, val, text) => { lock (_recent) _recent.Clear(); Publish(); });
        host.Tick(Tick);
        try
        {
            var m = typeof(TrackedPeckState).GetMethod(nameof(TrackedPeckState.DoPeck));
            host.Harmony.Patch(m, postfix: new HarmonyMethod(typeof(Solves).GetMethod(nameof(DoPeckPostfix), BindingFlags.Static | BindingFlags.Public)));
        }
        catch (Exception e) { host.Warn("patch DoPeck: " + e.Message); }
        Publish();
    }

    public static void DoPeckPostfix(TrackedPeckState __instance, PeckContext __0, bool __1)
    {
        if (__1 || __instance == null) return; // prediction: the synced peck follows
        try
        {
            string path; try { path = Api.TransformPath(__instance.transform); } catch { return; }
            if (path.Contains("PlayerCharacter")) return;
            int state = int.MinValue; string who = null;
            try { if (__0 != null) { state = __0.state; var pc = __0.GetPlayerCharacter(); if (pc != null && pc.playerNetworking != null) who = Api.Display(pc.playerNetworking); } } catch { }
            string lbl = ""; try { lbl = __instance.label ?? ""; } catch { }

            // 1. remember who is pressing things under each registered puzzle
            if (who != null)
                foreach (var (root, _) in PuzzleReset.Table())
                    if (path.IndexOf(root, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        lock (_pressers)
                        {
                            if (!_pressers.TryGetValue(root, out var d)) _pressers[root] = d = new Dictionary<string, float>();
                            d[who] = Time.unscaledTime;
                        }
                        break;
                    }

            // 2. hub + key milestones straight off the state change
            if (lbl == "GourdMonumentReward" && state == 1) Fire("hub", HubName(path) + " — all gourds pinned, key releasing", who, path);
            else if (path.EndsWith("/KeyStoneLogic") && state == 2) Fire("key", HubName(path) + " — big key released", who, path);
            else if (path.EndsWith("/BigKeyPlinthNetworking") && state == 1) Fire("key", "big key placed: " + PlinthName(path), who, path);
        }
        catch { }
    }

    private void Tick()
    {
        // puzzle edge detection (SolvedNow refreshes every ~2s)
        List<KeyValuePair<string, bool>> snap;
        lock (PuzzleReset.SolvedNow) snap = new List<KeyValuePair<string, bool>>(PuzzleReset.SolvedNow);
        foreach (var kv in snap)
        {
            _wasSolved.TryGetValue(kv.Key, out var was);
            if (kv.Value && !was) Fire("puzzle", PuzzleReset.LabelOf(kv.Key), Pressers(kv.Key), kv.Key);
            _wasSolved[kv.Key] = kv.Value;
        }
        if (++_tick % 20 == 0)
        {
            // prune stale pressers
            lock (_pressers)
                foreach (var d in _pressers.Values)
                {
                    var stale = new List<string>();
                    foreach (var p in d) if (Time.unscaledTime - p.Value > PresserWindow) stale.Add(p.Key);
                    foreach (var s in stale) d.Remove(s);
                }
        }
    }

    private static string Pressers(string root)
    {
        lock (_pressers)
        {
            if (!_pressers.TryGetValue(root, out var d) || d.Count == 0) return null;
            var names = new List<(string n, float t)>();
            foreach (var p in d) if (Time.unscaledTime - p.Value <= PresserWindow) names.Add((p.Key, p.Value));
            names.Sort((a, b) => b.t.CompareTo(a.t));
            var s = new List<string>(); foreach (var (n, _) in names) s.Add(n);
            return s.Count > 0 ? string.Join(", ", s) : null;
        }
    }

    private static void Fire(string kind, string what, string who, string dedupeKey)
    {
        // hub/key pecks can land twice (server + local replay); one notice per 5s per source
        lock (_debounce)
        {
            if (_debounce.TryGetValue(dedupeKey, out var last) && Time.unscaledTime - last < 5f) return;
            _debounce[dedupeKey] = Time.unscaledTime;
        }
        var t = DateTime.Now.ToString("HH:mm:ss");
        lock (_recent) { _recent.Add((t, what, who, kind)); if (_recent.Count > 20) _recent.RemoveAt(0); }
        _h?.Event("solve", (kind == "puzzle" ? "🏆 " : kind == "hub" ? "🗝 " : "🔑 ") + what + (who != null ? " — by " + who : ""));
        if (_chime) PlayChime();
        Publish();
    }

    // ---- chime synthesis (same approach as Chime.cs, different voice) ----
    private const int Rate = 44100;
    private const float NoteLen = 0.22f, NoteFade = 0.02f, NoteGap = 0.075f;

    private static void PlayChime()
    {
        try
        {
            if (_holder == null) { _holder = new GameObject("OrbSolveChime"); UnityEngine.Object.DontDestroyOnLoad(_holder); _src = null; }
            if (_src == null)
            {
                _src = _holder.AddComponent<AudioSource>();
                _src.playOnAwake = false; _src.spatialBlend = 0f;
                _src.bypassEffects = true; _src.bypassListenerEffects = true; _src.bypassReverbZones = true;
                _src.ignoreListenerPause = true; _src.ignoreListenerVolume = true;
            }
            _clip ??= Build();
            _src.PlayOneShot(_clip, _vol);
        }
        catch (Exception e) { _h?.Warn("solve chime: " + e.Message); }
    }

    private static AudioClip Build()
    {
        float[] f = { 1046.5f, 1318.5f, 1568f }; // C6 E6 G6, 75 ms apart
        int total = (int)(Rate * (NoteGap * 2 + NoteLen));
        var data = new Il2CppStructArray<float>(total);
        for (int i = 0; i < total; i++)
        {
            float t = i / (float)Rate;
            data[i] = Mathf.Clamp(0.4f * (Note(t, f[0]) + Note(t - NoteGap, f[1]) + Note(t - NoteGap * 2, f[2])), -1f, 1f);
        }
        var clip = AudioClip.Create("orbSolve", total, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // soft mallet: short attack, fast decay, a touch of the 3rd harmonic rather than the bell's octave
    private static float Note(float t, float freq)
    {
        if (t < 0f || t > NoteLen) return 0f;
        float env = Mathf.Min(1f, t / 0.004f) * Mathf.Exp(-t * 22f);
        if (t > NoteLen - NoteFade) env *= (NoteLen - t) / NoteFade;
        return env * (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.15f * Mathf.Sin(6f * Mathf.PI * freq * t));
    }

    private static void Publish()
    {
        var sb = new StringBuilder("{\"chime\":").Append(_chime ? "true" : "false").Append(",\"recent\":[");
        lock (_recent)
            for (int i = _recent.Count - 1; i >= 0; i--)
            {
                var (t, what, who, kind) = _recent[i];
                if (i < _recent.Count - 1) sb.Append(',');
                sb.Append("{\"t\":").Append(Api.Json(t)).Append(",\"kind\":").Append(Api.Json(kind)).Append(",\"what\":").Append(Api.Json(what)).Append(",\"who\":").Append(Api.Json(who)).Append('}');
            }
        _h?.Panel("solves", sb.Append("]}").ToString());
    }

    // "LandmarksNonChallenge/Lookouts/Lookout_BlueTube/RoofStuff/…" → "Lookout_BlueTube"; "…/IntroPavilion/…" → "IntroPavilion"
    private static string HubName(string path)
    {
        foreach (var seg in path.Split('/')) if (seg.StartsWith("Lookout_") || seg == "IntroPavilion") return seg;
        var parts = path.Split('/'); return parts.Length > 2 ? parts[2] : path;
    }
    // "…/BigKeyPlinth Chairlift/…" → "Chairlift"; a bare "BigKeyPlinth" → its parent folder
    private static string PlinthName(string path)
    {
        var parts = path.Split('/');
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].StartsWith("BigKeyPlinth")) return parts[i].Length > 12 ? parts[i].Substring(12).Trim() : (i > 0 ? parts[i - 1] : parts[i]);
        return path;
    }

    public void Unload()
    {
        try { if (_holder != null) UnityEngine.Object.Destroy(_holder); } catch { }
        _holder = null; _src = null; _clip = null;
    }
}

