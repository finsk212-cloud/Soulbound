using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.Personalities;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace Soulbound.NPCs
{
	// Example town NPC: 2B. Sprite sheets (TwoB.png, TwoB_Head.png) are generated placeholders
	// that follow the vanilla town NPC layout (40x56 frames, 2px padding, 25 frames).
	[AutoloadHead]
	public class TwoB : ModNPC
	{
		public const string ShopName = "Shop";

		public override void SetStaticDefaults()
		{
			Main.npcFrameCount[Type] = 25;

			NPCID.Sets.ExtraFramesCount[Type] = 9;
			NPCID.Sets.AttackFrameCount[Type] = 4;
			NPCID.Sets.DangerDetectRange[Type] = 450;
			NPCID.Sets.AttackType[Type] = 3; // 3 = swings a held weapon (katana-style)
			NPCID.Sets.AttackTime[Type] = 12;
			NPCID.Sets.AttackAverageChance[Type] = 30;
			NPCID.Sets.HatOffsetY[Type] = 4;

			NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new NPCID.Sets.NPCBestiaryDrawModifiers()
			{
				Velocity = 1f,
				Direction = -1
			};
			NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);

			// Housing preferences.
			NPC.Happiness
				.SetBiomeAffection<ForestBiome>(AffectionLevel.Like)
				.SetBiomeAffection<DesertBiome>(AffectionLevel.Dislike)
				.SetNPCAffection(NPCID.Guide, AffectionLevel.Like)
				.SetNPCAffection(NPCID.Mechanic, AffectionLevel.Like)
				.SetNPCAffection(NPCID.Merchant, AffectionLevel.Dislike);
		}

		public override void SetDefaults()
		{
			NPC.townNPC = true;
			NPC.friendly = true;
			NPC.width = 18;
			NPC.height = 40;
			NPC.aiStyle = NPCAIStyleID.Passive;
			NPC.damage = 25;
			NPC.defense = 20;
			NPC.lifeMax = 300;
			NPC.HitSound = SoundID.NPCHit1;
			NPC.DeathSound = SoundID.NPCDeath1;
			NPC.knockBackResist = 0.5f;

			AnimationType = NPCID.Guide;
		}

		public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry)
		{
			bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[]
			{
				BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
				new FlavorTextBestiaryInfoElement("Mods.Soulbound.NPCs.TwoB.Bestiary")
			});
		}

		// Moves in after the Eye of Cthulhu has been defeated.
		public override bool CanTownNPCSpawn(int numTownNPCs)
		{
			return NPC.downedBoss1;
		}

		public override List<string> SetNPCNameList()
		{
			return new List<string> { "2B", "YoRHa No.2 Type B" };
		}

		public override string GetChat()
		{
			WeightedRandom<string> chat = new WeightedRandom<string>();

			chat.Add(Dialogue("Normal1"));
			chat.Add(Dialogue("Normal2"));
			chat.Add(Dialogue("Normal3"));
			chat.Add(Dialogue("SharedFate"), 1.5); // ties in with the shared-health theme of the mod

			if (Main.hardMode)
				chat.Add(Dialogue("Hardmode"));

			if (Main.bloodMoon)
				chat.Add(Dialogue("BloodMoon"), 2.0);

			return chat;
		}

		public override void SetChatButtons(ref string button, ref string button2)
		{
			button = Language.GetTextValue("LegacyInterface.28"); // "Shop"
		}

		public override void OnChatButtonClicked(bool firstButton, ref string shopName)
		{
			if (firstButton)
				shopName = ShopName;
		}

		public override void AddShops()
		{
			new NPCShop(Type, ShopName)
				.Add(ItemID.Katana)
				.Add(ItemID.BlackBelt)
				.Add(ItemID.LesserHealingPotion)
				.Add(ItemID.GreaterHealingPotion, Condition.Hardmode)
				.Register();
		}

		// --- Combat: 2B swings a katana at nearby enemies ---

		public override void TownNPCAttackStrength(ref int damage, ref float knockback)
		{
			damage = 30;
			knockback = 4f;
		}

		public override void TownNPCAttackCooldown(ref int cooldown, ref int randExtraCooldown)
		{
			cooldown = 10;
			randExtraCooldown = 8;
		}

		public override void TownNPCAttackSwing(ref int itemWidth, ref int itemHeight)
		{
			itemWidth = 40;
			itemHeight = 40;
		}

		public override void DrawTownAttackSwing(ref Texture2D item, ref Rectangle itemFrame, ref int itemSize, ref float scale, ref Vector2 offset)
		{
			Main.instance.LoadItem(ItemID.Katana);
			item = TextureAssets.Item[ItemID.Katana].Value;
			itemFrame = item.Frame();
			itemSize = 40;
			scale = 1f;
			offset = Vector2.Zero;
		}

		private static string Dialogue(string key)
		{
			return Language.GetTextValue("Mods.Soulbound.NPCs.TwoB.Dialogue." + key);
		}
	}
}
