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
		public int previousLife = 0;
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
			respawnInvulnerability = 60;
			SoulboundPool.BothDead = false;

			int combinedMax = SoulboundPool.CalculateCombinedMaxLife();
			SoulboundPool.SharedMaxLife = combinedMax;
			SoulboundPool.SharedCurrentLife = combinedMax;

			Player.statLifeMax2 = combinedMax;
			Player.statLife = combinedMax;
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

				// Clamp position strictly so players cannot separate past screen distance
				Player.position = partner.Center - dir * maxDist - new Vector2(Player.width / 2f, Player.height / 2f);

				// Cancel outward velocity
				float outwardVel = Vector2.Dot(Player.velocity, -dir);
				if (outwardVel > 0f)
				{
					Player.velocity -= (-dir) * outwardVel;
				}
			}
		}

		public override void ModifyMaxStats(out StatModifier health, out StatModifier mana)
		{
			health = StatModifier.Default;
			mana = StatModifier.Default;

			if (SoulboundConfig.Instance == null || !SoulboundConfig.Instance.SharedHealthEnabled)
				return;

			// Expand statLifeMax so Terraria's Classic and Fancy HUDs calculate the full 10+ hearts (20 HP per heart)
			int combinedMax = SoulboundPool.CalculateCombinedMaxLife();
			int currentBase = SoulboundPool.GetBaseMaxLife(Player);
			health.Base = combinedMax - currentBase;
		}

		public override void PreSavePlayer()
		{
			// Restore vanilla base health before saving to disk so player file (.plr) is never corrupted
			Player.statLifeMax = SoulboundPool.GetBaseMaxLife(Player);
		}

		public override void NaturalLifeRegen(ref float regen)
		{
			if (SoulboundConfig.Instance != null && SoulboundConfig.Instance.SharedHealthEnabled)
			{
				int count = SoulboundPool.GetActivePlayerCount();
				if (count > 1)
				{
					regen /= count;
				}
			}
		}

		public override void PostUpdateEquips()
		{
			if (SoulboundConfig.Instance == null || !SoulboundConfig.Instance.SharedHealthEnabled)
				return;

			if (Player.dead || !Player.active)
				return;

			int combinedMax = SoulboundPool.CalculateCombinedMaxLife();
			SoulboundPool.SharedMaxLife = combinedMax;
			Player.statLifeMax2 = combinedMax;
		}

		public override void PostUpdateMiscEffects()
		{
			if (SoulboundConfig.Instance == null || !SoulboundConfig.Instance.SharedHealthEnabled)
				return;

			if (Player.dead || !Player.active)
				return;

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

				if (currentLife != previousLife)
				{
					// If respawn invulnerability is active, ignore unexpected damage drops
					if (respawnInvulnerability > 0 && currentLife < previousLife)
					{
						Player.statLife = previousLife;
						return;
					}

					int delta = currentLife - previousLife;
					int newPool = Math.Clamp(SoulboundPool.SharedCurrentLife + delta, 0, combinedMax);
					SoulboundPool.ModifyPool(newPool, Player.whoAmI);
					previousLife = SoulboundPool.SharedCurrentLife;
					Player.statLife = SoulboundPool.SharedCurrentLife;
				}
				else
				{
					Player.statLife = SoulboundPool.SharedCurrentLife;
					previousLife = Player.statLife;
				}
			}
			else
			{
				Player.statLife = SoulboundPool.SharedCurrentLife;
				previousLife = Player.statLife;
			}
		}

		public override bool PreKill(double damage, int hitDirection, bool pvp, ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource)
		{
			SoulboundPool.ModifyPool(0, Player.whoAmI);
			return true;
		}
	}
}
