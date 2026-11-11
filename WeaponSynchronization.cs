using Dapper;
using MySqlConnector;
using System.Collections.Concurrent;
using CounterStrikeSharp.API.Modules.Utils;
using System.Globalization;

namespace WeaponPaints;

internal class WeaponSynchronization
{
	private readonly WeaponPaintsConfig _config;
	private readonly Database _database;
	private readonly ConcurrentDictionary<int, SemaphoreSlim> _playerLoadLocks = new();

	internal WeaponSynchronization(Database database, WeaponPaintsConfig config)
	{
		_database = database;
		_config = config;
	}

	internal async Task<bool> GetPlayerData(PlayerInfo? player, Func<bool>? canCommit = null)
	{
		if (player == null || string.IsNullOrEmpty(player.SteamId)) return false;
		var loadLock = _playerLoadLocks.GetOrAdd(player.Slot, _ => new SemaphoreSlim(1, 1));
		await loadLock.WaitAsync();
		var stagingSlot = -(player.Slot + 1);
		var stagedPlayer = new PlayerInfo
		{
			UserId = player.UserId,
			Slot = stagingSlot,
			Index = player.Index,
			SteamId = player.SteamId,
			Name = player.Name,
			IpAddress = player.IpAddress
		};

		try
		{
			await using var connection = await _database.GetConnectionAsync();
			ClearPlayerData(stagingSlot);

			if (_config.Additional.KnifeEnabled)
				GetKnifeFromDatabase(stagedPlayer, connection);
			if (_config.Additional.GloveEnabled)
				GetGloveFromDatabase(stagedPlayer, connection);
			if (_config.Additional.AgentEnabled)
				GetAgentFromDatabase(stagedPlayer, connection);
			if (_config.Additional.MusicEnabled)
				GetMusicFromDatabase(stagedPlayer, connection);
			if (_config.Additional.SkinEnabled || _config.Additional.GloveEnabled)
				GetWeaponPaintsFromDatabase(stagedPlayer, connection);
			if (_config.Additional.PinsEnabled)
				GetPinsFromDatabase(stagedPlayer, connection);

			if (canCommit != null && !canCommit()) return false;

			CommitPlayerData(stagingSlot, player.Slot);

			return true;
		}
		catch (Exception ex)
		{
			Utility.Log($"Unable to load player data for SteamID {player.SteamId}: {ex.Message}");
			return false;
		}
		finally
		{
			ClearPlayerData(stagingSlot);
			loadLock.Release();
		}
	}

	private static void CommitPlayerData(int sourceSlot, int targetSlot)
	{
		MoveOrRemove(WeaponPaints.GPlayerWeaponsInfo, sourceSlot, targetSlot);
		MoveOrRemove(WeaponPaints.GPlayersKnife, sourceSlot, targetSlot);
		MoveOrRemove(WeaponPaints.GPlayersGlove, sourceSlot, targetSlot);
		MoveOrRemove(WeaponPaints.GPlayersAgent, sourceSlot, targetSlot);
		MoveOrRemove(WeaponPaints.GPlayersMusic, sourceSlot, targetSlot);
		MoveOrRemove(WeaponPaints.GPlayersPin, sourceSlot, targetSlot);
	}

	private static void MoveOrRemove<T>(ConcurrentDictionary<int, T> cache, int sourceSlot, int targetSlot)
	{
		if (cache.TryGetValue(sourceSlot, out var value))
			cache[targetSlot] = value;
		else
			cache.TryRemove(targetSlot, out _);
	}

	private static void ClearPlayerData(int slot)
	{
		WeaponPaints.GPlayerWeaponsInfo.TryRemove(slot, out _);
		WeaponPaints.GPlayersKnife.TryRemove(slot, out _);
		WeaponPaints.GPlayersGlove.TryRemove(slot, out _);
		WeaponPaints.GPlayersAgent.TryRemove(slot, out _);
		WeaponPaints.GPlayersMusic.TryRemove(slot, out _);
		WeaponPaints.GPlayersPin.TryRemove(slot, out _);
	}

