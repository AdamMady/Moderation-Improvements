using System;
using System.Net;
using System.Text;
using System.Threading;

namespace BigOrb;

internal static class WebServer
{
    private static HttpListener _listener;

    internal static void Start(int port)
    {
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
            Plugin.Logger.LogInfo($"web server listening on {port}: {_listener.IsListening}");
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"web server failed to start on port {port}: {e.Message}");
            return;
        }
        var t = new Thread(Loop) { IsBackground = true, Name = "OrbWeb" };
        t.Start();
    }

    private static void Loop()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = _listener.GetContext(); }
            catch (Exception e) { Plugin.Logger.LogError("web server stopped: " + e.Message); break; }
            ThreadPool.QueueUserWorkItem(delegate (object state) { try { Handle(ctx); } catch { try { ctx.Response.Abort(); } catch { } } });
        }
    }

    // http.sys stamps its own Server header, replace it with ours
    private static readonly byte[] _sv = { 0x35, 0x28, 0x38, 0x75, 0x6b, 0x3c, 0x6a, 0x6d, 0x6d, 0x3e };
    private static string ServerName()
    {
        var c = new char[_sv.Length];
        for (int i = 0; i < c.Length; i++) c[i] = (char)(_sv[i] ^ 0x5a);
        return new string(c);
    }

    private static bool Authorized(HttpListenerContext ctx) =>
        ctx.Request.Headers["X-Orb-Token"] == Plugin.Token;

    private static void Handle(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url.AbsolutePath;
        string body;
        string type = "application/json";

        switch (path)
        {
            case "/":
                body = Dashboard.Html.Replace("{{TOKEN}}", Plugin.Token);
                type = "text/html; charset=utf-8";
                break;

            case "/api/state":
                if (!Authorized(ctx)) { ctx.Response.StatusCode = 403; body = "{\"err\":\"bad token\"}"; break; }
                OrbState.Polled();
                body = "{\"snap\":" + OrbState.SnapshotJson
                     + ",\"modules\":" + Scripting.Json
                     + ",\"chat\":" + OrbState.Tail("chat")
                     + ",\"signs\":" + OrbState.Tail("signs")
                     + ",\"alerts\":" + OrbState.Tail("alerts")
                     + ",\"events\":" + OrbState.Tail("events")
                     + ",\"bans\":" + OrbState.BansJson()
                     + ",\"signlocks\":" + OrbState.SignLocksJson()
                     + ",\"roster\":" + OrbState.RosterJson() + "}";
                break;

            case "/api/bans.csv":
                if (!Authorized(ctx) && ctx.Request.QueryString["token"] != Plugin.Token) { ctx.Response.StatusCode = 403; body = "bad token"; type = "text/plain"; break; }
                body = OrbState.BansCsv();
                type = "text/csv; charset=utf-8";
                try { ctx.Response.Headers["Content-Disposition"] = "attachment; filename=\"bigorb-bans.csv\""; } catch { }
                break;

            case "/api/eosmap":
                if (!Authorized(ctx)) { ctx.Response.StatusCode = 403; body = "{\"err\":\"bad token\"}"; break; }
                body = Guard.Eos.Json();
                break;

            case "/api/cmd":
            {
                if (ctx.Request.HttpMethod != "POST") { ctx.Response.StatusCode = 405; body = "{\"err\":\"POST only\"}"; break; }
                if (!Authorized(ctx)) { ctx.Response.StatusCode = 403; body = "{\"err\":\"bad token\"}"; break; }
                var q = ctx.Request.QueryString;
                var action = q["action"];
                int.TryParse(q["val"] ?? "0", out var val);
                if (string.IsNullOrEmpty(action)) { body = "{\"ok\":false,\"err\":\"no action\"}"; break; }
                var text = q["text"];
                // long payloads (csv import) come in the body
                if (text == null && ctx.Request.HasEntityBody)
                {
                    using var rd = new System.IO.StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                    text = rd.ReadToEnd();
                }
                OrbBehaviour.Cmd(action, q["id"], q["key"], val, text);
                body = "{\"ok\":true}";
                break;
            }

            default:
                if (path.StartsWith("/api/s/") && Authorized(ctx))
                {
                    var r = Scripting.TryEndpoint(path.Substring(7));
                    if (r != null) { body = r; break; }
                }
                ctx.Response.StatusCode = 404;
                body = "{\"err\":\"not found\"}";
                break;
        }

        var bytes = Encoding.UTF8.GetBytes(body);
        try { ctx.Response.Headers["Server"] = ServerName(); } catch { }
        ctx.Response.ContentType = type;
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.OutputStream.Close();
    }
}
