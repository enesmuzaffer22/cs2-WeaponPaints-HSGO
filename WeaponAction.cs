using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace WeaponPaints
{
	public partial class WeaponPaints
	{
		private void GivePlayerWeaponSkin(CCSPlayerController player, CBasePlayerWeapon weapon)
		{
			if (!Config.Additional.SkinEnabled && !Config.Additional.KnifeEnabled) return;

			var designerName = weapon.DesignerName ?? "";
			bool isKnife = designerName.Contains("knife") || designerName.Contains("bayonet");
			if (!isKnife && !Config.Additional.SkinEnabled) return;
			
			switch (isKnife)
			{
				case true when !HasChangedKnife(player, out var _):
					return;
				
				case true:
				{
					var newDefIndex = WeaponDefindex.FirstOrDefault(x => x.Value == GPlayersKnife[player.Slot][player.Team]);
					if (newDefIndex.Key == 0) return;

					if (weapon.AttributeManager.Item.ItemDefinitionIndex != newDefIndex.Key)
					{
						SubclassChange(weapon, (ushort)newDefIndex.Key);
					}

					weapon.AttributeManager.Item.ItemDefinitionIndex = (ushort)newDefIndex.Key;
					weapon.AttributeManager.Item.EntityQuality = 3;
					
					weapon.AttributeManager.Item.AttributeList.Attributes.RemoveAll();
					weapon.AttributeManager.Item.NetworkedDynamicAttributes.Attributes.RemoveAll();
					break;
				}
				default:
					weapon.AttributeManager.Item.EntityQuality = 0;
					break;
			}

			if (!GPlayerWeaponsInfo.TryGetValue(player.Slot, out _)) return;

			UpdatePlayerEconItemId(weapon.AttributeManager.Item);

			int weaponDefIndex = weapon.AttributeManager.Item.ItemDefinitionIndex;
			int fallbackPaintKit;
			
			weapon.AttributeManager.Item.AccountID = (uint)player.SteamID;
			
			List<JObject> skinInfo;
			bool isLegacyModel;

			if (_config.Additional.GiveRandomSkin &&
			    !HasChangedPaint(player, weaponDefIndex, out _))
			{
				// Random skins
				var randomPaint = GetRandomPaint(weaponDefIndex);
				weapon.FallbackPaintKit = randomPaint;
				weapon.FallbackSeed = 0;
				weapon.FallbackWear = 0.01f;
			
				weapon.AttributeManager.Item.NetworkedDynamicAttributes.Attributes.RemoveAll();
				CAttributeListSetOrAddAttributeValueByName.Invoke(weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle, "set item texture prefab", randomPaint);
				CAttributeListSetOrAddAttributeValueByName.Invoke(weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle, "set item texture seed", 0);
				CAttributeListSetOrAddAttributeValueByName.Invoke(weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle, "set item texture wear", 0.01f);
			
				weapon.AttributeManager.Item.AttributeList.Attributes.RemoveAll();
				CAttributeListSetOrAddAttributeValueByName.Invoke(weapon.AttributeManager.Item.AttributeList.Handle, "set item texture prefab", randomPaint);
				CAttributeListSetOrAddAttributeValueByName.Invoke(weapon.AttributeManager.Item.AttributeList.Handle, "set item texture seed", 0);
				CAttributeListSetOrAddAttributeValueByName.Invoke(weapon.AttributeManager.Item.AttributeList.Handle, "set item texture wear", 0.01f);
			
				fallbackPaintKit = weapon.FallbackPaintKit;
			
				if (fallbackPaintKit == 0)
					return;
			
				skinInfo = SkinsList
					.Where(w => 
						w["weapon_defindex"]?.ToObject<int>() == weaponDefIndex && 
						w["paint"]?.ToObject<int>() == fallbackPaintKit)
					.ToList();
				
				isLegacyModel = skinInfo.Count <= 0 || skinInfo[0].Value<bool>("legacy_model");
				UpdatePlayerWeaponMeshGroupMask(player, weapon, isLegacyModel);
				return;
			}

			if (!HasChangedPaint(player, weaponDefIndex, out var weaponInfo) || weaponInfo == null)
				return;

			//Log($"Apply on {weapon.DesignerName}({weapon.AttributeManager.Item.ItemDefinitionIndex}) paint {gPlayerWeaponPaints[steamId.SteamId64][weapon.AttributeManager.Item.ItemDefinitionIndex]} seed {gPlayerWeaponSeed[steamId.SteamId64][weapon.AttributeManager.Item.ItemDefinitionIndex]} wear {gPlayerWeaponWear[steamId.SteamId64][weapon.AttributeManager.Item.ItemDefinitionIndex]}");

			weapon.AttributeManager.Item.AttributeList.Attributes.RemoveAll();
			weapon.AttributeManager.Item.NetworkedDynamicAttributes.Attributes.RemoveAll();
			
			UpdatePlayerEconItemId(weapon.AttributeManager.Item);

			weapon.AttributeManager.Item.CustomName = weaponInfo.Nametag;
			weapon.FallbackPaintKit = weaponInfo.Paint;
			
			weapon.FallbackSeed = weaponInfo is { Paint: 38, Seed: 0 } ? _fadeSeed++ : weaponInfo.Seed;
			
			weapon.FallbackWear = GetEffectiveWeaponWear(player, weaponDefIndex, weaponInfo.Wear);
			CAttributeListSetOrAddAttributeValueByName.Invoke(weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle, "set item texture prefab", weapon.FallbackPaintKit);
			CAttributeListSetOrAddAttributeValueByName.Invoke(weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle, "set item texture seed", weapon.FallbackSeed);
			CAttributeListSetOrAddAttributeValueByName.Invoke(weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle, "set item texture wear", weapon.FallbackWear);
			CAttributeListSetOrAddAttributeValueByName.Invoke(weapon.AttributeManager.Item.AttributeList.Handle, "set item texture prefab", weapon.FallbackPaintKit);
			CAttributeListSetOrAddAttributeValueByName.Invoke(weapon.AttributeManager.Item.AttributeList.Handle, "set item texture seed", weapon.FallbackSeed);
			CAttributeListSetOrAddAttributeValueByName.Invoke(weapon.AttributeManager.Item.AttributeList.Handle, "set item texture wear", weapon.FallbackWear);

			if (weaponInfo.StatTrak)
			{			
				weapon.AttributeManager.Item.EntityQuality = 9;

				CAttributeListSetOrAddAttributeValueByName.Invoke(weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle, "kill eater", ViewAsFloat((uint)weaponInfo.StatTrakCount));
				CAttributeListSetOrAddAttributeValueByName.Invoke(weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle, "kill eater score type", 0);
				
				CAttributeListSetOrAddAttributeValueByName.Invoke(weapon.AttributeManager.Item.AttributeList.Handle, "kill eater", ViewAsFloat((uint)weaponInfo.StatTrakCount));
				CAttributeListSetOrAddAttributeValueByName.Invoke(weapon.AttributeManager.Item.AttributeList.Handle, "kill eater score type", 0);
			}

			fallbackPaintKit = weapon.FallbackPaintKit;

			if (fallbackPaintKit == 0)
				return;

			if (weaponInfo.KeyChain is { Id: > 0 }) SetKeychain(player, weapon);
			SetStickers(player, weapon);

			skinInfo = SkinsList
				.Where(w => 
					w["weapon_defindex"]?.ToObject<int>() == weaponDefIndex && 
					w["paint"]?.ToObject<int>() == fallbackPaintKit)
				.ToList();
				
			isLegacyModel = skinInfo.Count <= 0 || skinInfo[0].Value<bool>("legacy_model");
			UpdatePlayerWeaponMeshGroupMask(player, weapon, isLegacyModel);
		}
		
		private void SetStickers(CCSPlayerController? player, CBasePlayerWeapon weapon)
		{
			if (player == null || !player.IsValid) return;

			int weaponDefIndex = weapon.AttributeManager.Item.ItemDefinitionIndex;

			if (!HasChangedPaint(player ,weaponDefIndex, out var weaponInfo) || weaponInfo == null)
				return;

			var attributeHandles = new[]
			{
				weapon.AttributeManager.Item.AttributeList.Handle,
				weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle
			};
			foreach (var sticker in weaponInfo.Stickers.Where(sticker => sticker.Slot is >= 0 and < 5))
			{
				int stickerSlot = sticker.Slot;
				foreach (var attributesHandle in attributeHandles)
				{
					SetStickerAttribute(attributesHandle, stickerSlot, "id", ViewAsFloat(sticker.Id));
					if (sticker.Schema > 0 || sticker.OffsetX != 0 || sticker.OffsetY != 0)
					{
						var schema = sticker.OffsetX != 0 || sticker.OffsetY != 0 ? 0 : sticker.Schema;
						SetStickerAttribute(attributesHandle, stickerSlot, "schema", schema);
					}
					SetStickerAttribute(attributesHandle, stickerSlot, "offset x", sticker.OffsetX);
					SetStickerAttribute(attributesHandle, stickerSlot, "offset y", sticker.OffsetY);
					SetStickerAttribute(attributesHandle, stickerSlot, "wear", sticker.Wear);
					SetStickerAttribute(attributesHandle, stickerSlot, "scale", sticker.Scale > 0 ? sticker.Scale : 1.0f);
					SetStickerAttribute(attributesHandle, stickerSlot, "rotation", sticker.Rotation);
				}
			}
		}

		private static void SetStickerAttribute(nint attributesHandle, int slot, string name, float value)
		{
			CAttributeListSetOrAddAttributeValueByName.Invoke(
				attributesHandle,
				$"sticker slot {slot} {name}",
				value);
		}

		private void SetKeychain(CCSPlayerController? player, CBasePlayerWeapon weapon)
		{
			if (player == null || !player.IsValid) return;

			int weaponDefIndex = weapon.AttributeManager.Item.ItemDefinitionIndex;

			if (!HasChangedPaint(player, weaponDefIndex, out var value) || value?.KeyChain == null)
				return;
			
			var keyChain = value.KeyChain;

			foreach (var attributesHandle in new[]
			         {
				         weapon.AttributeManager.Item.AttributeList.Handle,
				         weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle
			         })
			{
				CAttributeListSetOrAddAttributeValueByName.Invoke(attributesHandle, "keychain slot 0 id", ViewAsFloat(keyChain.Id));
				CAttributeListSetOrAddAttributeValueByName.Invoke(attributesHandle, "keychain slot 0 offset x", keyChain.OffsetX);
				CAttributeListSetOrAddAttributeValueByName.Invoke(attributesHandle, "keychain slot 0 offset y", keyChain.OffsetY);
				CAttributeListSetOrAddAttributeValueByName.Invoke(attributesHandle, "keychain slot 0 offset z", keyChain.OffsetZ);
				CAttributeListSetOrAddAttributeValueByName.Invoke(attributesHandle, "keychain slot 0 seed", ViewAsFloat(keyChain.Seed));
			}
		}

		private static void GiveKnifeToPlayer(CCSPlayerController? player)
		{
			if (!_config.Additional.KnifeEnabled || player == null || !player.IsValid) return;

			if (PlayerHasKnife(player)) return;

			//string knifeToGive = (CsTeam)player.TeamNum == CsTeam.Terrorist ? "weapon_knife_t" : "weapon_knife";
			player.GiveNamedItem(CsItem.Knife);
			Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInventoryServices");
		}

		private static bool PlayerHasKnife(CCSPlayerController? player)
		{
			if (!_config.Additional.KnifeEnabled) return false;

			if (player == null || !player.IsValid || !player.PlayerPawn.IsValid)
			{
				return false;
			}

			if (player.PlayerPawn.Value == null || player.PlayerPawn.Value.WeaponServices == null || player.PlayerPawn.Value.ItemServices == null)
				return false;

			var weapons = player.PlayerPawn.Value.WeaponServices?.MyWeapons;
			if (weapons == null) return false;
			foreach (var weapon in weapons)
			{
				if (!weapon.IsValid || weapon.Value == null || !weapon.Value.IsValid) continue;
				if (weapon.Value.DesignerName.Contains("knife") || weapon.Value.DesignerName.Contains("bayonet"))
				{
					return true;
				}
			}
			return false;
		}

		private sealed record HeldWeaponRefresh(
			CBasePlayerWeapon OldWeapon,
			string DesignerName,
			int Clip,
			int Reserve);

		private bool RefreshWeapons(
			CCSPlayerController? player,
			Func<bool> canContinue,
			Action<bool> completed)
		{
			if (!_gBCommandsAllowed || player == null || !player.IsValid || !player.PawnIsAlive ||
			    player.PlayerPawn.Value?.WeaponServices == null ||
			    player.Team is CsTeam.None or CsTeam.Spectator)
				return false;

			var weapons = player.PlayerPawn.Value.WeaponServices.MyWeapons;
			if (weapons.Count == 0) return false;

			var foundRefreshableWeapon = false;
			var weaponsToRecreate = new List<HeldWeaponRefresh>();
			var activeSlotCommand = GetActiveWeaponSlotCommand(player);

			foreach (var handle in weapons)
			{
				var weapon = handle.Value;
				if (weapon is not { IsValid: true } || weapon.DesignerName?.Contains("weapon_") != true)
					continue;

				try
				{
					var weaponData = weapon.As<CCSWeaponBase>().VData;
					if (weaponData == null || weaponData.GearSlot is not (
						gear_slot_t.GEAR_SLOT_RIFLE or
						gear_slot_t.GEAR_SLOT_PISTOL or
						gear_slot_t.GEAR_SLOT_KNIFE))
						continue;

					foundRefreshableWeapon = true;
					var defIndex = weapon.AttributeManager.Item.ItemDefinitionIndex;
					if (weaponData.GearSlot is gear_slot_t.GEAR_SLOT_RIFLE or gear_slot_t.GEAR_SLOT_PISTOL &&
					    HasChangedPaint(player, defIndex, out var weaponInfo) && weaponInfo != null)
					{
						var revision = AdvanceStickerWearRevision(player, defIndex);
						var effectiveWear = GetEffectiveWeaponWear(player, defIndex, weaponInfo.Wear);
						Logger.LogInformation(
							"!wp recreating {WeaponName} for {PlayerName}: stickers={StickerCount}, stickerWearRevision={Revision}, effectiveWear={EffectiveWear}",
							weapon.DesignerName,
							player.PlayerName,
							weaponInfo.Stickers.Count,
							revision,
							effectiveWear);
						weaponsToRecreate.Add(new HeldWeaponRefresh(
							weapon,
							weapon.DesignerName,
							weapon.Clip1,
							weapon.ReserveAmmo[0]));
						continue;
					}

					TryApplyWeaponSkin(player, weapon, "unchanged held weapon during !wp");
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex, "Failed to prepare a held weapon during !wp.");
				}
			}

			if (!foundRefreshableWeapon) return false;

			if (weaponsToRecreate.Count == 0)
			{
				Server.NextFrame(() => completed(canContinue()));
				return true;
			}

			foreach (var entry in weaponsToRecreate)
			{
				if (entry.OldWeapon.IsValid)
					entry.OldWeapon.AddEntityIOEvent("Kill", entry.OldWeapon, null, "", 0.01f);
			}

			RunAfterTicks(2, () => RestoreStickerWeapons(
				player,
				weaponsToRecreate,
				activeSlotCommand,
				canContinue,
				completed));
			return true;
		}

		private void RestoreStickerWeapons(
			CCSPlayerController player,
			IReadOnlyList<HeldWeaponRefresh> entries,
			string? activeSlotCommand,
			Func<bool> canContinue,
			Action<bool> completed,
			int waitAttempt = 0)
		{
			if (!canContinue() || !player.PawnIsAlive)
			{
				completed(false);
				return;
			}

			if (entries.Any(entry => entry.OldWeapon.IsValid))
			{
				if (waitAttempt >= 12)
				{
					Logger.LogWarning("Timed out waiting for old sticker weapon entities to be removed.");
					completed(false);
					return;
				}

				RunAfterTicks(1, () => RestoreStickerWeapons(
					player,
					entries,
					activeSlotCommand,
					canContinue,
					completed,
					waitAttempt + 1));
				return;
			}

			var restoredWeapons = new List<(CBasePlayerWeapon Weapon, HeldWeaponRefresh Entry)>();
			foreach (var entry in entries)
			{
				try
				{
					// Apply synchronously in this frame. On some servers the post-hook cannot resolve
					// the owner early enough, and even a one-frame delay leaves the client decal cache stale.
					var weapon = new CBasePlayerWeapon(player.GiveNamedItem(entry.DesignerName));
					if (!weapon.IsValid)
					{
						Logger.LogWarning("GiveNamedItem returned an invalid entity for {WeaponName}.", entry.DesignerName);
						completed(false);
						return;
					}

					if (!TryApplyWeaponSkin(player, weapon, "newly re-given weapon before first transmit"))
					{
						completed(false);
						return;
					}

					restoredWeapons.Add((weapon, entry));
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex, "Failed to restore {WeaponName} after sticker refresh.", entry.DesignerName);
					completed(false);
					return;
				}
			}

			Server.NextFrame(() =>
			{
				if (!canContinue())
				{
					completed(false);
					return;
				}

				foreach (var (weapon, entry) in restoredWeapons)
				{
					if (!weapon.IsValid)
					{
						completed(false);
						return;
					}

					weapon.Clip1 = entry.Clip;
					weapon.ReserveAmmo[0] = entry.Reserve;
				}

				player.ExecuteClientCommand("lastinv");
				RunAfterTicks(4, () =>
				{
					if (!canContinue() || !player.PawnIsAlive)
					{
						completed(false);
						return;
					}

					player.ExecuteClientCommand("lastinv");
					if (!string.IsNullOrEmpty(activeSlotCommand))
						player.ExecuteClientCommand(activeSlotCommand);
					ReApplyWeaponBodygroups(player);
					completed(true);
				});
			});
		}

		private static void RunAfterTicks(int ticks, Action action)
		{
			Server.RunOnTick(Server.TickCount + Math.Max(1, ticks), action);
		}

		private static void RunAfterSeconds(float seconds, Action action)
		{
			RunAfterTicks((int)Math.Ceiling(seconds / Server.TickInterval), action);
		}

		private static string? GetActiveWeaponSlotCommand(CCSPlayerController player)
		{
			var activeWeapon = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value;
			var gearSlot = activeWeapon?.As<CCSWeaponBase>().VData?.GearSlot;
			return gearSlot switch
			{
				gear_slot_t.GEAR_SLOT_RIFLE => "slot1",
				gear_slot_t.GEAR_SLOT_PISTOL => "slot2",
				gear_slot_t.GEAR_SLOT_KNIFE => "slot3",
				_ => null
			};
		}

		private bool ApplyWeaponSkinsInPlace(CCSPlayerController player)
		{
			var weapons = player.PlayerPawn.Value?.WeaponServices?.MyWeapons;
			if (weapons == null || !player.PawnIsAlive) return true;

			foreach (var handle in weapons)
			{
				var weapon = handle.Value;
				if (weapon is { IsValid: true })
					TryApplyWeaponSkin(player, weapon, "cached weapon apply");
			}

			return true;
		}

		private void ReApplyWeaponBodygroups(CCSPlayerController player)
		{
			var weapons = player.PlayerPawn.Value?.WeaponServices?.MyWeapons;
			if (weapons == null) return;

			foreach (var handle in weapons)
			{
				var weapon = handle.Value;
				if (weapon is not { IsValid: true } || weapon.FallbackPaintKit <= 0) continue;

				var defIndex = weapon.AttributeManager.Item.ItemDefinitionIndex;
				var skinInfo = SkinsList.FirstOrDefault(skin =>
					skin["weapon_defindex"]?.ToObject<int>() == defIndex &&
					skin["paint"]?.ToObject<int>() == weapon.FallbackPaintKit);
				UpdatePlayerWeaponMeshGroupMask(player, weapon, skinInfo == null || skinInfo.Value<bool>("legacy_model"));
			}
		}

		private bool TryApplyWeaponSkin(CCSPlayerController player, CBasePlayerWeapon weapon, string context)
		{
			try
			{
				GivePlayerWeaponSkin(player, weapon);
				AppliedWeaponEntities[weapon.Handle] = 0;
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "Failed to apply cosmetics to {Context}.", context);
				return false;
			}
		}

		private void GivePlayerGloves(CCSPlayerController player)
		{
			if (!Utility.IsPlayerValid(player) || (LifeState_t)player.LifeState != LifeState_t.LIFE_ALIVE) return;

			CCSPlayerPawn? pawn = player.PlayerPawn.Value;
			if (pawn == null || !pawn.IsValid)
				return;

			CEconItemView item = pawn.EconGloves;

			item.NetworkedDynamicAttributes.Attributes.RemoveAll();
			item.AttributeList.Attributes.RemoveAll();

			//force gloves model refresh to prevent model overlap
			player.ExecuteClientCommand("lastinv");
			RunAfterSeconds(0.08f, () =>
			{	
				try
				{
					if (!player.IsValid)
						return;

					if (!player.PawnIsAlive || !pawn.IsValid)
						return;

					if (!GPlayersGlove.TryGetValue(player.Slot, out var gloveInfo) ||
					    !gloveInfo.TryGetValue(player.Team, out var gloveId) ||
					    gloveId == 0 ||
					    !HasChangedPaint(player, gloveId, out var weaponInfo) || weaponInfo == null)
						return;

					item.ItemDefinitionIndex = gloveId;
					
					UpdatePlayerEconItemId(item);

					item.NetworkedDynamicAttributes.Attributes.RemoveAll();
					CAttributeListSetOrAddAttributeValueByName.Invoke(item.NetworkedDynamicAttributes.Handle, "set item texture prefab", weaponInfo.Paint);
					CAttributeListSetOrAddAttributeValueByName.Invoke(item.NetworkedDynamicAttributes.Handle, "set item texture seed", weaponInfo.Seed);
					var wear = ClampWear(weaponInfo.Wear);
					CAttributeListSetOrAddAttributeValueByName.Invoke(item.NetworkedDynamicAttributes.Handle, "set item texture wear", wear);

					item.AttributeList.Attributes.RemoveAll();
					CAttributeListSetOrAddAttributeValueByName.Invoke(item.AttributeList.Handle, "set item texture prefab", weaponInfo.Paint);
					CAttributeListSetOrAddAttributeValueByName.Invoke(item.AttributeList.Handle, "set item texture seed", weaponInfo.Seed);
					CAttributeListSetOrAddAttributeValueByName.Invoke(item.AttributeList.Handle, "set item texture wear", wear);

					item.Initialized = true;
				
					//force gloves model refresh to prevent model overlap
					player.ExecuteClientCommand("lastinv");
					SetBodygroup(pawn, "first_or_third_person", 0);
					RunAfterSeconds(0.2f, () =>
					{
						if (pawn.IsValid) SetBodygroup(pawn, "first_or_third_person", 1);
					});
				}
				catch (Exception) { }
			});
		}

		private static int GetRandomPaint(int defindex)
		{
			if (SkinsList.Count == 0)
				return 0;

			Random rnd = new Random();

			// Filter weapons by the provided defindex
			var filteredWeapons = SkinsList.Where(w => w["weapon_defindex"]?.ToString() == defindex.ToString()).ToList();

			if (filteredWeapons.Count == 0)
				return 0;

			var randomWeapon = filteredWeapons[rnd.Next(filteredWeapons.Count)];

			return int.TryParse(randomWeapon["paint"]?.ToString(), out var paintValue) ? paintValue : 0;
		}

		//xstage idea on css discord
		public static void SubclassChange(CBasePlayerWeapon weapon, ushort itemD)
		{
			weapon.AcceptInput("ChangeSubclass", value: itemD.ToString());
		}

		public static void SetBodygroup(CCSPlayerPawn pawn, string group, int value)
		{
			pawn.AcceptInput("SetBodygroup", value:$"{group},{value}");
		}

		private void UpdateWeaponMeshGroupMask(CBaseEntity weapon, bool isLegacy = false)
		{
				if (weapon.CBodyComponent?.SceneNode == null) return;
				//var skeleton = weapon.CBodyComponent.SceneNode.GetSkeletonInstance();
				// skeleton.ModelState.MeshGroupMask = isLegacy ? 2UL : 1UL;

				weapon.AcceptInput("SetBodygroup", value: $"body,{(isLegacy ? 1 : 0)}");
		}

		private void UpdatePlayerWeaponMeshGroupMask(CCSPlayerController player, CBasePlayerWeapon weapon, bool isLegacy)
		{
			UpdateWeaponMeshGroupMask(weapon, isLegacy);
		}

		private static float ClampWear(float wear)
		{
			return float.IsFinite(wear) && wear > 0.0f && wear <= 1.0f ? wear : 0.001f;
		}

		private static int AdvanceStickerWearRevision(CCSPlayerController player, int weaponDefIndex)
		{
			var key = (player.Slot, player.Team, weaponDefIndex);
			return StickerWearRevisions.AddOrUpdate(key, 1, (_, current) => current >= 10 ? 1 : current + 1);
		}

		private static float GetEffectiveWeaponWear(CCSPlayerController player, int weaponDefIndex, float configuredWear)
		{
			var baseWear = ClampWear(configuredWear);
			if (!StickerWearRevisions.TryGetValue((player.Slot, player.Team, weaponDefIndex), out var revision))
				return baseWear;

			var offset = revision * 0.006f;
			return baseWear <= 0.94f
				? Math.Min(baseWear + offset, 1.0f)
				: Math.Max(baseWear - offset, 0.001f);
		}

		private static void GivePlayerAgent(CCSPlayerController player)
		{
			if (!GPlayersAgent.TryGetValue(player.Slot, out var value)) return;

			var model = player.TeamNum == 3 ? value.CT : value.T;
			if (string.IsNullOrEmpty(model)) return;

			if (player.PlayerPawn.Value == null)
				return;

			try
			{
				Server.NextFrame(() =>
				{
					if (!player.IsValid) return;

					var pawn = player.PlayerPawn.Value;
					if (pawn == null || !pawn.IsValid) return;

					pawn.SetModel($"agents/models/{model}.vmdl");
				});
			}
			catch (Exception)
			{
			}
		}

		private static void GivePlayerMusicKit(CCSPlayerController player)
		{
			if (player.IsBot) return;
			if (!GPlayersMusic.TryGetValue(player.Slot, out var musicInfo) ||
			    !musicInfo.TryGetValue(player.Team, out var musicId) || musicId == 0) return;
			
			if (player.InventoryServices == null) return;

			player.MusicKitID = musicId;
			// player.MvpNoMusic = false;
			player.InventoryServices.MusicID = musicId;
			Utilities.SetStateChanged(player, "CCSPlayerController", "m_iMusicKitID");
			// Utilities.SetStateChanged(player, "CCSPlayerController", "m_bMvpNoMusic");
			Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInventoryServices");
			// player.MusicKitMVPs = musicId;
			// Utilities.SetStateChanged(player, "CCSPlayerController", "m_iMusicKitMVPs");
		}

		private static void GivePlayerPin(CCSPlayerController player)
		{
			if (!GPlayersPin.TryGetValue(player.Slot, out var pinInfo) ||
			    !pinInfo.TryGetValue(player.Team, out var pinId)) return;
			if (player.InventoryServices == null) return;
			
			player.InventoryServices.Rank[5] = pinId > 0 ? (MedalRank_t)pinId : MedalRank_t.MEDAL_RANK_NONE;
			Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInventoryServices");
		}
		
		private void GiveOnItemPickup(CCSPlayerController player)
		{
			var pawn = player.PlayerPawn.Value;
			if (pawn == null) return;
			
			var myWeapons = pawn.WeaponServices?.MyWeapons;
			if (myWeapons == null) return;
			
			foreach (var handle in myWeapons)
			{
				var weapon = handle.Value;
			
				if (weapon == null || !weapon.IsValid) continue;
				if (myWeapons.Count == 1)
				{
					var newWeapon = new CBasePlayerWeapon(player.GiveNamedItem(CsItem.USP));
					weapon.AddEntityIOEvent("Kill", weapon, null, "", 0.01f);
					player.GiveNamedItem(CsItem.Knife);
					player.ExecuteClientCommand("slot3");
					newWeapon.AddEntityIOEvent("Kill", newWeapon, null, "", 0.01f);
				}
					
				GivePlayerWeaponSkin(player, weapon);
			}
		}
		
		private void UpdatePlayerEconItemId(CEconItemView econItemView)
		{
			var itemId = _nextItemId++;
			
			econItemView.ItemID = itemId;
			econItemView.ItemIDLow = (uint)(itemId & 0xFFFFFFFFUL);
			econItemView.ItemIDHigh = (uint)(itemId >> 32);
		}

		private static CCSPlayerController? GetPlayerFromItemServices(CCSPlayer_ItemServices itemServices)
		{
			var pawn = itemServices.Pawn.Value;
			if (pawn == null || !pawn.IsValid || !pawn.Controller.IsValid || pawn.Controller.Value == null) return null;
			var player = new CCSPlayerController(pawn.Controller.Value.Handle);
			return !Utility.IsPlayerValid(player) ? null : player;
		}

		private static bool HasChangedKnife(CCSPlayerController player, out string? knifeValue)
		{
			knifeValue = null;

			// Check if player has knife info for their slot and team
			if (!GPlayersKnife.TryGetValue(player.Slot, out var knife) ||
			    !knife.TryGetValue(player.Team, out var value) ||
			    value == "weapon_knife") return false;
			knifeValue = value; // Assign the knife value to the out parameter
			return true;
		}
		
		private static bool HasChangedPaint(CCSPlayerController player, int weaponDefIndex, out WeaponInfo? weaponInfo)
		{
			weaponInfo = null;

			// Check if player has weapons info for their slot and team
			if (!GPlayerWeaponsInfo.TryGetValue(player.Slot, out var teamInfo) || 
			    !teamInfo.TryGetValue(player.Team, out var teamWeapons))
			{
				return false;
			}

			// Check if the specified weapon has a paint/skin change
			if (!teamWeapons.TryGetValue(weaponDefIndex, out var value) || value.Paint <= 0) return false;
			
			weaponInfo = value; // Assign the out variable when it exists
			return true;
		}

		private static float ViewAsFloat(uint value)
		{
			return BitConverter.Int32BitsToSingle((int)value);
		}
	}
}