	private void GetKnifeFromDatabase(PlayerInfo? player, MySqlConnection connection)
	{
		try
		{
			if (!_config.Additional.KnifeEnabled || string.IsNullOrEmpty(player?.SteamId))
				return;

			const string query = "SELECT `knife`, `weapon_team` FROM `wp_player_knife` WHERE `steamid` = @steamid ORDER BY `weapon_team` ASC";
			var rows = connection.Query<dynamic>(query, new { steamid = player.SteamId }); // Retrieve all records for the player

			foreach (var row in rows)
			{
				// Check if knife is null or empty
				if (string.IsNullOrEmpty(row.knife)) continue;

				// Determine the weapon team based on the query result
				CsTeam weaponTeam = (int)row.weapon_team switch
				{
					2 => CsTeam.Terrorist,
					3 => CsTeam.CounterTerrorist,
					_ => CsTeam.None,
				};

				// Get or create entries for the player’s slot
				var playerKnives = WeaponPaints.GPlayersKnife.GetOrAdd(player.Slot, _ => new ConcurrentDictionary<CsTeam, string>());

				if (weaponTeam == CsTeam.None)
				{
					// Assign knife to both teams if weaponTeam is None
					playerKnives[CsTeam.Terrorist] = row.knife;
					playerKnives[CsTeam.CounterTerrorist] = row.knife;
				}
				else
				{
					// Assign knife to the specific team
					playerKnives[weaponTeam] = row.knife;
				}
			}
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException("Unable to load knife data.", ex);
		}
	}

	private void GetGloveFromDatabase(PlayerInfo? player, MySqlConnection connection)
	{
		try
		{
			if (!_config.Additional.GloveEnabled || string.IsNullOrEmpty(player?.SteamId))
				return;

			const string query = "SELECT `weapon_defindex`, `weapon_team` FROM `wp_player_gloves` WHERE `steamid` = @steamid ORDER BY `weapon_team` ASC";
			var rows = connection.Query<dynamic>(query, new { steamid = player.SteamId }); // Retrieve all records for the player

			foreach (var row in rows)
			{
				// Check if weapon_defindex is null
				if (row.weapon_defindex == null) continue;
				// Determine the weapon team based on the query result
				var playerGloves = WeaponPaints.GPlayersGlove.GetOrAdd(player.Slot, _ => new ConcurrentDictionary<CsTeam, ushort>());
				CsTeam weaponTeam = (int)row.weapon_team switch
				{
					2 => CsTeam.Terrorist,
					3 => CsTeam.CounterTerrorist,
					_ => CsTeam.None,
				};

				// Get or create entries for the player’s slot

				if (weaponTeam == CsTeam.None)
				{
					// Assign glove ID to both teams if weaponTeam is None
					playerGloves[CsTeam.Terrorist] = (ushort)row.weapon_defindex;
					playerGloves[CsTeam.CounterTerrorist] = (ushort)row.weapon_defindex;
				}
				else
				{
					// Assign glove ID to the specific team
					playerGloves[weaponTeam] = (ushort)row.weapon_defindex;
				}
			}
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException("Unable to load glove data.", ex);
		}
	}

	private void GetAgentFromDatabase(PlayerInfo? player, MySqlConnection connection)
	{
		try
		{
			if (!_config.Additional.AgentEnabled || string.IsNullOrEmpty(player?.SteamId))
				return;

			const string query = "SELECT `agent_ct`, `agent_t` FROM `wp_player_agents` WHERE `steamid` = @steamid";
			var agentData = connection.QueryFirstOrDefault<(string, string)>(query, new { steamid = player.SteamId });

			if (agentData == default) return;
			var agentCT = agentData.Item1;
			var agentT = agentData.Item2;

			if (!string.IsNullOrEmpty(agentCT) || !string.IsNullOrEmpty(agentT))
			{
				WeaponPaints.GPlayersAgent[player.Slot] = (
					agentCT,
					agentT
				);
			}
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException("Unable to load agent data.", ex);
		}
	}

