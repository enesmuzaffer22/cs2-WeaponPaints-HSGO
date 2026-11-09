# WeaponPaints Web-Only

WeaponPaints Web-Only applies CS2 cosmetics from the existing WeaponPaints MySQL tables. Players choose cosmetics on a Steam-authenticated website; the game plugin reads those choices and applies them without MenuManager or in-game selection menus.

This fork is based on upstream WeaponPaints `3.3a` and remains licensed under GPL-3.0. See [LICENSE](LICENSE).

## Requirements

- CounterStrikeSharp with runtime, API 338 or newer
- .NET 10-compatible CounterStrikeSharp host
- MySQL or MariaDB-compatible database
- Existing WeaponPaints website/database data

MenuManagerCS2, MenuManagerCore, PlayerSettingsCS2, and AnyBaseLibCS2 are not required.

## Supported behavior

- Reads weapon skins, knives, gloves, agents, music kits, and pins from MySQL.
- Preserves team-specific selections, wear, seed, name tag, stickers, keychain, and StatTrak data.
- Applies weapon cosmetics on weapon creation/acquisition.
- Applies player cosmetics on spawn and after a completed refresh.
- Keeps `!ws`, `!wp`, `!st`/`!stattrak`, and server-console `wp_refresh`.
- Does not register knife, skin, glove, agent, music, pin, coin, kill, admin, ban, or kick commands.

## Installation

Extract the release package into `game/csgo/`. The resulting layout is:

```text
addons/counterstrikesharp/plugins/WeaponPaints/
addons/counterstrikesharp/gamedata/weaponpaints.json
```

Start the server once, then configure:

```text
addons/counterstrikesharp/configs/plugins/WeaponPaints/WeaponPaints.json
```

Never commit or distribute the generated database password.

## Commands

| Command | Context | Purpose |
| --- | --- | --- |
| `!ws` | Player chat | Shows the configured website address |
| `!wp` | Player chat | Reloads the player's DB data, then reapplies cosmetics |
| `!st`, `!stattrak` | Player chat | Toggles StatTrak for the held customized weapon |
| `wp_refresh <steamid64\|all>` | Server console only | Reloads one or all connected players |

The player command aliases are configurable. If `!ws` or `!wp` conflicts with a provider plugin, use alternatives such as `skinsite` and `skinrefresh` in the corresponding alias lists.

## Build

```bash
dotnet restore WeaponPaints.csproj
dotnet build WeaponPaints.csproj -c Release
```

The project targets `net10.0`. Runtime skin metadata is sourced from `website/data/skins_*.json`; localization files come from `lang/`; gamedata is `gamedata/weaponpaints.json`.

See [README-WEB-ONLY.md](README-WEB-ONLY.md) for configuration, database compatibility, backup, update, test, and rollback details.
