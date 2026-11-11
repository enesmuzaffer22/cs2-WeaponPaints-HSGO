using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void OnCommandRefresh(CCSPlayerController? player, CommandInfo command)
	{
		if (!Config.Additional.CommandWpEnabled) return;
		if (!Utility.IsPlayerValid(player) || player == null) return;
		if (!_gBCommandsAllowed || !player.PawnIsAlive)
		{
			if (!string.IsNullOrEmpty(Localizer["wp_command_refresh_unavailable"]))
				player.Print(Localizer["wp_command_refresh_unavailable"]);
			return;
		}

		if (CommandsCooldown.TryGetValue(player.Slot, out var cooldownEndTime) &&
		    DateTime.UtcNow < cooldownEndTime)
		{
			if (!string.IsNullOrEmpty(Localizer["wp_command_cooldown"]))
				player.Print(Localizer["wp_command_cooldown"]);
			return;
		}

		CommandsCooldown[player.Slot] = DateTime.UtcNow.AddSeconds(Config.CmdRefreshCooldownSeconds);
		QueuePlayerDataRefresh(
			player,
			reapplyCosmetics: true,
			notifyPlayer: true,
			refreshHeldWeapons: true);
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
			QueuePlayerDataRefresh(
				target,
				reapplyCosmetics: true,
				logToConsole: true,
				refreshHeldWeapons: true);
	}

	private void QueuePlayerDataRefresh(
		CCSPlayerController player,
		bool reapplyCosmetics,
		bool notifyPlayer = false,
		bool logToConsole = false,
		bool refreshHeldWeapons = false)
	{
		if (WeaponSync == null || !Utility.IsPlayerValid(player)) return;

		var expectedSteamId = player.SteamID;
		var playerInfo = CreatePlayerInfo(player);
		var refreshGeneration = PlayerRefreshGenerations.AddOrUpdate(player.Slot, 1, (_, current) => current + 1);
		var cosmeticsBeforeRefresh = CaptureCosmeticState(player.Slot);

		_ = Task.Run(async () =>
		{
			var loaded = await WeaponSync.GetPlayerData(playerInfo, () =>
				PlayerRefreshGenerations.TryGetValue(playerInfo.Slot, out var current) &&
				current == refreshGeneration);

			Server.NextFrame(() =>
			{
				if (!player.IsValid || player.IsBot || player.SteamID != expectedSteamId) return;

				if (!loaded)
				{
					FailPlayerRefresh(player, notifyPlayer, logToConsole);
					return;
				}

				if (reapplyCosmetics)
				{
					ScheduleCachedCosmeticApply(
						player,
						expectedSteamId,
						refreshGeneration,
						notifyPlayer,
						logToConsole,
						refreshHeldWeapons,
						cosmeticsBeforeRefresh);
				}
				else
				{
					CompletePlayerRefresh(player, notifyPlayer, logToConsole);
				}
			});
		});
	}

	private void ScheduleCachedCosmeticApply(
		CCSPlayerController player,
		ulong expectedSteamId,
		long refreshGeneration,
		bool notifyPlayer,
		bool logToConsole,
		bool refreshHeldWeapons,
		IReadOnlyDictionary<(CsTeam Team, int WeaponDefIndex), string> cosmeticsBeforeRefresh,
		int attempt = 0)
	{
		if (!player.IsValid || player.SteamID != expectedSteamId ||
		    !PlayerRefreshGenerations.TryGetValue(player.Slot, out var current) ||
		    current != refreshGeneration)
			return;

		IReadOnlySet<int> changedWeapons = refreshHeldWeapons
			? GetChangedWeaponDefIndexes(player.Slot, player.Team, cosmeticsBeforeRefresh)
			: new HashSet<int>();

		bool IsCurrentRefresh()
		{
			return player.IsValid && player.SteamID == expectedSteamId &&
			       PlayerRefreshGenerations.TryGetValue(player.Slot, out var generation) &&
			       generation == refreshGeneration;
		}

		if (ApplyCachedCosmetics(player, refreshHeldWeapons, changedWeapons, IsCurrentRefresh, success =>
		    {
			    if (!IsCurrentRefresh())
				    return;

			    if (success)
				    CompletePlayerRefresh(player, notifyPlayer, logToConsole);
			    else
				    FailPlayerRefresh(player, notifyPlayer, logToConsole);
		    }))
		{
			return;
		}

		if (attempt >= 3)
		{
			Logger.LogWarning(
				"Cosmetic refresh for {PlayerName} was loaded but could not be applied because the pawn or weapons were not ready.",
				player.PlayerName);
			FailPlayerRefresh(player, notifyPlayer, logToConsole);
			return;
		}

		RunAfterSeconds(0.2f * (attempt + 1), () =>
			ScheduleCachedCosmeticApply(
				player,
				expectedSteamId,
				refreshGeneration,
				notifyPlayer,
				logToConsole,
				refreshHeldWeapons,
				cosmeticsBeforeRefresh,
				attempt + 1));
	}

	private void FailPlayerRefresh(CCSPlayerController player, bool notifyPlayer, bool logToConsole)
	{
		Logger.LogWarning("Cosmetic refresh failed for {PlayerName}.", player.PlayerName);

		if (notifyPlayer && !string.IsNullOrEmpty(Localizer["wp_command_refresh_failed"]))
			player.Print(Localizer["wp_command_refresh_failed"]);

		if (logToConsole)
			Console.WriteLine($"[WeaponPaints] Database data loaded, but weapon refresh failed for {player.PlayerName}.");
	}

	private void CompletePlayerRefresh(CCSPlayerController player, bool notifyPlayer, bool logToConsole)
	{
		if (notifyPlayer && !string.IsNullOrEmpty(Localizer["wp_command_refresh_done"]))
			player.Print(Localizer["wp_command_refresh_done"]);

		if (logToConsole)
			Console.WriteLine($"[WeaponPaints] Database refresh completed for {player.PlayerName}.");
	}

	private bool ApplyCachedCosmetics(
		CCSPlayerController player,
		bool refreshHeldWeapons,
		IReadOnlySet<int> changedWeapons,
		Func<bool> canContinue,
		Action<bool> completed)
	{
		if (Config.Additional.GloveEnabled)
			GivePlayerGloves(player);
		var weaponsAccepted = true;
		if (Config.Additional.SkinEnabled || Config.Additional.KnifeEnabled)
		{
			weaponsAccepted = refreshHeldWeapons
				? RefreshWeapons(player, changedWeapons, canContinue, completed)
				: ApplyWeaponSkinsInPlace(player);
		}
		if (Config.Additional.AgentEnabled)
			GivePlayerAgent(player);
		if (Config.Additional.MusicEnabled)
			GivePlayerMusicKit(player);
		if (Config.Additional.PinsEnabled)
			GivePlayerPin(player);
		if (!refreshHeldWeapons || (!Config.Additional.SkinEnabled && !Config.Additional.KnifeEnabled))
			completed(true);

		return weaponsAccepted;
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
		TryApplyWeaponSkin(player, weapon, "StatTrak update");

		if (!string.IsNullOrEmpty(Localizer["wp_stattrak_action"]))
			player.Print(Localizer["wp_stattrak_action"]);
	}
}