	private void GetWeaponPaintsFromDatabase(PlayerInfo? player, MySqlConnection connection)
	{
		try
		{
			if ((!_config.Additional.SkinEnabled && !_config.Additional.GloveEnabled) ||
			    player == null || string.IsNullOrEmpty(player.SteamId))
				return;
				
			var playerWeapons = WeaponPaints.GPlayerWeaponsInfo.GetOrAdd(player.Slot,
				_ => new ConcurrentDictionary<CsTeam, ConcurrentDictionary<int, WeaponInfo>>());

			// var weaponInfos = new ConcurrentDictionary<int, WeaponInfo>();

			const string query = "SELECT * FROM `wp_player_skins` WHERE `steamid` = @steamid ORDER BY `weapon_team` ASC";
			var playerSkins = connection.Query<dynamic>(query, new { steamid = player.SteamId });

			foreach (var row in playerSkins)
			{
				int weaponDefIndex = row.weapon_defindex ?? 0;
				int weaponPaintId = row.weapon_paint_id ?? 0;
				float weaponWear = row.weapon_wear ?? 0f;
				int weaponSeed = row.weapon_seed ?? 0;
				string weaponNameTag = row.weapon_nametag ?? "";
				bool weaponStatTrak = row.weapon_stattrak ?? false;
				int weaponStatTrakCount = row.weapon_stattrak_count ?? 0;
				
				CsTeam weaponTeam = row.weapon_team switch
				{
					2 => CsTeam.Terrorist,
					3 => CsTeam.CounterTerrorist,
					_ => CsTeam.None,
				};
						
				string[]? keyChainParts = row.weapon_keychain?.ToString()?.Split(';');

				KeyChainInfo keyChainInfo = new KeyChainInfo();

				if (keyChainParts is { Length: 5 } &&
				    uint.TryParse(keyChainParts[0], out uint keyChainId) &&
				    float.TryParse(keyChainParts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float keyChainOffsetX) &&
				    float.TryParse(keyChainParts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float keyChainOffsetY) &&
				    float.TryParse(keyChainParts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float keyChainOffsetZ) &&
				    uint.TryParse(keyChainParts[4], out uint keyChainSeed))
				{
					// Successfully parsed the values
					keyChainInfo.Id = keyChainId;
					keyChainInfo.OffsetX = keyChainOffsetX;
					keyChainInfo.OffsetY = keyChainOffsetY;
					keyChainInfo.OffsetZ = keyChainOffsetZ;
					keyChainInfo.Seed = keyChainSeed;
				}
				else
				{
					// Failed to parse the values, default to 0
					keyChainInfo.Id = 0;
					keyChainInfo.OffsetX = 0f;
					keyChainInfo.OffsetY = 0f;
					keyChainInfo.OffsetZ = 0f;
					keyChainInfo.Seed = 0;
				}

				// Create the WeaponInfo object
				WeaponInfo weaponInfo = new WeaponInfo
				{
					Paint = weaponPaintId,
					Seed = weaponSeed,
					Wear = weaponWear,
					Nametag = weaponNameTag,
					KeyChain = keyChainInfo,
					StatTrak = weaponStatTrak,
					StatTrakCount = weaponStatTrakCount,
				};

				// Retrieve and parse sticker data (up to 5 slots)
				for (int i = 0; i <= 4; i++)
				{
					// Access the sticker data dynamically using reflection
					string stickerColumn = $"weapon_sticker_{i}";
					var stickerData = ((IDictionary<string, object>)row!)[stickerColumn]; // Safely cast row to a dictionary

					if (stickerData == null || string.IsNullOrEmpty(stickerData.ToString())) continue;
						
					var rawSticker = stickerData.ToString()!;
					var parts = rawSticker.Split(';');

					//"id;schema;x;y;wear;scale;rotation"
					if (parts.Length != 7 ||
					    !uint.TryParse(parts[0], out uint stickerId) ||
					    !uint.TryParse(parts[1], out uint stickerSchema) ||
					    !TryParseStickerFloat(parts[2], out float stickerOffsetX) ||
					    !TryParseStickerFloat(parts[3], out float stickerOffsetY) ||
					    !TryParseStickerFloat(parts[4], out float stickerWear) ||
					    !TryParseStickerFloat(parts[5], out float stickerScale) ||
					    !TryParseStickerFloat(parts[6], out float stickerRotation))
					{
						Utility.Log(
							$"Ignoring malformed sticker data for SteamID {player.SteamId}, weapon {weaponDefIndex}, slot {i}: '{rawSticker}'");
						continue;
					}

					if (stickerId == 0) continue;
						
					StickerInfo stickerInfo = new StickerInfo
					{
						Slot = i,
						Id = stickerId,
						Schema = stickerSchema,
						OffsetX = stickerOffsetX,
						OffsetY = stickerOffsetY,
						Wear = Math.Clamp(stickerWear, 0.0f, 1.0f),
						// A zero scale is the database default for an empty slot. Once an ID is
						// present it would make the decal invisible, so use the game's neutral scale.
						Scale = stickerScale > 0 ? stickerScale : 1.0f,
						Rotation = stickerRotation
					};

					weaponInfo.Stickers.Add(stickerInfo);
				}
					
				if (weaponTeam == CsTeam.None)
				{
					// Get or create entries for both teams
					var terroristWeapons = playerWeapons.GetOrAdd(CsTeam.Terrorist, _ => new ConcurrentDictionary<int, WeaponInfo>());
					var counterTerroristWeapons = playerWeapons.GetOrAdd(CsTeam.CounterTerrorist, _ => new ConcurrentDictionary<int, WeaponInfo>());

					// Add weaponInfo to both team weapon dictionaries
					terroristWeapons[weaponDefIndex] = weaponInfo;
					counterTerroristWeapons[weaponDefIndex] = weaponInfo;
				}
				else
				{
					// Add to the specific team
					var teamWeapons = playerWeapons.GetOrAdd(weaponTeam, _ => new ConcurrentDictionary<int, WeaponInfo>());
					teamWeapons[weaponDefIndex] = weaponInfo;
				}

				// weaponInfos[weaponDefIndex] = weaponInfo;
			}

			// WeaponPaints.GPlayerWeaponsInfo[player.Slot][weaponTeam] = weaponInfos;
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException("Unable to load weapon paint data.", ex);
		}
	}

