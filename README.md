# Moderation Improvements

Host-side moderation for Big Walk with a fullscreen in-game menu. Press **L** to open or close it. Escape and onscreen Close button also close it.

Features: kick, persistent bans, roster, CSV ban import/export, spoof and anonymous-login detection, voice flood limiting, fly/speed alerts, fake-system-chat filtering, moderation logs, sign rollback/locking, local nametags, join/leave chimes, and session password controls.

Updated to Big Orb 1.2.0 for Big Walk 1.6.0. The standard build keeps world tools disabled. The separate Puzzle Fixer build adds Puzzles, Hubs / Finale, Items / Progress, Solves and Advanced tabs, including every registered world command and data inspector. No HTTP listener or browser dashboard is started.

Install one variant at a time. Both variants use the same plugin GUID, configuration and bans. Save a backup before using world resets. Live testing on Big Walk 1.6.0 is still required.

Data is stored under `BepInEx/config/ModerationImprovements/`. Logs may include names, chat, signs, account IDs, and connection IDs. Original Big Orb's single built-in banned connection is retained; there is no online ban-list synchronization.

Discord: https://discord.gg/5z3WvVhxCf

## Credit and license

Moderation code taken from **Big Orb** by **RadioFreeOpportunity**. **AdamMady** created the in-game menu:
https://github.com/RadioFreeOpportunity/bigorb

Moderation Improvements repository:
https://github.com/AdamMady/Moderation-Improvements/

Original and modified work are licensed under **CC BY-NC-SA 4.0**. See `LICENSE` and `NOTICE`.

## Build

Requires .NET 6 SDK plus generated Big Walk BepInEx IL2CPP interop assemblies.

```powershell
dotnet build ModerationImprovements.csproj -c Release -p:BigWalkBepInEx="C:\path\to\Big Walk\BepInEx" -p:SkipDeploy=true
```

For the Puzzle Fixer variant, add `-p:WorldTools=true` to that command. Use BepInEx interop assemblies generated from Big Walk 1.6.0.
