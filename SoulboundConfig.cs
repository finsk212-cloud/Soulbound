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

		[Header("TetherSettings")]

		[DefaultValue(true)]
		public bool TetherEnabled;

		[Range(400, 2500)]
		[DefaultValue(1000)]
		[Increment(50)]
		public int MaxDistancePixels;
	}
}
