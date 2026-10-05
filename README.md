# Death Must Die : éditeur d'objets (DMD Item Editor)

Plugin BepInEx qui ajoute au jeu une fenêtre (touche **F8**) pour modifier n'importe quel objet en direct :
rareté, tier, base, affixes (tous les ~550 affixes du jeu, sur n'importe quel emplacement, avec n'importe
quel nombre de niveaux), création d'objets, don d'uniques, duplication et suppression.

Testé sur Death Must Die buildid Steam 21650210 (Unity 2021.3.11, Mono x64) avec BepInEx 5.4.23.5.

## Installation

1. Installer [BepInEx 5 x64](https://github.com/BepInEx/BepInEx/releases) dans le dossier du jeu
   (là où se trouve `Death Must Die.exe`), voir la
   [documentation officielle](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_mono.html).
2. Lancer le jeu une fois puis le fermer, pour que BepInEx crée `BepInEx\config\BepInEx.cfg`.
3. **Obligatoire** : dans `BepInEx\config\BepInEx.cfg`, passer `HideManagerGameObject = true`.
   Sans ça, le jeu détruit l'objet de BepInEx au chargement et aucun plugin ne reçoit `Update`/`OnGUI`.
4. Copier `DMDItemEditor.dll` dans `BepInEx\plugins\DMDItemEditor\`.

Sauvegarde d'abord `%USERPROFILE%\AppData\LocalLow\Realm Archive\Death Must Die\Saves\`.

## Utilisation

- **F8** ouvre ou ferme l'éditeur (les contrôles du jeu sont bloqués tant qu'il est ouvert).
- Onglets **Équipé / Sac / Coffre 1-4 / Bibliothèque / Tous** : choisir un objet à gauche, le modifier à droite.
  Les objets équipés sont réappliqués automatiquement (stats et capacités recalculées).
- Onglet **Créer** : choisir une base pour créer un brouillon, l'éditer, puis l'ajouter au sac ou au coffre.
  La liste des uniques permet de les donner tels quels ou de les éditer avant de les donner.
- **Sauvegarder la partie** force une sauvegarde immédiate (le jeu sauvegarde aussi de lui-même).

### Limites qui restent
- Rareté (Broken à Immortal) et tier (T1 à T5) restent dans les bornes du jeu : il indexe des tableaux avec.
- La base d'un objet **équipé** ne peut être remplacée que par une base du même emplacement ;
  déséquipe-le pour changer d'emplacement.
- Les uniques modifiés gardent leurs modifications grâce au patch du plugin. Si le plugin est retiré,
  le jeu les reconstruit depuis leur modèle d'origine (la sauvegarde reste valide).

## Configuration (`BepInEx\config\vieuxnorris.dmd.itemeditor.cfg`)

| Clé | Défaut | Rôle |
|---|---|---|
| `General.ToggleKey` | `F8` | Touche d'ouverture |
| `General.UiScale` | `1` | Échelle de la fenêtre (0.5 à 3) |
| `Items.PersistUniqueEdits` | `true` | Garder les modifications des uniques au chargement |
| `Items.DefaultAffixLevels` | `10` | Niveaux par défaut d'un affixe ajouté |

## Compiler

Prérequis : .NET SDK 8, le jeu et BepInEx installés.

```bash
dotnet build src/DMDItemEditor -c Release -p:GameDir="G:\Steam\steamapps\common\Death Must Die"
```

La DLL est copiée automatiquement dans `BepInEx\plugins\DMDItemEditor\` si BepInEx est présent.

## Comment ça marche

- Les objets (`Death.Items.Item`) ont des propriétés en lecture seule : le plugin écrit leurs champs de
  stockage via Harmony `AccessTools.FieldRefAccess`, ce qui garde la même instance partout (dépôt d'objets,
  emplacements, équipements).
- Après une modification, chaque `ItemSlot` qui contient l'objet déclenche `OnChangeEv(item, item)` :
  `EquipmentAbilityTracker` retire puis recrée les capacités à partir des nouveaux affixes.
- Le patch `ItemSaveLoad.TryLoadFrom` garde les affixes, la rareté et le tier sauvegardés des uniques,
  que le jeu remplace sinon par ceux du modèle.
- Notes de reverse détaillées : [MODDING_PLAN.md](MODDING_PLAN.md). Scripts d'analyse dans `tools/`.
