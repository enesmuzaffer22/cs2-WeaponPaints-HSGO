# Backup and Rollback Checklist

## Before test deployment

- [ ] Stop the server or unload WeaponPaints.
- [ ] Back up `addons/counterstrikesharp/plugins/WeaponPaints/`.
- [ ] Back up `addons/counterstrikesharp/gamedata/weaponpaints.json`.
- [ ] Back up `addons/counterstrikesharp/configs/plugins/WeaponPaints/WeaponPaints.json` outside the repository.
- [ ] Dump all `wp_player_*` tables using the hosting provider's supported database backup method.
- [ ] Record the current WeaponPaints, CounterStrikeSharp, and provider admin-plugin versions.
- [ ] Confirm the backup contains no publicly accessible credentials.
- [ ] Test database restore steps in a non-production location when possible.

## Test deployment

- [ ] Extract the web-only ZIP into a disposable/test `game/csgo/` tree.
- [ ] Confirm `MenuManagerApi.dll` is absent from the WeaponPaints directory.
- [ ] Preserve DB credentials; review config version 11 and command aliases.
- [ ] Keep MenuManager/PlayerSettings/AnyBase uninstalled for T01.
- [ ] Run T01-T12 and record server logs and PASS/FAIL results.

## Rollback

- [ ] Stop/unload the web-only plugin.
- [ ] Restore the previous WeaponPaints plugin directory.
- [ ] Restore the previous gamedata and config versions.
- [ ] Restore the old menu dependencies only if the previous build requires them.
- [ ] Restart and verify the previous plugin version and provider admin commands.
- [ ] Restore the database only if data was separately damaged or changed; normal code rollback does not require it because this refactor has no migration.

Database rollback can discard newer website selections. Prefer plugin/config/gamedata rollback unless a database restore is genuinely necessary.

