# TP4 – Unity VR (ST2OOS)

Scène : `Assets/TP4/Scenes/TP4_VR.unity`. Elle est générée automatiquement à la première ouverture du projet
par `Assets/TP4/Editor/TP4SceneBuilder.cs`. Pour la régénérer : menu **TP4 > Construire la scène TP4 (exercices 1 à 6)**.

## Exercice 1 – Configuration XR
- XR Plug-in Management : OpenXR est activé pour **Windows (Standalone)** et **Android**
  (`Assets/XR/XRGeneralSettingsPerBuildTarget.asset`).
- Profils d'interaction : Oculus Touch / Meta Quest Touch Plus (PC), Meta Quest Feature (Android).
- XR Rig : `XR Origin (XR Rig)` de XR Interaction Toolkit (déjà dans la scène).
- Test : Meta Quest Link actif sur le PC + application Meta Horizon Link en runtime OpenXR actif, puis **Play**.

## Exercice 2 – Grabbing
`Exercice 2 - Objets saisissables` : un cube et une sphère avec `Rigidbody` + `XRGrabInteractable`
(attache dynamique, lancer au relâchement). Bouton **grip** pour saisir/relâcher.

## Exercice 3 – Déplacement au joystick
`Scripts/JoystickLocomotion.cs` (sur le XR Origin), basé sur le **Input System** :
- joystick gauche → avant/arrière/gauche/droite, relatif au regard (`<XRController>{LeftHand}/{Primary2DAxis}`) ;
- joystick droit → rotation par cran de **45°** (modifiable à 30° dans l'inspecteur, `snapAngle`) ;
- clavier pour tester sans casque : W/A/S/D (ZQSD en AZERTY) et Q/E (A/E en AZERTY) ;
- les Move/Turn providers par défaut d'XRI sont désactivés au lancement pour éviter le double déplacement.

## Exercice 4 – Canvas UI au laser
`Exercice 4 - Canvas UI` (Canvas World Space + `TrackedDeviceGraphicRaycaster`) :
2 boutons (reset du score, pause des cibles), 1 slider (force de tir du pistolet), 1 `TMP_InputField`.
Le rayon laser est celui du **Near-Far Interactor** (XRI 3), qui remplace l'XR Ray Interactor ; la gâchette sert à cliquer.
`Scripts/VirtualKeyboard.cs` génère un clavier AZERTY (Maj, Espace, Effacer, Vider, OK) qui écrit dans l'InputField ;
le clavier système du Quest est désactivé. `Scripts/UIDemoPanel.cs` relie les widgets à la logique.

## Exercice 5 – Pistolet
`Exercice 5 - Pistolet` : `Rigidbody` + `XRGrabInteractable` (point de prise `GripAttach`) + `Scripts/VRPistol.cs`.
- **Gâchette** = événement *Activate* d'XRI → `Fire()` ;
- balle (`Prefabs/Balle.prefab`) instanciée à `Muzzle`, `Rigidbody` + `AddForce(..., ForceMode.Impulse)` ;
- réglages : `propulsionForce`, `bulletUseGravity`, `bulletLifetime`, `fireCooldown` ; flash de bouche + vibration.

**Modèle de l'Asset Store** : le pistolet est fait en primitives. Pour mettre le vôtre (ex. un pistolet gratuit
de l'Asset Store) : importez-le, glissez-le en enfant de `Modele`, supprimez les primitives, puis déplacez
`GripAttach` sur la crosse et `Muzzle` au bout du canon (axe Z bleu vers l'avant). Ajoutez un collider au modèle.

## Exercice 6 – Cibles et particules
- 5 cibles fixes (cube, cône, sphère, cylindre, capsule) + `Spawner de cibles` (`Scripts/TargetSpawner.cs`) :
  une cible aléatoire toutes les 1,5–3,5 s dans la zone (gizmo orange), 8 maximum, couleur aléatoire ;
- `Scripts/ShootingTarget.cs` détecte la balle (`OnCollisionEnter`), instancie `Prefabs/Explosion.prefab`
  (feu + fumée + étincelles, Particle System) et ajoute 10 points au score (`Scripts/ScoreBoard.cs`).

## Assets gratuits intégrés
- **PolyGun** (Asset Store) : pistolet `Pistol_05`. Son shader d'origine est Built-in (rose en URP) ;
  `Shaders/PolyGunURP.shader` en est un portage URP compatible VR, appliqué à tous les matériaux PolyGun.
- **Cartoon FX Remaster Free** (JMO) : explosions aléatoires + textes BOOM/POW sur les cibles, impact jaune des balles.
  Le camera shake du pack est coupé au lancement (`Scripts/VREffectsSettings.cs`), il donne la nausée en VR.
- Menu **TP4 > Intégrer PolyGun + Cartoon FX** pour relancer l'intégration (`Editor/TP4AssetIntegrator.cs`).
