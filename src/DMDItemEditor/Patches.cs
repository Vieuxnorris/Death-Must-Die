using System.Collections.Generic;
using System.Linq;
using Death.Data;
using Death.Items;
using HarmonyLib;

namespace DMDItemEditor
{
    /// <summary>
    /// Vanilla loading rebuilds a unique from its database template and drops the affixes, rarity and
    /// tier stored in the save. This keeps the saved values so edited uniques survive a reload.
    /// Without the mod, the game simply falls back to the template: the save stays valid.
    /// </summary>
    [HarmonyPatch(typeof(ItemSaveLoad), nameof(ItemSaveLoad.TryLoadFrom))]
    internal static class Patch_KeepEditedUniques
    {
        private static void Postfix(ItemSaveData save, ref Item item, ItemSaveLoad.LoadResult __result)
        {
            if (!Plugin.PersistUniqueEdits.Value) return;
            if (__result != ItemSaveLoad.LoadResult.Success || item == null || save == null || !save.IsUnique) return;
            if (save.Affixes == null || save.Affixes.Count == 0) return;
            if (save.Affixes.Any(a => !Database.ItemAffixes.Contains(AffixCode.FromString(a.Code)))) return;

            var affixes = save.Affixes.Select(a => new Item.AffixReference(a)).ToList();
            string subtype = Database.ItemSubtypes.Contains(save.SubtypeCode) ? save.SubtypeCode : item.SubtypeCode;
            Database.ItemSubtypes.TryGet(subtype, out ItemSubtype st);
            bool wasOwned = save.WasOwnedByPlayer;
            item = new Item(item.Code, st?.ItemType ?? item.Type, st?.ItemClass ?? item.Class, save.Rarity,
                TierId.FromIndex(System.Math.Max(0, System.Math.Min(TierId.Count - 1, save.TierIndex))), true, subtype, item.IconVariant, item.DropVariant, affixes)
            {
                WasOwnedByPlayer = wasOwned,
            };
        }
    }
}
