using System.Globalization;
using Death.Items;
using UnityEngine;

namespace DMDItemEditor
{
    internal sealed partial class EditorWindow
    {
        private static readonly string[] TierChoices = { "Selon l'acte", "T1", "T2", "T3", "T4", "T5" };
        private string _dropMultText, _uniqueText, _simulation, _godChanceText, _godLevelText, _godPreview;

        /// <summary>"Butin" tab: drop rate and drop quality for monster loot. Changes apply to the next kills.</summary>
        private void DrawLootPanel()
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Butin des monstres", Styles.Title);
            GUILayout.Label("Les réglages s'appliquent aux prochains monstres tués et sont gardés entre les parties.");

            LootSettings.Enabled.Value = GUILayout.Toggle(LootSettings.Enabled.Value, " Activer les réglages de butin (sinon : jeu normal)");
            GUI.enabled = LootSettings.Enabled.Value;

            GUILayout.Space(8);
            GUILayout.Label("Taux de drop", Styles.Title);
            LootSettings.AlwaysDrop.Value = GUILayout.Toggle(LootSettings.AlwaysDrop.Value, " Chaque monstre drop à coup sûr");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Multiplicateur de chance de drop", GUILayout.Width(260));
            _dropMultText ??= LootSettings.DropChanceMultiplier.Value.ToString(CultureInfo.InvariantCulture);
            _dropMultText = GUILayout.TextField(_dropMultText, GUILayout.Width(80));
            if (float.TryParse(_dropMultText.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float mult) && mult >= 0f)
                LootSettings.DropChanceMultiplier.Value = mult;
            GUILayout.Label("x  (1 = normal)");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Objets par drop", GUILayout.Width(260));
            GUILayout.Label("x" + LootSettings.ItemCountMultiplier.Value, GUILayout.Width(40));
            LootSettings.ItemCountMultiplier.Value = Mathf.RoundToInt(
                GUILayout.HorizontalSlider(LootSettings.ItemCountMultiplier.Value, 1, 20, GUILayout.Width(300)));
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label("Qualité du drop", Styles.Title);
            GUILayout.Label("Raretés autorisées (aucune cochée = probabilités normales ; plusieurs = tirage égal entre elles) :");
            GUILayout.BeginHorizontal();
            int mask = LootSettings.RarityMask.Value;
            for (int i = 0; i < (int)ItemRarity._Count; i++)
            {
                Color old = GUI.contentColor;
                GUI.contentColor = RarityColor((ItemRarity)i);
                bool on = GUILayout.Toggle((mask & (1 << i)) != 0, " " + RarityLabels[i], GUILayout.Width(110));
                GUI.contentColor = old;
                mask = on ? mask | (1 << i) : mask & ~(1 << i);
            }
            GUILayout.EndHorizontal();
            LootSettings.RarityMask.Value = mask;
            GUILayout.Label("Les uniques du jeu sont surtout Epic et Mythic. Les uniques Immortal sont réservés aux événements : " +
                            "un tirage Immortal donne un Mythic. Le tier forcé ne s'applique pas aux uniques (T1 à T3 selon l'acte).");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Tier des objets", GUILayout.Width(260));
            LootSettings.ForcedTier.Value = GUILayout.Toolbar(LootSettings.ForcedTier.Value, TierChoices);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Chance d'objet unique (%)", GUILayout.Width(260));
            _uniqueText ??= LootSettings.UniqueChancePercent.Value < 0 ? "" : LootSettings.UniqueChancePercent.Value.ToString(CultureInfo.InvariantCulture);
            _uniqueText = GUILayout.TextField(_uniqueText, GUILayout.Width(80));
            if (_uniqueText.Trim().Length == 0) LootSettings.UniqueChancePercent.Value = -1f;
            else if (float.TryParse(_uniqueText.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float u))
                LootSettings.UniqueChancePercent.Value = Mathf.Clamp(u, 0f, 100f);
            GUILayout.Label("(vide = normal)");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Nombre d'affixes (objets normaux)", GUILayout.Width(260));
            int count = LootSettings.AffixCount.Value;
            GUILayout.Label(count == 0 ? "normal" : count.ToString(), GUILayout.Width(50));
            LootSettings.AffixCount.Value = Mathf.RoundToInt(GUILayout.HorizontalSlider(count, 0, 30, GUILayout.Width(300)));
            GUILayout.Label("  ajoutés au maximum de leur valeur");
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            Color oldColor = GUI.contentColor;
            GUI.contentColor = new Color(1f, 0.25f, 0.25f);
            GUILayout.Label("★ GOD affix", Styles.Title, GUILayout.Width(260));
            GUI.contentColor = oldColor;
            GUILayout.Label(GodAffix.IsRegistered ? "cumule les bonus de tous les affixes de stats du jeu" : "(non chargé)");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Chance qu'un drop ait le GOD affix (%)", GUILayout.Width(260));
            _godChanceText ??= LootSettings.GodChancePercent.Value.ToString(CultureInfo.InvariantCulture);
            _godChanceText = GUILayout.TextField(_godChanceText, GUILayout.Width(80));
            if (float.TryParse(_godChanceText.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float g))
                LootSettings.GodChancePercent.Value = Mathf.Clamp(g, 0f, 100f);
            GUILayout.EndHorizontal();

