using Ricochet.AR;
using Ricochet.Audio;
using Ricochet.Cards;
using Ricochet.Core;
using Ricochet.Data;
using Ricochet.Enemies;
using Ricochet.Player;
using Ricochet.Weapons;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Ricochet.EditorTools
{
    public static partial class RicochetBuilder
    {
        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c ? c : go.AddComponent<T>();
        }

        static void DestroyRoot(string name)
        {
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == name) Object.DestroyImmediate(root);
        }

        static void DestroyChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t) Object.DestroyImmediate(t.gameObject);
        }

        static void BuildScene()
        {
            EnsureSceneOpen();

            // ---------- AR rig ----------
            var origin = Object.FindAnyObjectByType<XROrigin>();
            if (origin == null)
            {
                Debug.LogError("[Ricochet] No XR Origin in the scene. Add GameObject > XR > XR Origin (Mobile AR) first.");
                return;
            }
            var cam = origin.Camera;
            var planeManager = GetOrAdd<ARPlaneManager>(origin.gameObject);
            planeManager.planePrefab = LoadPrefab("P_RicochetPlane");
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
            var raycastManager = GetOrAdd<ARRaycastManager>(origin.gameObject);
            var anchorManager = GetOrAdd<ARAnchorManager>(origin.gameObject);

            // ---------- Clean previous build ----------
            foreach (var n in new[] { "Managers", "Pools", "UI Canvas", "Reticle" }) DestroyRoot(n);
            DestroyChild(cam.transform, "Hurtbox");
            DestroyChild(cam.transform, "Viewmodel");

            // ---------- Pools (world space, NOT under the camera) ----------
            var pools = new GameObject("Pools").transform;
            var boltPool = Empty("Bolts", pools).transform;
            var enemyPool = Empty("Enemies", pools).transform;
            var cardPool = Empty("Cards", pools).transform;
            var gadgetPool = Empty("Gadgets", pools).transform;

            // ---------- Player (the AR camera) ----------
            var hurt = Empty("Hurtbox", cam.transform);
            hurt.layer = L("PlayerHurtbox");
            var sphere = hurt.AddComponent<SphereCollider>();
            sphere.radius = 0.25f;
            var rb = hurt.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            BuildViewmodel(cam.transform, out var viewmodel, out var gun, out var muzzle, out var flash);

            var player = GetOrAdd<PlayerController>(cam.gameObject);
            Set(player, ("playerCamera", cam), ("hurtbox", sphere));
            var health = GetOrAdd<PlayerHealth>(cam.gameObject);
            var feedback = GetOrAdd<DamageFeedback>(cam.gameObject);
            Set(feedback, ("health", health), ("viewmodel", viewmodel));
            var blaster = GetOrAdd<LaserBlaster>(cam.gameObject);
            Set(blaster, ("stats", AssetDatabase.LoadAssetAtPath<WeaponStats>($"{SODir}/WeaponStats.asset")),
                ("aimCamera", cam), ("muzzle", muzzle), ("gunModel", gun), ("muzzleFlash", flash),
                ("boltPrefab", LoadPrefab("P_LaserBolt").GetComponent<LaserBolt>()), ("poolRoot", boltPool),
                ("aimMask", Mask("Enemy", "ReflectiveWall", "Mirror", "Prism", "ARFloor")));

            // ---------- Reticle ----------
            var reticle = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab("P_Reticle"));
            reticle.name = "Reticle";
            reticle.SetActive(false);

            // ---------- Managers ----------
            var managers = new GameObject("Managers").transform;

            var placementGo = Empty("Placement", managers);
            var placement = placementGo.AddComponent<ARPlacementController>();
            Set(placement, ("raycastManager", raycastManager), ("planeManager", planeManager), ("anchorManager", anchorManager),
                ("arCamera", cam), ("reticle", reticle.transform), ("arenaPrefab", LoadPrefab("P_Arena").GetComponent<ArenaContext>()));

            var audioGo = Empty("Audio", managers);
            var audio = audioGo.AddComponent<AudioManager>();
            Set(audio, ("library", AssetDatabase.LoadAssetAtPath<SoundLibrary>($"{SODir}/SoundLibrary.asset")));

            var vfxGo = Empty("VFX", managers);
            var vfx = vfxGo.AddComponent<VfxManager>();
            Set(vfx, ("sparks", MakeParticles("Sparks", vfxGo.transform, 0.6f)), ("puffs", MakeParticles("Puffs", vfxGo.transform, -0.1f)));

            var enemiesGo = Empty("Enemies", managers);
            var factory = enemiesGo.AddComponent<EnemyFactory>();
            Set(factory, ("walkerPrefab", LoadPrefab("P_Walker").GetComponent<WalkerEnemy>()),
                ("spitterPrefab", LoadPrefab("P_Spitter").GetComponent<SpitterEnemy>()),
                ("acidPrefab", LoadPrefab("P_AcidGlob").GetComponent<AcidGlob>()), ("poolRoot", enemyPool));
            var spawner = enemiesGo.AddComponent<EnemySpawner>();
            Set(spawner, ("factory", factory));

            var cardsGo = Empty("Cards", managers);
            var cardSpawner = cardsGo.AddComponent<CardSpawner>();
            Set(cardSpawner, ("poolRoot", cardPool), ("cardPrefabs", new Object[]
            {
                LoadPrefab("P_Card_MultiShot").GetComponent<AbilityCard>(),
                LoadPrefab("P_Card_Mirror").GetComponent<AbilityCard>(),
                LoadPrefab("P_Card_Prism").GetComponent<AbilityCard>(),
                LoadPrefab("P_Card_Freeze").GetComponent<AbilityCard>(),
                LoadPrefab("P_Card_MedKit").GetComponent<AbilityCard>(),
            }));
            var placer = cardsGo.AddComponent<AbilityPlacer>();
            Set(placer, ("aimCamera", cam), ("mirrorPrefab", LoadPrefab("P_Mirror").GetComponent<Mirror>()),
                ("prismPrefab", LoadPrefab("P_Prism").GetComponent<Prism>()), ("poolRoot", gadgetPool), ("floorMask", Mask("ARFloor")));

            var gmGo = Empty("GameManager", managers);
            var gm = gmGo.AddComponent<GameManager>();
            Set(gm, ("placement", placement), ("player", player), ("playerHealth", health), ("blaster", blaster),
                ("enemyFactory", factory), ("enemySpawner", spawner), ("cardSpawner", cardSpawner), ("abilityPlacer", placer),
                ("difficulties.options", new Object[]
                {
                    AssetDatabase.LoadAssetAtPath<DifficultySettings>($"{SODir}/Difficulty_Easy.asset"),
                    AssetDatabase.LoadAssetAtPath<DifficultySettings>($"{SODir}/Difficulty_Normal.asset"),
                    AssetDatabase.LoadAssetAtPath<DifficultySettings>($"{SODir}/Difficulty_Hard.asset"),
                }));

            // ---------- UI ----------
            BuildUI();

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Placeholder blaster made from primitives, bottom-right of the screen. Swap the "Gun" children for a real model later.</summary>
        static void BuildViewmodel(Transform cam, out Transform viewmodel, out Transform gun, out Transform muzzle, out GameObject flash)
        {
            viewmodel = Empty("Viewmodel", cam, new Vector3(0.075f, -0.08f, 0.17f)).transform;
            viewmodel.localEulerAngles = new Vector3(2f, -5f, 0f);
            gun = Empty("Gun", viewmodel).transform;

            Material body = M("M_GunBody"), cyan = M("M_GunAccent"), mag = M("M_GunAccent2");
            Prim(PrimitiveType.Cube, "Body", gun, Vector3.zero, new Vector3(0.032f, 0.042f, 0.13f), body);
            Prim(PrimitiveType.Cube, "Rail", gun, new Vector3(0f, 0.025f, 0f), new Vector3(0.012f, 0.008f, 0.11f), cyan);
            Prim(PrimitiveType.Cube, "StripL", gun, new Vector3(-0.0165f, -0.004f, 0.01f), new Vector3(0.002f, 0.006f, 0.09f), cyan);
            Prim(PrimitiveType.Cube, "StripR", gun, new Vector3(0.0165f, -0.004f, 0.01f), new Vector3(0.002f, 0.006f, 0.09f), cyan);
            Prim(PrimitiveType.Cylinder, "Barrel", gun, new Vector3(0f, 0.006f, 0.095f), new Vector3(0.018f, 0.035f, 0.018f), body, new Vector3(90f, 0f, 0f));
            Prim(PrimitiveType.Cylinder, "BarrelTip", gun, new Vector3(0f, 0.006f, 0.13f), new Vector3(0.024f, 0.004f, 0.024f), mag, new Vector3(90f, 0f, 0f));
            Prim(PrimitiveType.Cube, "EnergyCell", gun, new Vector3(0f, -0.006f, -0.035f), new Vector3(0.036f, 0.02f, 0.04f), cyan);
            Prim(PrimitiveType.Cube, "Grip", gun, new Vector3(0f, -0.045f, -0.04f), new Vector3(0.024f, 0.06f, 0.03f), body, new Vector3(15f, 0f, 0f));

            muzzle = Empty("Muzzle", gun, new Vector3(0f, 0.006f, 0.14f)).transform;
            flash = Prim(PrimitiveType.Quad, "MuzzleFlash", muzzle, Vector3.zero, Vector3.one * 0.07f, M("M_MuzzleFlash"));
            Prim(PrimitiveType.Quad, "MuzzleFlash2", flash.transform, Vector3.zero, Vector3.one, M("M_MuzzleFlash"), new Vector3(0f, 0f, 45f));
        }

        static ParticleSystem MakeParticles(string name, Transform parent, float gravity)
        {
            var go = Empty(name, parent);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.startLifetime = 0.5f;
            main.startSize = 0.03f;
            main.maxParticles = 600;
            main.gravityModifier = gravity;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = M("M_Particles");
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }
    }
}
