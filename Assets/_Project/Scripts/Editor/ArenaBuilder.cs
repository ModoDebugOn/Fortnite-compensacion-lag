using System.IO;
using LagCompensation.Presentation.Players;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LagCompensation.Editor
{
    /// <summary>
    /// Generates the demo's own art out of Unity primitives, so nothing on screen comes from a real
    /// game and everything can be regenerated after tweaking a number here:
    /// - Assets: grass texture, materials, the rifle, the wooden build wall, the arena and the
    ///   armed characters (prefab variants of the kit's capsules with the rifle in hand).
    /// - Scene: 00_Arena, the playground every concept scene starts from.
    ///
    /// Regenerating keeps the asset GUIDs, so scenes that already use the prefabs stay linked.
    /// </summary>
    static class ArenaBuilder
    {
        const string Root = "Assets/_Project";
        const string EnvMaterials = Root + "/Materials/Environment";
        const string WeaponMaterials = Root + "/Materials/Weapons";
        const string EnvPrefabs = Root + "/Prefabs/Environment";
        const string WeaponPrefabs = Root + "/Prefabs/Weapons";
        const string PlayerPrefabs = Root + "/Prefabs/Players";
        const string DebugMaterials = Root + "/Materials/Debug";
        const string DebugPrefabs = Root + "/Prefabs/Debug";
        const string ScenePath = Root + "/Scenes/00_Arena.unity";

        // The two leads: the shooter and the one who hides behind the wall.
        static readonly string[] Players = { "Peely", "Raven" };

        // Side of the grass square, in metres (1 m checker cells).
        const float GroundSize = 400f;

        // The layout of the duel (~23 m, a typical mid-range fight). Raven runs along the lane
        // towards +X and hides behind the wall; seen from Peely, the wall covers the end of the lane.
        static readonly Vector3 ShooterSpot = new Vector3(0f, 1f, -15f);
        static readonly Vector3 RunnerStart = new Vector3(-10f, 1f, 8f);
        static readonly Vector3 WallSpot = new Vector3(7f, 0f, 6.5f);

        // Where the rifle sits on the capsule (radius 0.5, pivot in the middle): right hip, forward.
        static readonly Vector3 RifleHold = new Vector3(0.45f, 0.05f, 0.3f);

        // ------------------------------------------------------------------ menu

        [MenuItem("Tools/Lag Compensation/Build Arena Assets")]
        static void BuildAssets()
        {
            foreach (var folder in new[] { EnvMaterials, WeaponMaterials, DebugMaterials, EnvPrefabs, WeaponPrefabs, PlayerPrefabs, DebugPrefabs })
                EnsureFolder(folder);

            var palette = new Palette();
            SavePrefab(BuildGhost(), DebugPrefabs + "/Ghost.prefab");
            GameObject rifle = SavePrefab(BuildRifle(palette), WeaponPrefabs + "/Rifle.prefab");
            GameObject wall = SavePrefab(BuildWall(palette), EnvPrefabs + "/BuildWall.prefab");
            SavePrefab(BuildArena(palette, wall), EnvPrefabs + "/Arena.prefab");
            foreach (string player in Players) BuildArmedVariant(player, rifle);

            AssetDatabase.SaveAssets();
            Debug.Log("[ArenaBuilder] Assets generated.");
        }

        [MenuItem("Tools/Lag Compensation/Build Arena Scene")]
        static void BuildScene()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Build Arena Scene", ScenePath + " already exists. Overwrite it?", "Overwrite", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            BuildAssets();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var light = Object.FindAnyObjectByType<Light>();
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;

            // Distance haze so the grass melts into the sky instead of ending in a hard line.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.78f, 0.86f, 0.93f);
            RenderSettings.fogStartDistance = 50f;
            RenderSettings.fogEndDistance = 160f;

            var camera = Camera.main;
            // Overview from behind and above the shooter, framing the whole duel.
            camera.transform.position = ShooterSpot + new Vector3(0f, 11f, -16f);
            camera.transform.LookAt(new Vector3(0f, 0f, 0f));
            camera.fieldOfView = 50f;
            camera.farClipPlane = 300f;

            Place(EnvPrefabs + "/Arena.prefab", Vector3.zero, 0f);
            Place(Root + "/Prefabs/Decoration/Crowd.prefab", Vector3.zero, 0f);
            // Peely faces the lane; Raven runs to the right, towards the wall.
            Place(PlayerPrefabs + "/PeelyArmed.prefab", ShooterSpot, 0f);
            Place(PlayerPrefabs + "/RavenArmed.prefab", RunnerStart, 90f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[ArenaBuilder] Scene saved at " + ScenePath);
        }

        // ------------------------------------------------------------------ art

        /// <summary>All the colours of the demo in one place, picked to read well on video.</summary>
        sealed class Palette
        {
            public readonly Material Gunmetal = Mat(WeaponMaterials, "Gunmetal", new Color(0.22f, 0.24f, 0.27f), 0.55f);
            public readonly Material GunBlack = Mat(WeaponMaterials, "GunBlack", new Color(0.08f, 0.08f, 0.09f), 0.4f);
            // "Legendary" orange-gold accent: a nod to loot rarity colours, without any real asset.
            public readonly Material Legendary = Mat(WeaponMaterials, "Legendary", new Color(1f, 0.62f, 0.12f), 0.7f);

            public readonly Material WoodLight = Mat(EnvMaterials, "WoodLight", new Color(0.78f, 0.55f, 0.32f), 0.15f);
            public readonly Material WoodDark = Mat(EnvMaterials, "WoodDark", new Color(0.62f, 0.41f, 0.22f), 0.15f);
            public readonly Material WoodFrame = Mat(EnvMaterials, "WoodFrame", new Color(0.40f, 0.25f, 0.13f), 0.15f);
            public readonly Material Grass = GrassMaterial();
            public readonly Material Leaves = Mat(EnvMaterials, "Leaves", new Color(0.22f, 0.58f, 0.25f), 0.1f);
        }

        /// <summary>A short, chunky assault rifle. Pivot at the grip, barrel along +Z, and a
        /// "Muzzle" child at the tip: that is where shots will start from.</summary>
        static GameObject BuildRifle(Palette p)
        {
            var rifle = new GameObject("Rifle");
            var model = new GameObject("Model").transform;
            model.SetParent(rifle.transform, false);
            model.localScale = Vector3.one * 1.3f;   // a bit oversized so it reads on video

            Box("Receiver", model, new Vector3(0f, 0.06f, 0.05f), new Vector3(0.09f, 0.14f, 0.45f), p.Gunmetal);
            Box("Stripe", model, new Vector3(0f, 0.06f, 0.05f), new Vector3(0.095f, 0.035f, 0.3f), p.Legendary);
            Box("Handguard", model, new Vector3(0f, 0.06f, 0.33f), new Vector3(0.075f, 0.09f, 0.18f), p.GunBlack);
            Cylinder("Barrel", model, new Vector3(0f, 0.08f, 0.47f), 0.025f, 0.32f, p.GunBlack);
            Box("Stock", model, new Vector3(0f, 0.04f, -0.27f), new Vector3(0.07f, 0.12f, 0.22f), p.Gunmetal);
            Box("ButtPlate", model, new Vector3(0f, 0.03f, -0.385f), new Vector3(0.075f, 0.15f, 0.03f), p.GunBlack);
            Box("Grip", model, new Vector3(0f, -0.05f, -0.02f), new Vector3(0.05f, 0.13f, 0.06f), p.GunBlack, -15f);
            Box("Magazine", model, new Vector3(0f, -0.07f, 0.14f), new Vector3(0.05f, 0.16f, 0.07f), p.Legendary, 10f);
            Box("ScopeMount", model, new Vector3(0f, 0.14f, 0.05f), new Vector3(0.03f, 0.03f, 0.06f), p.GunBlack);
            Cylinder("Scope", model, new Vector3(0f, 0.18f, 0.05f), 0.03f, 0.18f, p.GunBlack);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(model, false);
            muzzle.localPosition = new Vector3(0f, 0.08f, 0.63f);
            return rifle;
        }

        /// <summary>The wall players build to hide behind: vertical planks in two tones inside a
        /// darker frame. Pivot at the bottom centre; one collider for the whole wall.</summary>
        static GameObject BuildWall(Palette p)
        {
            const float width = 4f, height = 3.6f, thickness = 0.2f;
            const int planks = 5;

            var wall = new GameObject("BuildWall");
            float plankWidth = width / planks;
            for (int i = 0; i < planks; i++)
            {
                float x = -width / 2f + plankWidth * (i + 0.5f);
                Box("Plank" + i, wall.transform, new Vector3(x, height / 2f, 0f),
                    new Vector3(plankWidth - 0.03f, height, thickness), i % 2 == 0 ? p.WoodLight : p.WoodDark);
            }
            foreach (float y in new[] { 0.15f, height / 2f, height - 0.15f })
                Box("Beam", wall.transform, new Vector3(0f, y, 0f), new Vector3(width, 0.22f, thickness + 0.08f), p.WoodFrame);
            foreach (float x in new[] { -width / 2f + 0.1f, width / 2f - 0.1f })
                Box("Post", wall.transform, new Vector3(x, height / 2f, 0f), new Vector3(0.22f, height, thickness + 0.08f), p.WoodFrame);

            var collider = wall.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, height / 2f, 0f);
            collider.size = new Vector3(width, height, thickness);
            return wall;
        }

        /// <summary>The playground: a grass square, the build wall Raven hides behind, a couple of
        /// a few more builds, crates and trees. The crowd ring (radius ~28) goes around it.</summary>
        static GameObject BuildArena(Palette p, GameObject wallPrefab)
        {
            var arena = new GameObject("Arena");
            // Big enough that its edge never shows on camera.
            Box("Ground", arena.transform, new Vector3(0f, -0.1f, 0f), new Vector3(GroundSize, 0.2f, GroundSize), p.Grass, keepCollider: true);

            // The wall of the demo: the one Raven hides behind.
            Wall(arena.transform, wallPrefab, "CoverWall", WallSpot, 0f);
            // Other builds around, just so the field looks like a match in progress.
            Wall(arena.transform, wallPrefab, "BuildWall", new Vector3(-14f, 0f, -2f), 90f);
            Wall(arena.transform, wallPrefab, "BuildWall", new Vector3(16f, 0f, 18f), 30f);   // out of the server camera's way

            // Crates: cover next to Peely and a pile in the far left.
            Crate(arena.transform, new Vector3(-3.5f, 0f, -13.5f), 1.0f, 12f, p.WoodLight);
            Crate(arena.transform, new Vector3(-2.5f, 0f, -14.6f), 0.8f, -20f, p.WoodDark);
            Crate(arena.transform, new Vector3(-17f, 0f, 12f), 1.2f, 5f, p.WoodDark);
            Crate(arena.transform, new Vector3(-15.8f, 0f, 12.6f), 0.9f, 30f, p.WoodLight);

            foreach (var spot in new[] { new Vector3(13f, 0f, 17f), new Vector3(-8f, 0f, 20f), new Vector3(3f, 0f, 24f),
                                         new Vector3(22f, 0f, 4f), new Vector3(-22f, 0f, 6f), new Vector3(-20f, 0f, -16f),
                                         new Vector3(20f, 0f, -20f) })
                Tree(arena.transform, spot, p);
            return arena;
        }

        static void Wall(Transform parent, GameObject wallPrefab, string name, Vector3 position, float yaw)
        {
            var wall = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab, parent);
            wall.name = name;
            wall.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }

        static void Crate(Transform parent, Vector3 ground, float size, float yaw, Material material)
        {
            var crate = Box("Crate", parent, ground + Vector3.up * size / 2f, Vector3.one * size, material, keepCollider: true);
            crate.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        static void Tree(Transform parent, Vector3 position, Palette p)
        {
            var tree = new GameObject("Tree").transform;
            tree.SetParent(parent, false);
            tree.localPosition = position;
            Cylinder("Trunk", tree, new Vector3(0f, 1f, 0f), 0.25f, 2f, p.WoodFrame, upright: true);
            Sphere("Leaves", tree, new Vector3(0f, 2.8f, 0f), 2.4f, p.Leaves);
            Sphere("Leaves", tree, new Vector3(0.5f, 3.7f, 0.2f), 1.6f, p.Leaves);
        }

        /// <summary>A see-through capsule the size of a player: a version of a player that is not the
        /// real one (what the server knows, what another player sees, a past position...).
        /// No collider: ghosts are only drawn. Each instance picks its colour with GhostTint.</summary>
        static GameObject BuildGhost()
        {
            var ghost = Primitive(PrimitiveType.Capsule, "Ghost", null, GhostMaterial(), false);
            ghost.AddComponent<GhostTint>();
            return ghost;
        }

        static Material GhostMaterial()
        {
            var material = Mat(DebugMaterials, "Ghost", Color.white, 0.3f);
            // URP Lit in transparent mode (what the inspector does when Surface Type = Transparent).
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return material;
        }

        /// <summary>Prefab variant of the kit's capsule (e.g. Peely) holding the rifle. Being a
        /// variant, re-baking the outfit in the kit updates the armed version too.</summary>
        static void BuildArmedVariant(string player, GameObject rifle)
        {
            var character = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/Characters/{player}.prefab");
            if (character == null) { Debug.LogError($"[ArenaBuilder] Missing {player}.prefab: bake it with the character kit."); return; }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(character);
            var gun = (GameObject)PrefabUtility.InstantiatePrefab(rifle, instance.transform);
            gun.transform.localPosition = RifleHold;
            PrefabUtility.SaveAsPrefabAsset(instance, $"{PlayerPrefabs}/{player}Armed.prefab");
            Object.DestroyImmediate(instance);
        }

        // ------------------------------------------------------------------ helpers

        static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material,
                              float pitch = 0f, bool keepCollider = false)
        {
            var box = Primitive(PrimitiveType.Cube, name, parent, material, keepCollider);
            box.transform.localPosition = position;
            box.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            box.transform.localScale = size;
            return box;
        }

        /// <summary>Unity's cylinder is 2 units tall along Y; lying down it points along +Z.</summary>
        static void Cylinder(string name, Transform parent, Vector3 position, float radius, float length,
                             Material material, bool upright = false)
        {
            var cylinder = Primitive(PrimitiveType.Cylinder, name, parent, material, false);
            cylinder.transform.localPosition = position;
            cylinder.transform.localRotation = upright ? Quaternion.identity : Quaternion.Euler(90f, 0f, 0f);
            cylinder.transform.localScale = new Vector3(radius * 2f, length / 2f, radius * 2f);
        }

        static void Sphere(string name, Transform parent, Vector3 position, float diameter, Material material)
        {
            var sphere = Primitive(PrimitiveType.Sphere, name, parent, material, false);
            sphere.transform.localPosition = position;
            sphere.transform.localScale = Vector3.one * diameter;
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static void Place(string prefabPath, Vector3 position, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        }

        static GameObject SavePrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static Material Mat(string folder, string name, Color color, float smoothness, Texture texture = null)
        {
            string path = $"{folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Two greens in a 1 m checkerboard with thin darker lines: on video the grid makes
        /// distances and movement (and the lag!) easy to read.</summary>
        static Material GrassMaterial()
        {
            const int size = 128, cell = size / 2, line = 2;
            string path = EnvMaterials + "/GrassGrid.png";
            var a = new Color32(108, 186, 84, 255);
            var b = new Color32(98, 174, 76, 255);
            var edge = new Color32(84, 152, 66, 255);

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool onLine = x % cell < line || y % cell < line;
                bool even = (x / cell + y / cell) % 2 == 0;
                pixels[y * size + x] = onLine ? edge : even ? a : b;
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();

            var material = Mat(EnvMaterials, "Grass", Color.white, 0.05f, AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            material.SetTextureScale("_BaseMap", Vector2.one * GroundSize / 2f);   // 2 m per texture tile
            return material;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
