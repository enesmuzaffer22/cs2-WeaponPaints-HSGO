using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void OnCommandRefresh(CCSPlayerController? player, CommandInfo command)
	{
		if (!Config.Additional.CommandWpEnabled || !_gBCommandsAllowed) return;
		if (!Utility.IsPlayerValid(player) || player == null) return;

		if (CommandsCooldown.TryGetValue(player.Slot, out var cooldownEndTime) &&
		    DateTime.UtcNow < cooldownEndTime)
		{
			if (!string.IsNullOrEmpty(Localizer["wp_command_cooldown"]))
				player.Print(Localizer["wp_command_cooldown"]);
			return;
		}

		CommandsCooldown[player.Slot] = DateTime.UtcNow.AddSeconds(Config.CmdRefreshCooldownSeconds);
		QueuePlayerDataRefresh(player, reapplyCosmetics: true, notifyPlayer: true);
	}

	private void OnCommandWS(CCSPlayerController? player, CommandInfo command)
	{
		if (!Utility.IsPlayerValid(player) || player == null) return;

		if (!string.IsNullOrEmpty(Localizer["wp_info_website"]))
			player.Print(Localizer["wp_info_website", Config.Website]);
	}

	private void RegisterCommands()
	{
		RegisterAliases(_config.Additional.CommandStattrak, "Stattrak toggle", (player, info) =>
		{
			if (Utility.IsPlayerValid(player)) OnCommandStattrak(player, info);
		});

		RegisterAliases(_config.Additional.CommandSkin, "WeaponPaints website", (player, info) =>
		{
			if (Utility.IsPlayerValid(player)) OnCommandWS(player, info);
		});

		RegisterAliases(_config.Additional.CommandRefresh, "Reload WeaponPaints data", (player, info) =>
		{
			if (Utility.IsPlayerValid(player)) OnCommandRefresh(player, info);
		});

		AddCommand("wp_refresh", "Refresh WeaponPaints data from the server console", OnCommandSkinRefresh);
	}

	private void RegisterAliases(
		IEnumerable<string> aliases,
		string description,
		CommandInfo.CommandCallback callback)
	{
		foreach (var alias in aliases
		         .Where(alias => !string.IsNullOrWhiteSpace(alias))
		         .Select(alias => alias.Trim())
		         .Distinct(StringComparer.OrdinalIgnoreCase))
		{
			AddCommand($"css_{alias}", description, callback);
		}
	}

	private void OnCommandSkinRefresh(CCSPlayerController? player, CommandInfo command)
	{
		if (!Config.Additional.CommandWpEnabled || !_gBCommandsAllowed) return;
		if (player != null) return;

		var argument = command.GetArg(1);
		if (string.IsNullOrWhiteSpace(argument))
		{
			Console.WriteLine("[WeaponPaints] Usage: wp_refresh <steamid64|all>");
			Console.WriteLine("[WeaponPaints]   wp_refresh all");
			Console.WriteLine("[WeaponPaints]   wp_refresh 76561198012345678");
			return;
		}

		List<CCSPlayerController> targets;
		if (argument.Equals("all", StringComparison.OrdinalIgnoreCase))
		{
			targets = Utilities.GetPlayers()
				.Where(candidate => Utility.IsPlayerValid(candidate))
				.ToList();
		}
		else
		{
			var target = Utilities.GetPlayers().FirstOrDefault(candidate =>
				Utility.IsPlayerValid(candidate) && candidate.SteamID.ToString() == argument);

			if (target == null)
			{
				Console.WriteLine($"[WeaponPaints] Player with SteamID64 '{argument}' not found.");
				return;
			}

			targets = [target];
		}

		if (targets.Count == 0)
		{
			Console.WriteLine("[WeaponPaints] No connected players to refresh.");
			return;
		}

		Console.WriteLine($"[WeaponPaints] Queuing database refresh for {targets.Count} player(s).");
		foreach (var target in targets)
			QueuePlayerDataRefresh(target, reapplyCosmetics: true, logToConsole: true);
	}

	private void QueuePlayerDataRefresh(
		CCSPlayerController player,
		bool reapplyCosmetics,
		bool notifyPlayer = false,
		bool logToConsole = false)
	{
		if (WeaponSync == null || !Utility.IsPlayerValid(player)) return;

		var expectedSteamId = player.SteamID;
		var playerInfo = CreatePlayerInfo(player);

		_ = Task.Run(async () =>
		{
			var loaded = await WeaponSync.GetPlayerData(playerInfo, replaceExisting: true);

			Server.NextFrame(() =>
			{
				if (!player.IsValid || player.IsBot || player.SteamID != expectedSteamId) return;

				if (!loaded)
				{
					if (logToConsole)
						Console.WriteLine($"[WeaponPaints] Database refresh failed for {player.PlayerName}.");
					return;
				}

				if (reapplyCosmetics)
					ApplyCachedCosmetics(player);

				if (notifyPlayer && !string.IsNullOrEmpty(Localizer["wp_command_refresh_done"]))
					player.Print(Localizer["wp_command_refresh_done"]);

				if (logToConsole)
					Console.WriteLine($"[WeaponPaints] Database refresh completed for {player.PlayerName}.");
			});
		});
	}

	private void ApplyCachedCosmetics(CCSPlayerController player)
	{
		if (Config.Additional.GloveEnabled)
			GivePlayerGloves(player);
		if (Config.Additional.SkinEnabled || Config.Additional.KnifeEnabled)
			RefreshWeapons(player);
		if (Config.Additional.AgentEnabled)
			GivePlayerAgent(player);
		if (Config.Additional.MusicEnabled)
			GivePlayerMusicKit(player);
		if (Config.Additional.PinsEnabled)
			GivePlayerPin(player);
	}

	private static PlayerInfo CreatePlayerInfo(CCSPlayerController player)
	{
		return new PlayerInfo
		{
			UserId = player.UserId,
			Slot = player.Slot,
			Index = (int)player.Index,
			SteamId = player.SteamID.ToString(),
			Name = player.PlayerName,
			IpAddress = player.IpAddress?.Split(":")[0]
		};
	}

	private void OnCommandStattrak(CCSPlayerController? player, CommandInfo commandInfo)
	{
		if (player == null || !player.IsValid) return;

		var weapon = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value;
		if (weapon == null || !weapon.IsValid) return;

		if (!HasChangedPaint(player, weapon.AttributeManager.Item.ItemDefinitionIndex, out var weaponInfo) ||
		    weaponInfo == null)
			return;

		weaponInfo.StatTrak = !weaponInfo.StatTrak;
		RefreshWeapons(player);

		if (!string.IsNullOrEmpty(Localizer["wp_stattrak_action"]))
			player.Print(Localizer["wp_stattrak_action"]);
	}
}
