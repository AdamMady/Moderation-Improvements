using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace BigOrb;

// World features (puzzle/hub/finale reset, progress) are written as modules against this
// small host API so they stay self-contained. They are compiled in and registered at load.

public interface IOrbScript
{
    string Name { get; }
    void Load(ScriptHost host);
    void Unload() { }
}

public sealed class ScriptHost
{
    internal readonly Dictionary<string, Action<string, string, int, string>> Commands = new(StringComparer.OrdinalIgnoreCase);
    internal readonly List<(string name, Action tick)> Ticks = new();
    internal readonly Dictionary<string, string> Panels = new();
    internal readonly Dictionary<string, Func<string>> Endpoints = new(StringComparer.OrdinalIgnoreCase);
    public Harmony Harmony { get; }
    public string ScriptName { get; }

    internal ScriptHost(string scriptName, Harmony harmony) { ScriptName = scriptName; Harmony = harmony; }

    public void Command(string action, Action<string, string, int, string> handler) => Commands[action] = handler;
    public void Tick(Action tick) => Ticks.Add((ScriptName, tick));
    public void Panel(string key, string json)
    {
        json ??= "null";
        lock (Panels)
        {
            if (Panels.TryGetValue(key, out var old) && old == json) return;
            Panels[key] = json;
        }
        Scripting.PanelsDirty = true;
    }
    public void Endpoint(string name, Func<string> compute) => Endpoints[name] = compute;
    public void Log(string msg) => Plugin.Logger.LogInfo($"[{ScriptName}] {msg}");
    public void Warn(string msg) => Plugin.Logger.LogWarning($"[{ScriptName}] {msg}");
    public void Event(string kind, string detail) => OrbState.AddEvent(kind, null, ScriptName, detail);
    public void OnMain(Action a) => OrbState.MainQueue.Enqueue(a);
}

public static class Api
{
    public static IEnumerable<PlayerCharacter> Players() => OrbBehaviour.Players();
    public static PlayerCharacter Local() => OrbBehaviour.Local();
    public static PlayerCharacter ById(string id) => OrbBehaviour.ById(id);
    public static string Display(PlayerNetworking pn) => Patches.Display(pn);
    public static string MapUv(Vector3 pos) => "null";   // no map in this build
    public static int WorldVariant() => OrbBehaviour.WorldVariant();
    public static string Json(string s) => OrbState.J(s);
    public static string DataDir => OrbState.DataDir;
    public static void Event(string kind, string name, string detail) => OrbState.AddEvent(kind, null, name, detail);
    public static void ResetProps(string key, int radius) => Props.Reset(key, radius);
    public static (int reset, int matched, int held) ResetPropsAround(Vector3 center, float radius) => Props.ResetAround(center, radius);
    public static string TransformPath(Transform t) => OrbBehaviour.TransformPath(t);
    public static bool DashboardLive => OrbState.DashboardLive;
}

internal static class Scripting
{
    private static readonly List<(IOrbScript script, ScriptHost host)> _scripts = new();
    internal static volatile bool PanelsDirty;
    internal volatile static string Json = "{\"scripts\":[],\"panels\":{}}";

    internal static void Load(Harmony harmony)
    {
        foreach (var s in new IOrbScript[] { new PuzzleReset(), new HubReset(), new Finale(), new Progress(), new Solves() })
        {
            var h = new ScriptHost(s.Name, harmony);
            try { s.Load(h); _scripts.Add((s, h)); Plugin.Logger.LogInfo($"module {s.Name}: {h.Commands.Count} command(s)"); }
            catch (Exception e) { Plugin.Logger.LogError($"module {s.Name} failed to load: {e.Message}"); }
        }
        PanelsDirty = true;
    }

    internal static bool TryRun(string action, string id, string key, int val, string text)
    {
        foreach (var (s, h) in _scripts)
        {
            if (!h.Commands.TryGetValue(action, out var fn)) continue;
            try { fn(id, key, val, text); }
            catch (Exception e) { OrbState.AddEvent("module", null, h.ScriptName, $"{action} failed: {e.Message}"); Plugin.Logger.LogError($"[{h.ScriptName}] {action}: {e}"); }
            return true;
        }
        return false;
    }

    // endpoints compute on the main thread; the http thread waits briefly for the result
    internal static string TryEndpoint(string name)
    {
        foreach (var (s, h) in _scripts)
            if (h.Endpoints.TryGetValue(name, out var fn))
            {
                string result = null; var done = new System.Threading.ManualResetEventSlim(false);
                OrbState.MainQueue.Enqueue(() => { try { result = fn(); } catch (Exception e) { result = "{\"err\":" + OrbState.J(e.Message) + "}"; } finally { done.Set(); } });
                done.Wait(3000);
                return result ?? "{\"err\":\"timeout\"}";
            }
        return null;
    }

    internal static void Tick()
    {
        foreach (var (s, h) in _scripts)
            foreach (var (name, tick) in h.Ticks)
            {
                try { tick(); } catch (Exception e) { Plugin.Logger.LogError($"[{name}] tick: {e.Message}"); }
            }
        if (PanelsDirty) Publish();
    }

    private static void Publish()
    {
        PanelsDirty = false;
        var sb = new StringBuilder("{\"scripts\":[");
        bool first = true;
        foreach (var (s, h) in _scripts)
        {
            if (!first) sb.Append(','); first = false;
            sb.Append("{\"name\":").Append(OrbState.J(h.ScriptName)).Append(",\"commands\":[");
            bool f2 = true; foreach (var c in h.Commands.Keys) { if (!f2) sb.Append(','); f2 = false; sb.Append(OrbState.J(c)); }
            sb.Append("]}");
        }
        sb.Append("],\"panels\":{");
        first = true;
        foreach (var (s, h) in _scripts)
            lock (h.Panels)
                foreach (var kv in h.Panels)
                {
                    if (!first) sb.Append(','); first = false;
                    sb.Append(OrbState.J(kv.Key)).Append(':').Append(kv.Value);
                }
        sb.Append("}}");
        Json = sb.ToString();
    }
}
