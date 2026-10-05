using System;
using System.Collections.Generic;
using System.Linq;
using Death.Items;
using UnityEngine;

namespace DMDItemEditor
{
    internal sealed partial class EditorWindow
    {
        private static readonly string[] RarityLabels = { "Broken", "Common", "Rare", "Epic", "Mythic", "Immortal" };
        private static readonly string[] TierLabels = { "T1", "T2", "T3", "T4", "T5" };
        private const int MaxPickerRows = 80;

        private readonly Dictionary<int, string> _levelBuffers = new Dictionary<int, string>();
        private string _bulkLevels = "100";
        private string _affixSearch = "";
        private string _newAffixLevels;
        private bool _subtypePickerOpen;
        private string _subtypeSearch = "";
        private Vector2 _pickerScroll, _affixPickerScroll;
        private bool _confirmDelete;
        private Item _bufferOwner;

        private void DrawEditorPanel()
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            if (_selected == null)
            {
                GUILayout.Label("Sélectionne un objet à gauche.");
                GUILayout.EndVertical();
                return;
            }
            if (_bufferOwner != _selected) { _levelBuffers.Clear(); _bufferOwner = _selected; _confirmDelete = false; }
            if (_newAffixLevels == null) _newAffixLevels = Plugin.DefaultAffixLevels.Value.ToString();

            Item item = _selected;
            bool equipped = ItemAccess.IsEquippedAnywhere(item);
            _editScroll = GUILayout.BeginScrollView(_editScroll, GUILayout.ExpandHeight(true));

            Color old = GUI.contentColor;
            GUI.contentColor = RarityColor(item.Rarity);
            GUILayout.Label(DisplayName(item), Styles.Title);
            GUI.contentColor = old;
            GUILayout.Label($"Type : {item.Type}   Classe : {item.Class}   Code : {item.Code}" + (equipped ? "   (équipé)" : ""));

            // Rarity and tier: the game indexes arrays with them, so they stay within the real ranges.
            GUILayout.BeginHorizontal();
            GUILayout.Label("Rareté", GUILayout.Width(70));
            int rarity = GUILayout.Toolbar((int)item.Rarity, RarityLabels);
            GUILayout.EndHorizontal();
            if (rarity != (int)item.Rarity) { ItemAccess.SetRarity(item, (ItemRarity)rarity); MarkDirty(); }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Tier", GUILayout.Width(70));
            int tier = GUILayout.Toolbar(item.Tier.Index, TierLabels);
            GUILayout.EndHorizontal();
            if (tier != item.Tier.Index) { ItemAccess.SetTier(item, tier); MarkDirty(); }

            DrawSubtypeRow(item, equipped);

