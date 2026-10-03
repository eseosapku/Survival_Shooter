using System.Collections.Generic;
using System.IO;
using Ricochet.Audio;
using Ricochet.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ricochet.EditorTools
{
    /// <summary>
    /// One-click, repeatable project builder: Ricochet → Build Everything.
    /// Generates textures, sounds, materials, ScriptableObjects, prefabs, UI and wires the Game scene.
    /// Re-running it is safe: assets are updated in place (GUIDs kept), tuning assets are only created once.
    /// </summary>
    public static partial class RicochetBuilder
    {
        public const string FullName = "Eseosa Kay-Uwagboe Pascal";

        const string Root = "Assets/_Project";
        const string TexDir = Root + "/Textures";
        const string MatDir = Root + "/Materials";
        const string AudioDir = Root + "/Audio";
        const string PrefabDir = Root + "/Prefabs";
        const string SODir = Root + "/ScriptableObjects";
        const string ScenePath = Root + "/Scenes/Game.unity";

        // Layers
        static int L(string name) => LayerMask.NameToLayer(name);
        static int Mask(params string[] names) => LayerMask.GetMask(names);

        // Palette
        static readonly Color Cyan = new Color(0.2f, 1f, 1f);
        static readonly Color Magenta = new Color(1f, 0.2f, 0.8f);
        static readonly Color Lime = new Color(0.6f, 1f, 0.15f);

        [MenuItem("Ricochet/Build Everything", priority = 0)]
        public static void BuildEverything()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Ricochet", "Textures", 0.1f);
                BuildTextures();
                EditorUtility.DisplayProgressBar("Ricochet", "Audio", 0.2f);
                BuildAudio();
                EditorUtility.DisplayProgressBar("Ricochet", "Materials", 0.3f);
                BuildMaterials();
                EditorUtility.DisplayProgressBar("Ricochet", "Configs", 0.4f);
                BuildConfigs();
                EditorUtility.DisplayProgressBar("Ricochet", "Prefabs", 0.55f);
                BuildPrefabs();
                EditorUtility.DisplayProgressBar("Ricochet", "Scene & UI", 0.75f);
                BuildScene();
                AssetDatabase.SaveAssets();
                Debug.Log("[Ricochet] Build Everything finished.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // =====================================================================
        // Textures
        // =====================================================================

        static void BuildTextures()
        {
            Directory.CreateDirectory(TexDir);
            TextureFactory.FloorName($"{TexDir}/T_FloorName.png", FullName);
            TextureFactory.WallGrid($"{TexDir}/T_WallGrid.png");
            TextureFactory.Reticle($"{TexDir}/T_Reticle.png");
            TextureFactory.Glow($"{TexDir}/T_Glow.png");
            TextureFactory.RoundedRect($"{TexDir}/UI_Rounded.png", 64, 22f);
            TextureFactory.Circle($"{TexDir}/UI_Circle.png", 256, 0f);
            TextureFactory.Circle($"{TexDir}/UI_Ring.png", 256, 10f);
            TextureFactory.Vignette($"{TexDir}/UI_Vignette.png");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ImportWorldTexture("T_FloorName", true);
            ImportWorldTexture("T_WallGrid", true);
            ImportWorldTexture("T_Reticle", false);
            ImportWorldTexture("T_Glow", false);
            ImportSprite("UI_Rounded", new Vector4(24, 24, 24, 24));
            ImportSprite("UI_Circle", Vector4.zero);
            ImportSprite("UI_Ring", Vector4.zero);
            ImportSprite("UI_Vignette", Vector4.zero);
        }

        static void ImportWorldTexture(string name, bool repeat)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath($"{TexDir}/{name}.png");
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.SaveAndReimport();
        }

        static void ImportSprite(string name, Vector4 border)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath($"{TexDir}/{name}.png");
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.spriteBorder = border;
            imp.SaveAndReimport();
        }

        static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{name}.png");
        static Sprite Spr(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{TexDir}/{name}.png");

        // =====================================================================
        // Audio
        // =====================================================================

        static void BuildAudio()
        {
            SfxSynth.GenerateAll(AudioDir);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (AudioImporter)AssetImporter.GetAtPath(path);
                bool music = Path.GetFileName(path).StartsWith("MUS_");
                imp.forceToMono = true;
                var s = imp.defaultSampleSettings;
                s.loadType = music ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
                s.quality = 0.6f;
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
            }
        }

        static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{name}.wav");

        // =====================================================================
        // Materials
        // =====================================================================

        enum Surface { Opaque, Alpha, Additive }

        const string Unlit = "Universal Render Pipeline/Unlit";
        const string SimpleLit = "Universal Render Pipeline/Simple Lit";
        const string ParticlesUnlit = "Universal Render Pipeline/Particles/Unlit";

        static readonly Dictionary<string, Material> s_Mats = new Dictionary<string, Material>();

        static Material M(string name) => s_Mats.TryGetValue(name, out var m) ? m : AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{name}.mat");

        static Material MakeMat(string name, string shader, Color color, Surface surface = Surface.Opaque,
            Texture tex = null, Vector2? tiling = null, bool doubleSided = false)
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{name}.mat";
            var sh = Shader.Find(shader);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != sh) m.shader = sh;

            m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (tex)
            {
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
            }

            if (surface == Surface.Opaque)
            {
                m.SetFloat("_Surface", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                m.SetFloat("_ZWrite", 1f);
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Opaque");
                m.renderQueue = (int)RenderQueue.Geometry;
            }
            else
            {
                bool add = surface == Surface.Additive;
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", add ? 2f : 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)(add ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)(add ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            m.SetFloat("_Cull", doubleSided ? 0f : 2f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            s_Mats[name] = m;
            return m;
        }

        static void BuildMaterials()
        {
            s_Mats.Clear();
            // AR planes & placement
            MakeMat("M_FloorName", Unlit, Color.white, Surface.Alpha, Tex("T_FloorName"), new Vector2(2f, 2f), true);
            MakeMat("M_WallGrid", Unlit, new Color(1f, 1f, 1f, 0.9f), Surface.Alpha, Tex("T_WallGrid"), new Vector2(2f, 2f), true);
            MakeMat("M_PlaneLine", ParticlesUnlit, Color.white, Surface.Alpha);
            MakeMat("M_Reticle", Unlit, Color.white, Surface.Alpha, Tex("T_Reticle"), null, true);

            // FX
            MakeMat("M_LaserCore", Unlit, new Color(0.75f, 1f, 1f), Surface.Opaque);
            MakeMat("M_Trail", ParticlesUnlit, Color.white, Surface.Additive);
            MakeMat("M_Particles", ParticlesUnlit, Color.white, Surface.Additive, Tex("T_Glow"));
            MakeMat("M_MuzzleFlash", Unlit, new Color(0.4f, 1f, 1f, 0.9f), Surface.Additive, Tex("T_Glow"));
            MakeMat("M_Acid", Unlit, Lime, Surface.Opaque);

            // Beacon
            MakeMat("M_BeaconBase", SimpleLit, new Color(0.1f, 0.12f, 0.16f));
            MakeMat("M_BeaconCore", Unlit, Cyan);
            MakeMat("M_BeaconRing", Unlit, new Color(1f, 0.2f, 0.8f, 0.45f), Surface.Alpha, null, null, true);
            MakeMat("M_BeaconBeam", Unlit, new Color(0.2f, 1f, 1f, 0.25f), Surface.Additive, null, null, true);

            // Gun
            MakeMat("M_GunBody", SimpleLit, new Color(0.13f, 0.14f, 0.18f));
            MakeMat("M_GunAccent", Unlit, Cyan);
            MakeMat("M_GunAccent2", Unlit, Magenta);

            // Enemies
            MakeMat("M_WalkerSkin", SimpleLit, new Color(0.45f, 0.55f, 0.42f));
            MakeMat("M_WalkerCloth", SimpleLit, new Color(0.22f, 0.24f, 0.32f));
            MakeMat("M_WalkerPants", SimpleLit, new Color(0.25f, 0.2f, 0.16f));
            MakeMat("M_ZombieEye", Unlit, new Color(1f, 0.15f, 0.1f));
            MakeMat("M_SpitterSkin", SimpleLit, new Color(0.42f, 0.72f, 0.18f));
            MakeMat("M_SpitterBelly", SimpleLit, new Color(0.75f, 0.85f, 0.3f));
            MakeMat("M_SpitterGlow", Unlit, new Color(0.75f, 1f, 0.1f));

            // Gadgets & cards
            MakeMat("M_MirrorGlass", Unlit, new Color(0.75f, 0.95f, 1f, 0.55f), Surface.Alpha, null, null, true);
            MakeMat("M_MirrorFrame", Unlit, Cyan);
            MakeMat("M_PrismCrystal", Unlit, new Color(1f, 0.35f, 0.9f, 0.6f), Surface.Alpha, null, null, true);
            MakeMat("M_PrismCore", Unlit, new Color(1f, 0.8f, 1f));
            MakeMat("M_CardMultiShot", Unlit, new Color(1f, 0.6f, 0.1f));
            MakeMat("M_CardMirror", Unlit, new Color(0.3f, 0.9f, 1f));
            MakeMat("M_CardPrism", Unlit, new Color(1f, 0.3f, 0.9f));
            MakeMat("M_CardFreeze", Unlit, new Color(0.6f, 0.8f, 1f));
            MakeMat("M_CardMedKit", Unlit, new Color(0.3f, 1f, 0.4f));
            MakeMat("M_CardRing", Unlit, new Color(1f, 1f, 1f, 0.35f), Surface.Alpha, null, null, true);
        }

        // =====================================================================
        // ScriptableObject configs (created once; your inspector tweaks are kept on re-run)
        // =====================================================================

        static T LoadOrCreate<T>(string fileName, out bool created) where T : ScriptableObject
        {
            Directory.CreateDirectory(SODir);
            string path = $"{SODir}/{fileName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created = asset == null;
            if (created)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        static void BuildConfigs()
        {
            Difficulty("Difficulty_Easy", "Easy", 120f, 3.5f, 1.8f, 6, 0.15f, 0.30f, 0.75f, 120f);
            Difficulty("Difficulty_Normal", "Normal", 150f, 2.8f, 1.2f, 9, 0.25f, 0.45f, 1.0f, 100f);
            Difficulty("Difficulty_Hard", "Hard", 180f, 2.0f, 0.8f, 12, 0.35f, 0.60f, 1.3f, 80f);

            var walker = LoadOrCreate<EnemyStats>("EnemyStats_Walker", out bool c1);
            if (c1) Set(walker, ("hitsToKill", 3), ("moveSpeed", 0.5f), ("attackRange", 0.8f), ("damage", 10f), ("attackCooldown", 1.5f), ("scoreValue", 100), ("projectileSpeed", 3f));
            var spitter = LoadOrCreate<EnemyStats>("EnemyStats_Spitter", out bool c2);
            if (c2) Set(spitter, ("hitsToKill", 5), ("moveSpeed", 0.4f), ("attackRange", 3f), ("damage", 8f), ("attackCooldown", 2.5f), ("scoreValue", 150), ("projectileSpeed", 3f));

            var weapon = LoadOrCreate<WeaponStats>("WeaponStats", out bool c3);
            if (c3) Set(weapon, ("boltSpeed", 8f), ("boltLifetime", 2.5f), ("maxBounces", 3), ("boltRadius", 0.03f),
                ("shotsPerSecond", 5f), ("spreadAngle", 8f), ("baseSpreadTier", 1),
                ("heatPerShot", 0.12f), ("coolPerSecond", 0.35f), ("overheatLockTime", 1.5f));

            BuildSoundLibrary();
        }

        static void Difficulty(string file, string display, float round, float startInt, float minInt, int maxAlive,
            float spitA, float spitB, float dmg, float hp)
        {
            var d = LoadOrCreate<DifficultySettings>(file, out bool created);
            if (!created) return;
            Set(d, ("displayName", display), ("roundLength", round), ("startSpawnInterval", startInt), ("minSpawnInterval", minInt),
                ("maxEnemiesAlive", maxAlive), ("spitterChanceStart", spitA), ("spitterChanceEnd", spitB),
                ("enemyDamageMultiplier", dmg), ("playerMaxHealth", hp));
        }

        static void BuildSoundLibrary()
        {
            var lib = LoadOrCreate<SoundLibrary>("SoundLibrary", out _);
            var entries = new List<SoundLibrary.Entry>();

            void Add(SoundId id, string clip, float vol, float pMin, float pMax, bool spatial)
            {
                entries.Add(new SoundLibrary.Entry
                {
                    id = id,
                    clips = new[] { Clip(clip) },
                    volume = vol,
                    pitchRange = new Vector2(pMin, pMax),
                    spatial = spatial
                });
            }

            Add(SoundId.PlayerShoot, "SFX_PlayerShoot", 0.45f, 0.93f, 1.07f, false);
            Add(SoundId.PlayerDeath, "SFX_PlayerDeath", 0.9f, 1f, 1f, false);
            Add(SoundId.EnemySpawn, "SFX_EnemySpawn", 0.9f, 0.9f, 1.1f, true);
            Add(SoundId.SpitterShoot, "SFX_SpitterShoot", 0.9f, 0.9f, 1.1f, true);
            Add(SoundId.WalkerAttackHit, "SFX_WalkerAttackHit", 1f, 0.9f, 1.1f, false);
            Add(SoundId.LaserBounce, "SFX_LaserBounce", 0.55f, 0.9f, 1.15f, true);
            Add(SoundId.EnemyHit, "SFX_EnemyHit", 0.6f, 0.9f, 1.15f, true);
            Add(SoundId.EnemyDeath, "SFX_EnemyDeath", 0.85f, 0.85f, 1.1f, true);
            Add(SoundId.PlayerHurt, "SFX_PlayerHurt", 0.7f, 0.95f, 1.05f, false);
            Add(SoundId.CardPickup, "SFX_CardPickup", 0.7f, 1f, 1f, false);
            Add(SoundId.MirrorPlace, "SFX_MirrorPlace", 0.7f, 1f, 1f, true);
            Add(SoundId.Overheat, "SFX_Overheat", 0.6f, 1f, 1f, false);
            Add(SoundId.CountdownBeep, "SFX_CountdownBeep", 0.6f, 1f, 1f, false);
            Add(SoundId.CountdownGo, "SFX_CountdownGo", 0.7f, 1f, 1f, false);
            Add(SoundId.RoundWin, "SFX_RoundWin", 0.8f, 1f, 1f, false);
            Add(SoundId.UIClick, "SFX_UIClick", 0.5f, 0.95f, 1.05f, false);
            Add(SoundId.Music, "MUS_Loop", 1f, 1f, 1f, false);

            lib.EditorSetEntries(entries);
            EditorUtility.SetDirty(lib);
        }

        // =====================================================================
        // Serialized-field helpers (fields are private, so we go through SerializedObject)
        // =====================================================================

        public static void Set(Object target, params (string field, object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in values)
            {
                var p = so.FindProperty(field);
                if (p == null)
                {
                    Debug.LogError($"[Ricochet] {target.GetType().Name} has no serialized field '{field}'");
                    continue;
                }
                switch (value)
                {
                    case null: p.objectReferenceValue = null; break;
                    case Object o: p.objectReferenceValue = o; break;
                    case int i:
                        if (p.propertyType == SerializedPropertyType.Float) p.floatValue = i;
                        else p.intValue = i;
                        break;
                    case float f: p.floatValue = f; break;
                    case bool b: p.boolValue = b; break;
                    case string s: p.stringValue = s; break;
                    case Color c: p.colorValue = c; break;
                    case Vector2 v2: p.vector2Value = v2; break;
                    case Vector3 v3: p.vector3Value = v3; break;
                    case Object[] arr:
                        p.arraySize = arr.Length;
                        for (int k = 0; k < arr.Length; k++) p.GetArrayElementAtIndex(k).objectReferenceValue = arr[k];
                        break;
                    default: Debug.LogError($"[Ricochet] Unsupported value type for {field}"); break;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localEulerAngles = euler;
            g.transform.localScale = scale;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return g;
        }

        static GameObject Empty(string name, Transform parent, Vector3 pos = default)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            return g;
        }

        static T SavePrefab<T>(GameObject temp, string name) where T : Component
        {
            Directory.CreateDirectory(PrefabDir);
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(temp);
            return typeof(T) == typeof(Transform) ? prefab.transform as T : prefab.GetComponent<T>();
        }

        static void EnsureSceneOpen()
        {
            var active = EditorSceneManager.GetActiveScene();
            if (active.path != ScenePath)
            {
                EditorSceneManager.SaveOpenScenes();
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }
    }
}
