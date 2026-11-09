# WeaponPaints Web-Only Dependency Audit

Audit date: 2026-11-09
Audit scope: source inspection only; no refactor, restore, build, package, database, or game-server change was performed.

## Repository baseline

| Item | Observed value |
| --- | --- |
| Working branch | `main` (tracking `origin/main`) |
| Working tree before audit | Clean |
| Commit | `3b2eb5f96db976ff2b1def91debbc2f399859867` |
| Commit date | `2026-11-02T12:53:40+01:00` |
| Commit subject | `chore: update dependencies and build actions` |
| Plugin version | `3.3a` (`VERSION` and `ModuleVersion`) |
| Origin | `https://github.com/enesmuzaffer22/cs2-WeaponPaints-HSGO.git` |
| Upstream | `https://github.com/Nereziel/cs2-WeaponPaints.git` |
| Target framework | `net10.0` |
| Minimum CounterStrikeSharp API | `338` |
| Installed SDK selected by CLI | `.NET SDK 10.0.403` |

`dotnet --info` reported SDKs 8.0.418, 10.0.103, and 10.0.403. It also emitted an access-denied warning while querying the Windows Service Control Manager for workload information. The SDK, host, and runtime inventory was still printed. No restore or build was run in this audit phase.

## Declared compile-time dependencies

| Dependency | Version/source | Direct use | Web-only disposition |
| --- | --- | --- | --- |
| CounterStrikeSharp.API | NuGet `1.0.378` | Plugin lifecycle, events, commands, entities, memory hooks, timers, translations | Keep |
| Dapper | NuGet `2.1.89` | All MySQL queries and commands | Keep |
| MySqlConnector | NuGet `2.6.2` | Connection creation and transactions | Keep |
| Newtonsoft.Json | NuGet `13.0.4` | Runtime JSON data loading and `JObject` skin metadata | Keep |
| MenuManagerApi | `3rd_party/MenuManagerApi.dll` | Menu creation/opening and menu API capability | Remove after menu code is removed |

The README dependency versions are stale in several places (for example CounterStrikeSharp 1.0.376, Dapper 2.1.72, MySqlConnector 2.5.0, and Newtonsoft.Json 13.0.5-beta1). The `.csproj` values above are authoritative for this commit.

## MenuManager dependency map

### Direct source references

- `Variables.cs` imports `MenuManager`, declares `IMenuApi`, and creates `PluginCapability<IMenuApi>("menu:nfcore")`.
- `WeaponPaints.cs::OnAllPluginsLoaded` calls `MenuCapability.Get()`, invokes all six `Setup*Menu` methods, then registers the non-menu commands in the same `try` block.
- `Utility.cs::CreateMenu` imports `MenuManager`, reads `MenuType`, and creates selectable/button/center/chat/console MenuManager menus.
- `Commands.cs` contains the six menu builders and their selection callbacks:
  - `SetupKnifeMenu`
  - `SetupSkinsMenu`
  - `SetupGlovesMenu`
  - `SetupAgentsMenu`
  - `SetupMusicMenu`
  - `SetupPinsMenu`
- `WeaponPaints.csproj` directly references `3rd_party/MenuManagerApi.dll`.

### Startup failure mechanism

`OnAllPluginsLoaded` currently treats menu acquisition and every command registration as one required operation. If `MenuCapability.Get()` or menu construction fails, the catch block logs `Error while loading required plugins` and rethrows. As a result, even `!ws`, `!wp`, `!st`, and console `wp_refresh` are unavailable without MenuManager.

### PlayerSettingsCS2 and AnyBaseLibCS2 classification

| Component | Direct source reference | Direct assembly reference from WeaponPaints | Classification |
| --- | --- | --- | --- |
| PlayerSettingsCS2 | None found | None found | Not a direct WeaponPaints dependency |
| AnyBaseLibCS2 | None found | None found | Not a direct WeaponPaints dependency |

The vendored `MenuManagerApi.dll` identifies itself as version 1.0.0.0 and references only `CounterStrikeSharp.API` 1.0.228.0 and `System.Runtime` 8.0.0.0. It does not directly reference PlayerSettingsCS2 or AnyBaseLibCS2. The repository README lists those plugins as part of the MenuManager installation stack, so they are best classified as runtime/transitive requirements of the external MenuManager implementation, not WeaponPaints itself.