            if (item.IsUnique)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Plugin.PersistUniqueEdits.Value
                    ? "Unique : les modifications sont gardées par le mod."
                    : "Unique : PersistUniqueEdits=false, le jeu restaurera le modèle au chargement.");
                if (GUILayout.Button("Convertir en objet normal", GUILayout.Width(200)))
                { ItemAccess.MakeRegular(item); MarkDirty(); Report("Objet converti en objet normal."); }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);
            DrawAffixes(item);
            GUILayout.Space(6);
            DrawAffixPicker(item);
            GUILayout.Space(6);
            DrawItemActions(item);

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawSubtypeRow(Item item, bool equipped)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Base", GUILayout.Width(70));
            GUILayout.Label(item.SubtypeCode);
            if (GUILayout.Button(_subtypePickerOpen ? "Annuler" : "Changer la base", GUILayout.Width(140)))
                _subtypePickerOpen = !_subtypePickerOpen;
            GUILayout.EndHorizontal();
            if (!_subtypePickerOpen) return;

            if (equipped) GUILayout.Label("Objet équipé : seules les bases du même emplacement sont proposées.");
            ItemSubtype chosen = DrawSubtypePicker(equipped ? item.Type : (ItemType?)null);
            if (chosen == null) return;
            ItemAccess.SetSubtype(item, chosen);
            _subtypePickerOpen = false;
            MarkDirty();
            Report("Base changée : " + chosen.Code + " (rouvre l'inventaire pour l'icône).");
        }

        /// <summary>Searchable subtype list; returns the clicked subtype or null.</summary>
        private ItemSubtype DrawSubtypePicker(ItemType? onlyType)
        {
            ItemSubtype picked = null;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Recherche", GUILayout.Width(70));
            _subtypeSearch = GUILayout.TextField(_subtypeSearch);
            GUILayout.EndHorizontal();
            string q = _subtypeSearch.Trim().ToLowerInvariant();
            _pickerScroll = GUILayout.BeginScrollView(_pickerScroll, GUILayout.Height(160));
            int shown = 0;
            foreach (ItemSubtype st in _catalog.Subtypes)
            {
                if (onlyType.HasValue && st.ItemType != onlyType.Value) continue;
                string label = st.ItemType + " : " + st.Code + (st.IsUnique ? " (unique)" : "");
                if (q.Length > 0 && !label.ToLowerInvariant().Contains(q)) continue;
                if (GUILayout.Button(label, Styles.LeftButton)) picked = st;
                if (++shown >= MaxPickerRows) { GUILayout.Label("… affine la recherche"); break; }
            }
            GUILayout.EndScrollView();
            return picked;
        }

        private void DrawAffixes(Item item)
        {
            List<Item.AffixReference> affixes = ItemAccess.Affixes(item);
            GUILayout.Label($"Affixes ({affixes.Count})", Styles.Title);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Niveaux pour tous", GUILayout.Width(120));
            _bulkLevels = GUILayout.TextField(_bulkLevels, GUILayout.Width(90));
            if (GUILayout.Button("Appliquer", GUILayout.Width(90)) && int.TryParse(_bulkLevels, out int bulk))
            {
                for (int i = 0; i < affixes.Count; i++)
                    ItemAccess.SetAffix(item, i, affixes[i].Code, bulk, affixes[i].Enhanced);
                _levelBuffers.Clear();
                MarkDirty();
            }
            if (GUILayout.Button("Tout supprimer", GUILayout.Width(120))) { affixes.Clear(); _levelBuffers.Clear(); MarkDirty(); }
            GUILayout.EndHorizontal();

            for (int i = 0; i < affixes.Count; i++)
            {
                Item.AffixReference a = affixes[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(_catalog.AffixName(a.Code) + "  [" + a.Code + "]", GUILayout.Width(330));

                if (!_levelBuffers.TryGetValue(i, out string buffer)) buffer = a.Levels.ToString();
                string edited = GUILayout.TextField(buffer, GUILayout.Width(90));
                _levelBuffers[i] = edited;
                if (edited != buffer && int.TryParse(edited, out int levels) && levels != a.Levels)
                { ItemAccess.SetAffix(item, i, a.Code, levels, a.Enhanced); MarkDirty(); }

                bool enhanced = GUILayout.Toggle(a.Enhanced, "Amélioré", GUILayout.Width(90));
                if (enhanced != a.Enhanced) { ItemAccess.SetAffix(item, i, a.Code, a.Levels, enhanced); MarkDirty(); }

                bool removed = GUILayout.Button("✕", GUILayout.Width(28));
                GUILayout.EndHorizontal();
                if (removed) { ItemAccess.RemoveAffix(item, i); _levelBuffers.Clear(); MarkDirty(); break; }
            }
        }

        private void DrawAffixPicker(Item item)
        {
            GUILayout.Label("Ajouter un affixe (tous les affixes du jeu, sans restriction de type ou de rareté)", Styles.Title);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Recherche", GUILayout.Width(70));
            _affixSearch = GUILayout.TextField(_affixSearch);
            GUILayout.Label("Niveaux", GUILayout.Width(55));
            _newAffixLevels = GUILayout.TextField(_newAffixLevels, GUILayout.Width(70));
            GUILayout.EndHorizontal();

            string q = _affixSearch.Trim().ToLowerInvariant();
            _affixPickerScroll = GUILayout.BeginScrollView(_affixPickerScroll, GUILayout.Height(180));
            int shown = 0;
            foreach (Catalog.AffixEntry e in _catalog.Affixes)
            {
                if (q.Length > 0 && !e.Search.Contains(q)) continue;
                string label = e.Name + "  [" + e.Code + "]" + (e.Affix.IsSpecial ? "  ★" : "") + "  · " + e.Affix.StatGroup;
                if (GUILayout.Button(label, Styles.LeftButton))
                {
                    int levels = int.TryParse(_newAffixLevels, out int l) ? l : Plugin.DefaultAffixLevels.Value;
                    ItemAccess.AddAffix(item, e.Affix.Code, levels);
                    MarkDirty();
                    Report("Ajouté : " + e.Name);
                }
                if (++shown >= MaxPickerRows) { GUILayout.Label("… affine la recherche"); break; }
            }
            GUILayout.EndScrollView();
        }

        private void DrawItemActions(Item item)
        {
            if (_source == Source.Create) return; // the create panel has its own buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Dupliquer dans le sac"))
                Report(ItemAccess.GiveToBackpack(ItemAccess.Duplicate(item)) ? "Copie ajoutée au sac." : "Sac plein.");
            if (GUILayout.Button("Dupliquer dans le coffre"))
                Report(ItemAccess.GiveToStash(ItemAccess.Duplicate(item)) ? "Copie ajoutée au coffre." : "Coffre plein.");
            if (_selectedSlot != null && !_selectedSlot.IsReadonly)
            {
                if (!_confirmDelete && GUILayout.Button("Supprimer")) _confirmDelete = true;
                else if (_confirmDelete && GUILayout.Button("Confirmer la suppression"))
                {
                    ItemAccess.Delete(_selectedSlot);
                    Report("Objet supprimé.");
                    Select(null, null);
                }
            }
            GUILayout.EndHorizontal();
        }
    }
}
