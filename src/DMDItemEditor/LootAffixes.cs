using System;
using System.Collections.Generic;
using System.Linq;
using Death.Data;
using Death.Items;
using HarmonyLib;

namespace DMDItemEditor
{
    /// <summary>
    /// Post-processes items generated from loot recipes rewritten by <see cref="Patch_Loot"/>: sets an exact
    /// affix count on regular items and rolls the GOD affix. Shop and cheat items are untouched.
    /// </summary>
    [HarmonyPatch(typeof(ItemGenerator), nameof(ItemGenerator.Generate))]
    internal static class Patch_LootAffixes
    {
        private static readonly Random Rng = new Random();

        private static void Postfix(ItemGenerator.Recipe recipe, Item __result)
        {
            if (__result == null || recipe == null || !Patch_Loot.ForcedRecipes.TryGetValue(recipe, out _)) return;
            try
            {
                int count = LootSettings.AffixCount.Value;
                if (count > 0 && !__result.IsUnique) SetAffixCount(__result, count);

                float god = LootSettings.GodChancePercent.Value;
                if (god > 0f && GodAffix.IsRegistered && Rng.NextDouble() * 100.0 < god
                    && ItemAccess.Affixes(__result).All(a => a.Code != GodAffix.AffixCode))
                    ItemAccess.AddAffix(__result, GodAffix.AffixCode, GodAffix.DefaultLevels.Value);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Loot affix post-processing failed: " + e);
            }
        }

        /// <summary>A positive plain-stat affix (same family as Damage %), never GOD or a penalty.</summary>
        private static bool IsStatAffix(ItemAffix a) =>
            !a.IsSpecial && a.Code != GodAffix.AffixCode && a.StatGroup == Database.ItemAffixes.Get(AffixCode.Damage).StatGroup
            && GodAffix.IsBeneficial(a.Code.ToString());

        /// <summary>
        /// Trims extra affixes from the end (base affixes come first), or adds full-roll stat affixes the
        /// item could normally carry: right rarity, a non-zero range for its slot, one per affix group.
        /// </summary>
        internal static void SetAffixCount(Item item, int count)
        {
            List<Item.AffixReference> affixes = ItemAccess.Affixes(item);
            if (affixes.Count > count)
            {
                affixes.RemoveRange(count, affixes.Count - count);
                return;
            }

            var used = new HashSet<AffixCode>(affixes.Select(a => a.Code));
            var groups = new HashSet<string>(affixes
                .Select(a => Database.ItemAffixes.TryGet(a.Code, out ItemAffix d) ? d.AffixGroup : null)
                .Where(g => g != null));
            List<ItemAffix> pool = Database.ItemAffixes.All
                .Where(a => IsStatAffix(a) && a.MinRarity <= item.Rarity && !used.Contains(a.Code)
                            && a.GetMaxVal(item.Type, item.Tier) > 0 && a.ValueCostPerLevel(item.Tier) > 0)
                .ToList();

            while (affixes.Count < count && pool.Count > 0)
            {
                ItemAffix pick = pool[Rng.Next(pool.Count)];
                pool.Remove(pick);
                if (groups.Contains(pick.AffixGroup)) continue;
                groups.Add(pick.AffixGroup);
                int levels = Math.Max(1, pick.GetMaxVal(item.Type, item.Tier) / pick.ValueCostPerLevel(item.Tier));
                affixes.Add(new Item.AffixReference(pick.Code, levels, enhanced: true));
            }

            // Not enough distinct groups left: allow repeats of a group with different affixes,
            // still limited to affixes the item's rarity allows.
            pool = Database.ItemAffixes.All
                .Where(a => IsStatAffix(a) && a.MinRarity <= item.Rarity && !affixes.Any(x => x.Code == a.Code)
                            && a.GetMaxVal(item.Type, item.Tier) > 0 && a.ValueCostPerLevel(item.Tier) > 0)
                .ToList();
            while (affixes.Count < count && pool.Count > 0)
            {
                ItemAffix pick = pool[Rng.Next(pool.Count)];
                pool.Remove(pick);
                int levels = Math.Max(1, pick.GetMaxVal(item.Type, item.Tier) / pick.ValueCostPerLevel(item.Tier));
                affixes.Add(new Item.AffixReference(pick.Code, levels, enhanced: true));
            }
        }
    }
}
