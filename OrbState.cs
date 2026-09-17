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
    private static long LastPollTicks;
    internal static void Polled() => System.Threading.Interlocked.Exchange(ref LastPollTicks, Environment.TickCount64);

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

    internal class BanRecord { public string Identifier, Name; public ulong PlatformId; public string When, Address; }

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
        if (string.IsNullOrEmpty(id)) return null;
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
        DataDir = Path.Combine(BepInEx.Paths.ConfigPath, "RadiosModeration");
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(Path.Combine(DataDir, "logs"));
        LoadBans();
        SeedDefaultBans();
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

    internal static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

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
                // identifier \t name \t platformId \t when \t address
                foreach (var line in File.ReadAllLines(BansPath))
                {
                    var p = line.Split('\t');
                    if (p.Length >= 4 && p[0].Length > 0)
                        Bans[p[0]] = new BanRecord { Identifier = p[0], Name = p[1], PlatformId = ulong.TryParse(p[2], out var u) ? u : 0, When = p[3], Address = p.Length >= 5 && p[4].Length > 0 ? p[4] : null };
                }
            }
        }
        catch (Exception e) { Plugin.Logger.LogError("loading bans: " + e.Message); }
    }

    private static void SaveBans()
    {
        lock (Lock)
            File.WriteAllLines(BansPath, Bans.Values.Select(b => $"{b.Identifier}\t{b.Name?.Replace('\t', ' ')}\t{b.PlatformId}\t{b.When}\t{b.Address ?? ""}"));
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

    // an address ban is keyed "addr:<transport address>" so it can exist without an identifier
    internal static bool BanAdd(string id, string name, ulong platformId, string address = null)
    {
        if (IsUnsetIdentifier(id))
        {
            if (string.IsNullOrEmpty(address))
            {
                AddAlert("banfail", id, name, "identifier not synced yet, ban NOT recorded, retry in a moment");
                return false;
            }
            id = "addr:" + address;
        }
        lock (Lock) Bans[id] = new BanRecord { Identifier = id, Name = name, PlatformId = platformId, When = Now(), Address = address };
        SaveBans();
        AddEvent("ban", id, name, Platform(id) + (address != null ? " addr " + address : ""));
        return true;
    }

    internal static bool IsBannedAddress(string address)
    {
        if (string.IsNullOrEmpty(address) || address == "localhost") return false;
        lock (Lock) return Bans.Values.Any(b => b.Address == address);
    }

    // bans that pre-date the address column get the last address the identifier was seen from
    internal static void BanAttachAddress(string id, string address)
    {
        if (string.IsNullOrEmpty(address)) return;
        bool changed = false;
        lock (Lock) { if (Bans.TryGetValue(id, out var b) && b.Address == null) { b.Address = address; changed = true; } }
        if (changed) { SaveBans(); AddEvent("ban", id, RosterName(id), "ban now carries address " + address); }
    }

    // ---- csv: identifier,name,platformId,when,address ----
    internal static string BansCsv()
    {
        var sb = new StringBuilder("identifier,name,platformId,when,address\n");
        lock (Lock)
            foreach (var b in Bans.Values.OrderBy(b => b.When))
                sb.Append(Csv(b.Identifier)).Append(',').Append(Csv(b.Name)).Append(',').Append(b.PlatformId).Append(',').Append(Csv(b.When)).Append(',').Append(Csv(b.Address)).Append('\n');
        return sb.ToString();
    }
    private static string Csv(string s) { s ??= ""; return s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s; }

    // merge: existing entries are kept, imported ones add identifiers/addresses not already banned
    internal static (int added, int skipped, int bad) BansImport(string csv)
    {
        int added = 0, skipped = 0, bad = 0;
        if (string.IsNullOrEmpty(csv)) return (0, 0, 0);
        foreach (var raw in csv.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0 || line.StartsWith("identifier,") || line.StartsWith("#")) continue;
            var f = SplitCsv(line);
            if (f.Count < 1) { bad++; continue; }
            var id = f[0].Trim(); var name = f.Count > 1 ? f[1] : ""; var addr = f.Count > 4 ? f[4].Trim() : "";
            ulong.TryParse(f.Count > 2 ? f[2] : "0", out var plat);
            if (addr.Length == 0 && id.StartsWith("addr:")) addr = id.Substring(5);
            if (IsUnsetIdentifier(id) && addr.Length == 0) { bad++; continue; }
            if (id.StartsWith("addr:") || IsUnsetIdentifier(id)) id = "addr:" + addr;
            bool exists; lock (Lock) exists = Bans.ContainsKey(id) || (addr.Length > 0 && Bans.Values.Any(b => b.Address == addr));
            if (exists) { skipped++; continue; }
            lock (Lock) Bans[id] = new BanRecord { Identifier = id, Name = name, PlatformId = plat, When = f.Count > 3 && f[3].Length > 0 ? f[3] : Now(), Address = addr.Length > 0 ? addr : null };
            added++;
        }
        if (added > 0) SaveBans();
        AddEvent("banimport", null, "host", $"{added} added, {skipped} already banned, {bad} unreadable");
        return (added, skipped, bad);
    }
    private static List<string> SplitCsv(string line)
    {
        var r = new List<string>(); var sb = new StringBuilder(); bool q = false;
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (q) { if (c == '"') { if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else q = false; } else sb.Append(c); }
            else if (c == '"') q = true;
            else if (c == ',') { r.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        r.Add(sb.ToString());
        return r;
    }

    // shipped ban list: known modded clients, keyed by the one thing they cannot change
    private static readonly (string addr, string name)[] DefaultBans =
    {
        ("0002f3f3f940422b9e85ae1057c285c9", "spoofing client (anonymous EOS login, impersonates players, voice flood)"),
    };
    internal static void SeedDefaultBans()
    {
        int n = 0;
        foreach (var (addr, name) in DefaultBans)
            if (!IsBannedAddress(addr)) { lock (Lock) Bans["addr:" + addr] = new BanRecord { Identifier = "addr:" + addr, Name = name, When = Now(), Address = addr }; n++; }
        if (n > 0) { SaveBans(); AddEvent("ban", null, "host", $"{n} known bad address(es) added from the built-in list"); }
    }

    internal static void BanRemove(string id)
    {
        BanRecord r = null;
        lock (Lock) { if (Bans.TryGetValue(id, out r)) Bans.Remove(id); }
        if (r != null) { SaveBans(); AddEvent("unban", id, r.Name); }
    }

    internal static bool IsBanned(string id) { lock (Lock) return id != null && Bans.ContainsKey(id); }

    internal static string LastAddressFor(string id)
    {
        lock (Lock) return AddrById.TryGetValue(id ?? "", out var a) ? a : null;
    }
    // per-session: what each connection proved (transport address) next to what it claimed
    internal class AuthInfo { public int ConnId; public string Address, ClaimedId, Version, When; }
    private static readonly Dictionary<int, AuthInfo> AuthByConn = new();
    private static readonly Dictionary<string, string> AddrById = new();
    internal static AuthInfo AuthSeen(int connId, string address, string claimedId, string version)
    {
        var a = new AuthInfo { ConnId = connId, Address = address, ClaimedId = claimedId, Version = version, When = Now() };
        lock (Lock) { AuthByConn[connId] = a; if (!string.IsNullOrEmpty(address) && !string.IsNullOrEmpty(claimedId)) AddrById[claimedId] = address; }
        return a;
    }
    internal static AuthInfo AuthFor(int connId) { lock (Lock) return AuthByConn.TryGetValue(connId, out var a) ? a : null; }

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
                $"{{\"id\":{J(b.Identifier)},\"name\":{J(b.Name)},\"platformId\":\"{b.PlatformId}\",\"when\":{J(b.When)},\"addr\":{J(b.Address)}}}")) + "]";
    }
}
