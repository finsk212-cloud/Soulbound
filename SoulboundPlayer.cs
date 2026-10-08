using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Soulbound
{
	public class SoulboundPlayer : ModPlayer
	{
		public int previousLife = 0;

		// This player's own max life (base + life crystals/fruit + accessory/buff bonuses), before the shared pool is applied
		public int OwnMaxLife = 0;
		private bool justSpawned = true;
		public int respawnInvulnerability = 0;

		private Vector2 lastPosition = Vector2.Zero;
		public int TeleportGraceTimer = 0;
		public int TeleportAnchor = -1;

		public override void Initialize()
		{
			justSpawned = true;
			respawnInvulnerability = 0;
			previousLife = Player.statLife;
			lastPosition = Vector2.Zero;
			TeleportGraceTimer = 0;
			TeleportAnchor = -1;
		}

		public override void OnEnterWorld()
		{
			justSpawned = true;
			int respawnTicks = GetRespawnInvulnerabilityTicks();
			respawnInvulnerability = respawnTicks;
			Player.immune = true;
			Player.immuneTime = Math.Max(Player.immuneTime, respawnTicks);

			lastPosition = Player.position;
			TeleportGraceTimer = 0;
			TeleportAnchor = -1;
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
			int respawnTicks = GetRespawnInvulnerabilityTicks();
			respawnInvulnerability = respawnTicks;
			Player.immune = true;
			Player.immuneTime = Math.Max(Player.immuneTime, respawnTicks);
			Player.immuneNoBlink = true;

			lastPosition = Player.position;
			SoulboundPool.BothDead = false;

			int combinedMax = SoulboundPool.CalculateCombinedMaxLife();
			SoulboundPool.SharedMaxLife = combinedMax;
			SoulboundPool.SharedCurrentLife = combinedMax;

			Player.statLifeMax2 = combinedMax;
			Player.statLife = combinedMax;
			previousLife = combinedMax;

			// Respawn far away starts grace timer so partner doesn't rubberband
			StartTeleportGrace(Player.whoAmI, GetGraceDurationTicks());

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
				if (i != Player.whoAmI && Main.player[i].active && !Main.player[i].dead && !Main.player[i].ghost)
				{
					return Main.player[i];
				}
			}
			return null;
		}

		private int GetGraceDurationTicks()
		{
			int seconds = SoulboundConfig.Instance != null ? SoulboundConfig.Instance.TeleportGraceSeconds : 5;
			return Math.Max(1, seconds) * 60;
		}

		private int GetRespawnInvulnerabilityTicks()
		{
			int seconds = SoulboundConfig.Instance != null ? SoulboundConfig.Instance.RespawnInvincibilitySeconds : 1;
			// Minimum 30 ticks (0.5s), default 60 ticks (1.0s)
			return Math.Max(30, seconds * 60);
		}

		public void StartTeleportGrace(int anchorWhoAmI, int ticks)
		{
			TeleportAnchor = anchorWhoAmI;
			TeleportGraceTimer = ticks;
		}

		public void PullToPartner(Player partner)
		{
			if (partner == null || !partner.active)
				return;

			// Safely place player at partner's position
			Player.position = partner.position;
			Player.velocity = Vector2.Zero;
			Player.fallStart = (int)(Player.position.Y / 16f);

			// Cancel grappling hooks
			for (int g = 0; g < Player.grappling.Length; g++)
			{
				Player.grappling[g] = -1;
			}

			// Grant generous invulnerability so player doesn't instantly die on arrival
			int invincibilitySeconds = SoulboundConfig.Instance != null ? SoulboundConfig.Instance.PullInvincibilitySeconds : 3;
			int invincibilityTicks = Math.Max(1, invincibilitySeconds) * 60;
			Player.immune = true;
			Player.immuneTime = Math.Max(Player.immuneTime, invincibilityTicks);
			respawnInvulnerability = Math.Max(respawnInvulnerability, invincibilityTicks);

			// Play teleport sound and spawn clean dust
			SoundEngine.PlaySound(SoundID.Item6, Player.position);
			for (int d = 0; d < 35; d++)
			{
				Dust.NewDust(Player.position, Player.width, Player.height, DustID.MagicMirror, 0f, 0f, 150, default, 1.5f);
			}

			if (Main.netMode != NetmodeID.SinglePlayer)
			{
				NetMessage.SendData(MessageID.PlayerControls, -1, -1, null, Player.whoAmI);
			}
		}

		public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable)
		{
			if (respawnInvulnerability > 0)
				return true;

			return base.ImmuneTo(damageSource, cooldownCounter, dodgeable);
		}

		public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot)
		{
			if (respawnInvulnerability > 0)
				return false;

			return base.CanBeHitByNPC(npc, ref cooldownSlot);
		}

		public override bool CanBeHitByProjectile(Projectile proj)
		{
			if (respawnInvulnerability > 0)
				return false;

			return base.CanBeHitByProjectile(proj);
		}

		public override void PreUpdateMovement()
		{
			if (respawnInvulnerability > 0)
				respawnInvulnerability--;

			if (SoulboundConfig.Instance == null || !SoulboundConfig.Instance.TetherEnabled)
			{
				lastPosition = Player.position;
				return;
			}

			if (Player.dead || !Player.active || Player.ghost)
			{
				lastPosition = Player.position;
				return;
			}

			Player partner = GetPartner();
			if (partner == null)
			{
				lastPosition = Player.position;
				return;
			}

			float maxDist = SoulboundConfig.Instance.MaxDistancePixels;
			Vector2 toPartner = partner.Center - Player.Center;
			float dist = toPartner.Length();

			// Detect teleportation (Magic Mirror, Recall Potion, Pylon, Teleporter, etc.)
			if (lastPosition != Vector2.Zero)
			{
				float movedDelta = Vector2.Distance(Player.position, lastPosition);
				if (movedDelta > 400f)
				{
					// This player just teleported! Start grace period
					int graceTicks = GetGraceDurationTicks();
					StartTeleportGrace(Player.whoAmI, graceTicks);

					if (Main.netMode != NetmodeID.SinglePlayer && SoulboundMod.Instance != null)
					{
						ModPacket packet = SoulboundMod.Instance.GetPacket();
						packet.Write((byte)SoulboundMessageType.TeleportGraceStarted);
						packet.Write((byte)Player.whoAmI);
						packet.Write(graceTicks);
						packet.Send();
					}
				}
			}

			// If distance is suddenly huge (e.g. partner teleported or spawn separation) and grace isn't active
			if (dist > maxDist * 1.4f && TeleportGraceTimer <= 0)
			{
				StartTeleportGrace(partner.whoAmI, GetGraceDurationTicks());
			}

			// If both players are now back within safe tether distance, clear grace timer
			if (dist <= maxDist && TeleportGraceTimer > 0)
			{
				TeleportGraceTimer = 0;
				TeleportAnchor = -1;
			}

			// While teleport grace period is active, DO NOT clamp or jerk players
			if (TeleportGraceTimer > 0)
			{
				TeleportGraceTimer--;

				// If grace window just expired and players are still separated:
				if (TeleportGraceTimer <= 0 && dist > maxDist)
				{
					// If we are the distant player, pull us safely to the anchor partner
					if (Player.whoAmI == Main.myPlayer)
					{
						if (TeleportAnchor == partner.whoAmI || TeleportAnchor == -1)
						{
							PullToPartner(partner);
						}
					}
				}

				lastPosition = Player.position;
				return;
			}

			// Normal tether enforcement:
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

			lastPosition = Player.position;
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

		public override void PostUpdateMiscEffects()
		{
			if (SoulboundConfig.Instance == null || !SoulboundConfig.Instance.SharedHealthEnabled)
				return;

			if (Player.dead || !Player.active || Player.ghost)
				return;

			// statLifeMax2 is reset every tick, so here it holds only this player's own max (accessories included)
			OwnMaxLife = Math.Max(1, Player.statLifeMax2);

			int combinedMax = SoulboundPool.CalculateCombinedMaxLife();
			SoulboundPool.SharedMaxLife = combinedMax;
			Player.statLifeMax2 = combinedMax;

			// Partner left or max shrank: keep the pool within the new max
			if (SoulboundPool.Initialized && SoulboundPool.SharedCurrentLife > combinedMax)
				SoulboundPool.SharedCurrentLife = combinedMax;

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
					// If respawn or teleport invulnerability is active, ignore unexpected damage drops
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
			if (respawnInvulnerability > 0)
			{
				Player.statLife = Math.Max(Player.statLife, 1);
				return false;
			}

			SoulboundPool.BothDead = true;
			SoulboundPool.SharedCurrentLife = 0;
			SoulboundPool.ModifyPool(0, Player.whoAmI);
			return true;
		}
	}
}
