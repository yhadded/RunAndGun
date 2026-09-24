# Run & Gun 2D — TP02 ST2OOS

Mini-jeu 2D de type Run & Gun inspiré de Metal Slug (Unity 6, 2D, nouveau Input System).

## Ouvrir le projet
1. Cloner le dépôt, puis **Unity Hub → Add → Add project from disk** → choisir ce dossier.
2. Ouvrir avec Unity 6 (6000.x). Si Unity propose d'activer le nouvel Input System, répondre **Yes** (redémarrage).
3. Au premier lancement, le script `Assets/Editor/TP02Builder.cs` génère automatiquement les prefabs, animations, scènes et UI.
   Pour tout régénérer : menu **TP02 → Build everything**.
4. Ouvrir `Assets/Scenes/MainMenu` → **Play**.

## Contrôles
| Touche | Action |
|---|---|
| A / D (Q / D en AZERTY) | Se déplacer |
| W / S (Z / S) | Viser haut / bas (bas en l'air) |
| Espace | Sauter |
| J ou clic gauche | Tirer |
| E | Changer d'arme |

## Organisation
```
Assets/
├── Animations/   clips + Animator Controllers (générés)
├── Audio/        effets sonores (générés)
├── Editor/       TP02Builder (génération du projet)
├── Prefabs/      Player, Enemies, Projectiles, Pickups, Level, UI, FX (générés)
├── Scenes/       MainMenu, Level01, Level02 (générées)
├── Scripts/      Core, Player, Enemies, Gameplay, UI
└── Sprites/      Player, Enemies, Tiles, Items, Background, UI (générés par SpriteGenerator)
```
