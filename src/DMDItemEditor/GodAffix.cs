using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using Death.Data;
using Death.Data.Tables;
using Death.Items;
using Death.UserInterface.Localization;
using HarmonyLib;

namespace DMDItemEditor
{
    /// <summary>
    /// "GOD": a real affix added to the game's affix table. It carries the abilities of every positive
    /// plain-stat affix (damage, speed, crit, health, armor, area, luck...), so N levels of GOD grant N
    /// levels of all of them at once. Items carrying it need the mod to load: without it the game drops them.
    /// </summary>
    internal static class GodAffix
    {
        public const string Code = "GOD";
        public static readonly AffixCode AffixCode = AffixCode.FromString(Code);

        public static ConfigEntry<int> DefaultLevels;
        public static ConfigEntry<bool> IncludeCounts;

        /// <summary>Stats that hurt the player, break the UI or only matter to designers.</summary>
        private static readonly HashSet<string> Excluded = new HashSet<string>
        {
            "msp", "enemhp%", "-thr%_e", "215", "msrarm",
            "aslot", "dslot", "sslot", "cslot", "pslot", "suslot",
        };

        /// <summary>Projectile, pierce, bounce and summon counts: very strong, and heavy on performance.</summary>
        private static readonly HashSet<string> Counts = new HashSet<string>
        {
            "acnt", "dcnt", "scnt", "ccnt", "pcnt", "cnt", "sucnt", "sumcnt", "apnc", "abnc",
        };

        /// <summary>Lines shown in the tooltip; the rest is summarised.</summary>
        private static readonly string[] Featured = { "d%", "as%", "cc%", "cr%", "h", "a", "ms%", "are%" };

        private struct Segment { public string Code; public int Start; public int Count; }
        private static readonly List<Segment> Segments = new List<Segment>();

        public static void Bind(ConfigFile config)
        {
            DefaultLevels = config.Bind("GodAffix", "DefaultLevels", 500,
                "Levels of the GOD affix added from the editor or to drops. Every bundled stat gets this many levels " +
                "(500 is about +200% damage, +200% attack speed, +35% crit, +500 life).");
            IncludeCounts = config.Bind("GodAffix", "IncludeProjectileCounts", false,
                "Also bundle projectile, pierce, bounce and summon counts (can make the game lag). Restart to apply.");
        }

        /// <summary>False for penalties ("x-" variants, enemy health, movement penalty) and designer-only stats.</summary>
        public static bool IsBeneficial(string code) => !code.EndsWith("-") && !Excluded.Contains(code);

        public static bool IsRegistered => Database.IsLoaded && Database.ItemAffixes.Contains(AffixCode);

        public static void Register(ItemAffixesTable table)
        {
            if (table.Contains(AffixCode)) return;
            ItemAffix template = table.Get(AffixCode.Damage);
            var abilities = new List<ItemAffix.Ability>();
            Segments.Clear();
            foreach (ItemAffix a in table.All.ToList())
            {
                string code = a.Code.ToString();
                if (a.IsSpecial || a.StatGroup != template.StatGroup || a.SkillSlot != template.SkillSlot) continue;
                if (!IsBeneficial(code) || a.Abilities == null || a.Abilities.Length == 0) continue;
                if (!IncludeCounts.Value && Counts.Contains(code)) continue;
                Segments.Add(new Segment { Code = code, Start = abilities.Count, Count = a.Abilities.Length });
                abilities.AddRange(a.Abilities);
            }

            var god = new ItemAffix(table.Count, AffixCode, "GOD", template.StatGroup, template.Tiers,
                template.RelatedStatuses, template.RelatedKeywords, template.SkillSlot, abilities.ToArray(),
                template.GoldPerValuePoint, 0f, true, ItemRarity.Broken, false);
            table.Add(god);
            Plugin.Log.LogInfo($"GOD affix registered: {Segments.Count} stat affixes, {abilities.Count} abilities.");
        }

        /// <summary>
        /// Builds the tooltip line exactly like GUI_ItemInfo.AddRegularAffix (stats per ability, then the
        /// localization call), so the preview shows what the game's own item tooltip will display.
        /// </summary>
        public static string PreviewTooltip(int levels, TierId tier)
        {
            ItemAffix god = Database.ItemAffixes.Get(AffixCode);
            object[] args = new object[god.Abilities.Length];
            for (int i = 0; i < args.Length; i++)
            {
                var arg = new Death.UserInterface.Descriptions.LocArg_Stats { ColorizeSign = true };
                Death.Run.Core.Stats stats = god.Abilities[i].GenerateStatsForLevel(levels, tier);
                for (int j = 0; j < arg.Stats.Count; j++)
                    arg.Stats[j] = stats.Get((Death.Run.Core.StatId)j);
                args[i] = arg;
            }
            return LocalizationManager.GetAffixDescr(Code, args);
        }

        /// <summary>Tooltip text: the main stats with their real values, then a count of the other bonuses.</summary>
        public static string Describe(object[] args)
        {
            var parts = new List<string>();
            foreach (string code in Featured)
            {
                int i = Segments.FindIndex(s => s.Code == code);
                if (i < 0) continue;
                Segment s = Segments[i];
                if (args == null || s.Start + s.Count > args.Length) continue;
                parts.Add(LocalizationManager.GetAffixDescr(code, args.Skip(s.Start).Take(s.Count).ToArray()));
            }
            int others = Segments.Count - parts.Count;
            return "<color=#ff3030><b>GOD</b></color>  " + string.Join(", ", parts) + $"  <color=#ff3030>+ {others} autres bonus</color>";
        }
    }

    [HarmonyPatch(typeof(Database), nameof(Database.Init))]
    internal static class Patch_RegisterGodAffix
    {
        private static void Postfix(Database __instance)
        {
            try { GodAffix.Register(__instance.ItemAffixesTable); }
            catch (System.Exception e) { Plugin.Log.LogError("Could not register the GOD affix: " + e); }
        }
    }

    [HarmonyPatch(typeof(LocalizationManager), nameof(LocalizationManager.GetAffixDescr))]
    internal static class Patch_GodAffixText
    {
        private static bool Prefix(string key, object[] args, ref string __result)
        {
            if (key != GodAffix.Code) return true;
            __result = GodAffix.Describe(args);
            return false;
        }
    }
}
