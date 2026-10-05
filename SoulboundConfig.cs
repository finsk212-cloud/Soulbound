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

		[Header("TeleportGraceSettings")]

		[Range(2, 15)]
		[DefaultValue(5)]
		[Increment(1)]
		public int TeleportGraceSeconds;

		[Range(1, 10)]
		[DefaultValue(3)]
		[Increment(1)]
		public int PullInvincibilitySeconds;
	}

	public class SoulboundClientConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		public static SoulboundClientConfig Instance => Terraria.ModLoader.ModContent.GetInstance<SoulboundClientConfig>();

		[Header("VisualSettings")]

		[DefaultValue(true)]
		public bool ShowDistanceMeter;

		[DefaultValue(true)]
		public bool ShowTeleportWarning;
	}
}
