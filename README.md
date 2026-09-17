# Moderation Improvements

Host-side moderation for Big Walk with a fullscreen in-game menu. Press **L** to open or close it. Escape and onscreen Close button also close it.

Features: kick, persistent bans, roster, CSV ban import/export, spoof and anonymous-login detection, voice flood limiting, fly/speed alerts, fake-system-chat filtering, moderation logs, sign rollback/locking, local nametags, join/leave chimes, and session password controls.

World, puzzle, item, finale, and progress features from Big Orb are excluded from build.

Data remains under `BepInEx/config/RadiosModeration/` so updates retain existing bans. Logs may include names, chat, signs, account IDs, and connection IDs. Original Big Orb's single built-in banned connection is retained; there is no online ban-list synchronization.

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
