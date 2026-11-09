# WeaponPaints Web-Only Operations Guide

## Purpose

This build removes the in-game cosmetic selection UI and its MenuManager dependency. The website remains the selection interface and continues writing to the existing MySQL tables. The CounterStrikeSharp plugin reads those rows, caches them per connected player, and applies them in CS2.

No website rewrite or database migration is required by this refactor.

## Required components

- CounterStrikeSharp with runtime (minimum API 338)
- A host compatible with the project's `net10.0` target
- MySQL or MariaDB-compatible database
- The existing website and Steam login configuration
- WeaponPaints plugin package from this branch

The following are deliberately not required:

- MenuManagerCS2
- MenuManagerCore
- PlayerSettingsCS2
- AnyBaseLibCS2

## Backup checklist

Before replacing an existing installation:

1. Stop the CS2 server or unload the old plugin.
2. Copy the complete existing `addons/counterstrikesharp/plugins/WeaponPaints/` directory.
3. Copy `addons/counterstrikesharp/gamedata/weaponpaints.json`.
4. Copy `addons/counterstrikesharp/configs/plugins/WeaponPaints/WeaponPaints.json` to a secure location.
5. Create a database backup containing all `wp_player_*` tables.
6. Record the old plugin build/version and CounterStrikeSharp version.
7. Keep database credentials and backups outside the repository and release ZIP.

## Package layout

Extract the package into the CS2 `game/csgo/` directory:

```text
addons/
  counterstrikesharp/
    gamedata/
      weaponpaints.json
    plugins/
      WeaponPaints/
        WeaponPaints.dll
        WeaponPaints.deps.json
        Dapper.dll
        MySqlConnector.dll
        Newtonsoft.Json.dll
        data/
          skins_en.json
          skins_*.json
        lang/
          en.json
          ...
```

The exact transitive runtime DLL set is determined by the Release build. `CounterStrikeSharp.API.dll` is supplied by the server and is not packaged. `MenuManagerApi.dll` must not be present in the deployed plugin directory.

## Configuration

The generated config is located at:

```text
addons/counterstrikesharp/configs/plugins/WeaponPaints/WeaponPaints.json
```

Web-only configuration shape:

```json
{
  "ConfigVersion": 11,
  "SkinsLanguage": "en",
  "DatabaseHost": "127.0.0.1",
  "DatabasePort": 3306,
  "DatabaseUser": "weaponpaints",
  "DatabasePassword": "replace-me",
  "DatabaseName": "weaponpaints",
  "CmdRefreshCooldownSeconds": 3,
  "Website": "https://example.com/skins",
  "Additional": {
    "KnifeEnabled": true,
    "GloveEnabled": true,
    "MusicEnabled": true,
    "AgentEnabled": true,
    "SkinEnabled": true,
    "PinsEnabled": true,
    "CommandWpEnabled": true,
    "CommandStattrak": ["stattrak", "st"],
    "CommandSkin": ["ws"],
    "CommandRefresh": ["wp"],
    "GiveRandomSkin": false
  }
}
```

Old menu-related properties in an existing JSON file are ignored because the web-only config model no longer consumes them. They can be removed after a successful test deployment.

### Command collision alternatives

CounterStrikeSharp maps `css_ws` to chat command `!ws` and `css_wp` to `!wp`. To avoid a provider collision, change the alias lists, for example:

```json
"CommandSkin": ["skinsite"],
"CommandRefresh": ["skinrefresh"]
```

This produces `!skinsite` and `!skinrefresh`. WeaponPaints does not register or modify `!admin`, `!ban`, `!kick`, or provider ACL rules.

## Database compatibility

The following table names and meanings are unchanged:

- `wp_player_skins`
- `wp_player_knife`
- `wp_player_gloves`
- `wp_player_agents`
- `wp_player_music`
- `wp_player_pins`

Steam identity remains the decimal SteamID64 string. Team value `2` is Terrorist and `3` is Counter-Terrorist. Existing wear, seed, name-tag, sticker, keychain, and StatTrak columns retain their current representation.

The plugin retains the upstream `CREATE TABLE IF NOT EXISTS` startup check. It does not add an ALTER, DROP, rename, or data migration.

The bundled website in this repository selects only weapon skins and knives. A custom/external site may continue selecting other cosmetic types by writing the existing tables.

## Runtime flow

1. A player connects and the plugin loads all enabled cosmetic rows on a background task.
2. The player's slot cache is replaced, preventing removed website selections from remaining stale.
3. Entity changes are scheduled back on the CounterStrikeSharp game thread.
4. Weapon skins and knife models apply when weapons are created/acquired.
5. Gloves, agent, music kit, and pin apply on spawn and after an explicit refresh.

`!wp` and `wp_refresh` wait for the database load before reapplying cached cosmetics. Active guns are recreated by the existing refresh logic, so there can be a brief weapon switch/re-equip effect. A dead or spectator player may need to spawn before pawn-bound cosmetics become visible.

No MySQL query runs every tick.

## Commands

### Player chat

- `!ws`: prints the configured `Website` value.
- `!wp`: reloads and reapplies the caller's cosmetics, subject to `CmdRefreshCooldownSeconds` and round-state restrictions.
- `!st` / `!stattrak`: toggles StatTrak on the held customized weapon. The state and counter are written to `wp_player_skins` when the player disconnects.

### Server console

```text
wp_refresh all
wp_refresh 76561198012345678
```

Player-context calls to `wp_refresh` are ignored. Completion is logged separately for each valid connected target after its DB load finishes.

## Updating

1. Take the backups listed above.
2. Stop/unload the plugin.
3. Remove old MenuManager assemblies from the WeaponPaints plugin directory, especially `MenuManagerApi.dll`.
4. Replace the plugin files and gamedata with the new package.
5. Preserve and review the existing database config.
6. Start the server and inspect CounterStrikeSharp logs.
7. Run the clean-server and provider-server tests in `docs/test-matrix.md` before production traffic.

## Rollback

1. Stop/unload the web-only plugin.
2. Restore the backed-up old WeaponPaints plugin directory.
3. Restore the old `weaponpaints.json` and config if their versions differ.
4. Reinstall the old build's menu dependencies only if that build requires them.
5. Restore the database backup only if data was independently changed; this refactor performs no migration.
6. Restart the server and verify the previous build/version in logs.

Because the schema is unchanged, normal rollback should require only plugin/config/gamedata restoration. Avoid restoring an older database backup unless necessary, because doing so can discard website selections made after the backup.

## Build from source

Use the SDK selected by the target framework:

```bash
dotnet --version
dotnet restore WeaponPaints.csproj
dotnet build WeaponPaints.csproj -c Release
```

Successful compilation proves source compatibility only. CounterStrikeSharp lifecycle, native hooks, provider admin coexistence, MySQL behavior, and cosmetic rendering still require a CS2 test server.

