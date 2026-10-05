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

		public static void Reset()
		{
			Initialized = false;
			SharedCurrentLife = 200;
			SharedMaxLife = 200;
		}

		public static int CalculateTotalMaxLife()
		{
			int totalMax = 0;
			int activeCount = 0;
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				Player p = Main.player[i];
				if (p.active && !p.dead)
				{
					totalMax += p.statLifeMax2;
					activeCount++;
				}
			}

			return activeCount > 0 ? totalMax : 200;
		}

		public static void UpdatePoolFromServer(int currentLife, int maxLife)
		{
			SharedCurrentLife = Math.Clamp(currentLife, 0, maxLife);
			SharedMaxLife = Math.Max(1, maxLife);
			Initialized = true;
		}

		public static void ReceiveSyncedPool(int currentLife, int maxLife)
		{
			SharedCurrentLife = Math.Clamp(currentLife, 0, maxLife);
			SharedMaxLife = Math.Max(1, maxLife);
			Initialized = true;

			Player localPlayer = Main.LocalPlayer;
			if (localPlayer.active && !localPlayer.dead)
			{
				localPlayer.statLife = SharedCurrentLife;
				localPlayer.statLifeMax2 = SharedMaxLife;

				if (SharedCurrentLife <= 0)
				{
					localPlayer.statLife = 0;
					localPlayer.KillMe(
						PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral(localPlayer.name + " shared their soulbound partner's fate.")),
						1000,
						0
					);
				}
			}
		}

		public static void ModifyPoolDelta(int delta, int fromPlayer)
		{
			if (delta == 0)
				return;

			int totalMax = CalculateTotalMaxLife();
			SharedMaxLife = totalMax;

			if (!Initialized)
			{
				SharedCurrentLife = totalMax;
				Initialized = true;
			}

			SharedCurrentLife = Math.Clamp(SharedCurrentLife + delta, 0, SharedMaxLife);

			// Apply to local player immediately
			if (Main.LocalPlayer.active && !Main.LocalPlayer.dead)
			{
				Main.LocalPlayer.statLife = SharedCurrentLife;
				Main.LocalPlayer.statLifeMax2 = SharedMaxLife;
			}

			// Broadcast across network
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
