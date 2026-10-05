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
						// Server updates pool and relays to all clients
						SoulboundPool.UpdatePoolFromServer(poolLife, poolMaxLife);

						ModPacket packet = GetPacket();
						packet.Write((byte)SoulboundMessageType.SyncHealthPool);
						packet.Write(fromPlayer);
						packet.Write(poolLife);
						packet.Write(poolMaxLife);
						packet.Send(-1, fromPlayer);
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
						// Send current pool to requesting client
						ModPacket packet = GetPacket();
						packet.Write((byte)SoulboundMessageType.SyncHealthPool);
						packet.Write((byte)255);
						packet.Write(SoulboundPool.SharedCurrentLife);
						packet.Write(SoulboundPool.SharedMaxLife);
						packet.Send(whoAmI);
					}
					break;
				}
			}
		}
	}
}
