using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using Claw.Core.Chaos;
using Claw.Core.Structures;
using Death.Items;
using Death.Run.Core;
using HarmonyLib;

namespace DMDItemEditor
{
    /// <summary>Drop settings, saved in the plugin's config file and edited from the "Butin" tab.</summary>
    internal static class LootSettings
    {
        public static ConfigEntry<bool> Enabled;
        public static ConfigEntry<float> DropChanceMultiplier;
        public static ConfigEntry<bool> AlwaysDrop;
        public static ConfigEntry<int> ItemCountMultiplier;
        public static ConfigEntry<int> RarityMask;
        public static ConfigEntry<int> ForcedTier;
        public static ConfigEntry<float> UniqueChancePercent;
        public static ConfigEntry<int> AffixCount;
        public static ConfigEntry<float> GodChancePercent;

        public static void Bind(ConfigFile config)
        {
            Enabled = config.Bind("Loot", "Enabled", false, "Apply the drop settings below to monster loot.");
            DropChanceMultiplier = config.Bind("Loot", "DropChanceMultiplier", 1f,
                "Multiplies the chance that a monster drops items (1 = vanilla).");
            AlwaysDrop = config.Bind("Loot", "AlwaysDrop", false, "Every monster that can drop items drops them.");
            ItemCountMultiplier = config.Bind("Loot", "ItemCountMultiplier", 1,
                new ConfigDescription("Items per drop are multiplied by this.", new AcceptableValueRange<int>(1, 50)));
            RarityMask = config.Bind("Loot", "RarityMask", 0,
                "Bit mask of allowed rarities (1=Broken 2=Common 4=Rare 8=Epic 16=Mythic 32=Immortal). " +
                "0 = vanilla odds. With several bits, each allowed rarity is equally likely.");
            ForcedTier = config.Bind("Loot", "ForcedTier", 0,
                new ConfigDescription("Tier of dropped items, 1 to 5. 0 = the act's tier.", new AcceptableValueRange<int>(0, TierId.Count)));
            UniqueChancePercent = config.Bind("Loot", "UniqueChancePercent", -1f,
                "Chance for a drop to be a unique, 0 to 100. -1 = vanilla.");
            AffixCount = config.Bind("Loot", "AffixCount", 0,
                new ConfigDescription("Exact number of affixes on dropped regular items, filled with full rolls. 0 = vanilla.",
                    new AcceptableValueRange<int>(0, 60)));
            GodChancePercent = config.Bind("Loot", "GodChancePercent", 0f,
                "Chance for a dropped item to also carry the GOD affix, 0 to 100.");
        }
    }

    [HarmonyPatch(typeof(LootGenerator))]
    internal static class Patch_Loot
    {
        private static readonly Action<LootGenerator, LootData, float> ReGenerate =
            AccessTools.MethodDelegate<Action<LootGenerator, LootData, float>>(AccessTools.Method(typeof(LootGenerator), "ReGenerate"));
        private static readonly AccessTools.FieldRef<LootGenerator, IRng> RngRef =
            AccessTools.FieldRefAccess<LootGenerator, IRng>("_rng");
        private static readonly AccessTools.FieldRef<LootGenerator, List<ItemGenerator.Recipe>> LootRef =
            AccessTools.FieldRefAccess<LootGenerator, List<ItemGenerator.Recipe>>("_loot");

        /// <summary>Rarity sets created by this patch; the generator's rarity cap is bypassed for them only.</summary>
        internal static readonly ConditionalWeakTable<object, object> ForcedRaritySets = new ConditionalWeakTable<object, object>();

        /// <summary>Recipes rewritten by this patch: only their items get the affix-count and GOD changes.</summary>
        internal static readonly ConditionalWeakTable<object, object> ForcedRecipes = new ConditionalWeakTable<object, object>();

        /// <summary>Same as vanilla, with the drop chance multiplied (or skipped when AlwaysDrop is on).</summary>
        [HarmonyPrefix]
        [HarmonyPatch(nameof(LootGenerator.ReGenerateWithDropChance))]
        private static bool DropChancePrefix(LootGenerator __instance, LootData loot, float totalPlaytimeMin, float dropChanceModifier)
        {
            if (!LootSettings.Enabled.Value) return true;
            LootRef(__instance).Clear();
            float chance = StatRules.ApplyModifier(loot.ItemDropChance, dropChanceModifier) * Math.Max(0f, LootSettings.DropChanceMultiplier.Value);
            if (LootSettings.AlwaysDrop.Value || RngRef(__instance).RollChance(Math.Min(1f, chance)))
                ReGenerate(__instance, loot, totalPlaytimeMin);
            return false;
        }

        /// <summary>Rewrites the recipes the vanilla roll produced: quality overrides, then extra copies.</summary>
        [HarmonyPostfix]
        [HarmonyPatch("ReGenerate")]
        private static void ReGeneratePostfix(LootGenerator __instance)
        {
            if (!LootSettings.Enabled.Value) return;
            List<ItemGenerator.Recipe> loot = LootRef(__instance);
            if (loot.Count == 0) return;

            WeighedRandomSet<ItemRarity> rarities = BuildRaritySet();
            int tier = LootSettings.ForcedTier.Value;
            float unique = LootSettings.UniqueChancePercent.Value;
            var rewritten = new List<ItemGenerator.Recipe>(loot.Count);
            foreach (ItemGenerator.Recipe r in loot)
            {
                TierId t = tier >= 1 ? TierId.FromId(Math.Min(tier, TierId.Count)) : r.Tier;
                // Uniques only exist in tiers 1-3 and must match the unique tier exactly, so it keeps the act's value.
                rewritten.Add(new ItemGenerator.Recipe(r.TreasureClassCode, t, r.UniqueTier,
                    unique >= 0f ? Math.Min(100f, unique) / 100f : r.UniqueChance,
                    rarities ?? r.RarityProbabilities, r.TypeProbabilities)
                {
                    ForceSubtype = r.ForceSubtype,
                });
                ForcedRecipes.Add(rewritten[rewritten.Count - 1], null);
            }

            loot.Clear();
            int copies = Math.Max(1, LootSettings.ItemCountMultiplier.Value);
            for (int i = 0; i < copies; i++) loot.AddRange(rewritten);
        }

        private static WeighedRandomSet<ItemRarity> BuildRaritySet()
        {
            int mask = LootSettings.RarityMask.Value;
            if (mask == 0) return null;
            var set = new WeighedRandomSet<ItemRarity>();
            for (int i = 0; i < (int)ItemRarity._Count; i++)
                if ((mask & (1 << i)) != 0) set.Add((ItemRarity)i, 1f);
            if (set.IsEmpty) return null;
            ForcedRaritySets.Add(set, null);
            return set;
        }
    }

    /// <summary>
    /// The item generator drops rarities above the game's allowed maximum, which would leave a forced
    /// "Immortal only" set empty and make every drop fail. Forced sets are picked from directly.
    /// </summary>
    [HarmonyPatch(typeof(ItemGenerator), "PickRandomRarity")]
    internal static class Patch_ForcedRarity
    {
        private static readonly AccessTools.FieldRef<ItemGenerator, IRng> RngRef =
            AccessTools.FieldRefAccess<ItemGenerator, IRng>("_rng");

        private static bool Prefix(ItemGenerator __instance, IReadOnlyWeighedRandomSet<ItemRarity> raritySet, ref ItemRarity __result)
        {
            if (raritySet == null || !Patch_Loot.ForcedRaritySets.TryGetValue(raritySet, out _)) return true;
            __result = raritySet.PickRandom(RngRef(__instance));
            return false;
        }
    }
}
