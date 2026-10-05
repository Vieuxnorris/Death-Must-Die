using System;
using System.Collections.Generic;
using System.Linq;
using Death.Data;
using Death.Items;
using HarmonyLib;
using UnityEngine;

namespace DMDItemEditor
{
    /// <summary>Searchable lists of everything an item can be made of, read from the loaded Database.</summary>
    internal sealed class Catalog
    {
        public sealed class AffixEntry
        {
            public ItemAffix Affix;
            public string Code;
            public string Name;
            public string Search;
        }

        public List<AffixEntry> Affixes { get; private set; } = new List<AffixEntry>();
        public List<ItemSubtype> Subtypes { get; private set; } = new List<ItemSubtype>();
        public List<UniqueItemTemplate> Uniques { get; private set; } = new List<UniqueItemTemplate>();
        public bool IsBuilt { get; private set; }

        private Dictionary<string, AffixEntry> _byCode = new Dictionary<string, AffixEntry>();

        public void Build()
        {
            Dictionary<string, string> names = ReadAffixNames();
            Affixes = Database.ItemAffixes.All
                .Select(a =>
                {
                    string code = a.Code.ToString();
                    names.TryGetValue(code, out string name);
                    name = string.IsNullOrEmpty(name) ? code : name;
                    return new AffixEntry
                    {
                        Affix = a, Code = code, Name = name,
                        Search = (code + " " + name + " " + a.AffixGroup + " " + a.StatGroup).ToLowerInvariant(),
                    };
                })
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            _byCode = Affixes.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.First());
            Subtypes = Database.ItemSubtypes.All.OrderBy(s => s.ItemType).ThenBy(s => s.Code).ToList();
            Uniques = Database.ItemUniques.All.OrderBy(u => u.Item.Type).ThenBy(u => u.Item.Code).ToList();
            IsBuilt = true;
            Plugin.Log.LogInfo($"Catalog: {Affixes.Count} affixes, {Subtypes.Count} subtypes, {Uniques.Count} uniques.");
        }

        public string AffixName(AffixCode code) =>
            _byCode.TryGetValue(code.ToString(), out AffixEntry e) ? e.Name : code.ToString();

        public AffixEntry FindAffix(AffixCode code) =>
            _byCode.TryGetValue(code.ToString(), out AffixEntry e) ? e : null;

        /// <summary>
        /// Affix display names come from the designers' CSV (column 2, e.g. "Health Regen"), which the
        /// Database keeps as a TextAsset. Missing names fall back to the affix code.
        /// </summary>
        private static Dictionary<string, string> ReadAffixNames()
        {
            var names = new Dictionary<string, string>();
            try
            {
                object db = AccessTools.Field(typeof(Database), "gCurrent").GetValue(null);
                var asset = AccessTools.Field(typeof(Database), "_itemAffixes").GetValue(db) as TextAsset;
                if (asset == null) return names;
                foreach (string line in asset.text.Split('\n'))
                {
                    string[] cols = line.Split(';');
                    if (cols.Length < 3 || cols[1].Length == 0 || cols[1] == "code" || cols[1].StartsWith("#")) continue;
                    string name = cols[2].Trim();
                    if (name.Length > 0 && !names.ContainsKey(cols[1])) names[cols[1]] = name;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not read affix names: " + e.Message);
            }
            return names;
        }
    }
}
