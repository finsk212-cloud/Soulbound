using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulbound
{
	public enum SoulboundMessageType : byte
	{
		SyncExactLife = 1
	}

	public class SoulboundMod : Mod
	{
		public static SoulboundMod Instance => ModContent.GetInstance<SoulboundMod>();

		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			SoulboundMessageType msgType = (SoulboundMessageType)reader.ReadByte();

			switch (msgType)
			{
				case SoulboundMessageType.SyncExactLife:
				{
					byte fromPlayer = reader.ReadByte();
					int newLife = reader.ReadInt32();

					if (Main.netMode == NetmodeID.Server)
					{
						// Forward packet to all other clients
						ModPacket packet = GetPacket();
						packet.Write((byte)SoulboundMessageType.SyncExactLife);
						packet.Write(fromPlayer);
						packet.Write(newLife);
						packet.Send(-1, fromPlayer);
					}
					else if (Main.netMode == NetmodeID.MultiplayerClient)
					{
						Player localPlayer = Main.LocalPlayer;
						if (localPlayer.active && !localPlayer.dead && localPlayer.whoAmI != fromPlayer)
						{
							var soulPlayer = localPlayer.GetModPlayer<SoulboundPlayer>();
							soulPlayer.ReceiveSyncedLife(newLife);
						}
					}
					break;
				}
			}
		}
	}
}
