using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace Soulbound
{
	public class SoulboundSystem : ModSystem
	{
		public override void PostDrawInterface(SpriteBatch spriteBatch)
		{
			if (Main.gameMenu || Main.dedServ)
				return;

			Player localPlayer = Main.LocalPlayer;
			if (localPlayer == null || !localPlayer.active || localPlayer.dead)
				return;

			if (SoulboundConfig.Instance == null || !SoulboundConfig.Instance.TetherEnabled)
				return;

			SoulboundPlayer sbp = localPlayer.GetModPlayer<SoulboundPlayer>();
			Player partner = sbp.GetPartner();
			if (partner == null)
				return;

			float dist = Vector2.Distance(localPlayer.Center, partner.Center);
			float maxDist = SoulboundConfig.Instance.MaxDistancePixels;
			float ratio = dist / maxDist;
			int meters = (int)Math.Round(dist / 26.25f);

			// 1. Clean Teleport Warning Banner
			if (sbp.TeleportGraceTimer > 0 && (SoulboundClientConfig.Instance == null || SoulboundClientConfig.Instance.ShowTeleportWarning))
			{
				float secondsLeft = sbp.TeleportGraceTimer / 60f;
				string bannerText;

				if (sbp.TeleportAnchor == localPlayer.whoAmI)
				{
					bannerText = $"[!] You teleported away! Partner will be pulled in {secondsLeft:0.0}s";
				}
				else
				{
					bannerText = $"[!] Partner recalled away! Use Magic Mirror or be pulled in {secondsLeft:0.0}s";
				}

				Vector2 bannerPos = new Vector2(Main.screenWidth / 2f, 95f);
				float pulse = (float)Math.Sin(Main.GameUpdateCount * 0.2f) * 0.15f + 0.85f;
				Color bannerColor = new Color((byte)(255 * pulse), (byte)(215 * pulse), (byte)(40 * pulse));
				Utils.DrawBorderString(spriteBatch, bannerText, bannerPos, bannerColor, 1.15f, 0.5f, 0.5f);
			}

			// 2. Overhead Distance Meter above playable character's head
			if (SoulboundClientConfig.Instance != null && !SoulboundClientConfig.Instance.ShowDistanceMeter)
				return;

			string text;
			Color textColor;

			if (sbp.TeleportGraceTimer > 0)
			{
				float secondsLeft = sbp.TeleportGraceTimer / 60f;
				text = $"[!] Recalling in {secondsLeft:0.0}s";
				float pulse = (float)Math.Sin(Main.GameUpdateCount * 0.2f) * 0.2f + 0.8f;
				textColor = new Color((byte)(255 * pulse), (byte)(190 * pulse), (byte)(40 * pulse));
			}
			else if (ratio < 0.65f)
			{
				// Safe / close: clean mint-white text
				text = $"{meters}m";
				textColor = new Color(210, 245, 225);
			}
			else if (ratio < 0.90f)
			{
				// Warning zone: gradually shifts to yellow with warning symbol
				float t = (ratio - 0.65f) / 0.25f;
				textColor = Color.Lerp(new Color(210, 245, 225), new Color(255, 230, 45), t);
				text = $"! {meters}m";
			}
			else
			{
				// Limit / Danger zone: gradually shifts from yellow to vibrant danger red
				float t = Math.Clamp((ratio - 0.90f) / 0.10f, 0f, 1f);
				textColor = Color.Lerp(new Color(255, 230, 45), new Color(255, 50, 50), t);

				if (ratio >= 0.98f)
				{
					float pulse = (float)Math.Sin(Main.GameUpdateCount * 0.25f) * 0.2f + 0.8f;
					textColor = new Color((byte)(255 * pulse), (byte)(50 * pulse), (byte)(50 * pulse));
				}

				text = $"[!] {meters}m";
			}

			Vector2 worldPos = localPlayer.gravDir == -1
				? localPlayer.Bottom + new Vector2(0f, 14f)
				: localPlayer.Top + new Vector2(0f, -14f);

			Vector2 screenPos = Terraria.Utils.ToScreenPosition(worldPos);
			Utils.DrawBorderString(spriteBatch, text, screenPos, textColor, 1f, 0.5f, 1f);
		}
	}
}
