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

		public override void Initialize()
		{
			justSpawned = true;
			previousLife = Player.statLife;
		}

		public override void OnEnterWorld()
		{
			justSpawned = true;
			SoulboundPool.Reset();

			if (Main.netMode == NetmodeID.MultiplayerClient)
			{
				// Ask server for current pool
				ModPacket packet = Mod.GetPacket();
				packet.Write((byte)SoulboundMessageType.RequestHealthSync);
				packet.Send();
			}
		}

		public override void OnRespawn()
		{
			justSpawned = true;
			int totalMax = SoulboundPool.CalculateTotalMaxLife();
			SoulboundPool.SharedMaxLife = totalMax;
			SoulboundPool.SharedCurrentLife = totalMax; // Respawn pool restored
			Player.statLife = totalMax;
			Player.statLifeMax2 = totalMax;
			previousLife = totalMax;

			if (Main.netMode != NetmodeID.SinglePlayer)
			{
				ModPacket packet = Mod.GetPacket();
				packet.Write((byte)SoulboundMessageType.SyncHealthPool);
				packet.Write((byte)Player.whoAmI);
				packet.Write(totalMax);
				packet.Write(totalMax);
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

				// Hard clamp so players cannot separate past screen distance
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

			// Expand statLifeMax2 to the combined health pool
			int combinedMax = SoulboundPool.CalculateTotalMaxLife();
			SoulboundPool.SharedMaxLife = combinedMax;
			Player.statLifeMax2 = combinedMax;

			// On initial spawn, sync starting pool
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

			// Detect any natural changes on the local player (Regen, Potion heal, Life Crystal, Nurse, Lava, Suffocation, Traps)
			if (Player.whoAmI == Main.myPlayer)
			{
				int currentLife = Player.statLife;
				if (currentLife != previousLife)
				{
					int delta = currentLife - previousLife;
					SoulboundPool.ModifyPoolDelta(delta, Player.whoAmI);
					previousLife = SoulboundPool.SharedCurrentLife;
				}
				else
				{
					// Ensure player displays current shared pool
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

			if (Player.whoAmI != Main.myPlayer)
				return;

			// Enemy hits or player hurt: subtract damage from the combined pool
			SoulboundPool.ModifyPoolDelta(-info.Damage, Player.whoAmI);
			previousLife = SoulboundPool.SharedCurrentLife;
			Player.statLife = SoulboundPool.SharedCurrentLife;

			if (SoulboundPool.SharedCurrentLife <= 0)
			{
				Player.statLife = 0;
				Player.KillMe(
					PlayerDeathReason.ByCustomReason(NetworkText.FromLiteral(Player.name + " shared their soulbound partner's fatal blow.")),
					1000,
					0
				);
			}
		}

		// When drinking a health potion, Terraria calls GetHealLife.
		// We guarantee the heal amount applies fully to the combined health pool:
		public override void GetHealLife(Item item, bool quickHeal, ref int healValue)
		{
			if (SoulboundConfig.Instance == null || !SoulboundConfig.Instance.SharedHealthEnabled)
				return;

			// healValue is the full potion heal amount (e.g. 50 for Lesser, 100 for Regular, 150 for Greater)
			// Terraria will add healValue to Player.statLife, which PostUpdateMiscEffects detects as a delta!
		}
	}
}
