using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace Soulbound
{
	public class SoulboundSystem : ModSystem
	{
		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
		{
			if (Main.dedServ)
				return;

			int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
			if (mouseTextIndex == -1)
				return;

			// Insert tether visual layer beneath the UI panels but above world
			layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
				"Soulbound: Tether Beam",
				delegate
				{
					DrawTetherLayer();
					return true;
				},
				InterfaceScaleType.Game) // Game scale: matches world coordinates - Main.screenPosition!
			);
		}

		private static void DrawTetherLayer()
		{
			if (SoulboundConfig.Instance == null || !SoulboundConfig.Instance.VisualTetherLine)
				return;

			Player localPlayer = Main.LocalPlayer;
			if (!localPlayer.active || localPlayer.dead)
				return;

			var soulPlayer = localPlayer.GetModPlayer<SoulboundPlayer>();
			Player partner = soulPlayer.GetPartner();
			if (partner == null || !partner.active || partner.dead)
				return;

			DrawTetherBeam(localPlayer, partner);
		}

		private static void DrawTetherBeam(Player p1, Player p2)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			if (pixel == null)
				return;

			Vector2 pos1 = p1.Center - Main.screenPosition;
			Vector2 pos2 = p2.Center - Main.screenPosition;

			Vector2 diff = pos2 - pos1;
			float dist = diff.Length();
			if (dist < 1f)
				return;

			float maxDist = SoulboundConfig.Instance?.MaxDistancePixels ?? 1200f;
			float ratio = MathHelper.Clamp(dist / maxDist, 0f, 1f);

			// Dynamic color:
			// Calm Cyan/Azure -> Intense Pink/Magenta -> Warning Crimson as distance reaches limit
			Color beamColor;
			if (ratio < 0.6f)
			{
				float t = ratio / 0.6f;
				beamColor = Color.Lerp(new Color(60, 220, 255, 180), new Color(180, 100, 255, 200), t);
			}
			else
			{
				float t = (ratio - 0.6f) / 0.4f;
				beamColor = Color.Lerp(new Color(180, 100, 255, 200), new Color(255, 60, 90, 230), t);
			}

			float rotation = (float)Math.Atan2(diff.Y, diff.X);

			// Draw glowing core beam
			int thickness = ratio > 0.8f ? 3 : 2;
			Main.spriteBatch.Draw(
				pixel,
				pos1,
				null,
				beamColor,
				rotation,
				new Vector2(0f, 0.5f),
				new Vector2(dist, thickness),
				SpriteEffects.None,
				0f
			);

			// Outer aura glow
			Main.spriteBatch.Draw(
				pixel,
				pos1,
				null,
				beamColor * 0.40f,
				rotation,
				new Vector2(0f, 0.5f),
				new Vector2(dist, thickness + 4),
				SpriteEffects.None,
				0f
			);
		}
	}
}
