# Web-Only Test Matrix

Date: 2026-11-09
Environment available: source tree and .NET 10 build environment only; no CounterStrikeSharp/CS2 test server, provider admin plugin, or test MySQL instance was supplied.

| ID | Test | Status | Evidence / remaining work |
| --- | --- | --- | --- |
| T01 | Load without MenuManager, PlayerSettings, or AnyBase | NOT TESTED | Source/project/package references are absent and Release build passes, but plugin load requires a real CounterStrikeSharp server. |
| T02 | Provider admin plugin: `!admin`, `!ban`, `!kick` | NOT TESTED | WeaponPaints registers none of these names and has no ACL integration; coexistence still requires the provider environment. |
| T03 | Select on website, then connect | NOT TESTED | DB read and connection pipeline are present; needs Steam-authenticated website, MySQL, and CS2. |
| T04 | Change selection while connected, then `!wp` | NOT TESTED | Code awaits DB load, replaces slot cache, schedules apply on the game thread, and explicitly reapplies skins to recreated weapon entities on the next frame; visual result requires CS2. |
| T05 | Run `!ws` | NOT TESTED | `css_ws` remains configurable and prints `Website`; chat rendering requires CS2. |
| T06 | Run `wp_refresh <steamid64>` and `wp_refresh all` | NOT TESTED | Console-only validation and per-player completion logging are implemented; needs connected players. |
| T07 | Map change, disconnect/reconnect, respawn | NOT TESTED | Cache cleanup/hot reload/spawn paths were inspected and compile; lifecycle behavior requires CS2. |
| T08 | T/CT team changes and different selections | NOT TESTED | Team 2/3 cache mapping is unchanged; rendering requires CS2. |
| T09 | New player/no rows and DB outage | NOT TESTED | Empty rows clear stale cache; connection failure is logged and reports refresh failure. Fault injection requires test DB/server. |
| T10 | Plugin hot reload/restart | NOT TESTED | Native unhook is retained and connected players are reloaded; duplicate-handler validation requires CS2. |
| T11 | StatTrak and other cosmetic types | NOT TESTED | StatTrak toggle/kill count/disconnect sync and all DB read/apply paths compile; game behavior requires CS2. |
| T12 | Clean server and customized provider server | NOT TESTED | Neither environment was available. |

## Static/build checks

| ID | Check | Status | Evidence |
| --- | --- | --- | --- |
| S01 | Baseline Release build | PASS | 0 warnings, 0 errors |
| S02 | Final restore and clean Release build | PASS | 0 warnings, 0 errors |
| S03 | No active MenuManager source/project references | PASS | Repository search over `*.cs` and `*.csproj` returned no matches |
| S04 | No direct PlayerSettings/AnyBase references | PASS | Repository search over `*.cs` and `*.csproj` returned no matches |
| S05 | No menu/admin/kill command registration | PASS | Only configurable StatTrak, website, refresh, and literal console refresh registrations remain |
| S06 | Package contains no menu plugin assembly | PASS | Recursive staged-package name check returned no MenuManager, PlayerSettings, or AnyBase files |
| S07 | MySQL schema names preserved | PASS | Existing six table names and team/SteamID query semantics remain in source; no ALTER/DROP/rename added |
| S08 | Website source unchanged | PASS | No file under `website/` was modified |
| S09 | Automated test suite | NOT AVAILABLE | `dotnet test` exits successfully, but the solution contains no test project |

Do not promote this build to production until the relevant T01-T12 rows are exercised on a disposable/test server and updated to PASS or FAIL.
