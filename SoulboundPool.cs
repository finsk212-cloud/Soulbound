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

		public static void Reset()
		{
			Initialized = false;
			BothDead = false;
			SharedCurrentLife = 200;
			SharedMaxLife = 200;
		}

		public static int CalculateCombinedMaxLife()
		{
			int totalMax = 0;
			int activeCount = 0;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p.active)
				{
					totalMax += p.statLifeMax;
					activeCount++;
				}
			}

			return activeCount > 0 ? totalMax : 200;
		}

		public static void UpdatePoolFromServer(int currentLife, int maxLife)
		{
			SharedMaxLife = Math.Max(1, maxLife);
			SharedCurrentLife = Math.Clamp(currentLife, 0, SharedMaxLife);
			Initialized = true;
		}

		public static void ReceiveSyncedPool(int currentLife, int maxLife)
		{
			SharedMaxLife = Math.Max(1, maxLife);
			SharedCurrentLife = Math.Clamp(currentLife, 0, SharedMaxLife);
			Initialized = true;

			Player localPlayer = Main.LocalPlayer;
			if (localPlayer.active)
			{
				localPlayer.statLife = SharedCurrentLife;
				localPlayer.statLifeMax2 = SharedMaxLife;

				// If the pool reached 0 and player is not dead yet, kill them
				if (SharedCurrentLife <= 0 && !localPlayer.dead)
				{
					BothDead = true;
					localPlayer.statLife = 0;
					localPlayer.KillMe(
						PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral(localPlayer.name + " shared their soulbound partner's fate.")),
						1000,
						0
					);
				}
			}
		}

		public static void ModifyPool(int newLife, int fromPlayer)
		{
			int maxLife = CalculateCombinedMaxLife();
			SharedMaxLife = maxLife;
			SharedCurrentLife = Math.Clamp(newLife, 0, SharedMaxLife);
			Initialized = true;

			Player localPlayer = Main.LocalPlayer;
			if (localPlayer.active)
			{
				localPlayer.statLife = SharedCurrentLife;
				localPlayer.statLifeMax2 = SharedMaxLife;

				if (SharedCurrentLife <= 0 && !localPlayer.dead)
				{
					BothDead = true;
					localPlayer.statLife = 0;
					localPlayer.KillMe(
						PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral(localPlayer.name + " shared their soulbound partner's fatal blow.")),
						1000,
						0
					);
				}
			}

			// Broadcast to network
			if (Main.netMode != NetmodeID.SinglePlayer)
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
