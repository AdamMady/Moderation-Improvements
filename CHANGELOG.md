# Changelog

## 1.1.0

Defence against modded clients

- Bans now record the connection's network identity (EOS ProductUserId) next to the
  account identifier and are enforced at the authentication handshake, before the player
  spawns. Bans made with 1.0.0 pick up the address the next time that identifier is seen.
- Kicks are enforced server-side. The game's own kick is a message the client is asked to
  act on; a modified client can ignore it.
- Every new connection is looked up on Epic. A Steam-linked account claiming a different
  Steam ID is a proven spoof. An account with no linked Steam/PSN/Xbox login at all is an
  anonymous device login, which the unmodified game never uses.
- The identity fields a client reports (epic id, platform id) are checked against the
  connection once they arrive. Mismatches are proof of a modified client.
- One address claiming several identifiers is flagged; three or more is banned.
- Voice packets are rate-limited per connection at the relay (default 120/s; a talking
  player sends about 50). Sustained flooding bans the connection.
- Guest chat that imitates a system message ("host has been removed") is dropped.
- New alert kinds: idspoof, idrotate, epicspoof, platspoof, eosspoof, eosanon, voiceflood,
  fakesys. New config section `guard` with `autoBan`, `banAnonymousLogins`,
  `voicePacketsPerSecond`, `dropFakeSystemChat`.
- The ban list ships with the connection address of a client seen impersonating players
  and flooding voice in several hosts' lobbies.

Ban list sharing

- Export the ban list as CSV (`/api/bans.csv` or the button) and import one from another
  host. Imports merge and never remove.
- Manual ban field accepts a connection address as well as an identifier.

World tools

- Puzzle reset: every puzzle back to its loaded state (pieces, vice, gourd). Hub reset.
  Black tower finale reset by stage or whole. Live progress view: gourds turned in, puzzles
  launched, hubs and key state, gourds loose in the world with a pull-to-me button.
- Solve feed with an optional chime.
- Item reset: items back to where they were when the world loaded, near you or everywhere,
  or by prop group. A fresh-world baseline for the 4-player world is built in; other sizes
  are captured the first time they are hosted.

Other

- Player entries in the API carry the connection address.
- `eosmap.tsv` records which real account each connection address resolved to.

## 1.0.0

Initial release.
