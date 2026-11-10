using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace WeaponPaints;

[MinimumApiVersion(338)]
public partial class WeaponPaints : BasePlugin, IPluginConfig<WeaponPaintsConfig>
{
	internal static WeaponPaints Instance { get; private set; } = new();

	public WeaponPaintsConfig Config { get; set; } = new();
    private static WeaponPaintsConfig _config { get; set; } = new();
    public override string ModuleAuthor => "Nereziel & daffyy";
	public override string ModuleDescription => "Applies web-managed weapon paints and cosmetics from MySQL";
	public override string ModuleName => "WeaponPaints";
	public override string ModuleVersion => "3.3a-web-only.4";

	public override void Load(bool hotReload)
	{
		Instance = this;

		if (hotReload)
		{
			OnMapStart(string.Empty);
			
			GPlayerWeaponsInfo.Clear();
			GPlayersKnife.Clear();
			GPlayersGlove.Clear();
			GPlayersAgent.Clear();
			GPlayersPin.Clear();
			GPlayersMusic.Clear();
			Players.Clear();

			foreach (var player in Enumerable
					         .OfType<CCSPlayerController>(Utilities.GetPlayers().TakeWhile(_ => WeaponSync != null))
				         .Where(player => player.IsValid &&
					         !string.IsNullOrEmpty(player.IpAddress) && player is
						         { IsBot: false, Connected: PlayerConnectedState.Connected }))
			{
				Players.Add(player);
				QueuePlayerDataRefresh(player, reapplyCosmetics: true);
			}
		}

		Utility.LoadSkinsFromFile(ModuleDirectory + $"/data/skins_{_config.SkinsLanguage}.json", Logger);

		RegisterListeners();
	}

	public override void Unload(bool hotReload)
	{
		// Native hook outlives the plugin's AssemblyLoadContext; without this, css_plugins reload
		// leaves the old OnGiveNamedItemPost delegate registered and the next GiveNamedItem crashes the server.
		if (!_giveNamedItemHooked) return;

		VirtualFunctions.GiveNamedItemFunc.Unhook(OnGiveNamedItemPost, HookMode.Post);
		_giveNamedItemHooked = false;
	}

	public void OnConfigParsed(WeaponPaintsConfig config)
	{
		Config = config;
		_config = config;

		if (config.DatabaseHost.Length < 1 || config.DatabaseName.Length < 1 || config.DatabaseUser.Length < 1)
		{
			throw new InvalidOperationException("You need to setup Database credentials in \"configs/plugins/WeaponPaints/WeaponPaints.json\"!");
		}

		if (!File.Exists(Path.GetDirectoryName(Path.GetDirectoryName(ModuleDirectory)) + "/gamedata/weaponpaints.json"))
		{
			throw new InvalidOperationException("You need to upload \"weaponpaints.json\" to \"gamedata directory\"!");
		}
		
		var builder = new MySqlConnectionStringBuilder
		{
			Server = config.DatabaseHost,
			UserID = config.DatabaseUser,
			Password = config.DatabasePassword,
			Database = config.DatabaseName,
			Port = (uint)config.DatabasePort,
			Pooling = true,
			MaximumPoolSize = 640,
		};

		Database = new Database(builder.ConnectionString);
		// Created here instead of OnMapStart: on a cold boot the first map can start before the
		// listener is registered, leaving WeaponSync null and every command silently ignored.
		WeaponSync = new WeaponSynchronization(Database, config);

		_ = Utility.CheckDatabaseTables();
		_localizer = Localizer;

		Utility.Config = config;
		Utility.ShowAd(ModuleVersion);
		Task.Run(async () => await Utility.CheckVersion(ModuleVersion, Logger));
	}

	public override void OnAllPluginsLoaded(bool hotReload)
	{
		RegisterCommands();
	}
}
