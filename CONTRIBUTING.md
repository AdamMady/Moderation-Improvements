# Contributing

Read this first: **this project is not actively maintained.** It was written for one
host's own lobbies and put here in case it is useful. PRs and issues get looked at when
someone has the time, which may be never. If you need a change, fork it. That is what
the licence is for.

That said, if you do open a PR, here is what makes it mergeable.

## What is in scope

Big Orb is a host-side moderation tool. Things that belong:

- kick / ban / logging / sign protection, and bugs in those
- anti-cheat heuristics, if they reduce false positives without adding raycasts or
  per-frame work
- dashboard fixes, accessibility, mobile layout
- compatibility fixes after a game update (say which build you tested on)

Things that do not belong:

- anything that affects guests' clients beyond what the game's own host powers already do
  (no forced teleports, no forced cosmetics, no invisibility, no message rewriting)
- anything that talks to the internet. The dashboard is localhost only and stays that way.
- new dependencies. It is one DLL with no runtime deps and should stay that way.
- Thunderstore / r2modman packaging, CI, release automation

If you want those things, make your own mod. Forks are encouraged.

## Before you open a PR

- Build it: `dotnet build -c Release -p:SkipDeploy=true` must succeed with no warnings.
- Run it: host a session with at least one other real client and use the feature you
  changed. "Compiles" is not tested. Say in the PR what you tested and on which game build.
- Keep it small. One fix or one feature per PR. Reformatting, renames and drive-by
  cleanup go in a separate PR, or better, nowhere.
- No new config keys unless the feature cannot work without one. Defaults must be safe:
  anything that kicks or bans automatically defaults to off.

## Code

- Match the style of the file you are in. Plain C#, no frameworks, no reflection tricks
  where a direct call works.
- Everything that touches the game runs on the main thread via `OrbState.MainQueue`.
  The HTTP thread only ever sees strings.
- Harmony patches: one class per hook, bind parameters as `__0`, `__1`, never by name
  (a renamed parameter in a game update should disable one hook, not the whole plugin).
  Add the class to the list in `Patches.PatchAllSafe`.
- Wrap anything that can throw inside a patch in `try/catch`. A patch that throws
  breaks the game for everyone in the lobby.
- IL2CPP inlines small methods; a patch on an inlined method silently never fires.
  If your hook does nothing, hook another point in the call chain and dedupe.
- Comments explain why, not what. Keep them short.

## Dashboard

- It is one HTML string in `Dashboard.cs`. Keep it that way; no build step, no bundler.
- No external resources. No CDN scripts, no fonts, no images. Inline everything.
- Every API call must go through `cmd()` so it carries the token.
- Both themes. If you add a colour, add it to both `:root` blocks.

## Commits and PRs

- One topic per commit, message in the imperative ("Fix ban gate on unsynced id").
- PR description: what changed, why, how you tested it. Screenshots for dashboard changes.
- By opening a PR you agree your contribution is licensed under the project licence
  (CC BY-NC-SA 4.0). No separate CLA.

## Reporting bugs

Open an issue with:

- game build (Steam, the date it updated) and BepInEx version
- the `BepInEx/LogOutput.log` lines mentioning `Big Orb`
- what you did, what happened, what you expected

Issues without a log get closed. Issues asking for features from the "does not belong"
list get closed. Issues asking when something will be fixed get closed.
