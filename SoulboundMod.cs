using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulbound
{
	public enum SoulboundMessageType : byte
	{
		SyncHealthPool = 1,
		RequestHealthSync = 2
	}

	public class SoulboundMod : Mod
	{
		public static SoulboundMod Instance => ModContent.GetInstance<SoulboundMod>();

		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			SoulboundMessageType msgType = (SoulboundMessageType)reader.ReadByte();

			switch (msgType)
			{
				case SoulboundMessageType.SyncHealthPool:
				{
					byte fromPlayer = reader.ReadByte();
					int poolLife = reader.ReadInt32();
					int poolMaxLife = reader.ReadInt32();

					if (Main.netMode == NetmodeID.Server)
					{
						// Server validates, stores pool, and relays to all clients (including fromPlayer to confirm)
						SoulboundPool.UpdatePoolFromServer(poolLife, poolMaxLife);

						ModPacket packet = GetPacket();
						packet.Write((byte)SoulboundMessageType.SyncHealthPool);
						packet.Write(fromPlayer);
						packet.Write(SoulboundPool.SharedCurrentLife);
						packet.Write(SoulboundPool.SharedMaxLife);
						packet.Send(-1, whoAmI);
					}
					else if (Main.netMode == NetmodeID.MultiplayerClient)
					{
						SoulboundPool.ReceiveSyncedPool(poolLife, poolMaxLife);
					}
					break;
				}

				case SoulboundMessageType.RequestHealthSync:
				{
					if (Main.netMode == NetmodeID.Server)
					{
						// Calculate current server-side pool and send to requesting client
						int currentLife = SoulboundPool.SharedCurrentLife;
						int maxLife = SoulboundPool.CalculateCombinedMaxLife();

						ModPacket packet = GetPacket();
						packet.Write((byte)SoulboundMessageType.SyncHealthPool);
						packet.Write((byte)255);
						packet.Write(currentLife);
						packet.Write(maxLife);
						packet.Send(whoAmI);
					}
					break;
				}
			}
		}
	}
}
