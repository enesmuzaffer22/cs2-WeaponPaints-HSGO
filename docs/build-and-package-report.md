# Build and Package Report

Report date: 2026-11-09
Branch: `feat/web-only-no-menumanager`
Base commit: `3b2eb5f96db976ff2b1def91debbc2f399859867`
Plugin version: `3.3a-web-only.2`

## Toolchain

- Target framework: `net10.0`
- SDK used: `.NET SDK 10.0.403`
- CounterStrikeSharp minimum API attribute: `338`
- CounterStrikeSharp NuGet compile reference: `1.0.378`
- Dapper: `2.1.89`
- MySqlConnector: `2.6.2`
- Newtonsoft.Json: `13.0.4`

`dotnet --info` and build startup printed an environment-only warning while checking Windows workload/service state in the sandbox. It did not produce a compiler warning or prevent restore/build.

## Baseline

Before source changes:

- `dotnet restore WeaponPaints.csproj`: PASS
- `dotnet build WeaponPaints.csproj -c Release --no-restore`: PASS
- Compiler result: 0 warnings, 0 errors

## Final validation

Commands used:

```powershell
dotnet restore WeaponPaints.csproj --ignore-failed-sources
dotnet clean WeaponPaints.csproj -c Release
dotnet build WeaponPaints.csproj -c Release --no-restore
```

Results:

- Restore: PASS (all projects up to date)
- Clean: PASS
- Release build: PASS
- Compiler result: 0 warnings, 0 errors
- Output: `bin/Release/net10.0/WeaponPaints.dll`
- MenuManager/PlayerSettings/AnyBase source or project references: none
- MenuManager/PlayerSettings/AnyBase files in staged package: none
- Weapons recreated by `!wp` receive an explicit next-frame skin application pass, avoiding the first-refresh entity-initialization race
- `dotnet test WeaponPaints.sln -c Release --no-restore --no-build`: exited successfully, but the solution contains no test project, so no automated tests were executed

## Package

Artifact:

```text
artifacts/WeaponPaints-WebOnly-3.3a-web-only.2.zip
```

- Size: 2,860,824 bytes
- SHA-256: `93C0C90FEEF10BDEF3EBC9CC70FF9A65F09E90D575B20BAB92DE69921E6C0F40`
- Layout root: `addons/counterstrikesharp/`
- Includes plugin runtime files, all localized `skins_*.json` metadata, `lang/`, and `gamedata/weaponpaints.json`
- Excludes generated config and database credentials
- Excludes `CounterStrikeSharp.API.dll`, which the server supplies
- Excludes `MenuManagerApi.dll`

The staged directory next to the ZIP is retained for inspection and is ignored by Git together with `artifacts/`.

## Reproduction

The GitHub workflow now builds Release and copies only runtime `skins_*.json` metadata into the plugin data directory. Its release layout remains compatible with extraction into `game/csgo/`.

Compilation and packaging do not validate native hooks or actual CS2 behavior. See `docs/test-matrix.md` for server-dependent status.
