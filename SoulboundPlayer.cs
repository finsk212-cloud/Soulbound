using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Soulbound
{
	public class SoulboundPlayer : ModPlayer
	{
		private bool isReceivingSync = false;
		private int lastLife = 0;

		public override void Initialize()
		{
			isReceivingSync = false;
			lastLife = Player.statLife;
		}

		public override void OnEnterWorld()
		{
			lastLife = Player.statLife;
		}

		public Player GetPartner()
		{
			for (int i = 0; i < Main.maxPlayers; i++)
			{
				if (i != Player.whoAmI && Main.player[i].active && !Main.player[i].dead)
				{
					return Main.player[i];
				}
			}
			return null;
		}

		public override void PreUpdateMovement()
		{
			if (SoulboundConfig.Instance == null || !SoulboundConfig.Instance.TetherEnabled)
				return;

			if (Player.dead || !Player.active)
				return;

			Player partner = GetPartner();
			if (partner == null)
				return;

			float maxDist = SoulboundConfig.Instance.MaxDistancePixels;
			Vector2 toPartner = partner.Center - Player.Center;
			float dist = toPartner.Length();

			if (dist > maxDist && dist > 0.001f)
			{
				Vector2 dir = toPartner / dist;

				// Clamp position strictly so player cannot separate past maxDist
				Player.position = partner.Center - dir * maxDist - new Vector2(Player.width / 2f, Player.height / 2f);

				// Cancel any velocity heading away from partner
				float outwardVel = Vector2.Dot(Player.velocity, -dir);
				if (outwardVel > 0f)
				{
					Player.velocity -= (-dir) * outwardVel;
				}
			}
		}

		public override void PostUpdate()
		{
			if (SoulboundConfig.Instance == null || !SoulboundConfig.Instance.SharedHealthEnabled)
				return;

			if (Player.dead || !Player.active)
				return;

			// If our health changed naturally (e.g. potion, regeneration, lava, falling, drowning), sync exact HP to partner
			if (!isReceivingSync && Player.statLife != lastLife)
			{
				BroadcastLifeToPartner(Player.statLife);
			}

			lastLife = Player.statLife;
		}

		public override void OnHurt(Player.HurtInfo info)
		{
			if (SoulboundConfig.Instance == null || !SoulboundConfig.Instance.SharedHealthEnabled)
				return;

			if (isReceivingSync)
				return;

			// Broadcast updated health after hurt
			BroadcastLifeToPartner(Player.statLife);
			lastLife = Player.statLife;
		}

		private void BroadcastLifeToPartner(int life)
		{
			if (Main.netMode == NetmodeID.SinglePlayer)
				return;

			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)SoulboundMessageType.SyncExactLife);
			packet.Write((byte)Player.whoAmI);
			packet.Write(life);
			packet.Send();
		}

		public void ReceiveSyncedLife(int syncedLife)
		{
			if (Player.dead || !Player.active)
				return;

			isReceivingSync = true;
			try
			{
				Player.statLife = Math.Clamp(syncedLife, 0, Player.statLifeMax2);
				lastLife = Player.statLife;

				if (Player.statLife <= 0)
				{
					Player.statLife = 0;
					Player.KillMe(
						PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral(Player.name + " shared their soulbound partner's fate.")),
						1000,
						0
					);
				}
			}
			finally
			{
				isReceivingSync = false;
			}
		}
	}
}