Conclusion: after all MenuManager types and the DLL reference are removed, PlayerSettingsCS2 and AnyBaseLibCS2 can be removed from WeaponPaints installation requirements. This conclusion still requires T01 validation on a clean server because the MenuManager implementation and hosting provider's plugin loader are not present in this repository.

## Command audit

| Default command | Registration | Current behavior | Web-only decision |
| --- | --- | --- | --- |
| `!ws` | `css_ws` from `CommandSkin` | Prints `Website` plus localized cosmetic/refresh information | Keep; informational only |
| `!wp` | `css_wp` from `CommandRefresh` | Starts DB reload, then immediately reapplies cached cosmetics | Keep; repair async ordering and stale-cache behavior |
| `wp_refresh <steamid64\|all>` | Literal server command | Ignores player callers; refreshes one/all connected players | Keep; repair async ordering and stale-cache behavior |
| `!stattrak`, `!st` | `CommandStattrak` | Toggles the held customized weapon's StatTrak state in cache | Keep for compatibility; document that it writes on disconnect |
| `!kill` | `CommandKill` | Calls `CommitSuicide` | Remove/disable by default in web-only profile; unrelated and collision-prone |
| `!knife` | `CommandKnife` | Opens knife menu and writes selection to DB | Do not register |
| `!skins` | `CommandSkinSelection` | Opens weapon/skin menus and writes selection to DB | Do not register |
| `!gloves` | `CommandGlove` | Opens glove menu and writes selection to DB | Do not register |
| `!agents` | `CommandAgent` | Opens agent menu and writes selection to DB | Do not register |
| `!music` | `CommandMusic` | Opens music menu and writes selection to DB | Do not register |
| `!pin`, `!pins`, `!coin`, `!coins` | `CommandPin` | Opens pin menu and writes selection to DB | Do not register |

`!ws` and `!wp` aliases are already configurable lists, so providers can use alternatives such as `skinsite` and `skinrefresh` without adding ACL or admin integration. No source reference to `!admin`, `!ban`, `!kick`, provider ACLs, or admin permissions was found.

`wp_refresh` is effectively console-only because its handler immediately returns when `player != null`; it does not depend on a CounterStrikeSharp admin permission string.

## Database and cache flow

### Startup and connection lifecycle

1. `OnConfigParsed` validates DB settings and `gamedata/weaponpaints.json`.
2. It builds a pooled MySQL connection string, creates `Database` and `WeaponSynchronization`, and starts `Utility.CheckDatabaseTables()` without awaiting it.
3. `Load` loads JSON metadata and registers event/native listeners.
4. On full player connection, `GetPlayerData` runs on a background task and fills slot-keyed concurrent dictionaries.

The plugin performs `CREATE TABLE IF NOT EXISTS` for the six existing tables at startup. This is pre-existing behavior, not a migration added by the web-only work. The implementation must not add, alter, drop, or rename schema. Any proposal to change this existing creation behavior should be a separate, explicit decision.

### Cosmetic read/apply paths

| Cosmetic | Database read/cache | Application trigger |
| --- | --- | --- |
| Weapon skins, wear, seed, name tag, stickers, keychain, StatTrak | `wp_player_skins` -> `GPlayerWeaponsInfo[slot][team][defindex]` | `GiveNamedItem` post-hook and entity-spawn listener call `GivePlayerWeaponSkin`; `RefreshWeapons` recreates held weapons |
| Knife model | `wp_player_knife` -> `GPlayersKnife[slot][team]` | Weapon creation path changes knife subclass/definition; refresh recreates knife |
| Gloves | `wp_player_gloves` plus matching paint in `wp_player_skins` -> glove and weapon caches | Player spawn and refresh call `GivePlayerGloves` |
| Agent | `wp_player_agents` -> `GPlayersAgent[slot]` | Player spawn and refresh call `GivePlayerAgent` |
| Music kit | `wp_player_music` -> `GPlayersMusic[slot][team]` | Player spawn/refresh updates inventory; round MVP handler overrides kit |
| Pin/coin | `wp_player_pins` -> `GPlayersPin[slot][team]` | Player spawn and refresh update inventory rank slot 5 |

Steam identity is read from `CCSPlayerController.SteamID.ToString()` and used as the `steamid` parameter. Team values map `2` to Terrorist, `3` to Counter-Terrorist, and other values to both teams during reads.

### Database writes

Menu selection callbacks are the only callers of:

