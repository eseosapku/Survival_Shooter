using Ricochet.AR;
using Ricochet.Cards;
using Ricochet.Data;
using Ricochet.Enemies;
using Ricochet.UI;
using Ricochet.Weapons;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;

namespace Ricochet.EditorTools
{
    public static partial class RicochetBuilder
    {
        static void BuildPrefabs()
        {
            BuildPlanePrefab();
            BuildReticlePrefab();
            BuildArenaPrefab();
            BuildLaserBoltPrefab();
            BuildAcidPrefab();
            BuildWalkerPrefab();
            BuildSpitterPrefab();
            BuildMirrorPrefab();
            BuildPrismPrefab();
            BuildCardPrefabs();
            BuildFloatingTextPrefab();
        }

        static GameObject LoadPrefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab");

        // ---------------- AR ----------------

        /// <summary>Same components as XR → AR Default Plane, plus our ARPlaneStyler and a neon outline.</summary>
        static void BuildPlanePrefab()
        {
            var go = new GameObject("P_RicochetPlane");
            go.AddComponent<ARPlane>();
            go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = M("M_FloorName");
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.AddComponent<MeshCollider>();

            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = M("M_PlaneLine");
            line.useWorldSpace = false;
            line.loop = true;
            line.widthMultiplier = 0.01f;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;

            go.AddComponent<ARPlaneMeshVisualizer>();
            var styler = go.AddComponent<ARPlaneStyler>();
            Set(styler, ("floorMaterial", M("M_FloorName")), ("wallMaterial", M("M_WallGrid")));
            go.layer = L("ARFloor");
            SavePrefab<Transform>(go, "P_RicochetPlane");
        }

        static void BuildReticlePrefab()
        {
            var go = new GameObject("P_Reticle");
            Prim(PrimitiveType.Quad, "Ring", go.transform, new Vector3(0f, 0.005f, 0f), Vector3.one * 0.3f, M("M_Reticle"), new Vector3(90f, 0f, 0f));
            Prim(PrimitiveType.Cube, "Arrow", go.transform, new Vector3(0f, 0.006f, 0.19f), new Vector3(0.03f, 0.002f, 0.06f), M("M_BeaconCore"));
            SavePrefab<Transform>(go, "P_Reticle");
        }

        static void BuildArenaPrefab()
        {
            var go = new GameObject("P_Arena");
            var ctx = go.AddComponent<ArenaContext>();
            var visual = Empty("Model", go.transform).transform;

            Prim(PrimitiveType.Cylinder, "Base", visual, new Vector3(0f, 0.01f, 0f), new Vector3(0.3f, 0.01f, 0.3f), M("M_BeaconBase"));
            Prim(PrimitiveType.Cylinder, "Ring", visual, new Vector3(0f, 0.004f, 0f), new Vector3(0.8f, 0.002f, 0.8f), M("M_BeaconRing"));
            var spinner = Empty("Spinner", visual).transform;
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                Prim(PrimitiveType.Cube, $"Pylon{i}", spinner, new Vector3(Mathf.Sin(a) * 0.11f, 0.08f, Mathf.Cos(a) * 0.11f),
                    new Vector3(0.02f, 0.14f, 0.02f), M("M_GunAccent2"), new Vector3(0f, i * 120f, 12f));
            }
            var core = Prim(PrimitiveType.Sphere, "Core", visual, new Vector3(0f, 0.22f, 0f), Vector3.one * 0.08f, M("M_BeaconCore"));
            Prim(PrimitiveType.Cylinder, "Beam", visual, new Vector3(0f, 0.5f, 0f), new Vector3(0.025f, 0.5f, 0.025f), M("M_BeaconBeam"));

            Set(ctx, ("beacon", core.transform), ("spinner", spinner), ("fallbackRadius", 4f), ("floorMask", Mask("ARFloor")));
            SavePrefab<Transform>(go, "P_Arena");
        }

        // ---------------- Projectiles ----------------

        static TrailRenderer AddTrail(GameObject go, Color color, float time, float width)
        {
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = M("M_Trail");
            trail.time = time;
            trail.minVertexDistance = 0.01f;
            trail.widthMultiplier = width;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            return trail;
        }

        static void BuildLaserBoltPrefab()
        {
            var go = new GameObject("P_LaserBolt");
            Prim(PrimitiveType.Sphere, "Core", go.transform, Vector3.zero, new Vector3(0.03f, 0.03f, 0.14f), M("M_LaserCore"));
            Prim(PrimitiveType.Quad, "Glow", go.transform, Vector3.zero, Vector3.one * 0.12f, M("M_MuzzleFlash"));
            var trail = AddTrail(go, Cyan, 0.08f, 0.035f);
            var bolt = go.AddComponent<LaserBolt>();
            Set(bolt, ("trail", trail), ("hitMask", Mask("Enemy", "ReflectiveWall", "Mirror", "Prism", "ARFloor")));
            SetLayerRecursive(go, L("PlayerProjectile"));
            SavePrefab<Transform>(go, "P_LaserBolt");
        }

