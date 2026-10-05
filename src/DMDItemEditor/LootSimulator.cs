using System;
using System.Linq;
using System.Text;
using Death.Data;
using Death.Items;
using Death.Run.Core;
using Death.Utils;

namespace DMDItemEditor
{
    /// <summary>
    /// Runs the game's own loot pipeline (LootGenerator then ItemGenerator, with the patches active) for a
    /// number of simulated kills, so drop settings can be checked without fighting. Nothing is given.
    /// </summary>
    internal static class LootSimulator
    {
        public static string Run(int kills)
        {
            MonsterData monster = Database.Monsters.AllMonsters.FirstOrDefault(m =>
                m.Loot != null && m.Loot.ItemDropChance > 0f && !m.Loot.ItemCountProbability.IsEmpty && !m.Loot.TreasureClassProbability.IsEmpty);
            if (monster == null) return "Aucun monstre avec du butin trouvé.";

            var loot = new LootGenerator(GlobalRng.Instance, TierId.First, TierId.First, Database.ItemDropsPerMin, Database.DarknessDropBonus.Get(0));
            ItemGenerator generator = Database.CreateItemGenerator();
            int dropping = 0, items = 0, uniques = 0, gods = 0, affixTotal = 0;
            var perRarity = new int[(int)ItemRarity._Count];
            var perTier = new int[TierId.Count];

            for (int k = 0; k < kills; k++)
            {
                loot.ReGenerateWithDropChance(monster.Loot, 10f);
                if (loot.Loot.Count > 0) dropping++;
                using (ItemGenerator.Context context = ItemAccess.Profile.GenerateItemContext())
                {
                    foreach (ItemGenerator.Recipe recipe in loot.Loot)
                    {
                        Item item = generator.Generate(recipe, context);
                        if (item == null) continue;
                        items++;
                        if (item.IsUnique) uniques++;
                        affixTotal += item.Affixes.Count;
                        if (item.Affixes.Any(a => a.Code == GodAffix.AffixCode)) gods++;
                        perRarity[Math.Min((int)item.Rarity, perRarity.Length - 1)]++;
                        perTier[Math.Max(0, Math.Min(item.Tier.Index, perTier.Length - 1))]++;
                    }
                }
            }

            var sb = new StringBuilder();
            sb.Append($"{kills} « {monster.Code} » tués (acte T1) : {dropping} ont droppé, {items} objets, dont {uniques} uniques.\n");
            sb.Append($"Affixes par objet : {(items > 0 ? (float)affixTotal / items : 0f):0.0} en moyenne. Objets avec GOD affix : {gods}.\n");
            sb.Append("Raretés : ");
            for (int i = 0; i < perRarity.Length; i++) sb.Append($"{(ItemRarity)i} {perRarity[i]}   ");
            sb.Append("\nTiers : ");
            for (int i = 0; i < perTier.Length; i++) sb.Append($"T{i + 1} {perTier[i]}   ");
            return sb.ToString();
        }
    }
}
