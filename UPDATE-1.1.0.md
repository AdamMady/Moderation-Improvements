# Moderation Improvements 1.1.0

Updated moderation backend to Big Orb 1.2.0, upstream commit 7b33007.

- Supports Big Walk 1.6.0's platform identifiers, authentication message and lobby manager.
- Uses connection-authenticated sign authors and the new server-side sign setter.
- Preserves persistent bans, lobby locking, non-host local bans, Discord/repository links and HideNSeek nametag/position suppression.
- Keeps L/menu-key controls, text-entry protection and lobby-only menu access.

The separate Puzzle Fixer variant adds puzzle resets/helpers, hub and finale resets, item baselines/restoration, gourd progress, solve feed/chimes, and an Advanced tab exposing every registered world-module command. All controls run inside the game; no browser dashboard or HTTP listener is started.

Both variants use the same plugin GUID and configuration/ban folder. Install one variant at a time; the Puzzle Fixer DLL replaces the standard DLL.

Built against assemblies extracted from BigWalkLatest. In-game behavior has not yet been tested on that build. Existing 1.0.3 release folders remain unchanged.

Original Big Orb: https://github.com/RadioFreeOpportunity/bigorb
Moderation Improvements: https://github.com/AdamMady/Moderation-Improvements/
Big Orb and these adaptations remain CC BY-NC-SA 4.0.