        static void BuildAcidPrefab()
        {
            var go = new GameObject("P_AcidGlob");
            Prim(PrimitiveType.Sphere, "Blob", go.transform, Vector3.zero, Vector3.one * 0.1f, M("M_Acid"));
            Prim(PrimitiveType.Sphere, "Drip", go.transform, new Vector3(0.03f, -0.02f, -0.03f), Vector3.one * 0.05f, M("M_SpitterGlow"));
            var trail = AddTrail(go, Lime, 0.18f, 0.06f);
            var glob = go.AddComponent<AcidGlob>();
            Set(glob, ("hitMask", Mask("PlayerHurtbox", "ReflectiveWall", "Mirror", "ARFloor")), ("radius", 0.06f), ("trail", trail));
            SetLayerRecursive(go, L("EnemyProjectile"));
            SavePrefab<Transform>(go, "P_AcidGlob");
        }

        // ---------------- Enemies (placeholder models: children of "Model") ----------------

        static GameObject EnemyRoot(string name, float height, float radius, out Transform model, out CapsuleCollider col)
        {
            var go = new GameObject(name);
            col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, height * 0.5f, 0f);
            col.height = height;
            col.radius = radius;
            model = Empty("Model", go.transform).transform;
            return go;
        }

        static void BuildWalkerPrefab()
        {
            var go = EnemyRoot("P_Walker", 1.65f, 0.28f, out var model, out var col);
            Material skin = M("M_WalkerSkin"), cloth = M("M_WalkerCloth"), pants = M("M_WalkerPants"), eye = M("M_ZombieEye");

            Prim(PrimitiveType.Cube, "LegL", model, new Vector3(-0.1f, 0.38f, 0f), new Vector3(0.14f, 0.76f, 0.16f), pants, new Vector3(-6f, 0f, 0f));
            Prim(PrimitiveType.Cube, "LegR", model, new Vector3(0.1f, 0.38f, 0.02f), new Vector3(0.14f, 0.76f, 0.16f), pants, new Vector3(8f, 0f, 0f));
            var torso = Prim(PrimitiveType.Cube, "Torso", model, new Vector3(0f, 1.05f, 0.04f), new Vector3(0.44f, 0.58f, 0.25f), cloth, new Vector3(12f, 0f, 3f));
            Prim(PrimitiveType.Cube, "TornPatch", torso.transform, new Vector3(0.2f, -0.2f, 0.52f), new Vector3(0.45f, 0.35f, 0.05f), skin);
            var head = Prim(PrimitiveType.Sphere, "Head", model, new Vector3(0.02f, 1.48f, 0.1f), new Vector3(0.27f, 0.3f, 0.27f), skin, new Vector3(15f, 0f, -10f));
            Prim(PrimitiveType.Sphere, "EyeL", head.transform, new Vector3(-0.18f, 0.08f, 0.42f), Vector3.one * 0.18f, eye);
            Prim(PrimitiveType.Sphere, "EyeR", head.transform, new Vector3(0.18f, 0.08f, 0.42f), Vector3.one * 0.18f, eye);
            Prim(PrimitiveType.Cube, "Jaw", head.transform, new Vector3(0f, -0.38f, 0.3f), new Vector3(0.55f, 0.22f, 0.4f), M("M_WalkerPants"), new Vector3(20f, 0f, 0f));
            // Classic zombie arms reaching forward.
            Prim(PrimitiveType.Cube, "ArmL", model, new Vector3(-0.29f, 1.22f, 0.3f), new Vector3(0.1f, 0.1f, 0.6f), skin, new Vector3(-8f, 6f, 0f));
            Prim(PrimitiveType.Cube, "ArmR", model, new Vector3(0.29f, 1.16f, 0.28f), new Vector3(0.1f, 0.1f, 0.58f), skin, new Vector3(4f, -4f, 0f));

            go.AddComponent<HitFlash>();
            var walker = go.AddComponent<WalkerEnemy>();
            Set(walker, ("stats", AssetDatabase.LoadAssetAtPath<EnemyStats>($"{SODir}/EnemyStats_Walker.asset")),
                ("model", model), ("hitCollider", col), ("deathColor", new Color(0.6f, 0.9f, 0.5f)));
            SetLayerRecursive(go, L("Enemy"));
            SavePrefab<Transform>(go, "P_Walker");
        }

        static void BuildSpitterPrefab()
        {
            var go = EnemyRoot("P_Spitter", 1.4f, 0.4f, out var model, out var col);
            Material skin = M("M_SpitterSkin"), belly = M("M_SpitterBelly"), glow = M("M_SpitterGlow"), eye = M("M_ZombieEye");

            Prim(PrimitiveType.Cube, "LegL", model, new Vector3(-0.15f, 0.22f, 0f), new Vector3(0.16f, 0.44f, 0.16f), skin);
            Prim(PrimitiveType.Cube, "LegR", model, new Vector3(0.15f, 0.22f, 0f), new Vector3(0.16f, 0.44f, 0.16f), skin);
            Prim(PrimitiveType.Sphere, "Body", model, new Vector3(0f, 0.78f, 0f), new Vector3(0.78f, 0.74f, 0.68f), skin);
            Prim(PrimitiveType.Sphere, "Belly", model, new Vector3(0f, 0.72f, 0.14f), new Vector3(0.6f, 0.56f, 0.46f), belly);
            Prim(PrimitiveType.Sphere, "Pustule1", model, new Vector3(-0.3f, 0.95f, 0.18f), Vector3.one * 0.12f, glow);
            Prim(PrimitiveType.Sphere, "Pustule2", model, new Vector3(0.32f, 0.7f, 0.15f), Vector3.one * 0.1f, glow);
            Prim(PrimitiveType.Sphere, "Pustule3", model, new Vector3(0.12f, 1.05f, -0.25f), Vector3.one * 0.14f, glow);
            Prim(PrimitiveType.Sphere, "ArmL", model, new Vector3(-0.42f, 0.85f, 0.05f), new Vector3(0.16f, 0.32f, 0.16f), skin, new Vector3(0f, 0f, 25f));
            Prim(PrimitiveType.Sphere, "ArmR", model, new Vector3(0.42f, 0.85f, 0.05f), new Vector3(0.16f, 0.32f, 0.16f), skin, new Vector3(0f, 0f, -25f));
            var head = Prim(PrimitiveType.Sphere, "Head", model, new Vector3(0f, 1.22f, 0.08f), Vector3.one * 0.34f, skin);
            Prim(PrimitiveType.Sphere, "EyeL", head.transform, new Vector3(-0.2f, 0.18f, 0.4f), Vector3.one * 0.16f, eye);
            Prim(PrimitiveType.Sphere, "EyeR", head.transform, new Vector3(0.2f, 0.18f, 0.4f), Vector3.one * 0.16f, eye);
            var mouthGlow = Prim(PrimitiveType.Sphere, "MouthGlow", model, new Vector3(0f, 1.14f, 0.22f), Vector3.one * 0.15f, glow);
            var mouth = Empty("Mouth", model, new Vector3(0f, 1.14f, 0.32f)).transform;

            go.AddComponent<HitFlash>();
            var spitter = go.AddComponent<SpitterEnemy>();
            Set(spitter, ("stats", AssetDatabase.LoadAssetAtPath<EnemyStats>($"{SODir}/EnemyStats_Spitter.asset")),
                ("model", model), ("hitCollider", col), ("deathColor", Lime),
                ("mouth", mouth), ("mouthGlow", mouthGlow.transform));
            SetLayerRecursive(go, L("Enemy"));
            SavePrefab<Transform>(go, "P_Spitter");
        }

        // ---------------- Gadgets ----------------

        static void BuildMirrorPrefab()
        {
            var go = new GameObject("P_Mirror");
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.32f, 0f);
            box.size = new Vector3(0.5f, 0.6f, 0.03f);
            var visual = Empty("Visual", go.transform).transform;
            Prim(PrimitiveType.Cube, "Glass", visual, new Vector3(0f, 0.32f, 0f), new Vector3(0.48f, 0.58f, 0.01f), M("M_MirrorGlass"));
            Prim(PrimitiveType.Cube, "FrameTop", visual, new Vector3(0f, 0.62f, 0f), new Vector3(0.52f, 0.02f, 0.03f), M("M_MirrorFrame"));
            Prim(PrimitiveType.Cube, "FrameBottom", visual, new Vector3(0f, 0.02f, 0f), new Vector3(0.52f, 0.02f, 0.03f), M("M_MirrorFrame"));
            Prim(PrimitiveType.Cube, "FrameL", visual, new Vector3(-0.25f, 0.32f, 0f), new Vector3(0.02f, 0.62f, 0.03f), M("M_MirrorFrame"));
            Prim(PrimitiveType.Cube, "FrameR", visual, new Vector3(0.25f, 0.32f, 0f), new Vector3(0.02f, 0.62f, 0.03f), M("M_MirrorFrame"));
            Prim(PrimitiveType.Cube, "Foot", visual, new Vector3(0f, 0.005f, 0f), new Vector3(0.3f, 0.01f, 0.15f), M("M_BeaconBase"));
            var mirror = go.AddComponent<Mirror>();
            Set(mirror, ("visual", visual));
            SetLayerRecursive(go, L("Mirror"));
            SavePrefab<Transform>(go, "P_Mirror");
        }

        static void BuildPrismPrefab()
        {
            var go = new GameObject("P_Prism");
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.22f, 0f);
            box.size = new Vector3(0.24f, 0.4f, 0.24f);
            var visual = Empty("Visual", go.transform).transform;
            Prim(PrimitiveType.Cylinder, "Base", visual, new Vector3(0f, 0.01f, 0f), new Vector3(0.22f, 0.01f, 0.22f), M("M_BeaconBase"));
            var spinner = Empty("Spinner", visual, new Vector3(0f, 0.22f, 0f)).transform;
            Prim(PrimitiveType.Cube, "CrystalA", spinner, Vector3.zero, new Vector3(0.14f, 0.32f, 0.14f), M("M_PrismCrystal"), new Vector3(0f, 45f, 0f));
            Prim(PrimitiveType.Cube, "CrystalB", spinner, Vector3.zero, new Vector3(0.12f, 0.26f, 0.12f), M("M_PrismCrystal"), new Vector3(0f, 0f, 0f));
            Prim(PrimitiveType.Sphere, "Core", spinner, Vector3.zero, Vector3.one * 0.06f, M("M_PrismCore"));
            var prism = go.AddComponent<Prism>();
            Set(prism, ("visual", visual), ("spinner", spinner));
            SetLayerRecursive(go, L("Prism"));
            SavePrefab<Transform>(go, "P_Prism");
        }

        // ---------------- Cards ----------------

        static void BuildCardPrefabs()
        {
            BuildCard<MultiShotCard>("P_Card_MultiShot", "MULTI-SHOT", "III", "M_CardMultiShot", new Color(1f, 0.6f, 0.1f));
            BuildCard<MirrorCard>("P_Card_Mirror", "MIRROR +1", "M", "M_CardMirror", new Color(0.3f, 0.9f, 1f));
            BuildCard<PrismCard>("P_Card_Prism", "PRISM +1", "P", "M_CardPrism", new Color(1f, 0.3f, 0.9f));
            BuildCard<FreezeCard>("P_Card_Freeze", "FREEZE PULSE", "*", "M_CardFreeze", new Color(0.6f, 0.8f, 1f));
            BuildCard<MedKitCard>("P_Card_MedKit", "MED KIT +30", "+", "M_CardMedKit", new Color(0.3f, 1f, 0.4f));
        }

        static void BuildCard<T>(string prefabName, string title, string icon, string mat, Color color) where T : AbilityCard
        {
            var go = new GameObject(prefabName);
            Prim(PrimitiveType.Cylinder, "FloorRing", go.transform, new Vector3(0f, 0.003f, 0f), new Vector3(0.36f, 0.002f, 0.36f), M("M_CardRing"));
            var visual = Empty("Visual", go.transform, new Vector3(0f, 0.25f, 0f)).transform;
            Prim(PrimitiveType.Cube, "Card", visual, Vector3.zero, new Vector3(0.18f, 0.26f, 0.012f), M(mat));
            Prim(PrimitiveType.Cube, "Inset", visual, Vector3.zero, new Vector3(0.14f, 0.22f, 0.014f), M("M_BeaconBase"));
            AddCardIcon(visual, icon, color, 0.008f, 0f);
            AddCardIcon(visual, icon, color, -0.008f, 180f);

            var card = go.AddComponent<T>();
            Set(card, ("title", title), ("color", color), ("visual", visual));
            SavePrefab<Transform>(go, prefabName);
        }

        static void AddCardIcon(Transform parent, string icon, Color color, float z, float yaw)
        {
            var g = new GameObject("Icon");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = new Vector3(0f, 0f, z);
            // TextMeshPro faces -Z by default; rotate so it faces outward on each side.
            g.transform.localEulerAngles = new Vector3(0f, yaw + 180f, 0f);
            var t = g.AddComponent<TextMeshPro>();
            t.font = TMP_Settings.defaultFontAsset;
            t.text = icon;
            t.fontSize = 1.2f;
            t.fontStyle = FontStyles.Bold;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta = new Vector2(0.14f, 0.2f);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            g.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ---------------- UI ----------------

        static void BuildFloatingTextPrefab()
        {
            var go = new GameObject("P_FloatingText", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(700f, 100f);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = TMP_Settings.defaultFontAsset;
            t.fontSize = 64f;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            var ft = go.AddComponent<FloatingText>();
            Set(ft, ("label", t));
            SavePrefab<Transform>(go, "P_FloatingText");
        }
    }
}
