# Death Must Die : plan de modding (éditeur d'objets)

Reconnaissance faite le 2026-10-05 sur la machine de Julien.

## Le jeu

| Élément | Valeur |
|---|---|
| Installation | `G:\Steam\steamapps\common\Death Must Die` (Steam appid 2334730, buildid 21650210) |
| Moteur | Unity 2021.3.11f1, 64 bits |
| Backend | **Mono** (dossier `MonoBleedingEdge`, assemblies .NET dans `Death Must Die_Data\Managed`) |
| Code du jeu | `Managed\Death.dll` (2,3 Mo), plus `Claw.*.dll`, `Death.*.dll`, `ZloediUtils.dll` |
| Anti-triche | Aucun détecté (jeu solo) |
| Loader installé | Aucun (pas de BepInEx ni MelonLoader) |
| Sauvegardes | `%USERPROFILE%\AppData\LocalLow\Realm Archive\Death Must Die\Saves\Slot_0.sav` (+ `Backups\`) |
| Logs | `...\Realm Archive\Death Must Die\Player.log` |
| Dépôt GitHub | `Vieuxnorris/Death-Must-Die` : vide (README seul) |

## Modèle de données des objets (lu dans les métadonnées de `Death.dll`)

- `Death.Items.Item` : `Code`, `Type` (ItemType), `Class`, `Rarity`, `Tier` (TierId), `IsUnique`, `SubtypeCode`,
  `IconVariant`, `DropVariant`, `_affixes` (liste), `WasOwnedByPlayer`, `IsLockedForEquip`, `IsInCooldown`.
- `ItemType` : Weapon, Head, Torso, Hands, Waist, Feet, Ring, Amulet, Relic, Jewel, Lore.
- `ItemRarity` : Broken, Common, Rare, Epic, Mythic, Immortal (0 à 5).
- `ItemAffix` (définition) : `Code`, `AffixGroup`, `StatGroup`, `GoldPerValuePoint`, `EquipCooldown`, `SkillSlot`,
  `Abilities[]`, `Tiers[]`, `MinRarity`, `IsSpecial`, `RelatedStatuses`, `RelatedKeywords`.
- `ItemSubtype` : `ItemType`, `ItemClass`, `MinRarity`/`MaxRarity`, `AlwaysRollAffixes`, `BaseAffixes`,
  `ForbiddenAffixes`, `SpecificCharacter`, `BaseValueMultiplier`.
- Génération : `ItemGenerator` (dont `GenerateItemForCheats`), `LootGenerator`, `ItemRepository` (Track/UnTrack).
- Le jeu contient une classe de triche interne `Death.App.Cheats_Items` (`Cheat_GiveGeneratedItem`,
  `Cheat_GiveUniqueItem`, `Cheat_FloodStashWithItems`...) : point d'appui idéal pour un mod.

### Tables de définition
`Death.Data.Database` (ScriptableObject) charge des TextAssets CSV séparés par `;`, stockés en clair dans
`sharedassets0.assets` : `Items_AffixesBoons` (~550 affixes), `Items_Uniques` (~415 uniques),
`Items_Subtypes` (~350), `Items_Archtypes`, `Items_Treasures`, `Items_TypePerMinute`.
Extraction sans dépendance : `extract_textassets.py` (lecture du layout TextAsset Unity 2021).

### Format de sauvegarde
`Slot_0.sav` = **deflate brut** (sans en-tête zlib) d'un JSON UTF-8 avec BOM :
```
{ Version: 10, IsAutosave, serializedSaveData: { keys: [...], values: ["<json string>", ...] }, thumbnailBase64, ... }
```
`values[0]` (`Death.App.Profile+ProfileState`) est lui-même un JSON contenant `PlayerRepoState.Entries[]`,
`ShopRepoState`, `Stashes`, `InventoryData`, `Gold`... Chaque objet est un `ItemSaveData` :
```json
{ "Code": "<guid ou code unique>", "Type": 0, "Rarity": 3, "TierIndex": 0, "IsUnique": false,
  "SubtypeCode": "Wswo_Basic_02", "IconVariant": "default", "DropVariant": "default",
  "Affixes": [ { "Code": "d%", "Levels": 120, "Enhanced": false } ], "WasOwnedByPlayer": true }
```
La sauvegarde actuelle contient déjà des objets à 17 affixes et 1000 niveaux : le jeu les accepte au chargement.

## Communauté (existant)
- Mods BepInEx (archivés, le code du jeu change beaucoup entre versions) : https://github.com/JustArion/DeathMustDieMods
- Nexus (BepInEx comme loader) : https://www.nexusmods.com/deathmustdie/mods/2
- Éditeur de sauvegarde existant (or, affixes, valeurs) : https://github.com/DenislavLitsov/DeathMustDieSaveEditor
- Table Cheat Engine : https://fearlessrevolution.com/viewtopic.php?t=27470
- BepInEx sur Unity Mono : https://docs.bepinex.dev/master/articles/user_guide/installation/unity_mono.html

## Routes possibles pour l'éditeur

1. **Mod in-game BepInEx 5 + Harmony (recommandé)**. Fenêtre IMGUI ouverte par une touche : liste des objets
   (inventaire, coffre, équipement), édition de tout champ (`Item`, affixes, niveaux, rareté, tier, sous-type,
   unique), création d'objets via `ItemGenerator`/`Cheats_Items`, et même édition des tables de définition
   (nouveaux affixes, plafonds de tiers). Aucune limite réelle, effet immédiat.
2. **Éditeur de sauvegarde externe** (desktop, hors jeu). Simple et robuste aux mises à jour, mais limité à ce
   que la sauvegarde stocke (pas de nouvelles définitions d'affixes) et nécessite de relancer le jeu.

Les deux partagent le même modèle (catalogue d'affixes/sous-types/uniques extrait des CSV).

## Outils nécessaires (non installés)
- **.NET SDK 8** (seuls les runtimes sont présents) pour compiler le plugin.
- **ILSpy / ilspycmd** pour lire le corps des méthodes (`ItemSaveLoad.TryLoadFrom`, validations, plafonds).
- **BepInEx 5.4.x x64** dans le dossier du jeu (sauvegarder `Slot_0.sav` avant).

## Réponses trouvées en décompilant (2026-10-05)
- `ItemSaveLoad.TryLoadFrom` ne vérifie que l'existence des codes d'affixes et du sous-type : **aucun plafond
  de niveaux**. Les uniques sont recréés depuis `Database.ItemUniques` et leurs affixes sauvegardés ignorés
  (corrigé par le patch du plugin).
- Rareté et tier indexent des tableaux (`ItemAffix.Tiers[tier.Index]`, `TierId.Count = 5`) : à garder dans les bornes.
- Les équipements (`CharacterLoadouts` → `Equipment` → `ItemSlot`) référencent les objets du `PlayerItemRepo`
  par id. `EquipmentAbilityTracker` écoute `ItemSlot.OnChangeEv` pour reconstruire les capacités.
- BepInEx : le jeu détruit l'objet gestionnaire, il faut `HideManagerGameObject = true` dans `BepInEx.cfg`.
