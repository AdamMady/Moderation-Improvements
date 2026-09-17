# Moderation Improvements

A fork of RadioFreeOpportunity's [Big Orb](https://github.com/RadioFreeOpportunity/bigorb) that adds a menu that opens up when you press "L"!

## What it does

- **Session code and password** in the header: copy the join code, show/change/remove the password
- **Player list** with look colours, platform badge and account identifier
- **Kick** and **ban**. Bans persist and are enforced server-side at the door, before the
  player spawns. A ban records the connection's network identity as well as the account
  identifier, so switching accounts does not get around it. Offline bans from the session
  roster or by pasting an identifier or connection address.
- **Ban list import / export** as CSV, so hosts can share lists. Imports merge; nothing is removed.
- **Modded-client detection.** The identity a client reports (account id, name, platform id,
  Epic id) is whatever its software says; the network identity it connected with is not.
  The two are compared, the network identity is looked up on Epic, and voice traffic is
  rate-limited at the relay. A client caught lying about who it is, logged into Epic with no
  real account behind it, or flooding voice is banned by its connection automatically
  (`guard.autoBan`, on by default). Chat that imitates a system message is dropped.
- **Chat log** and **sign edit log** with the author of each edit
- **Sign rollback**: erase a sign or restore any earlier text from the log
- **Sign lock**: pin a sign's text so guests' edits are reverted (host can still edit it)
- **Fly / speed alerts** with an optional auto-kick (off by default)
- **Local nametags** over other players (only you see them)
- Everything is written to per-session `.jsonl` logs

Only the host is affected. Guests do not need the plugin and nothing is sent to them
beyond the game's own traffic.

## Install

1. Install [BepInEx 6 (IL2CPP)](https://thunderstore.io/c/big-walk/p/BepInEx/BepInExPack_IL2CPP/)
   for Big Walk, via a mod manager or manually.
2. Drop `BigOrb.dll` from the release into `BepInEx/plugins/BigOrb/`.
3. Start the game. When you begin hosting, the dashboard opens in your browser
   (or open `http://localhost:7845/` yourself).

Built and tested against the Steam build of Big Walk current in September 2026
(Unity 6000.3.17) with BepInEx `6.0.0-be.755`. Other versions may or may not work.

## Credit

This is all basically [Big Orb By RadioFreeOpportunity](https://github.com/RadioFreeOpportunity/bigorb) but with an ingame menu, All actual moderation code goes to them!

## License

[CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/). In short:

- Use it, change it, share it, for free.
- Credit the original: keep the copyright/licence notice and link back to this repository
  in anything you distribute that is based on it.
- No commercial use. Do not sell it, sell access to it, or ship it as part of any paid
  bundle, pack or service.
- Anything you make from it must be released under this same licence, so it stays free.

Full text in `LICENSE`, short version in `NOTICE`.
