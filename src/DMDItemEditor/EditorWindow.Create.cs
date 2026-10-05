using System.Linq;
using Death.Items;
using UnityEngine;

namespace DMDItemEditor
{
    internal sealed partial class EditorWindow
    {
        private Item _draft;
        private string _uniqueSearch = "";
        private Vector2 _createScroll, _uniqueScroll;

        /// <summary>
        /// Left column of the "Créer" tab. A new item starts as a draft that lives outside the inventory and
        /// is edited with the normal editor on the right, then handed to the backpack or the stash.
        /// </summary>
        private void DrawCreatePanel()
        {
            GUILayout.BeginVertical(GUILayout.Width(330));
            _createScroll = GUILayout.BeginScrollView(_createScroll, GUILayout.ExpandHeight(true));

            GUILayout.Label("Nouvel objet : choisis une base", Styles.Title);
            ItemSubtype picked = DrawSubtypePicker(null);
            if (picked != null)
            {
                var keep = _draft != null ? ItemAccess.Affixes(_draft).ToList() : Enumerable.Empty<Item.AffixReference>();
                _draft = ItemAccess.CreateItem(picked, ItemRarity.Mythic, TierId.Count - 1, keep);
                Select(_draft, null);
            }

            if (_draft != null)
            {
                GUILayout.Label("Brouillon : " + DisplayName(_draft));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Ajouter au sac")) Give(ItemAccess.GiveToBackpack, "sac");
                if (GUILayout.Button("Ajouter au coffre")) Give(ItemAccess.GiveToStash, "coffre");
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            GUILayout.Label("Objets uniques", Styles.Title);
            _uniqueSearch = GUILayout.TextField(_uniqueSearch);
            string q = _uniqueSearch.Trim().ToLowerInvariant();
            _uniqueScroll = GUILayout.BeginScrollView(_uniqueScroll, GUILayout.Height(260));
            int shown = 0;
            foreach (UniqueItemTemplate u in _catalog.Uniques)
            {
                string label = u.Item.Type + " : " + u.Item.Code + (u.IsHidden ? " (caché)" : "");
                if (q.Length > 0 && !label.ToLowerInvariant().Contains(q)) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(label);
                if (GUILayout.Button("Éditer", GUILayout.Width(60))) { _draft = u.Item.Clone(); Select(_draft, null); }
                if (GUILayout.Button("Donner", GUILayout.Width(65)))
                    Report(ItemAccess.GiveToBackpack(u.Item.Clone()) ? u.Item.Code + " ajouté au sac." : "Sac plein.");
                GUILayout.EndHorizontal();
                if (++shown >= MaxPickerRows) { GUILayout.Label("… affine la recherche"); break; }
            }
            GUILayout.EndScrollView();

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void Give(System.Func<Item, bool> give, string where)
        {
            // Hand over a copy so the draft can be given again or tweaked further.
            Item copy = ItemAccess.Duplicate(_draft);
            Report(give(copy) ? "Objet ajouté au " + where + "." : "Plus de place dans le " + where + ".");
        }
    }
}
