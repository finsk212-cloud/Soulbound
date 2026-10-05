using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulbound
{
	public enum SoulboundMessageType : byte
	{
		SyncDamageToPartner = 1,
		SyncHealToPartner = 2
	}

	public class SoulboundMod : Mod
	{
		public static SoulboundMod Instance => ModContent.GetInstance<SoulboundMod>();

		public override void HandlePacket(BinaryReader reader, int whoAmI)
		{
			SoulboundMessageType msgType = (SoulboundMessageType)reader.ReadByte();

			switch (msgType)
			{
				case SoulboundMessageType.SyncDamageToPartner:
				{
					byte fromPlayer = reader.ReadByte();
					int damage = reader.ReadInt32();

					if (Main.netMode == NetmodeID.Server)
					{
						// Forward packet to all other clients
						ModPacket packet = GetPacket();
						packet.Write((byte)SoulboundMessageType.SyncDamageToPartner);
						packet.Write(fromPlayer);
						packet.Write(damage);
						packet.Send(-1, fromPlayer);
					}
					else if (Main.netMode == NetmodeID.MultiplayerClient)
					{
						// On clients, find local partner and apply shared damage
						Player localPlayer = Main.LocalPlayer;
						if (localPlayer.active && !localPlayer.dead && localPlayer.whoAmI != fromPlayer)
						{
							var soulPlayer = localPlayer.GetModPlayer<SoulboundPlayer>();
							if (soulPlayer.IsLinkedWith(fromPlayer))
							{
								soulPlayer.ApplySharedDamageDirectly(damage);
							}
						}
					}
					break;
				}

				case SoulboundMessageType.SyncHealToPartner:
				{
					byte fromPlayer = reader.ReadByte();
					int healAmount = reader.ReadInt32();

					if (Main.netMode == NetmodeID.Server)
					{
						// Forward packet to all other clients
						ModPacket packet = GetPacket();
						packet.Write((byte)SoulboundMessageType.SyncHealToPartner);
						packet.Write(fromPlayer);
						packet.Write(healAmount);
						packet.Send(-1, fromPlayer);
					}
					else if (Main.netMode == NetmodeID.MultiplayerClient)
					{
						Player localPlayer = Main.LocalPlayer;
						if (localPlayer.active && !localPlayer.dead && localPlayer.whoAmI != fromPlayer)
						{
							var soulPlayer = localPlayer.GetModPlayer<SoulboundPlayer>();
							if (soulPlayer.IsLinkedWith(fromPlayer))
							{
								soulPlayer.ApplySharedHealDirectly(healAmount);
							}
						}
					}
					break;
				}
			}
		}
	}
}
