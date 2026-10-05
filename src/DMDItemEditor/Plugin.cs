using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace DMDItemEditor
{
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "vieuxnorris.dmd.itemeditor";
        public const string Name = "DMD Item Editor";
        public const string Version = "0.2.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<float> UiScale;
        internal static ConfigEntry<bool> PersistUniqueEdits;
        internal static ConfigEntry<int> DefaultAffixLevels;

        private EditorWindow _window;

        private void Awake()
        {
            Log = Logger;
            ToggleKey = Config.Bind("General", "ToggleKey", new KeyboardShortcut(KeyCode.F8),
                "Opens or closes the item editor.");
            UiScale = Config.Bind("General", "UiScale", 1f,
                new ConfigDescription("Editor window scale.", new AcceptableValueRange<float>(0.5f, 3f)));
            PersistUniqueEdits = Config.Bind("Items", "PersistUniqueEdits", true,
                "Keep edited affixes, rarity and tier of unique items when the save is loaded. " +
                "When false, uniques are rebuilt from their template like in the vanilla game.");
            DefaultAffixLevels = Config.Bind("Items", "DefaultAffixLevels", 10,
                "Levels given to an affix when it is added from the editor.");

            LootSettings.Bind(Config);

            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            _window = new EditorWindow();
            Log.LogInfo($"{Name} {Version} loaded. Press {ToggleKey.Value} in game.");
        }

        private bool _firstUpdateLogged;

        private void Update()
        {
            if (!_firstUpdateLogged) { _firstUpdateLogged = true; Log.LogInfo("Update loop running."); }
            if (ToggleKey.Value.IsDown()) _window.Toggle();
            _window.Update();
        }

        private void OnGUI() => _window.OnGUI();
    }
}