- `SyncKnifeToDatabase`
- `SyncGloveToDatabase`
- `SyncAgentToDatabase`
- `SyncWeaponPaintsToDatabase`
- `SyncMusicToDatabase`
- `SyncPinToDatabase`

After menu removal these methods have no web-only caller and can be removed. `SyncStatTrakToDatabase` is independent of menus and runs on player disconnect, preserving the StatTrak toggle and kill counter. Therefore the web-only plugin remains mostly DB-read-only but still updates StatTrak fields when that feature is used.

### Refresh correctness risks found

Both player `!wp` and console `wp_refresh` launch `GetPlayerData` with `Task.Run` and immediately call the cosmetic application methods. The new DB data is not awaited, so the immediate refresh commonly applies old cache values. In addition, `GetPlayerData` merges rows into existing slot dictionaries instead of replacing them. A preference removed from the website may therefore remain cached until disconnect/reload.

The web-only implementation should:

1. Load all enabled cosmetic rows off the game thread.
2. Replace or explicitly clear the target player's cached snapshot, rather than only merging rows.
3. Return to the CounterStrikeSharp game thread before touching entities or calling application methods.
4. Revalidate the player/slot before applying the loaded snapshot.

No per-tick MySQL query exists. `OnTick` only displays a menu-selection image and can be removed with that menu-only UI state.

## Bundled website compatibility

The repository website authenticates via Steam OpenID and uses `$_SESSION['steamid']`, which matches the plugin's SteamID64 string lookup.

The bundled `website/index.php` currently reads/writes only:

- `wp_player_skins`
- `wp_player_knife`

It writes identical knife and newly inserted skin selections for teams 2 and 3. Existing skin updates omit `weapon_team` in the `WHERE` clause, so both team rows are updated together. The bundled site does not expose glove, agent, music, pin, sticker, keychain, name-tag, or StatTrak selection. Those features can still be read and applied by the plugin if an existing external/custom site writes the documented tables and columns.

No website rewrite is required or planned. The table names, columns, team values, SteamID representation, and stored value semantics must remain unchanged.

## Lifecycle and hot-reload observations

- `OnConfigParsed` creates DB services before `Load`; this avoids `WeaponSync` being null on an early map start.
- `Load` registers listeners. On hot reload it clears all cosmetic caches and reloads connected players asynchronously.
- `Unload` explicitly unhooks the native `GiveNamedItem` hook to prevent a stale delegate from surviving AssemblyLoadContext reload.
- Menu acquisition is confined to `OnAllPluginsLoaded`; moving independent command registration out of its MenuManager failure path is safe and necessary.
- `OnPlayerSpawn` currently returns early when both knife and glove features are disabled, even though agent, music, and pin application is also inside that handler. This pre-existing coupling should be corrected while preserving each feature flag.
- Command registration must occur exactly once per plugin instance. Hot-reload verification must check duplicate commands and handlers on a real server.

## Runtime files and release layout

The GitHub Actions workflow builds with .NET 10 and stages:

```text
addons/counterstrikesharp/plugins/WeaponPaints/
addons/counterstrikesharp/gamedata/weaponpaints.json
```

The plugin output includes `lang/**` through the `.csproj`. The workflow copies `website/data` into the plugin output as `data/`; the runtime currently loads localized skins, gloves, agents, music, and collectibles JSON from that directory. After menus are removed, only skin metadata is demonstrably needed by runtime application logic (legacy-model selection and optional random skins). Final packaging should be based on verified runtime access, while the separate website package must retain all data it needs.

Expected non-framework runtime libraries include WeaponPaints, Dapper, MySqlConnector, and Newtonsoft.Json assemblies. `CounterStrikeSharp.API.dll` is intentionally removed from the release output because the server supplies it. `MenuManagerApi.dll` must be absent from the web-only package.

## Audit conclusion

The menu subsystem is structurally separable from the DB-read and cosmetic-application paths. WeaponPaints directly depends on MenuManager only for in-game selection UI. PlayerSettingsCS2 and AnyBaseLibCS2 are not directly used by this repository. Removing the six menu builders, their callbacks/UI state, the capability fields, and the project DLL reference should leave the web-driven read/apply path intact.

The highest-risk parts of the implementation are not the MenuManager type removals themselves; they are correct asynchronous refresh, stale-cache replacement, game-thread handoff, hot reload, and packaging the still-required JSON/runtime files. These require build validation and a real CounterStrikeSharp test server before production use.
