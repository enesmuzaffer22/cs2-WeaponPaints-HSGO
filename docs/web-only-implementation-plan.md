# Web-Only Implementation Plan

This plan follows the dependency audit at commit `3b2eb5f96db976ff2b1def91debbc2f399859867`. Code changes begin only after user approval of the audit and this plan.

## 1. Baseline and safety

- Create `feat/web-only-no-menumanager` from the audited commit.
- Record the clean status, SDK, restore, and unmodified Release build result.
- Add a backup checklist for the current plugin directory, config, gamedata, and database. Do not copy credentials into the repository.

## 2. Decouple startup and commands

- Remove `MenuCapability.Get()`, `MenuApi`, and MenuManager error handling.
- Register only web-only commands once, independently of external plugins.
- Keep configurable `!ws`, `!wp`, `!st`/`!stattrak`, and console `wp_refresh`.
- Disable/remove menu commands and make `!kill` disabled by default or remove its registration from the web-only profile.
- Preserve the cosmetic feature flags because they control DB reads and application, not just menus.

## 3. Remove menu implementation

- Delete the six `Setup*Menu` methods and their selection callbacks.
- Remove `Utility.CreateMenu`, MenuManager imports, `MenuType`, menu-only alias settings, selection-image state, and the menu-only tick listener.
- Remove menu-only metadata lists/loaders when no application path uses them; retain skin JSON used by runtime skin application and random-skin support.
- Keep StatTrak toggle/count behavior unless build or server evidence shows it is unsafe.

## 4. Remove external menu dependencies

- Remove the `MenuManagerApi.dll` reference and vendored binary from the deliverable/source if no reference remains.
- Verify with repository search and build output that no MenuManager, PlayerSettings, or AnyBase type/assembly remains.
- Keep CounterStrikeSharp, Dapper, MySqlConnector, Newtonsoft.Json, and the .NET 10 target.

## 5. Make web refresh reliable

- Refactor per-player DB loading into an awaited operation that produces/replaces a complete cache snapshot.
- Apply cosmetics on the game thread only after the DB load completes and the player is revalidated.
- Use the same refresh pipeline for connect/hot reload, `!wp`, and `wp_refresh` where practical.
- Preserve table/column semantics and add no schema migration.
- Document that weapon recreation, next acquisition, or respawn may be required for particular cosmetic types if verified in server tests.

## 6. Documentation, build, and package

- Update the README requirements and command/config sections.
- Add `README-WEB-ONLY.md` with installation, DB configuration, commands/aliases, backup, update, and rollback.
- Run restore and Release build with .NET 10; report all warnings.
- Stage a CounterStrikeSharp-compatible package containing required runtime libraries, `lang/`, required `data/`, and `gamedata/weaponpaints.json`, with no secrets and no MenuManager binary.
- Record changed files and reproducible build/package commands.

## 7. Verification and stop conditions

- Fill T01-T12 as PASS, FAIL, or NOT TESTED; do not infer game-server success from compilation.
- Locally verify clean source search, build output, package layout, and absence of menu assemblies.
- Mark provider-admin coexistence, live MySQL behavior, map changes, respawns, and hot reload NOT TESTED unless a suitable server is explicitly provided.
- Stop and request approval before any schema change, production DB access, credential change, server upload, or deployment.
