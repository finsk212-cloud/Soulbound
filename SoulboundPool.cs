using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Soulbound
{
	public static class SoulboundPool
	{
		public static int SharedCurrentLife = 200;
		public static int SharedMaxLife = 200;
		public static bool Initialized = false;
		public static bool BothDead = false;
		public static bool IsKillingPartner = false;

		public static void Reset()
		{
			Initialized = false;
			BothDead = false;
			IsKillingPartner = false;
			SharedCurrentLife = 200;
			SharedMaxLife = 200;
		}

		public static int GetBaseMaxLife(Player player)
		{
			if (player == null || !player.active)
				return 100;

			return 100 + (player.ConsumedLifeCrystals * 20) + (player.ConsumedLifeFruit * 5);
		}

		public static int GetActivePlayerCount()
		{
			int count = 0;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				if (Main.player[i].active && !Main.player[i].ghost)
					count++;
			}
			return Math.Max(1, count);
		}

		public static int CalculateCombinedMaxLife()
		{
			int totalMax = 0;
			int activeCount = 0;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p.active && !p.ghost)
				{
					totalMax += GetBaseMaxLife(p);
					activeCount++;
				}
			}

			// When only 1 player is connected in testing or waiting for partner, prepare 200 HP (10 hearts)
			if (activeCount <= 1)
			{
				totalMax = Math.Max(totalMax * 2, 200);
			}

			return Math.Max(totalMax, 200);
		}

		public static void UpdatePoolFromServer(int currentLife, int maxLife)
		{
			SharedMaxLife = Math.Max(1, maxLife);
			SharedCurrentLife = Math.Clamp(currentLife, 0, SharedMaxLife);
			Initialized = true;

			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p.active)
				{
					p.statLife = SharedCurrentLife;
					p.statLifeMax2 = SharedMaxLife;
					if (p.TryGetModPlayer(out SoulboundPlayer sbp))
					{
						sbp.previousLife = SharedCurrentLife;
					}
				}
			}
		}

		public static void ReceiveSyncedPool(int currentLife, int maxLife)
		{
			SharedMaxLife = Math.Max(1, maxLife);
			SharedCurrentLife = Math.Clamp(currentLife, 0, SharedMaxLife);
			Initialized = true;

			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p.active)
				{
					p.statLife = SharedCurrentLife;
					p.statLifeMax2 = SharedMaxLife;
					if (p.TryGetModPlayer(out SoulboundPlayer sbp))
					{
						sbp.previousLife = SharedCurrentLife;
					}
				}
			}

			Player localPlayer = Main.LocalPlayer;
			if (localPlayer.active && SharedCurrentLife <= 0 && !localPlayer.dead && !IsKillingPartner)
			{
				// If local player has respawn invulnerability active, ignore fatal synchronization
				if (localPlayer.TryGetModPlayer(out SoulboundPlayer sbp) && sbp.respawnInvulnerability > 0)
					return;

				try
				{
					IsKillingPartner = true;
					BothDead = true;
					localPlayer.statLife = 0;
					localPlayer.KillMe(
						PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral(localPlayer.name + " shared their soulbound partner's fatal blow.")),
						1000,
						0
					);
				}
				finally
				{
					IsKillingPartner = false;
				}
			}
		}

		public static void ModifyPool(int newLife, int fromPlayer)
		{
			int maxLife = CalculateCombinedMaxLife();
			SharedMaxLife = maxLife;
			SharedCurrentLife = Math.Clamp(newLife, 0, SharedMaxLife);
			Initialized = true;

			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p.active)
				{
					p.statLife = SharedCurrentLife;
					p.statLifeMax2 = SharedMaxLife;
					if (p.TryGetModPlayer(out SoulboundPlayer sbp))
					{
						sbp.previousLife = SharedCurrentLife;
					}
				}
			}

			Player localPlayer = Main.LocalPlayer;
			// Only kill localPlayer if they aren't ALREADY the one dying and not in respawn invulnerability
			if (localPlayer.active && SharedCurrentLife <= 0 && !localPlayer.dead && !IsKillingPartner && localPlayer.whoAmI != fromPlayer)
			{
				if (localPlayer.TryGetModPlayer(out SoulboundPlayer sbp) && sbp.respawnInvulnerability > 0)
					return;

				try
				{
					IsKillingPartner = true;
					BothDead = true;
					localPlayer.statLife = 0;
					localPlayer.KillMe(
						PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral(localPlayer.name + " shared their soulbound partner's fatal blow.")),
						1000,
						0
					);
				}
				finally
				{
					IsKillingPartner = false;
				}
			}

			// Broadcast to network
			if (Main.netMode != NetmodeID.SinglePlayer && SoulboundMod.Instance != null)
			{
				ModPacket packet = SoulboundMod.Instance.GetPacket();
				packet.Write((byte)SoulboundMessageType.SyncHealthPool);
				packet.Write((byte)fromPlayer);
				packet.Write(SharedCurrentLife);
				packet.Write(SharedMaxLife);
				packet.Send();
			}
		}
	}
}
