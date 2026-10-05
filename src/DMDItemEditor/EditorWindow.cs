using System;
using System.Collections.Generic;
using System.Linq;
using Death;
using Death.Items;
using UnityEngine;

namespace DMDItemEditor
{
    /// <summary>IMGUI window: pick an item on the left, edit everything about it on the right.</summary>
    internal sealed partial class EditorWindow
    {
        private enum Source { Equipped, Backpack, Stash1, Stash2, Stash3, Stash4, Library, AllOwned, Create, Loot }

        private static readonly string[] SourceLabels =
            { "Équipé", "Sac", "Coffre 1", "Coffre 2", "Coffre 3", "Coffre 4", "Bibliothèque", "Tous", "Créer", "Butin" };

        private static readonly Color[] RarityColors =
        {
            new Color(0.6f, 0.6f, 0.6f), Color.white, new Color(0.4f, 0.6f, 1f),
            new Color(0.75f, 0.45f, 1f), new Color(1f, 0.6f, 0.2f), new Color(1f, 0.3f, 0.3f),
        };

        private readonly Catalog _catalog = new Catalog();
        private Rect _rect = new Rect(60, 60, 1100, 720);
        private bool _visible;
        private bool _controlsVoted;
        private Source _source = Source.Backpack;
        private Vector2 _listScroll, _editScroll;
        private Item _selected;
        private ItemSlot _selectedSlot;
        private string _status = "";
        private float _statusTime;
        private bool _dirty;
        private float _dirtySince;
        private float _lastToggle = -1f;

        public void Toggle()
        {
            if (Time.unscaledTime - _lastToggle < 0.2f) return; // Update and OnGUI may both see the same press
            _lastToggle = Time.unscaledTime;
            _visible = !_visible;
            Plugin.Log.LogInfo(_visible ? "Editor opened." : "Editor closed.");
            if (_visible && !_catalog.IsBuilt && ItemAccess.IsReady) _catalog.Build();
            SetControlsBlocked(_visible);
        }

        private void SetControlsBlocked(bool blocked)
        {
            if (blocked == _controlsVoted) return;
            try { Game.VoteControlsDisabled(blocked); _controlsVoted = blocked; }
            catch (Exception e) { Plugin.Log.LogWarning("Could not toggle game controls: " + e.Message); }
        }

        /// <summary>Equipped items are re-applied a moment after the last edit, not on every keystroke.</summary>
        public void Update()
        {
            if (_dirty && Time.unscaledTime - _dirtySince > 0.3f)
            {
                _dirty = false;
                if (_selected != null && ItemAccess.IsReady)
                {
                    try { ItemAccess.NotifyChanged(_selected); }
                    catch (Exception e) { Report("Erreur de mise à jour : " + e.Message); Plugin.Log.LogError(e); }
                }
            }
        }

        private void MarkDirty()
        {
            _dirty = true;
            _dirtySince = Time.unscaledTime;
        }

        private void Report(string message)
        {
            _status = message;
            _statusTime = Time.unscaledTime;
            Plugin.Log.LogInfo(message);
        }

        public void OnGUI()
        {
            if (!_visible) return;
            float scale = Plugin.UiScale.Value;
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            _rect = GUILayout.Window(0x444D44, _rect, DrawWindow, "Death Must Die : éditeur d'objets  (" + Plugin.ToggleKey.Value + ")", Styles.Window);
            GUI.matrix = previous;
        }

