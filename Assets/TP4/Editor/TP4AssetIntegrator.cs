using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace TP4.EditorTools
{
    /// <summary>
    /// Intègre les packs gratuits importés dans la scène du TP4 :
    /// - PolyGun : convertit ses matériaux vers le shader URP "TP4/PolyGun URP" et remplace
    ///   le pistolet en primitives par Pistol_05 (prise en main et bout du canon recalculés) ;
    /// - Cartoon FX Remaster : explosions + textes BOOM/POW sur les cibles, impact des balles.
    /// Menu : TP4 > Intégrer PolyGun + Cartoon FX. Lancé automatiquement une fois si besoin.
    /// </summary>
    public static class TP4AssetIntegrator
    {
        const string ScenePath = "Assets/TP4/Scenes/TP4_VR.unity";
        const string PolyGunRoot = "Assets/PolyGun";
        const string GunShaderName = "PolyGun/Gun Shader";
        const string UrpGunShaderName = "TP4/PolyGun URP";
        const string ModelName = "Modele";

        static readonly string[] k_Explosions = { "CFXR Explosion 1", "CFXR2 WW Explosion", "CFXR3 Fire Explosion B" };
        static readonly string[] k_Popups = { "CFXR _BOOM_", "CFXR _POW_", "CFXR2 _WHAM_ 3", "CFXR3 _WOW_" };
        const string ImpactName = "CFXR Hit D 3D (Yellow)";

        [MenuItem("TP4/Intégrer PolyGun + Cartoon FX")]
        public static void IntegrateFromMenu() => Integrate(true);

        public static bool NeedsIntegration()
        {
            if (FindPrefab("Pistol_05", PolyGunRoot) == null && FindPrefab(k_Explosions[0], null) == null)
                return false;
            if (!File.Exists(ScenePath)) return false;
            string text = File.ReadAllText(ScenePath);
            bool gunMissing = !AllGunsInScene(text);
            return gunMissing || AnyGunMaterialToConvert() || TargetsNeedFx() || CartoonFxShadersNeedUrp() || CountSoftParticleMaterials(false) > 0;
        }

        public static void Integrate(bool verbose)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            int fxShaders = FixCartoonFxShaders();
            int softFixed = CountSoftParticleMaterials(true);
            int mats = ConvertPolyGunMaterials();
            bool fx = WireCartoonFx();

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int guns = SetupAllGuns();
            FixAmbientLighting();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            string msg = $"[TP4] Intégration : {mats} matériau(x) PolyGun convertis en URP, " +
                         $"{guns} arme(s) posée(s) sur la table, " +
                         $"effets Cartoon FX {(fx ? "branchés" : "non trouvés")}" +
                         (fxShaders > 0 ? $", {fxShaders} shader(s) Cartoon FX recompilé(s) pour URP" : "") +
                         (softFixed > 0 ? $", soft particles coupées sur {softFixed} matériau(x) Cartoon FX." : ".");
            Debug.Log(msg);
            if (verbose) EditorUtility.DisplayDialog("TP4", msg, "OK");
        }

        // ------------------------------------------------------------------
        // PolyGun : matériaux
        // ------------------------------------------------------------------
        static bool AnyGunMaterialToConvert()
        {
            if (!AssetDatabase.IsValidFolder(PolyGunRoot)) return false;
            return AssetDatabase.FindAssets("t:Material", new[] { PolyGunRoot })
                .Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)))
                .Any(m => m != null && m.shader != null && m.shader.name == GunShaderName);
        }

        static int ConvertPolyGunMaterials()
        {
            if (!AssetDatabase.IsValidFolder(PolyGunRoot)) return 0;
            Shader urp = Shader.Find(UrpGunShaderName);
            if (urp == null)
            {
                Debug.LogWarning("[TP4] Shader " + UrpGunShaderName + " introuvable (Assets/TP4/Shaders).");
                return 0;
            }
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { PolyGunRoot }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null || mat.shader == null) continue;
                string name = mat.shader.name;
                if (name == GunShaderName)
                {
                    mat.shader = urp; // mêmes noms de propriétés : couleurs et masques conservés
                    EditorUtility.SetDirty(mat);
                    count++;
                }
                else if ((name == "Standard" || name.StartsWith("Legacy Shaders/") || name == "Hidden/InternalErrorShader") && lit != null)
                {
                    Color c = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                    Texture tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                    mat.shader = lit;
                    mat.SetColor("_BaseColor", c);
                    if (tex != null) mat.SetTexture("_BaseMap", tex);
                    EditorUtility.SetDirty(mat);
                    count++;
                }
            }
            return count;
        }

        // ------------------------------------------------------------------
        // PolyGun : modèle du pistolet
        // ------------------------------------------------------------------
        // Les 4 armes du pack gratuit, de gauche à droite sur la table.
        // (nom du prefab, position sur la table en m, automatique, cadence, force, gravité, cooldown)
        static readonly (string prefab, float x, bool auto, float rate, float force, bool gravity, float cooldown)[] k_Guns =
        {
            ("Pistol_05", -0.55f, false, 0f, 1.5f, true, 0.15f),
            ("SMG_06", -0.10f, true, 12f, 1.3f, true, 0.1f),
            ("AR_06", 0.40f, true, 9f, 1.8f, true, 0.1f),
            ("SR_00", 0.95f, false, 0f, 3f, false, 0.8f),
        };
        const string GunRootPrefix = "Arme - ";

        static bool AllGunsInScene(string sceneText)
        {
            foreach (var g in k_Guns)
                if (FindPrefab(g.prefab, PolyGunRoot) != null && !sceneText.Contains(g.prefab)) return false;
            return true;
        }

        const string PistolRootName = "Exercice 5 - Pistolet";

        /// <summary>Pistolet de l'exercice 5 (relié au slider du panneau UI), jamais une arme "Arme - ...".</summary>
        static VRPistol FindExercisePistol()
        {
            GameObject named = GameObject.Find(PistolRootName);
            if (named != null && named.TryGetComponent(out VRPistol p)) return p;
            foreach (var candidate in Object.FindObjectsByType<VRPistol>(FindObjectsInactive.Include))
                if (!candidate.name.StartsWith(GunRootPrefix)) return candidate;
            return null;
        }

        static int SetupAllGuns()
        {
            VRPistol pistol = FindExercisePistol();
            if (pistol == null)
            {
                Debug.LogWarning("[TP4] Aucun VRPistol dans la scène (pistolet de l'exercice 5).");
                return 0;
            }

            // Table agrandie pour accueillir les fusils (~1 m de long).
            GameObject table = GameObject.Find("Table");
            if (table != null)
            {
                Vector3 s = table.transform.localScale;
                table.transform.localScale = new Vector3(Mathf.Max(s.x, 2.7f), s.y, Mathf.Max(s.z, 1.1f));
            }
            Transform tableT = table != null ? table.transform : null;
            Vector3 tableCenter = tableT != null ? tableT.position : pistol.transform.position;
            float tableTop = tableT != null ? tableT.position.y + tableT.lossyScale.y / 2f : pistol.transform.position.y;
            Quaternion tableRot = tableT != null ? tableT.rotation : Quaternion.identity;

            // Objets saisissables de l'exercice 2 déplacés à gauche de la table.
            PlaceOnTable(GameObject.Find("Cube à saisir"), tableCenter, tableRot, tableTop, -1.15f, 0.07f);
            PlaceOnTable(GameObject.Find("Sphère à saisir"), tableCenter, tableRot, tableTop, -0.95f, 0.07f);

            // Supprime les armes ajoutées lors d'une intégration précédente.
            foreach (GameObject rootGo in pistol.gameObject.scene.GetRootGameObjects())
                if (rootGo != pistol.gameObject && rootGo.name.StartsWith(GunRootPrefix))
                    Object.DestroyImmediate(rootGo);

            ParticleSystem flashTemplate = pistol.muzzleFlash;
            int count = 0;
            foreach (var g in k_Guns)
            {
                GameObject prefab = FindPrefab(g.prefab, PolyGunRoot);
                if (prefab == null) continue;

                VRPistol gun;
                if (g.prefab == "Pistol_05")
                {
                    gun = pistol; // garde le lien avec le slider "Force de tir" du panneau UI
                }
                else
                {
                    var root = new GameObject(GunRootPrefix + g.prefab);
                    var rb = root.AddComponent<Rigidbody>();
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    var grab = root.AddComponent<XRGrabInteractable>();
                    grab.useDynamicAttach = false;
                    grab.throwOnDetach = true;
                    gun = root.AddComponent<VRPistol>();
                    gun.bulletPrefab = pistol.bulletPrefab;
                    gun.hapticAmplitude = pistol.hapticAmplitude;
                    gun.hapticDuration = pistol.hapticDuration;
                }

                gun.automatic = g.auto;
                gun.roundsPerSecond = g.rate > 0f ? g.rate : gun.roundsPerSecond;
                gun.propulsionForce = g.force;
                gun.bulletUseGravity = g.gravity;
                gun.fireCooldown = g.cooldown;

                if (!SetupGunModel(gun, prefab)) continue;

                if (gun.muzzleFlash == null && flashTemplate != null)
                {
                    var flash = Object.Instantiate(flashTemplate.gameObject, gun.muzzle, false);
                    flash.name = "MuzzleFlash";
                    flash.transform.localPosition = Vector3.zero;
                    flash.transform.localRotation = Quaternion.identity;
                    gun.muzzleFlash = flash.GetComponent<ParticleSystem>();
                }

                // Pose l'arme à plat sur la table, canon vers les cibles.
                Transform rootT = gun.transform;
                rootT.rotation = tableRot;
                Bounds lb = LocalBounds(rootT);
                Vector3 local = new Vector3(g.x, 0f, 0f) - new Vector3(lb.center.x, 0f, lb.center.z);
                Vector3 pos = tableCenter + tableRot * local;
                pos.y = tableTop - lb.min.y + 0.005f;
                rootT.position = pos;
                if (rootT.TryGetComponent(out Rigidbody body)) body.mass = g.prefab.StartsWith("Pistol") ? 1f : 3f;
                EditorUtility.SetDirty(gun);
                count++;
            }
            return count;
        }

        static void PlaceOnTable(GameObject go, Vector3 center, Quaternion rot, float top, float x, float height)
        {
            if (go == null) return;
            Vector3 p = center + rot * new Vector3(x, 0f, -0.2f);
            p.y = top + height;
            go.transform.position = p;
        }

        static Bounds LocalBounds(Transform root)
        {
            var b = new Bounds();
            bool first = true;
            foreach (var box in root.GetComponentsInChildren<BoxCollider>())
            {
                Vector3 c = root.InverseTransformPoint(box.transform.TransformPoint(box.center));
                Vector3 e = root.InverseTransformVector(box.transform.TransformVector(box.size)) / 2f;
                e = new Vector3(Mathf.Abs(e.x), Mathf.Abs(e.y), Mathf.Abs(e.z));
                var bb = new Bounds(c, e * 2f);
                if (first) { b = bb; first = false; } else b.Encapsulate(bb);
            }
            return b;
        }

        /// <summary>
        /// Remplace le modèle d'une arme par un prefab PolyGun et recalcule la prise en main
        /// (juste derrière la gâchette), le bout du canon et le collider.
        /// </summary>
        static bool SetupGunModel(VRPistol gunComp, GameObject prefab)
        {
            Transform root = gunComp.transform;
            Transform model = root.Find(ModelName);
            if (model == null)
            {
                model = new GameObject(ModelName).transform;
                model.SetParent(root, false);
            }

            for (int i = model.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(model.GetChild(i).gameObject);
            foreach (var col in model.GetComponents<Collider>())
                Object.DestroyImmediate(col);
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            model.localScale = Vector3.one;

            var gun = (GameObject)PrefabUtility.InstantiatePrefab(prefab, model);
            gun.transform.localPosition = Vector3.zero;
            gun.transform.localRotation = Quaternion.identity;
            gun.transform.localScale = Vector3.one;
            foreach (var col in gun.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(col);

            List<Vector3> pts = CollectPoints(gun, root);
            if (pts.Count == 0) return false;
            Bounds b = BoundsOf(pts);

            // Canon le long de X ? On aligne le modèle sur Z (les modèles PolyGun regardent vers +Z).
            if (b.size.x > b.size.z * 1.3f)
            {
                model.localRotation = Quaternion.Euler(0f, -90f, 0f);
                pts = CollectPoints(gun, root);
                b = BoundsOf(pts);
            }
            // Échelle aberrante (modèle en cm, etc.)
            if (b.size.z < 0.08f || b.size.z > 2f)
            {
                model.localScale = Vector3.one * (0.25f / Mathf.Max(0.0001f, b.size.z));
                pts = CollectPoints(gun, root);
                b = BoundsOf(pts);
            }

            // Prise en main : juste sous et derrière la gâchette.
            Transform trigger = FindDeep(gun.transform, "Trigger");
            Vector3 grip;
            float triggerY;
            if (trigger != null)
            {
                List<Vector3> tp = CollectPoints(trigger.gameObject, root, false);
                Vector3 t = tp.Count > 0 ? Average(tp) : root.InverseTransformPoint(trigger.position);
                triggerY = t.y;
                grip = t + new Vector3(0f, -0.035f, -0.045f);
            }
            else
            {
                float lowY = b.min.y + b.size.y * 0.35f;
                var low = pts.Where(p => p.y <= lowY).ToList();
                grip = low.Count > 0 ? Average(low) : new Vector3(b.center.x, b.min.y, b.center.z);
                triggerY = grip.y + 0.035f;
            }
            grip.x = b.center.x;

            // Recentre le modèle pour que la prise soit à l'origine de l'arme.
            model.localPosition -= grip;
            for (int i = 0; i < pts.Count; i++) pts[i] -= grip;
            triggerY -= grip.y;
            b = BoundsOf(pts);

            Transform attach = root.Find("GripAttach");
            if (attach == null)
            {
                attach = new GameObject("GripAttach").transform;
                attach.SetParent(root, false);
            }
            attach.localPosition = Vector3.zero;
            attach.localRotation = Quaternion.Euler(15f, 0f, 0f);

            // Bout du canon : points les plus en avant, au-dessus de la gâchette (évite bipied, chargeur...).
            var upper = pts.Where(p => p.y > triggerY).ToList();
            if (upper.Count == 0) upper = pts;
            float frontZ = upper.Max(p => p.z);
            var front = upper.Where(p => p.z >= frontZ - 0.012f).ToList();
            Vector3 muzzlePos = Average(front);
            muzzlePos.z = frontZ + 0.01f;

            Transform muzzle = gunComp.muzzle != null ? gunComp.muzzle : root.Find("Muzzle");
            if (muzzle == null)
            {
                muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(root, false);
            }
            muzzle.localPosition = muzzlePos;
            muzzle.localRotation = Quaternion.identity;
            gunComp.muzzle = muzzle;

            // Un seul collider englobant le modèle : simple et suffisant pour saisir l'arme.
            var box = model.gameObject.AddComponent<BoxCollider>();
            SetBox(box, model, root, b);

            var grab = root.GetComponent<XRGrabInteractable>();
            if (grab != null)
            {
                grab.colliders.Clear();
                grab.attachTransform = attach;
                EditorUtility.SetDirty(grab);
            }
            return true;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        static void SetBox(BoxCollider box, Transform model, Transform root, Bounds rootSpace)
        {
            Vector3 c = model.InverseTransformPoint(root.TransformPoint(rootSpace.center));
            Vector3 s = model.InverseTransformVector(root.TransformVector(rootSpace.size));
            box.center = c;
            box.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }

        static List<Vector3> CollectPoints(GameObject go, Transform space, bool children = true)
        {
            var pts = new List<Vector3>();
            MeshFilter[] filters = children ? go.GetComponentsInChildren<MeshFilter>(true) : go.GetComponents<MeshFilter>();
            foreach (var mf in filters)
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null) continue;
                Vector3[] verts = mesh.vertices;
                if (verts == null || verts.Length == 0)
                {
                    Bounds lb = mesh.bounds;
                    verts = new[] { lb.min, lb.max };
                }
                foreach (Vector3 v in verts)
                    pts.Add(space.InverseTransformPoint(mf.transform.TransformPoint(v)));
            }
            SkinnedMeshRenderer[] skins = children ? go.GetComponentsInChildren<SkinnedMeshRenderer>(true) : go.GetComponents<SkinnedMeshRenderer>();
            foreach (var smr in skins)
            {
                if (smr.sharedMesh == null) continue;
                foreach (Vector3 v in smr.sharedMesh.vertices)
                    pts.Add(space.InverseTransformPoint(smr.transform.TransformPoint(v)));
            }
            return pts;
        }

        static Bounds BoundsOf(List<Vector3> pts)
        {
            var b = new Bounds(pts[0], Vector3.zero);
            foreach (var p in pts) b.Encapsulate(p);
            return b;
        }

        static Vector3 Average(List<Vector3> pts)
        {
            Vector3 s = Vector3.zero;
            foreach (var p in pts) s += p;
            return s / pts.Count;
        }

        // ------------------------------------------------------------------
        // Cartoon FX Remaster
        // ------------------------------------------------------------------
        // Les shaders .cfxrshader sont générés par un importeur qui choisit Built-in ou URP.
        // S'il a détecté le Built-in à l'import, la partie URP est supprimée : particules invisibles.
        // On force donc "ForceUniversalRenderPipeline" (valeur 2) et on réimporte.
        static IEnumerable<AssetImporter> CartoonFxShaderImporters()
        {
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.EndsWith(".cfxrshader")) continue;
                AssetImporter importer = AssetImporter.GetAtPath(path);
                if (importer != null) yield return importer;
            }
        }

        // Les "soft particles" lisent la texture de profondeur de la caméra. Elle est désactivée dans
        // la config URP du projet (plus rapide sur Quest) : ces matériaux deviennent alors invisibles.
        // On coupe donc l'option sur les matériaux Cartoon FX.
        static int CountSoftParticleMaterials(bool fix)
        {
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/JMO Assets")) continue;
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;
                bool soft = mat.IsKeywordEnabled("_FADING_ON") ||
                            (mat.HasProperty("_UseSP") && mat.GetFloat("_UseSP") > 0.5f);
                if (!soft) continue;
                count++;
                if (!fix) continue;
                mat.DisableKeyword("_FADING_ON");
                if (mat.HasProperty("_UseSP")) mat.SetFloat("_UseSP", 0f);
                EditorUtility.SetDirty(mat);
            }
            return count;
        }

        // Scène créée par script : pas d'éclairage ambiant calculé, les faces à l'ombre sont noires.
        // Un ambiant "Trilight" fixe éclaire correctement sans avoir à générer l'éclairage.
        static void FixAmbientLighting()
        {
            if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight) return;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.45f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.24f, 0.22f);
        }

        static bool CartoonFxShadersNeedUrp()
        {
            foreach (AssetImporter importer in CartoonFxShaderImporters())
            {
                var so = new SerializedObject(importer);
                var detected = so.FindProperty("detectedRenderPipeline");
                if (detected != null && !detected.stringValue.Contains("Universal")) return true;
            }
            return false;
        }

        static int FixCartoonFxShaders()
        {
            int count = 0;
            foreach (AssetImporter importer in CartoonFxShaderImporters().ToList())
            {
                var so = new SerializedObject(importer);
                var mode = so.FindProperty("renderPipelineDetection");
                var detected = so.FindProperty("detectedRenderPipeline");
                bool wrongMode = mode != null && mode.enumValueIndex != 2;
                bool notUrp = detected != null && !detected.stringValue.Contains("Universal");
                if (!wrongMode && !notUrp) continue;
                if (mode != null) mode.enumValueIndex = 2;
                so.ApplyModifiedPropertiesWithoutUndo();
                importer.SaveAndReimport();
                count++;
            }
            return count;
        }

        static bool TargetsNeedFx()
        {
            if (FindPrefab(k_Explosions[0], null) == null) return false;
            foreach (var t in TargetPrefabs())
                if (t.explosionPrefabs == null || t.explosionPrefabs.Length == 0) return true;
            return false;
        }

        static IEnumerable<ShootingTarget> TargetPrefabs()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/TP4/Prefabs" }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go != null && go.TryGetComponent(out ShootingTarget t)) yield return t;
            }
        }

        static bool WireCartoonFx()
        {
            GameObject[] explosions = k_Explosions.Select(n => FindPrefab(n, null)).Where(p => p != null).ToArray();
            if (explosions.Length == 0) return false;
            GameObject[] popups = k_Popups.Select(n => FindPrefab(n, null)).Where(p => p != null).ToArray();

            foreach (ShootingTarget t in TargetPrefabs())
            {
                t.explosionPrefabs = explosions;
                t.explosionScale = 0.45f;
                t.popupPrefabs = popups;
                t.popupChance = 0.5f;
                t.popupScale = 0.6f;
                EditorUtility.SetDirty(t);
                PrefabUtility.SavePrefabAsset(t.gameObject);
            }

            GameObject impact = FindPrefab(ImpactName, null);
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/TP4/Prefabs" }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go == null || !go.TryGetComponent(out Bullet bullet)) continue;
                bullet.impactPrefab = impact;
                bullet.impactScale = 0.25f;
                EditorUtility.SetDirty(bullet);
                PrefabUtility.SavePrefabAsset(go);
            }
            return true;
        }

        static GameObject FindPrefab(string name, string folder)
        {
            string[] guids = folder != null && AssetDatabase.IsValidFolder(folder)
                ? AssetDatabase.FindAssets("t:Prefab " + name, new[] { folder })
                : AssetDatabase.FindAssets("t:Prefab " + name);
            // Nom exact, en évitant les anciennes versions ("Old").
            string best = guids.Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetFileNameWithoutExtension(p) == name)
                .OrderBy(p => p.Contains("/Old") ? 1 : 0)
                .FirstOrDefault();
            return best != null ? AssetDatabase.LoadAssetAtPath<GameObject>(best) : null;
        }
    }

    [InitializeOnLoad]
    static class TP4AssetIntegratorAuto
    {
        const string Key = "TP4_AssetIntegration_Checked_v5";

        static TP4AssetIntegratorAuto()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += TryRun;
        }

        static void TryRun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryRun;
                return;
            }
            if (SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, true);
            if (TP4AssetIntegrator.NeedsIntegration())
                TP4AssetIntegrator.Integrate(false);
        }
    }
}
