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
		private int previousLife = 0;
		private bool justSpawned = true;
		private int respawnInvulnerability = 0;

		public override void Initialize()
		{
			justSpawned = true;
			respawnInvulnerability = 0;
			previousLife = Player.statLife;
		}

		public override void OnEnterWorld()
		{
			justSpawned = true;
			respawnInvulnerability = 0;
			SoulboundPool.Reset();

			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				ModPacket packet = Mod.GetPacket();
				packet.Write((byte)SoulboundMessageType.RequestHealthSync);
				packet.Send();
			}
		}

		public override void OnRespawn()
		{
			justSpawned = true;
			// 60 frames (1 second) of respawn grace period so delayed packets don't instantly re-kill
			respawnInvulnerability = 60;
			SoulboundPool.BothDead = false;

			int combinedMax = SoulboundPool.CalculateCombinedMaxLife();
			SoulboundPool.SharedMaxLife = combinedMax;
			SoulboundPool.SharedCurrentLife = combinedMax;
			Player.statLife = combinedMax;
			Player.statLifeMax2 = combinedMax;
			previousLife = combinedMax;

			if (Main.netMode != NetmodeID.SinglePlayer)
			{
				ModPacket packet = Mod.GetPacket();
				packet.Write((byte)SoulboundMessageType.SyncHealthPool);
				packet.Write((byte)Player.whoAmI);
				packet.Write(combinedMax);
				packet.Write(combinedMax);
				packet.Send();
			}
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
			if (respawnInvulnerability > 0)
				respawnInvulnerability--;

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

				// Clamp position so players cannot separate past screen distance
				Player.position = partner.Center - dir * maxDist - new Vector2(Player.width / 2f, Player.height / 2f);

				// Cancel outward velocity
				float outwardVel = Vector2.Dot(Player.velocity, -dir);
				if (outwardVel > 0f)
				{
					Player.velocity -= (-dir) * outwardVel;
				}
			}
		}

		public override void PostUpdateMiscEffects()
		{
			if (SoulboundConfig.Instance == null || !SoulboundConfig.Instance.SharedHealthEnabled)
				return;

			if (Player.dead || !Player.active)
				return;

			// Combined max life from base statLifeMax
			int combinedMax = SoulboundPool.CalculateCombinedMaxLife();
			SoulboundPool.SharedMaxLife = combinedMax;
			Player.statLifeMax2 = combinedMax;

			if (justSpawned)
			{
				if (!SoulboundPool.Initialized)
				{
					SoulboundPool.SharedCurrentLife = combinedMax;
					SoulboundPool.Initialized = true;
				}
				Player.statLife = SoulboundPool.SharedCurrentLife;
				previousLife = Player.statLife;
				justSpawned = false;
				return;
			}

			if (Player.whoAmI == Main.myPlayer)
			{
				int currentLife = Player.statLife;

				// Did this player heal (potion, regeneration, heart pick up, Nurse)?
				if (currentLife > previousLife)
				{
					int healedAmount = currentLife - previousLife;
					int newPool = Math.Min(combinedMax, SoulboundPool.SharedCurrentLife + healedAmount);
					SoulboundPool.ModifyPool(newPool, Player.whoAmI);
					previousLife = SoulboundPool.SharedCurrentLife;
				}
				// Did this player take direct passive environmental damage (lava, suffocation, fall damage)?
				else if (currentLife < previousLife && respawnInvulnerability <= 0)
				{
					int damageTaken = previousLife - currentLife;
					int newPool = Math.Max(0, SoulboundPool.SharedCurrentLife - damageTaken);
					SoulboundPool.ModifyPool(newPool, Player.whoAmI);
					previousLife = SoulboundPool.SharedCurrentLife;
				}
				else
				{
					// Normal frame: keep player's health exactly locked to the shared pool
					Player.statLife = SoulboundPool.SharedCurrentLife;
					previousLife = Player.statLife;
				}
			}
			else
			{
				// Remote player on our client
				Player.statLife = SoulboundPool.SharedCurrentLife;
				previousLife = Player.statLife;
			}
		}

		public override void OnHurt(Player.HurtInfo info)
		{
			if (SoulboundConfig.Instance == null || !SoulboundConfig.Instance.SharedHealthEnabled)
				return;

			if (Player.whoAmI != Main.myPlayer || respawnInvulnerability > 0)
				return;

			// Direct enemy hits
			int newPool = Math.Max(0, SoulboundPool.SharedCurrentLife - info.Damage);
			SoulboundPool.ModifyPool(newPool, Player.whoAmI);
			previousLife = SoulboundPool.SharedCurrentLife;
			Player.statLife = SoulboundPool.SharedCurrentLife;
		}

		public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource)
		{
			// When dead, reset pool to 0
			SoulboundPool.SharedCurrentLife = 0;
			return true;
		}
	}
}
