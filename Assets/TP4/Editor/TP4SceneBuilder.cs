using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace TP4.EditorTools
{
    /// <summary>
    /// Construit automatiquement la scène du TP4 (exercices 1 à 6) :
    /// Assets/TP4/Scenes/TP4_VR.unity, plus les prefabs, matériaux et effets.
    /// Menu : TP4 > Construire la scène TP4. Lancé tout seul à la première ouverture du projet.
    /// </summary>
    public static class TP4SceneBuilder
    {
        const string Root = "Assets/TP4";
        const string ScenePath = Root + "/Scenes/TP4_VR.unity";
        const string XROriginPrefabPath =
            "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        const string GridMaterialPath = Root + "/Materials/Grid.mat";

        // Repère du joueur (XR Origin) : toutes les positions sont exprimées par rapport à lui.
        static Vector3 s_BasePos;
        static Quaternion s_BaseRot;

        [MenuItem("TP4/Construire la scène TP4 (exercices 1 à 6)")]
        public static void BuildFromMenu()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("TP4", "La scène TP4_VR existe déjà. La reconstruire ?", "Oui", "Annuler"))
                return;
            Build();
        }

        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(Root);
            EnsureFolder(Root + "/Scenes");
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Meshes");

            // ---------- Matériaux ----------
            Material matTable = LitMaterial("Table", new Color(0.35f, 0.27f, 0.2f));
            Material matGunBody = LitMaterial("Pistolet_Corps", new Color(0.12f, 0.12f, 0.14f), 0.6f, 0.55f);
            Material matGunGrip = LitMaterial("Pistolet_Crosse", new Color(0.25f, 0.16f, 0.1f));
            Material matGrab = LitMaterial("Objet_Saisissable", new Color(0.2f, 0.6f, 1f));
            Material matGrab2 = LitMaterial("Objet_Saisissable_2", new Color(0.3f, 0.85f, 0.4f));
            Material matBullet = LitMaterial("Balle", new Color(1f, 0.8f, 0.2f), 0.9f, 0.8f);
            Material matPanel = LitMaterial("Stand", new Color(0.8f, 0.8f, 0.82f));
            Material[] targetMats =
            {
                LitMaterial("Cible_Rouge", new Color(0.9f, 0.2f, 0.2f)),
                LitMaterial("Cible_Orange", new Color(1f, 0.55f, 0.1f)),
                LitMaterial("Cible_Violet", new Color(0.6f, 0.3f, 0.9f)),
                LitMaterial("Cible_Cyan", new Color(0.1f, 0.8f, 0.85f)),
                LitMaterial("Cible_Jaune", new Color(1f, 0.9f, 0.2f)),
            };
            Texture2D softTex = SoftParticleTexture();
            Material matFire = ParticleMaterial("Particules_Feu", softTex, additive: true);
            Material matSmoke = ParticleMaterial("Particules_Fumee", softTex, additive: false);

            // ---------- Prefabs ----------
            GameObject explosionPrefab = BuildExplosionPrefab(matFire, matSmoke);
            GameObject bulletPrefab = BuildBulletPrefab(matBullet, matFire);
            Mesh coneMesh = ConeMesh();
            GameObject[] targetPrefabs =
            {
                BuildTargetPrefab("Cible_Cube", PrimitiveType.Cube, null, targetMats[0], explosionPrefab),
                BuildTargetPrefab("Cible_Cone", null, coneMesh, targetMats[1], explosionPrefab),
                BuildTargetPrefab("Cible_Sphere", PrimitiveType.Sphere, null, targetMats[2], explosionPrefab),
                BuildTargetPrefab("Cible_Cylindre", PrimitiveType.Cylinder, null, targetMats[3], explosionPrefab),
                BuildTargetPrefab("Cible_Capsule", PrimitiveType.Capsule, null, targetMats[4], explosionPrefab),
            };

            // ---------- Scène ----------
            if (File.Exists(ScenePath)) AssetDatabase.DeleteAsset(ScenePath);
            var scene = CreateBaseScene();
            if (!scene.IsValid()) return;

            // Exercice 1 : XR Origin (XR Rig) ajouté par CreateBaseScene().
            XROrigin origin = Object.FindAnyObjectByType<XROrigin>();
            if (origin == null)
            {
                Debug.LogError("[TP4] Aucun XR Origin trouvé dans la scène de base.");
                return;
            }
            s_BasePos = origin.transform.position;
            Vector3 fwd = Vector3.ProjectOnPlane(origin.transform.forward, Vector3.up);
            s_BaseRot = Quaternion.LookRotation(fwd.sqrMagnitude > 0.001f ? fwd : Vector3.forward, Vector3.up);

            // Exercice 3 : locomotion au joystick.
            var loco = origin.GetComponent<JoystickLocomotion>();
            if (loco == null) loco = origin.gameObject.AddComponent<JoystickLocomotion>();
            loco.snapAngle = 45f;
            loco.moveSpeed = 2f;

            // Décor : table.
            var table = Primitive("Table", PrimitiveType.Cube, null, P(0f, 0.4f, 0.9f), Quaternion.identity,
                new Vector3(1.4f, 0.8f, 0.6f), matTable);
            table.isStatic = true;

            // Exercice 2 : objets saisissables.
            var grabRoot = new GameObject("Exercice 2 - Objets saisissables");
            Place(grabRoot.transform, P(0f, 0f, 0f), Quaternion.identity);
            MakeGrabbable(Primitive("Cube à saisir", PrimitiveType.Cube, grabRoot.transform,
                P(-0.45f, 0.87f, 0.85f), Quaternion.identity, Vector3.one * 0.12f, matGrab), 0.5f);
            MakeGrabbable(Primitive("Sphère à saisir", PrimitiveType.Sphere, grabRoot.transform,
                P(-0.25f, 0.87f, 0.85f), Quaternion.identity, Vector3.one * 0.12f, matGrab2), 0.4f);

            // Exercice 5 : pistolet.
            var pistol = BuildPistol(P(0.25f, 0.92f, 0.85f), matGunBody, matGunGrip, bulletPrefab, matFire);

            // Exercice 6 : cibles fixes + spawner.
            var targetsRoot = new GameObject("Exercice 6 - Cibles");
            Place(targetsRoot.transform, P(0f, 0f, 0f), Quaternion.identity);
            Vector3[] fixedPositions =
            {
                P(-2f, 1.2f, 6f), P(-0.7f, 1.6f, 7f), P(0.7f, 1.0f, 6.5f), P(2f, 1.4f, 7.5f), P(0f, 2.2f, 8f)
            };
            for (int i = 0; i < fixedPositions.Length; i++)
            {
                var t = (GameObject)PrefabUtility.InstantiatePrefab(targetPrefabs[i % targetPrefabs.Length], targetsRoot.transform);
                t.transform.position = fixedPositions[i];
            }
            // Petits socles pour le décor du stand de tir.
            Primitive("Ligne de tir", PrimitiveType.Cube, targetsRoot.transform, P(0f, 0.01f, 1.5f), Quaternion.identity,
                new Vector3(3f, 0.02f, 0.05f), targetMats[4]).isStatic = true;
            Primitive("Mur du fond", PrimitiveType.Cube, targetsRoot.transform, P(0f, 2f, 12f), Quaternion.identity,
                new Vector3(14f, 4f, 0.3f), matPanel).isStatic = true;

            var spawnerGo = new GameObject("Spawner de cibles");
            spawnerGo.transform.SetParent(targetsRoot.transform, false);
            Place(spawnerGo.transform, P(0f, 1.7f, 8f), s_BaseRot);
            var spawner = spawnerGo.AddComponent<TargetSpawner>();
            spawner.targetPrefabs = targetPrefabs;
            spawner.areaSize = new Vector3(7f, 2.2f, 4f);
            spawner.maxTargets = 8;

            // Exercice 4 : Canvas UI + clavier virtuel.
            BuildUI(origin, pistol, spawner);

            // Panneau d'aide.
            BuildHelpText();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (TP4AssetIntegrator.NeedsIntegration()) TP4AssetIntegrator.Integrate(false);
            Debug.Log("[TP4] Scène construite : " + ScenePath + ". Lancez Play avec le Quest branché via Quest Link.");
        }

        // =====================================================================
        // Scène de base (exercice 1) : lumière, sol, XR Interaction Manager,
        // EventSystem XR et XR Origin (XR Rig) de XR Interaction Toolkit.
        // =====================================================================
        static UnityEngine.SceneManagement.Scene CreateBaseScene()
        {
            var originPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPrefabPath);
            if (originPrefab == null)
            {
                Debug.LogError("[TP4] Prefab XR Origin introuvable : " + XROriginPrefabPath +
                               " (importer le sample Starter Assets de XR Interaction Toolkit).");
                return default;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<XRUIInputModule>();

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Sol";
            floor.transform.localScale = Vector3.one * 10f;
            floor.isStatic = true;
            var grid = AssetDatabase.LoadAssetAtPath<Material>(GridMaterialPath);
            if (grid != null) floor.GetComponent<MeshRenderer>().sharedMaterial = grid;

            var origin = (GameObject)PrefabUtility.InstantiatePrefab(originPrefab);
            origin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("[TP4] Impossible d'enregistrer " + ScenePath);
                return default;
            }
            return scene;
        }

        // =====================================================================
        // Pistolet (exercice 5)
        // =====================================================================
        static VRPistol BuildPistol(Vector3 pos, Material body, Material grip, GameObject bulletPrefab, Material fireMat)
        {
            var root = new GameObject("Exercice 5 - Pistolet");
            Place(root.transform, pos, s_BaseRot);

            // Modèle en primitives (à remplacer par un modèle de l'Asset Store : glisser le modèle
            // en enfant de "Modele", supprimer les primitives et ajuster GripAttach / Muzzle).
            var model = new GameObject("Modele").transform;
            model.SetParent(root.transform, false);
            Primitive("Carcasse", PrimitiveType.Cube, model, Vector3.zero, Quaternion.identity, Vector3.one, body,
                local: true, localPos: new Vector3(0f, 0.035f, 0.03f), localScale: new Vector3(0.035f, 0.045f, 0.2f));
            Primitive("Canon", PrimitiveType.Cylinder, model, Vector3.zero, Quaternion.identity, Vector3.one, body,
                local: true, localPos: new Vector3(0f, 0.045f, 0.15f), localRot: Quaternion.Euler(90f, 0f, 0f),
                localScale: new Vector3(0.022f, 0.04f, 0.022f));
            Primitive("Crosse", PrimitiveType.Cube, model, Vector3.zero, Quaternion.identity, Vector3.one, grip,
                local: true, localPos: new Vector3(0f, -0.035f, -0.045f), localRot: Quaternion.Euler(-15f, 0f, 0f),
                localScale: new Vector3(0.03f, 0.11f, 0.045f));
            Primitive("Pontet", PrimitiveType.Cube, model, Vector3.zero, Quaternion.identity, Vector3.one, body,
                local: true, localPos: new Vector3(0f, -0.005f, 0.005f), localScale: new Vector3(0.012f, 0.025f, 0.035f));
            Primitive("Gachette", PrimitiveType.Cube, model, Vector3.zero, Quaternion.identity, Vector3.one, grip,
                local: true, localPos: new Vector3(0f, -0.002f, 0.0f), localScale: new Vector3(0.006f, 0.02f, 0.008f));

            // Point de prise : la main se place ici, canon vers l'avant.
            var attach = new GameObject("GripAttach").transform;
            attach.SetParent(root.transform, false);
            attach.localPosition = new Vector3(0f, -0.03f, -0.04f);
            attach.localRotation = Quaternion.Euler(15f, 0f, 0f);

            // Bout du canon : Z = direction du tir.
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root.transform, false);
            muzzle.localPosition = new Vector3(0f, 0.045f, 0.2f);
            muzzle.localRotation = Quaternion.identity;

            var flash = BuildMuzzleFlash(muzzle, fireMat);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.8f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.attachTransform = attach;
            grab.useDynamicAttach = false;
            grab.throwOnDetach = true;

            var gun = root.AddComponent<VRPistol>();
            gun.bulletPrefab = bulletPrefab;
            gun.muzzle = muzzle;
            gun.muzzleFlash = flash;
            gun.propulsionForce = 1.5f;
            gun.bulletUseGravity = true;
            return gun;
        }

        static ParticleSystem BuildMuzzleFlash(Transform parent, Material mat)
        {
            var go = new GameObject("MuzzleFlash");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.5f, 0.1f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.005f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            return ps;
        }

        // =====================================================================
        // UI (exercice 4)
        // =====================================================================
        static void BuildUI(XROrigin origin, VRPistol pistol, TargetSpawner spawner)
        {
            var res = new TMP_DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
            };
            var uiRes = new DefaultControls.Resources
            {
                standard = res.standard, background = res.background, inputField = res.inputField,
                knob = res.knob, checkmark = res.checkmark, dropdown = res.dropdown, mask = res.mask,
            };

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer < 0) uiLayer = 0;

            var canvasGo = new GameObject("Exercice 4 - Canvas UI", typeof(RectTransform));
            canvasGo.layer = uiLayer;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = origin.Camera;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 2f;
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>(); // permet le clic au laser XR

            var crt = (RectTransform)canvasGo.transform;
            crt.sizeDelta = new Vector2(640f, 980f);
            crt.localScale = Vector3.one * 0.0012f;
            // À gauche du joueur, légèrement tourné vers lui.
            crt.position = P(-1.25f, 1.35f, 1.1f);
            crt.rotation = s_BaseRot * Quaternion.Euler(0f, -40f, 0f);

            // Fond
            var bg = UIObject("Fond", crt, Vector2.zero, crt.sizeDelta, uiLayer);
            var bgImg = bg.AddComponent<Image>();
            bgImg.sprite = res.background;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0.08f, 0.1f, 0.16f, 0.92f);

            float y = 440f;
            Label("Titre", crt, "TP4 - Panneau UI (laser)", new Vector2(0f, y), new Vector2(600f, 60f), 38f, uiLayer, FontStyles.Bold);
            y -= 60f;
            var scoreLabel = Label("Score", crt, "Score : 0", new Vector2(0f, y), new Vector2(600f, 50f), 30f, uiLayer);
            var board = canvasGo.AddComponent<ScoreBoard>();
            board.label = scoreLabel;

            // Boutons
            y -= 70f;
            var resetBtn = TMP_DefaultControls.CreateButton(res);
            SetupUI(resetBtn, crt, new Vector2(-150f, y), new Vector2(280f, 60f), uiLayer, "Bouton Reset");
            var resetLabel = resetBtn.GetComponentInChildren<TMP_Text>();
            resetLabel.text = "Remettre le score à 0";
            resetLabel.fontSize = 22f;

            var spawnBtn = TMP_DefaultControls.CreateButton(res);
            SetupUI(spawnBtn, crt, new Vector2(150f, y), new Vector2(280f, 60f), uiLayer, "Bouton Spawn");
            var spawnLabel = spawnBtn.GetComponentInChildren<TMP_Text>();
            spawnLabel.text = "Pause des cibles";
            spawnLabel.fontSize = 22f;

            // Slider
            y -= 80f;
            var forceLabel = Label("Label Force", crt, "Force de tir", new Vector2(0f, y), new Vector2(600f, 40f), 26f, uiLayer);
            y -= 50f;
            var sliderGo = DefaultControls.CreateSlider(uiRes);
            SetupUI(sliderGo, crt, new Vector2(0f, y), new Vector2(520f, 30f), uiLayer, "Slider Force");
            var slider = sliderGo.GetComponent<Slider>();
            slider.minValue = 0.3f;
            slider.maxValue = 5f;
            slider.value = pistol.propulsionForce;

            // InputField
            y -= 70f;
            var inputGo = TMP_DefaultControls.CreateInputField(res);
            SetupUI(inputGo, crt, new Vector2(0f, y), new Vector2(520f, 60f), uiLayer, "InputField Nom");
            var input = inputGo.GetComponent<TMP_InputField>();
            input.characterLimit = 30;
            input.pointSize = 28f;
            if (input.placeholder is TMP_Text ph) ph.text = "Votre nom...";
            if (input.textComponent != null) input.textComponent.fontSize = 28f;

            y -= 60f;
            var greeting = Label("Message", crt, "Tapez votre nom avec le clavier virtuel", new Vector2(0f, y),
                new Vector2(600f, 45f), 26f, uiLayer);
            greeting.color = new Color(1f, 0.85f, 0.4f);

            // Clavier virtuel
            var kbGo = UIObject("Clavier virtuel", crt, new Vector2(0f, -320f), new Vector2(620f, 300f), uiLayer);
            var kbBg = kbGo.AddComponent<Image>();
            kbBg.sprite = res.background;
            kbBg.type = Image.Type.Sliced;
            kbBg.color = new Color(0.15f, 0.18f, 0.25f, 1f);
            var kb = kbGo.AddComponent<VirtualKeyboard>();
            kb.targetField = input;
            kb.keySprite = res.standard;
            kb.keySize = new Vector2(52f, 50f);
            kb.spacing = 6f;

            var panel = canvasGo.AddComponent<UIDemoPanel>();
            panel.resetScoreButton = resetBtn.GetComponent<Button>();
            panel.toggleSpawnButton = spawnBtn.GetComponent<Button>();
            panel.toggleSpawnLabel = spawnLabel;
            panel.forceSlider = slider;
            panel.forceLabel = forceLabel;
            panel.nameField = input;
            panel.greetingLabel = greeting;
            panel.pistol = pistol;
            panel.spawner = spawner;

            SetLayerRecursively(canvasGo, uiLayer);
        }

        static void BuildHelpText()
        {
            var go = new GameObject("Aide - commandes");
            Place(go.transform, P(0f, 1.9f, 1.6f), s_BaseRot);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text =
                "<b>TP4 - Unity VR</b>\n" +
                "Joystick gauche : se déplacer   |   Joystick droit : tourner (45°)\n" +
                "Grip : saisir   |   Gâchette : tirer   |   Laser : panneau UI à gauche";
            tmp.fontSize = 1.1f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.rectTransform.sizeDelta = new Vector2(3.2f, 0.5f);
        }

        // =====================================================================
        // Prefabs
        // =====================================================================
        static GameObject BuildBulletPrefab(Material mat, Material trailMat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Balle";
            go.transform.localScale = Vector3.one * 0.025f;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            go.AddComponent<Bullet>();
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.12f;
            trail.startWidth = 0.015f;
            trail.endWidth = 0f;
            trail.sharedMaterial = trailMat;
            trail.startColor = new Color(1f, 0.8f, 0.3f, 1f);
            trail.endColor = new Color(1f, 0.4f, 0.1f, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return SavePrefab(go, "Balle");
        }

        static GameObject BuildExplosionPrefab(Material fire, Material smoke)
        {
            var root = new GameObject("Explosion");

            // Boule de feu
            var ps = root.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.4f), new Color(1f, 0.35f, 0.05f));
            main.gravityModifier = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white, new Color(1f, 0.3f, 0f)));
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));
            root.GetComponent<ParticleSystemRenderer>().sharedMaterial = fire;

            // Fumée
            var smokeGo = new GameObject("Fumee");
            smokeGo.transform.SetParent(root.transform, false);
            var sps = smokeGo.AddComponent<ParticleSystem>();
            sps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var smain = sps.main;
            smain.duration = 1f;
            smain.loop = false;
            smain.playOnAwake = true;
            smain.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            smain.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            smain.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            smain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            smain.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.35f, 0.35f, 0.7f), new Color(0.6f, 0.6f, 0.6f, 0.5f));
            smain.gravityModifier = -0.05f;
            smain.simulationSpace = ParticleSystemSimulationSpace.World;
            var sem = sps.emission;
            sem.rateOverTime = 0f;
            sem.SetBursts(new[] { new ParticleSystem.Burst(0.05f, 20) });
            var sshape = sps.shape;
            sshape.shapeType = ParticleSystemShapeType.Sphere;
            sshape.radius = 0.15f;
            var scol = sps.colorOverLifetime;
            scol.enabled = true;
            scol.color = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white, Color.white));
            var ssize = sps.sizeOverLifetime;
            ssize.enabled = true;
            ssize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            smokeGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = smoke;

            // Débris (petites étincelles)
            var sparkGo = new GameObject("Etincelles");
            sparkGo.transform.SetParent(root.transform, false);
            var sp = sparkGo.AddComponent<ParticleSystem>();
            sp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var spm = sp.main;
            spm.duration = 1f;
            spm.loop = false;
            spm.playOnAwake = true;
            spm.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            spm.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            spm.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
            spm.startColor = new Color(1f, 0.85f, 0.4f);
            spm.gravityModifier = 1f;
            spm.simulationSpace = ParticleSystemSimulationSpace.World;
            var spe = sp.emission;
            spe.rateOverTime = 0f;
            spe.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });
            var sps2 = sp.shape;
            sps2.shapeType = ParticleSystemShapeType.Sphere;
            sps2.radius = 0.05f;
            var spr = sparkGo.GetComponent<ParticleSystemRenderer>();
            spr.sharedMaterial = fire;
            spr.renderMode = ParticleSystemRenderMode.Stretch;
            spr.velocityScale = 0.04f;
            spr.lengthScale = 1f;

            return SavePrefab(root, "Explosion");
        }

        static GameObject BuildTargetPrefab(string name, PrimitiveType? type, Mesh customMesh, Material mat, GameObject explosion)
        {
            GameObject go;
            if (type.HasValue)
            {
                go = GameObject.CreatePrimitive(type.Value);
            }
            else
            {
                go = new GameObject();
                go.AddComponent<MeshFilter>().sharedMesh = customMesh;
                go.AddComponent<MeshRenderer>();
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = customMesh;
                mc.convex = true;
            }
            go.name = name;
            go.transform.localScale = Vector3.one * 0.45f;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var target = go.AddComponent<ShootingTarget>();
            target.explosionPrefab = explosion;
            target.points = 10;
            return SavePrefab(go, name);
        }

        // =====================================================================
        // Helpers
        // =====================================================================
        static Vector3 P(float x, float y, float z) => s_BasePos + s_BaseRot * new Vector3(x, y, z);

        static void Place(Transform t, Vector3 pos, Quaternion rot)
        {
            t.position = pos;
            t.rotation = rot;
        }

        static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 pos, Quaternion rot,
            Vector3 scale, Material mat, bool local = false, Vector3 localPos = default, Quaternion? localRot = null,
            Vector3? localScale = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            if (local)
            {
                go.transform.localPosition = localPos;
                go.transform.localRotation = localRot ?? Quaternion.identity;
                go.transform.localScale = localScale ?? Vector3.one;
            }
            else
            {
                go.transform.position = pos;
                go.transform.rotation = s_BaseRot * rot;
                go.transform.localScale = scale;
            }
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static void MakeGrabbable(GameObject go, float mass)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            var grab = go.AddComponent<XRGrabInteractable>();
            grab.useDynamicAttach = true; // l'objet reste à l'endroit où on l'a attrapé
            grab.throwOnDetach = true;
        }

        static GameObject UIObject(string name, RectTransform parent, Vector2 pos, Vector2 size, int layer)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return go;
        }

        static void SetupUI(GameObject go, RectTransform parent, Vector2 pos, Vector2 size, int layer, string name)
        {
            go.name = name;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            SetLayerRecursively(go, layer);
        }

        static TMP_Text Label(string name, RectTransform parent, string text, Vector2 pos, Vector2 size, float fontSize,
            int layer, FontStyles style = FontStyles.Normal)
        {
            var go = UIObject(name, parent, pos, size, layer);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        static Gradient FadeGradient(Color start, Color end)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static GameObject SavePrefab(GameObject go, string name)
        {
            string path = $"{Root}/Prefabs/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static Shader FindShader(params string[] names)
        {
            foreach (var n in names)
            {
                var s = Shader.Find(n);
                if (s != null) return s;
            }
            return null;
        }

        static Material LitMaterial(string name, Color color, float metallic = 0f, float smoothness = 0.4f)
        {
            string path = $"{Root}/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = FindShader("Universal Render Pipeline/Lit", "Standard");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null) mat.shader = shader;
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Glossiness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material ParticleMaterial(string name, Texture2D tex, bool additive)
        {
            string path = $"{Root}/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = FindShader("Universal Render Pipeline/Particles/Unlit", "Particles/Standard Unlit", "Sprites/Default");
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null) mat.shader = shader;

            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_MainTex", tex);
            mat.SetColor("_BaseColor", Color.white);
            // Transparent (URP) : 0 = Alpha, 2 = Additive
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", additive ? 2f : 0f);
            int src = (int)UnityEngine.Rendering.BlendMode.SrcAlpha;
            int dst = additive ? (int)UnityEngine.Rendering.BlendMode.One : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha;
            mat.SetFloat("_SrcBlend", src);
            mat.SetFloat("_DstBlend", dst);
            mat.SetFloat("_SrcBlendAlpha", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", dst);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Texture2D SoftParticleTexture()
        {
            string path = $"{Root}/Materials/ParticuleDouce.png";
            if (!File.Exists(path))
            {
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, d * d));
                }
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    importer.alphaIsTransparency = true;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.mipmapEnabled = true;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Mesh ConeMesh()
        {
            string path = $"{Root}/Meshes/Cone.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            const int seg = 32;
            const float r = 0.5f, h = 1f;
            var verts = new List<Vector3>();
            var tris = new List<int>();

            // Côtés : sommet dupliqué par segment pour de jolies normales.
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                int b = verts.Count;
                verts.Add(new Vector3(Mathf.Cos(a0) * r, -h / 2f, Mathf.Sin(a0) * r));
                verts.Add(new Vector3(0f, h / 2f, 0f));
                verts.Add(new Vector3(Mathf.Cos(a1) * r, -h / 2f, Mathf.Sin(a1) * r));
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            }
            // Base
            int c = verts.Count;
            verts.Add(new Vector3(0f, -h / 2f, 0f));
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                verts.Add(new Vector3(Mathf.Cos(a) * r, -h / 2f, Mathf.Sin(a) * r));
            }
            for (int i = 0; i < seg; i++)
            {
                tris.Add(c); tris.Add(c + 1 + i); tris.Add(c + 2 + i);
            }

            var mesh = new Mesh { name = "Cone" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    /// <summary>Construit la scène une seule fois, automatiquement, à la première ouverture du projet.</summary>
    [InitializeOnLoad]
    static class TP4AutoBuild
    {
        const string Key = "TP4_AutoBuild_Done";

        static TP4AutoBuild()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += TryBuild;
        }

        static void TryBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryBuild;
                return;
            }
            if (SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, true);
            if (File.Exists("Assets/TP4/Scenes/TP4_VR.unity")) return;
            TP4SceneBuilder.Build();
        }
    }
}