            GUI.enabled = true; // the GOD level is also used by the editor's "Ajouter GOD" button
            GUILayout.BeginHorizontal();
            GUILayout.Label("Niveau du GOD affix", GUILayout.Width(260));
            _godLevelText ??= GodAffix.DefaultLevels.Value.ToString();
            _godLevelText = GUILayout.TextField(_godLevelText, GUILayout.Width(80));
            if (int.TryParse(_godLevelText, out int gl) && gl > 0) GodAffix.DefaultLevels.Value = gl;
            GUILayout.Label("(chaque stat reçoit ce nombre de niveaux)");
            if (GodAffix.IsRegistered && GUILayout.Button("Aperçu de l'infobulle", GUILayout.Width(160)))
            {
                try { _godPreview = GodAffix.PreviewTooltip(GodAffix.DefaultLevels.Value, TierId.FromIndex(TierId.Count - 1)); }
                catch (System.Exception e) { _godPreview = "Erreur : " + e.Message; Plugin.Log.LogError(e); }
                Plugin.Log.LogInfo("GOD tooltip preview: " + _godPreview);
            }
            GUILayout.EndHorizontal();
            // <nobr> is a TextMeshPro tag the game's tooltip understands; IMGUI would print it.
            if (!string.IsNullOrEmpty(_godPreview))
                GUILayout.Label(_godPreview.Replace("<nobr>", "").Replace("</nobr>", ""), Styles.Wrap);

            GUILayout.Space(10);
            GUILayout.Label("Vérifier sans combattre", Styles.Title);
            if (GUILayout.Button("Simuler 200 monstres tués avec ces réglages", GUILayout.Width(360)))
            {
                try { _simulation = LootSimulator.Run(200); Plugin.Log.LogInfo("Simulation: " + _simulation); }
                catch (System.Exception e) { _simulation = "Erreur : " + e.Message; Plugin.Log.LogError(e); }
            }
            if (!string.IsNullOrEmpty(_simulation)) GUILayout.Label(_simulation);

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Remettre les réglages de butin par défaut", GUILayout.Width(320)))
            {
                LootSettings.DropChanceMultiplier.Value = 1f;
                LootSettings.AlwaysDrop.Value = false;
                LootSettings.ItemCountMultiplier.Value = 1;
                LootSettings.RarityMask.Value = 0;
                LootSettings.ForcedTier.Value = 0;
                LootSettings.UniqueChancePercent.Value = -1f;
                LootSettings.AffixCount.Value = 0;
                LootSettings.GodChancePercent.Value = 0f;
                _dropMultText = null;
                _uniqueText = null;
                _godChanceText = null;
            }
            GUILayout.EndVertical();
        }
    }
}