	private static bool TryParseStickerFloat(string value, out float result)
	{
		if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
		    float.IsFinite(result))
			return true;

		// External web panels sometimes serialize decimals using the host locale.
		// The sticker format is semicolon-delimited, so a comma can safely be normalized.
		return float.TryParse(
			value.Replace(',', '.'),
			NumberStyles.Float,
			CultureInfo.InvariantCulture,
			out result) && float.IsFinite(result);
	}

	private void GetMusicFromDatabase(PlayerInfo? player, MySqlConnection connection)
	{
		try
		{
			if (!_config.Additional.MusicEnabled || string.IsNullOrEmpty(player?.SteamId))
				return;

			const string query = "SELECT `music_id`, `weapon_team` FROM `wp_player_music` WHERE `steamid` = @steamid ORDER BY `weapon_team` ASC";
			var rows = connection.Query<dynamic>(query, new { steamid = player.SteamId }); // Retrieve all records for the player

			foreach (var row in rows)
			{
				// Check if music_id is null
				if (row.music_id == null) continue;

				// Determine the weapon team based on the query result
				CsTeam weaponTeam = (int)row.weapon_team switch
				{
					2 => CsTeam.Terrorist,
					3 => CsTeam.CounterTerrorist,
					_ => CsTeam.None,
				};

				// Get or create entries for the player’s slot
				var playerMusic = WeaponPaints.GPlayersMusic.GetOrAdd(player.Slot, _ => new ConcurrentDictionary<CsTeam, ushort>());

				if (weaponTeam == CsTeam.None)
				{
					// Assign music ID to both teams if weaponTeam is None
					playerMusic[CsTeam.Terrorist] = (ushort)row.music_id;
					playerMusic[CsTeam.CounterTerrorist] = (ushort)row.music_id;
				}
				else
				{
					// Assign music ID to the specific team
					playerMusic[weaponTeam] = (ushort)row.music_id;
				}
			}
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException("Unable to load music data.", ex);
		}
	}

	private void GetPinsFromDatabase(PlayerInfo? player, MySqlConnection connection)
	{
		try
		{
			if (!_config.Additional.PinsEnabled || string.IsNullOrEmpty(player?.SteamId))
				return;

			const string query = "SELECT `id`, `weapon_team` FROM `wp_player_pins` WHERE `steamid` = @steamid ORDER BY `weapon_team` ASC";
			var rows = connection.Query<dynamic>(query, new { steamid = player.SteamId }); // Retrieve all records for the player

			foreach (var row in rows)
			{
				// Check if id is null
				if (row.id == null) continue;

				// Determine the weapon team based on the query result
				CsTeam weaponTeam = (int)row.weapon_team switch
				{
					2 => CsTeam.Terrorist,
					3 => CsTeam.CounterTerrorist,
					_ => CsTeam.None,
				};

				// Get or create entries for the player’s slot
				var playerPins = WeaponPaints.GPlayersPin.GetOrAdd(player.Slot, _ => new ConcurrentDictionary<CsTeam, ushort>());

				if (weaponTeam == CsTeam.None)
				{
					// Assign pin ID to both teams if weaponTeam is None
					playerPins[CsTeam.Terrorist] = (ushort)row.id;
					playerPins[CsTeam.CounterTerrorist] = (ushort)row.id;
				}
				else
				{
					// Assign pin ID to the specific team
					playerPins[weaponTeam] = (ushort)row.id;
				}
			}
		}
		catch (Exception ex)
		{
			throw new InvalidOperationException("Unable to load pin data.", ex);
		}
	}

	internal async Task SyncKnifeToDatabase(PlayerInfo player, string knife, CsTeam[] teams)
	{
		if (!_config.Additional.KnifeEnabled || string.IsNullOrEmpty(player.SteamId) || string.IsNullOrEmpty(knife) || teams.Length == 0) return;

		const string query = "INSERT INTO `wp_player_knife` (`steamid`, `weapon_team`, `knife`) VALUES(@steamid, @team, @newKnife) ON DUPLICATE KEY UPDATE `knife` = @newKnife";

		try
		{
			await using var connection = await _database.GetConnectionAsync();
        
			// Loop through each team and insert/update accordingly
			foreach (var team in teams)
			{
				await connection.ExecuteAsync(query, new { steamid = player.SteamId, team, newKnife = knife });
			}
		}
		catch (Exception e)
		{
			Utility.Log($"Error syncing knife to database: {e.Message}");
		}
	}
	
	internal async Task SyncGloveToDatabase(PlayerInfo player, ushort gloveDefIndex, CsTeam[] teams)
	{
		// Check if the necessary conditions are met
		if (!_config.Additional.GloveEnabled || string.IsNullOrEmpty(player.SteamId) || teams.Length == 0) 
			return;

		const string query = @"
        INSERT INTO `wp_player_gloves` (`steamid`, `weapon_team`, `weapon_defindex`) 
        VALUES(@steamid, @team, @gloveDefIndex) 
        ON DUPLICATE KEY UPDATE `weapon_defindex` = @gloveDefIndex";

		try
		{
			// Get a database connection
			await using var connection = await _database.GetConnectionAsync();
        
			// Loop through each team and insert/update accordingly
			foreach (var team in teams)
			{
				// Execute the SQL command for each team
				await connection.ExecuteAsync(query, new { 
					steamid = player.SteamId, 
					team = (int)team, // Cast the CsTeam enum to int for insertion
					gloveDefIndex 
				});
			}
		}
		catch (Exception e)
		{
			// Log any exceptions that occur
			Utility.Log($"Error syncing glove to database: {e.Message}");
		}
	}

	internal async Task SyncAgentToDatabase(PlayerInfo player)
	{
		if (!_config.Additional.AgentEnabled || string.IsNullOrEmpty(player.SteamId)) return;

		const string query = """
		                     					INSERT INTO `wp_player_agents` (`steamid`, `agent_ct`, `agent_t`)
		                     					VALUES(@steamid, @agent_ct, @agent_t)
		                     					ON DUPLICATE KEY UPDATE
		                     						`agent_ct` = @agent_ct,
		                     						`agent_t` = @agent_t
		                     """;
		try
		{
			await using var connection = await _database.GetConnectionAsync();

			await connection.ExecuteAsync(query, new { steamid = player.SteamId, agent_ct = WeaponPaints.GPlayersAgent[player.Slot].CT, agent_t = WeaponPaints.GPlayersAgent[player.Slot].T });
		}
		catch (Exception e)
		{
			Utility.Log($"Error syncing agents to database: {e.Message}");
		}
	}

	internal async Task SyncWeaponPaintsToDatabase(PlayerInfo player)
	{
		if (string.IsNullOrEmpty(player.SteamId) || !WeaponPaints.GPlayerWeaponsInfo.TryGetValue(player.Slot, out var teamWeaponInfos))
			return;

		try
		{
			await using var connection = await _database.GetConnectionAsync();

			// Loop through each team (Terrorist and CounterTerrorist)
			foreach (var (teamId, weaponsInfo) in teamWeaponInfos)
			{
				foreach (var (weaponDefIndex, weaponInfo) in weaponsInfo)
				{
					var paintId = weaponInfo.Paint;
					var wear = weaponInfo.Wear;
					var seed = weaponInfo.Seed;

					// Prepare the queries to check and update/insert weapon skin data
					const string queryCheckExistence = "SELECT COUNT(*) FROM `wp_player_skins` WHERE `steamid` = @steamid AND `weapon_defindex` = @weaponDefIndex AND `weapon_team` = @weaponTeam";
		                
					var existingRecordCount = await connection.ExecuteScalarAsync<int>(
						queryCheckExistence, 
						new { steamid = player.SteamId, weaponDefIndex, weaponTeam = teamId }
					);

					string query;
					object parameters;

					if (existingRecordCount > 0)
					{
						// Update existing record
						query = "UPDATE `wp_player_skins` SET `weapon_paint_id` = @paintId, `weapon_wear` = @wear, `weapon_seed` = @seed " +
						        "WHERE `steamid` = @steamid AND `weapon_defindex` = @weaponDefIndex AND `weapon_team` = @weaponTeam";
						parameters = new { steamid = player.SteamId, weaponDefIndex, weaponTeam = (int)teamId, paintId, wear, seed };
					}
					else
					{
						// Insert new record
						query = "INSERT INTO `wp_player_skins` (`steamid`, `weapon_defindex`, `weapon_team`, `weapon_paint_id`, `weapon_wear`, `weapon_seed`) " +
						        "VALUES (@steamid, @weaponDefIndex, @weaponTeam, @paintId, @wear, @seed)";
						parameters = new { steamid = player.SteamId, weaponDefIndex, weaponTeam = (int)teamId, paintId, wear, seed };
					}

					await connection.ExecuteAsync(query, parameters);
				}
			}
		}
		catch (Exception e)
		{
			Utility.Log($"Error syncing weapon paints to database: {e.Message}");
		}
	}

	internal async Task SyncMusicToDatabase(PlayerInfo player, ushort music, CsTeam[] teams)
	{
		if (!_config.Additional.MusicEnabled || string.IsNullOrEmpty(player.SteamId)) return;

		const string query = "INSERT INTO `wp_player_music` (`steamid`, `weapon_team`, `music_id`) VALUES(@steamid, @team, @newMusic) ON DUPLICATE KEY UPDATE `music_id` = @newMusic";

		try
		{
			await using var connection = await _database.GetConnectionAsync();
        
			// Loop through each team and insert/update accordingly
			foreach (var team in teams)
			{
				await connection.ExecuteAsync(query, new { steamid = player.SteamId, team, newMusic = music });
			}
		}
		catch (Exception e)
		{
			Utility.Log($"Error syncing music kit to database: {e.Message}");
		}
	}
		
	internal async Task SyncPinToDatabase(PlayerInfo player, ushort pin, CsTeam[] teams)
	{
		if (!_config.Additional.PinsEnabled || string.IsNullOrEmpty(player.SteamId)) return;

		const string query = "INSERT INTO `wp_player_pins` (`steamid`, `weapon_team`, `id`) VALUES(@steamid, @team, @newPin) ON DUPLICATE KEY UPDATE `id` = @newPin";

		try
		{
			await using var connection = await _database.GetConnectionAsync();
        
			// Loop through each team and insert/update accordingly
			foreach (var team in teams)
			{
				await connection.ExecuteAsync(query, new { steamid = player.SteamId, team, newPin = pin });
			}
		}
		catch (Exception e)
		{
			Utility.Log($"Error syncing pin to database: {e.Message}");
		}
	}

	internal async Task SyncStatTrakToDatabase(PlayerInfo player)
	{
	    if (WeaponPaints.WeaponSync == null || WeaponPaints.GPlayerWeaponsInfo.IsEmpty) return;
	    if (string.IsNullOrEmpty(player.SteamId))
	        return;

	    try
	    {
	        await using var connection = await _database.GetConnectionAsync();
	        await using var transaction = await connection.BeginTransactionAsync();

	        // Check if player's slot exists in GPlayerWeaponsInfo
	        if (!WeaponPaints.GPlayerWeaponsInfo.TryGetValue(player.Slot, out var teamWeaponsInfo))
	            return;
	        
	        // Iterate through each team in the player's weapon info
	        foreach (var teamInfo in teamWeaponsInfo)
	        {
	            // Retrieve weaponInfos for the current team
	            var weaponInfos = teamInfo.Value;

	            // Get StatTrak weapons for the current team
	            var statTrakWeapons = weaponInfos
		            .ToDictionary(
			            w => w.Key, 
			            w => (w.Value.StatTrak, w.Value.StatTrakCount) // Store both StatTrak and StatTrakCount in a tuple
		            );

	            // Check if there are StatTrak weapons to sync
	            if (statTrakWeapons.Count == 0) continue;
	            
	            // Get the current team ID
	            int weaponTeam = (int)teamInfo.Key;

	            // Sync StatTrak values for the current team
	            foreach (var (defindex, (statTrak, statTrakCount)) in statTrakWeapons)
	            {
		            const string query = @"
					    UPDATE `wp_player_skins` 
					    SET `weapon_stattrak` = @StatTrak, 
					        `weapon_stattrak_count` = @StatTrakCount
					    WHERE `steamid` = @steamid 
					      AND `weapon_defindex` = @weaponDefIndex
					      AND `weapon_team` = @weaponTeam";

	                var parameters = new
	                {
	                    steamid = player.SteamId,
	                    weaponDefIndex = defindex,
	                    StatTrak = statTrak,
	                    StatTrakCount = statTrakCount,
	                    weaponTeam
	                };

	                await connection.ExecuteAsync(query, parameters, transaction);
	            }
	        }

	        await transaction.CommitAsync();
	    }
	    catch (Exception e)
	    {
	        Utility.Log($"Error syncing stattrak to database: {e.Message}");
	    }
	}
}
