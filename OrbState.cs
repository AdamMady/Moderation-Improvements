using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BigOrb;

// Shared between the http thread and the game thread. Only strings/collections in
// here, no il2cpp objects. Anything touching the game goes through MainQueue.
internal static class OrbState
{
    internal static readonly ConcurrentQueue<Action> MainQueue = new();
    internal static string DataDir;

    private static readonly object Lock = new();
    private static readonly List<string> Chat = new();     // already-serialised json
    private static readonly List<string> Signs = new();
    private static readonly List<string> Alerts = new();
    private static readonly List<string> Events = new();
    internal static readonly Dictionary<string, BanRecord> Bans = new(); // key: identifier

    internal volatile static string SnapshotJson = "{\"hosting\":false,\"players\":[]}";

    // log files are per hosting session
    internal volatile static string SessionName = "no session";
    private volatile static string _sessionTag = "nosession";

    internal static void NewSession(string worldName)
    {
        SessionName = string.IsNullOrEmpty(worldName) ? "unnamed" : worldName;
        var safe = new string(SessionName.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        _sessionTag = $"{safe}-{DateTime.Now:yyyyMMdd-HHmmss}";
        lock (Lock) { Chat.Clear(); Signs.Clear(); Alerts.Clear(); Events.Clear(); Roster.Clear(); SignLocks.Clear(); }
        AddEvent("session", null, "host", $"hosting started: {SessionName}");
    }

    internal class BanRecord { public string Identifier, Name; public ulong PlatformId; public string When; }

    internal class SeenRecord
    {
        public string Id, Name, Username; public ulong PlatformId;
        public string C1, C2, C3; public string FirstSeen, LastSeen; public bool Online;
    }
    private static readonly Dictionary<string, SeenRecord> Roster = new();

    internal static void RosterSeen(string id, string name, string username, ulong platformId, string c1, string c2, string c3, bool online)
    {
        lock (Lock)
        {
            if (!Roster.TryGetValue(id, out var r))
                Roster[id] = r = new SeenRecord { Id = id, FirstSeen = DateTime.Now.ToString("HH:mm:ss") };
            r.Name = name; r.Username = username; r.PlatformId = platformId;
            r.C1 = c1; r.C2 = c2; r.C3 = c3; r.Online = online;
            if (online) r.LastSeen = DateTime.Now.ToString("HH:mm:ss");
        }
    }

    internal static void RosterOffline(string id)
    {
        lock (Lock) { if (Roster.TryGetValue(id, out var r)) r.Online = false; }
    }

    internal static string RosterName(string id)
    {
        lock (Lock) return Roster.TryGetValue(id, out var r) ? r.Name : null;
    }

    internal static string RosterJson()
    {
        lock (Lock)
            return "[" + string.Join(",", Roster.Values.OrderBy(r => r.FirstSeen).Select(r =>
                $"{{\"id\":{J(r.Id)},\"name\":{J(r.Name)},\"username\":{J(r.Username)},\"platformId\":\"{r.PlatformId}\"," +
                $"\"colors\":[{J(r.C1)},{J(r.C2)},{J(r.C3)}],\"first\":{J(r.FirstSeen)},\"last\":{J(r.LastSeen)}," +
                $"\"online\":{(r.Online ? "true" : "false")},\"banned\":{(IsBanned(r.Id) ? "true" : "false")}}}")) + "]";
    }

    internal static void Init()
    {
        DataDir = Path.Combine(BepInEx.Paths.ConfigPath, "BigOrb");
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(Path.Combine(DataDir, "logs"));
        LoadBans();
    }

    internal static string J(string s)
    {
        if (s == null) return "null";
        var sb = new StringBuilder("\"");
        foreach (var c in s)
            sb.Append(c switch
            {
                '"' => "\\\"", '\\' => "\\\\", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t",
                _ when c < ' ' => $"\\u{(int)c:x4}",
                _ => c.ToString()
            });
        return sb.Append('"').ToString();
    }

    private static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    private static void Add(List<string> list, string json, string file)
    {
        lock (Lock)
        {
            list.Add(json);
            if (list.Count > 500) list.RemoveAt(0);
        }
        try { File.AppendAllText(Path.Combine(DataDir, "logs", $"{_sessionTag}-{file}.jsonl"), json + "\n"); }
        catch { /* logging must never break the game */ }
    }

    internal static void AddChat(string id, string name, string msg) =>
        Add(Chat, $"{{\"t\":{J(Now())},\"id\":{J(id)},\"name\":{J(name)},\"msg\":{J(msg)}}}", "chat");

    internal static void AddSign(string key, string text, string byId, string byName, uint netId = 0) =>
        Add(Signs, $"{{\"t\":{J(Now())},\"key\":{J(key)},\"text\":{J(text)},\"byId\":{J(byId)},\"byName\":{J(byName)},\"net\":{netId}}}", "signs");

    internal static void AddAlert(string kind, string id, string name, string detail) =>
        Add(Alerts, $"{{\"t\":{J(Now())},\"kind\":{J(kind)},\"id\":{J(id)},\"name\":{J(name)},\"detail\":{J(detail)}}}", "alerts");

    internal static void AddEvent(string kind, string id, string name, string detail = null) =>
        Add(Events, $"{{\"t\":{J(Now())},\"kind\":{J(kind)},\"id\":{J(id)},\"name\":{J(name)},\"detail\":{J(detail)}}}", "events");

    internal static string Tail(string which, int n = 100)
    {
        var list = which switch { "chat" => Chat, "signs" => Signs, "alerts" => Alerts, _ => Events };
        lock (Lock) return "[" + string.Join(",", list.Skip(Math.Max(0, list.Count - n))) + "]";
    }

    // ---- bans ----
    private static string BansPath => Path.Combine(DataDir, "bans.json");

    internal static void LoadBans()
    {
        try
        {
            if (!File.Exists(BansPath)) return;
            lock (Lock)
            {
                Bans.Clear();
                // identifier \t name \t platformId \t when
                foreach (var line in File.ReadAllLines(BansPath))
                {
                    var p = line.Split('\t');
                    if (p.Length >= 4 && p[0].Length > 0)
                        Bans[p[0]] = new BanRecord { Identifier = p[0], Name = p[1], PlatformId = ulong.TryParse(p[2], out var u) ? u : 0, When = p[3] };
                }
            }
        }
        catch (Exception e) { Plugin.Logger.LogError("loading bans: " + e.Message); }
    }

    private static void SaveBans()
    {
        lock (Lock)
            File.WriteAllLines(BansPath, Bans.Values.Select(b => $"{b.Identifier}\t{b.Name?.Replace('\t', ' ')}\t{b.PlatformId}\t{b.When}"));
    }

    // identifier = platform account id. steam is a 17 digit steamid64, psn is 19 digits,
    // others are a uuid with a -NN suffix. "0"/empty = syncvar hasn't arrived yet, never ban that.
    internal static bool IsUnsetIdentifier(string id) => string.IsNullOrEmpty(id) || id == "0";

    internal static string Platform(string id)
    {
        if (IsUnsetIdentifier(id)) return "unset";
        if (id.Length == 17 && id.StartsWith("7656119") && id.All(char.IsDigit)) return "steam";
        if (id.Length >= 18 && id.All(char.IsDigit)) return "psn";
        if (id.Length >= 36 && id[8] == '-' && id[13] == '-' && id[18] == '-' && id[23] == '-')
            return id.Length > 36 && id[36] == '-' ? "uuid" + id.Substring(36) : "uuid";
        return "other";
    }

    internal static bool BanAdd(string id, string name, ulong platformId)
    {
        if (IsUnsetIdentifier(id))
        {
            AddAlert("banfail", id, name, "identifier not synced yet, ban NOT recorded, retry in a moment");
            return false;
        }
        lock (Lock) Bans[id] = new BanRecord { Identifier = id, Name = name, PlatformId = platformId, When = Now() };
        SaveBans();
        AddEvent("ban", id, name, Platform(id));
        return true;
    }

    internal static void BanRemove(string id)
    {
        BanRecord r = null;
        lock (Lock) { if (Bans.TryGetValue(id, out r)) Bans.Remove(id); }
        if (r != null) { SaveBans(); AddEvent("unban", id, r.Name); }
    }

    internal static bool IsBanned(string id) { lock (Lock) return id != null && Bans.ContainsKey(id); }

    // ---- sign locks (not persisted, netIds change every session) ----
    internal class SignLock { public uint NetId; public string Key, Text, When; }
    private static readonly Dictionary<uint, SignLock> SignLocks = new();

    internal static void LockSign(uint netId, string key, string text)
    {
        lock (Lock) SignLocks[netId] = new SignLock { NetId = netId, Key = key, Text = text ?? "", When = Now() };
    }

    internal static bool UnlockSign(uint netId) { lock (Lock) return SignLocks.Remove(netId); }

    internal static string LockedText(uint netId)
    {
        lock (Lock) return SignLocks.TryGetValue(netId, out var l) ? l.Text : null;
    }

    internal static string SignLocksJson()
    {
        lock (Lock)
            return "[" + string.Join(",", SignLocks.Values.OrderBy(l => l.When).Select(l =>
                $"{{\"net\":{l.NetId},\"key\":{J(l.Key)},\"text\":{J(l.Text)},\"when\":{J(l.When)}}}")) + "]";
    }

    internal static string BansJson()
    {
        lock (Lock)
            return "[" + string.Join(",", Bans.Values.Select(b =>
                $"{{\"id\":{J(b.Identifier)},\"name\":{J(b.Name)},\"platformId\":\"{b.PlatformId}\",\"when\":{J(b.When)}}}")) + "]";
    }
}
