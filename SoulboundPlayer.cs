using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace Soulbound
{
	public class SoulboundPlayer : ModPlayer
	{
		private bool isProcessingSharedDamage = false;
		private bool isProcessingSharedHeal = false;
		private int soundCooldown = 0;
		private int lastTrackedLife = 0;

		public override void Initialize()
		{
			isProcessingSharedDamage = false;
			isProcessingSharedHeal = false;
			soundCooldown = 0;
			lastTrackedLife = Player.statLife;
		}

		public override void OnEnterWorld()
		{
			lastTrackedLife = Player.statLife;
		}

		public bool IsLinkedWith(int otherWhoAmI)
		{
			// Prioritize 2 players for now: all active alive players in the session are soulbound together!
			if (otherWhoAmI < 0 || otherWhoAmI >= Main.maxPlayers)
				return false;

			Player other = Main.player[otherWhoAmI];
			return other.active && !other.dead;
		}

		public Player GetPartner()
		{
			// Find the primary partner
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
			if (soundCooldown > 0)
				soundCooldown--;

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
				float excess = dist - maxDist;

				// Spring-tether constraint:
				// Pull the player smoothly and firmly back toward partner
				float pullStrength = MathHelper.Clamp(excess * 0.08f, 1.5f, 25f);
				Player.velocity += dir * pullStrength;

				// If they are heading further away, dampen the outward velocity component
				float velDot = Vector2.Dot(Player.velocity, -dir);
				if (velDot > 0f)
				{
					Player.velocity -= (-dir) * velDot * 0.75f;
				}

				// Hard clamp: if distance is egregiously beyond 1.5x max distance (e.g. teleport/magic mirror), pull player directly to edge
				if (excess > maxDist * 0.6f)
				{
					Player.position = partner.Center - dir * maxDist - new Vector2(Player.width / 2f, Player.height / 2f);
					Player.velocity = Vector2.Zero;
				}

				// Sound & particle feedback
				if (soundCooldown <= 0 && SoulboundConfig.Instance.PlayPullSound)
				{
					SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = -0.2f }, Player.Center);
					soundCooldown = 25;
				}

				// Spawn tether tension dust particles
				if (Main.rand.NextBool(2))
				{
					Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.MagicMirror, dir.X * 4f, dir.Y * 4f, 150, Color.Cyan, 1.2f);
					dust.noGravity = true;
				}
			}
		}

		public override void PostUpdate()
		{
			// Track natural/potion healing
			if (!Player.dead && Player.active)
			{
				if (Player.statLife > lastTrackedLife && !isProcessingSharedHeal)
				{
					int healedAmount = Player.statLife - lastTrackedLife;
					if (healedAmount > 0 && SoulboundConfig.Instance != null && SoulboundConfig.Instance.SharedHealthEnabled)
					{
						SyncHealToPartners(healedAmount);
					}
				}
				lastTrackedLife = Player.statLife;
			}
		}

		public override void OnHurt(Player.HurtInfo info)
		{
			if (isProcessingSharedDamage)
				return;

			if (SoulboundConfig.Instance == null || !SoulboundConfig.Instance.SharedHealthEnabled)
				return;

			int damage = (int)(info.Damage * SoulboundConfig.Instance.DamageShareRatio);
			if (damage <= 0)
				return;

			SyncDamageToPartners(damage);
		}

		private void SyncDamageToPartners(int damage)
		{
			if (Main.netMode == NetmodeID.SinglePlayer)
				return;

			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)SoulboundMessageType.SyncDamageToPartner);
			packet.Write((byte)Player.whoAmI);
			packet.Write(damage);
			packet.Send();
		}

		private void SyncHealToPartners(int healAmount)
		{
			if (Main.netMode == NetmodeID.SinglePlayer)
				return;

			ModPacket packet = Mod.GetPacket();
			packet.Write((byte)SoulboundMessageType.SyncHealToPartner);
			packet.Write((byte)Player.whoAmI);
			packet.Write(healAmount);
			packet.Send();
		}

		public void ApplySharedDamageDirectly(int damage)
		{
			if (Player.dead || !Player.active)
				return;

			isProcessingSharedDamage = true;
			try
			{
				Player.statLife -= damage;

				// Combat text indicator (vibrant soulbound purple-pink)
				CombatText.NewText(new Rectangle((int)Player.position.X, (int)Player.position.Y, Player.width, Player.height), new Color(220, 80, 220), damage);

				// Visual feedback dust
				for (int i = 0; i < 8; i++)
				{
					Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.Shadowflame, 0f, 0f, 100, default, 1.2f);
					dust.velocity *= 1.5f;
					dust.noGravity = true;
				}

				if (Player.statLife <= 0)
				{
					Player.statLife = 0;
					Player.KillMe(PlayerDeathReason.ByCustomReason(Terraria.Localization.NetworkText.FromLiteral(Player.name + " shared their soulbound partner's fatal wound.")), damage, 0);
				}
			}
			finally
			{
				isProcessingSharedDamage = false;
				lastTrackedLife = Player.statLife;
			}
		}

		public void ApplySharedHealDirectly(int healAmount)
		{
			if (Player.dead || !Player.active)
				return;

			isProcessingSharedHeal = true;
			try
			{
				Player.statLife = Math.Min(Player.statLifeMax2, Player.statLife + healAmount);
				CombatText.NewText(new Rectangle((int)Player.position.X, (int)Player.position.Y, Player.width, Player.height), new Color(80, 240, 160), healAmount);
			}
			finally
			{
				isProcessingSharedHeal = false;
				lastTrackedLife = Player.statLife;
			}
		}
	}
}
