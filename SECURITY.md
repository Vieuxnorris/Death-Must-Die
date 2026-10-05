# Sécurité

## Signaler une vulnérabilité

Merci de ne pas ouvrir d'issue publique pour un problème de sécurité. Utiliser plutôt le signalement privé de
GitHub : onglet **Security** du dépôt > **Report a vulnerability**.

Sont concernés, par exemple : une archive de release modifiée ou dont le contenu ne correspond pas au code,
un script de `tools/` qui télécharge ou exécute autre chose que prévu, ou une fuite de données personnelles.

## Ce que contient (et ne contient pas) ce dépôt

- Uniquement le code source du plugin et des scripts d'outillage.
- Aucun fichier du jeu, aucune DLL, aucun code décompilé, aucune sauvegarde : la compilation référence les DLL
  de votre propre installation du jeu (voir `GameDir` dans le `.csproj`).
- Les zips de release embarquent BepInEx 5 (LGPL-2.1) tel que publié sur
  <https://github.com/BepInEx/BepInEx/releases> ; `tools/package.ps1` vérifie son empreinte SHA-256 avant de
  l'inclure.

## Bonnes pratiques pour les joueurs

- Ne télécharger les zips que depuis la page **Releases** de ce dépôt.
- Sauvegarder `Slot_0.sav` avant d'installer ou de mettre à jour un mod.