        private void DrawWindow(int id)
        {
            if (!ItemAccess.IsReady)
            {
                GUILayout.Label("Charge une partie (menu principal passé) puis rouvre l'éditeur.");
                if (GUILayout.Button("Fermer")) Toggle();
                GUI.DragWindow();
                return;
            }
            if (!_catalog.IsBuilt) _catalog.Build();

            try
            {
                int src = GUILayout.Toolbar((int)_source, SourceLabels);
                if (src != (int)_source) { _source = (Source)src; Select(null, null); }

                if (_source == Source.Loot)
                {
                    DrawLootPanel();
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    if (_source == Source.Create) DrawCreatePanel();
                    else DrawItemList();
                    GUILayout.Space(8);
                    DrawEditorPanel();
                    GUILayout.EndHorizontal();
                }

                DrawFooter();
            }
            catch (Exception e)
            {
                // IMGUI layout mismatches after an exception are harmless; log the real cause once.
                if (!(e is ArgumentException)) { Report("Erreur : " + e.Message); Plugin.Log.LogError(e); }
            }
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        private void Select(Item item, ItemSlot slot)
        {
            if (_dirty && _selected != null) { _dirty = false; ItemAccess.NotifyChanged(_selected); }
            _selected = item;
            _selectedSlot = slot;
            _subtypePickerOpen = false;
        }

        // ---- Left: item list ----------------------------------------------------------------------

        private void DrawItemList()
        {
            GUILayout.BeginVertical(GUILayout.Width(330));
            _listScroll = GUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));
            switch (_source)
            {
                case Source.Equipped:
                    foreach (var (label, slot) in ItemAccess.EquipmentSlots()) DrawSlotButton(slot, label);
                    break;
                case Source.Backpack: DrawSlots(ItemAccess.BackpackSlots()); break;
                case Source.Stash1: DrawSlots(ItemAccess.StashSlots(0)); break;
                case Source.Stash2: DrawSlots(ItemAccess.StashSlots(1)); break;
                case Source.Stash3: DrawSlots(ItemAccess.StashSlots(2)); break;
                case Source.Stash4: DrawSlots(ItemAccess.StashSlots(3)); break;
                case Source.Library: DrawSlots(ItemAccess.LibrarySlots()); break;
                case Source.AllOwned:
                    foreach (Item item in ItemAccess.AllPlayerItems()) DrawItemButton(item, null, null);
                    break;
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawSlots(IEnumerable<ItemSlot> slots)
        {
            int count = 0;
            foreach (ItemSlot slot in slots)
            {
                if (slot.IsEmpty) continue;
                DrawSlotButton(slot, null);
                count++;
            }
            if (count == 0) GUILayout.Label("(vide ou verrouillé)");
        }

        private void DrawSlotButton(ItemSlot slot, string label)
        {
            if (slot.IsEmpty) { GUILayout.Label(label + " : (vide)"); return; }
            DrawItemButton(slot.Item, slot, label);
        }

        private void DrawItemButton(Item item, ItemSlot slot, string label)
        {
            Color old = GUI.contentColor;
            GUI.contentColor = RarityColor(item.Rarity);
            string text = (label != null ? label + " : " : "") + DisplayName(item) + "  [" + item.Affixes.Count + " affixes]";
            if (item == _selected) text = "▶ " + text;
            if (GUILayout.Button(text, Styles.LeftButton)) Select(item, slot);
            GUI.contentColor = old;
        }

        internal static Color RarityColor(ItemRarity rarity)
        {
            int i = (int)rarity;
            return i >= 0 && i < RarityColors.Length ? RarityColors[i] : Color.white;
        }

        internal static string DisplayName(Item item) =>
            (item.IsUnique ? "★ " + item.Code : item.SubtypeCode) + " (" + item.Rarity + ", T" + item.Tier.Id + ")";

        private void DrawFooter()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Sauvegarder la partie", GUILayout.Width(200)))
            {
                try { ItemAccess.SaveGame(); Report("Sauvegarde demandée."); }
                catch (Exception e) { Report("Échec de la sauvegarde : " + e.Message); Plugin.Log.LogError(e); }
            }
            GUILayout.Label(Time.unscaledTime - _statusTime < 8f ? _status : "");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Fermer", GUILayout.Width(90))) Toggle();
            GUILayout.EndHorizontal();
        }

        private static class Styles
        {
            private static GUIStyle _leftButton, _title, _window;

            /// <summary>The default window skin is see-through over the game; use an opaque dark background.</summary>
            public static GUIStyle Window
            {
                get
                {
                    if (_window != null) return _window;
                    var bg = new Texture2D(1, 1);
                    bg.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.11f, 0.97f));
                    bg.Apply();
                    _window = new GUIStyle(GUI.skin.window);
                    _window.normal.background = bg;
                    _window.onNormal.background = bg;
                    return _window;
                }
            }

            public static GUIStyle Title =>
                _title ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 15 };
            public static GUIStyle LeftButton =>
                _leftButton ??= new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };
        }
    }
}
