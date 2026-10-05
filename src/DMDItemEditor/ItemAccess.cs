using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Death;
using Death.App;
using Death.Data;
using Death.Items;
using HarmonyLib;

namespace DMDItemEditor
{
    /// <summary>
    /// Reads and writes the game's item objects. <see cref="Item"/> exposes get-only auto-properties,
    /// so edits go through their compiler-generated backing fields and keep the same object identity
    /// (the ItemRepository, slots and loadouts all key on the instance).
    /// </summary>
    internal static class ItemAccess
    {
        private static readonly AccessTools.FieldRef<Item, ItemType> TypeRef = Backing<ItemType>("Type");
        private static readonly AccessTools.FieldRef<Item, ItemClass> ClassRef = Backing<ItemClass>("Class");
        private static readonly AccessTools.FieldRef<Item, ItemRarity> RarityRef = Backing<ItemRarity>("Rarity");
        private static readonly AccessTools.FieldRef<Item, TierId> TierRef = Backing<TierId>("Tier");
        private static readonly AccessTools.FieldRef<Item, bool> IsUniqueRef = Backing<bool>("IsUnique");
        private static readonly AccessTools.FieldRef<Item, string> CodeRef = Backing<string>("Code");
        private static readonly AccessTools.FieldRef<Item, string> SubtypeRef = Backing<string>("SubtypeCode");
        private static readonly AccessTools.FieldRef<Item, List<Item.AffixReference>> AffixesRef =
            AccessTools.FieldRefAccess<Item, List<Item.AffixReference>>("_affixes");
        private static readonly AccessTools.FieldRef<ItemSlot, ItemSlot.ItemSlotChanged> SlotChangedRef =
            AccessTools.FieldRefAccess<ItemSlot, ItemSlot.ItemSlotChanged>("OnChangeEv");

        private static AccessTools.FieldRef<Item, T> Backing<T>(string property) =>
            AccessTools.FieldRefAccess<Item, T>($"<{property}>k__BackingField");

        public static Profile Profile => Game.ActiveProfile;

        public static bool IsReady
        {
            get
            {
                try { return Game.IsInitialized && Database.IsLoaded && Game.ActiveProfile != null; }
                catch (Exception) { return false; }
            }
        }

        // ---- Collections -------------------------------------------------------------------------

        public static IEnumerable<(string Label, ItemSlot Slot)> EquipmentSlots()
        {
            Equipment equipment = Profile.GetActiveEquipment();
            if (equipment == null) yield break;
            for (int i = 0; i < (int)ItemType._Count; i++)
            {
                ItemSlot slot = equipment.GetSlot((ItemType)i);
                if (slot != null) yield return (((ItemType)i).ToString(), slot);
            }
        }

        public static IEnumerable<ItemSlot> BackpackSlots() => Profile.Backpack;

        public static IEnumerable<ItemSlot> StashSlots(int stashIndex)
        {
            if (stashIndex < Profile.Stashes.MaxStashes && Profile.Stashes.TryGetStash(stashIndex, out StashData stash))
                return stash.GetAllSlots();
            return Enumerable.Empty<ItemSlot>();
        }

        public static IEnumerable<ItemSlot> LibrarySlots() => Profile.Library;

        /// <summary>Every item the player owns, including ones only referenced by other loadouts.</summary>
        public static IEnumerable<Item> AllPlayerItems() => Profile.PlayerItemRepo.GetAllItems().ToList();

        /// <summary>All slots that may hold a given item, used to notify listeners after an edit.</summary>
        private static IEnumerable<ItemSlot> AllSlots()
        {
            foreach (Equipment equipment in Profile.GetAllEquipments())
                foreach (ItemSlot slot in equipment) yield return slot;
            foreach (ItemSlot slot in Profile.Backpack) yield return slot;
            foreach (ItemSlot slot in Profile.Library) yield return slot;
            foreach (ItemSlot slot in Profile.Stashes.GetAllSlots()) yield return slot;
        }

        public static bool IsEquippedAnywhere(Item item) =>
            Profile.GetAllEquipments().Any(e => e.EnumerateItems().Contains(item));

        // ---- Edits -------------------------------------------------------------------------------

        public static void SetRarity(Item item, ItemRarity rarity) => RarityRef(item) = rarity;

        public static void SetTier(Item item, int tierIndex) =>
            TierRef(item) = TierId.FromIndex(Math.Max(0, Math.Min(TierId.Count - 1, tierIndex)));

        /// <summary>Changes the base (look, weapon, implicit class). Type and class follow the subtype.</summary>
        public static void SetSubtype(Item item, ItemSubtype subtype)
        {
            SubtypeRef(item) = subtype.Code;
            TypeRef(item) = subtype.ItemType;
            ClassRef(item) = subtype.ItemClass;
        }

        /// <summary>Turns a unique into a regular item that keeps its affixes and base.</summary>
        public static void MakeRegular(Item item)
        {
            if (!item.IsUnique) return;
            IsUniqueRef(item) = false;
            CodeRef(item) = Guid.NewGuid().ToString();
        }

        public static List<Item.AffixReference> Affixes(Item item) => AffixesRef(item);

        public static void SetAffix(Item item, int index, AffixCode code, int levels, bool enhanced) =>
            AffixesRef(item)[index] = new Item.AffixReference(code, levels, enhanced);

        public static void AddAffix(Item item, AffixCode code, int levels) =>
            AffixesRef(item).Add(new Item.AffixReference(code, levels));

        public static void RemoveAffix(Item item, int index) => AffixesRef(item).RemoveAt(index);

        /// <summary>
        /// Re-applies an edited item: every slot holding it fires its change event with (item, item), so
        /// the run's EquipmentAbilityTracker removes the old abilities and rebuilds them from the new affixes.
        /// </summary>
        public static void NotifyChanged(Item item)
        {
            foreach (ItemSlot slot in AllSlots())
            {
                if (slot.Item != item) continue;
                SlotChangedRef(slot)?.Invoke(item, item);
            }
        }

        // ---- Creation ----------------------------------------------------------------------------

        public static Item CreateItem(ItemSubtype subtype, ItemRarity rarity, int tierIndex, IEnumerable<Item.AffixReference> affixes)
        {
            TierId tier = TierId.FromIndex(Math.Max(0, Math.Min(TierId.Count - 1, tierIndex)));
            return new Item(Guid.NewGuid().ToString(), subtype.ItemType, subtype.ItemClass, rarity, tier, false,
                subtype.Code, "default", "default", affixes.Select(a => a.Clone()).ToList());
        }

        /// <summary>A regular copy with a fresh code (uniques are copied from their template).</summary>
        public static Item Duplicate(Item item)
        {
            Item copy = item.Clone();
            if (!copy.IsUnique) CodeRef(copy) = Guid.NewGuid().ToString();
            return copy;
        }

        public static bool GiveToBackpack(Item item) => Profile.Backpack.TryAdd(item);

        public static bool GiveToStash(Item item) =>
            Profile.Stashes.TryGetEmptySlot(out ItemSlot slot) && SetSlot(slot, item);

        private static bool SetSlot(ItemSlot slot, Item item)
        {
            slot.Set(item);
            item.WasOwnedByPlayer = true;
            return true;
        }

        public static void Delete(ItemSlot slot) => slot.Set(null, force: true);

        // ---- Persistence -------------------------------------------------------------------------

        public static void SaveGame()
        {
            var manager = (IGameManager)AccessTools.Field(typeof(Game), "gGameManager").GetValue(null);
            manager.SaveGameAsync().Forget();
        }
    }
}
