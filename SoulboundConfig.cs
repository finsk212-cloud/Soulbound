using System;
using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace Soulbound
{
	public class SoulboundConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ServerSide;

		public static SoulboundConfig Instance => Terraria.ModLoader.ModContent.GetInstance<SoulboundConfig>();

		[Header("SharedHealthSettings")]

		[DefaultValue(true)]
		public bool SharedHealthEnabled;

		[Range(0.0f, 1.0f)]
		[DefaultValue(1.0f)]
		[Slider]
		public float DamageShareRatio;

		[Header("TetherSettings")]

		[DefaultValue(true)]
		public bool TetherEnabled;

		[Range(600, 3000)]
		[DefaultValue(1200)]
		[Increment(50)]
		public int MaxDistancePixels;

		[DefaultValue(true)]
		public bool VisualTetherLine;

		[DefaultValue(true)]
		public bool PlayPullSound;
	}
}
