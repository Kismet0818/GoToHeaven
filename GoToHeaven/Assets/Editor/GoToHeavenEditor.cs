using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GoToHeaven.EditorTools
{
    /// <summary>
    /// Project utilities that keep the runtime-only setup healthy:
    /// texture import settings, quick checks and save management.
    /// </summary>
    public static class GoToHeavenEditor
    {
        const string ArtRoot = "Assets/Resources/GoToHeaven/Art";
        const string LevelRoot = "Assets/Resources/GoToHeaven/Levels";
        const string BootScene = "Assets/Scenes/Boot.scene";

        [MenuItem("GoToHeaven/Open Boot Scene", priority = 1)]
        public static void OpenBootScene()
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScene);
            if (scene != null) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(BootScene);
            else Debug.LogWarning($"[GoToHeaven] Boot scene not found at {BootScene}.");
        }

        [MenuItem("GoToHeaven/Validate Project", priority = 20)]
        public static void ValidateProject()
        {
            int problems = 0;
            foreach (var path in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot }))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(path);
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;
                if (importer.textureType != TextureImporterType.Sprite) problems++;
                if (importer.filterMode != FilterMode.Point) problems++;
            }

            var scenes = AssetDatabase.FindAssets("t:SceneAsset", new[] { "Assets/Scenes" });
            if (scenes.Length == 0)
            {
                Debug.LogError("[GoToHeaven] No scene found under Assets/Scenes.");
                problems++;
            }

            var levels = AssetDatabase.FindAssets("t:Prefab", new[] { LevelRoot });
            if (levels.Length == 0)
            {
                Debug.LogError($"[GoToHeaven] No level prefab found under {LevelRoot}.");
                problems++;
            }

            if (!HasLayer("Ground") || !HasLayer("OneWay") || !HasLayer("Hazard") || !HasLayer("Collectible") || !HasLayer("Player"))
            {
                Debug.LogError("[GoToHeaven] Required layers are missing in ProjectSettings/TagManager.asset.");
                problems++;
            }

            if (problems == 0)
                Debug.Log($"[GoToHeaven] Setup OK. {levels.Length} level(s), art under {ArtRoot}, boot scene {BootScene}.");
            else
                Debug.LogWarning($"[GoToHeaven] Validation found {problems} issue(s). Run GoToHeaven/Fix Art Import Settings if textures look blurry.");
        }

        [MenuItem("GoToHeaven/Sync Art From Material", priority = 22)]
        public static void SyncArtFromMaterial()
        {
            const string sourceRoot = "Assets/Scenes/UI/Material/Pixel Adventure 1/Free/";
            const string targetRoot = "Assets/Resources/GoToHeaven/Art/";
            var map = new Dictionary<string, string>
            {
                { "Main Characters/Mask Dude/Idle (32x32).png", "Characters/mask_idle.png" },
                { "Main Characters/Mask Dude/Run (32x32).png", "Characters/mask_run.png" },
                { "Main Characters/Mask Dude/Jump (32x32).png", "Characters/mask_jump.png" },
                { "Main Characters/Mask Dude/Fall (32x32).png", "Characters/mask_fall.png" },
                { "Main Characters/Mask Dude/Double Jump (32x32).png", "Characters/mask_double_jump.png" },
                { "Main Characters/Mask Dude/Wall Jump (32x32).png", "Characters/mask_wall_jump.png" },
                { "Main Characters/Mask Dude/Hit (32x32).png", "Characters/mask_hit.png" },
                { "Terrain/Terrain (16x16).png", "Environment/terrain.png" },
                { "Items/Fruits/Apple.png", "Items/fruit.png" },
                { "Items/Fruits/Collected.png", "Items/fruit_collected.png" },
                { "Items/Checkpoints/Checkpoint/Checkpoint (Flag Idle)(64x64).png", "Props/checkpoint_flag.png" },
                { "Items/Checkpoints/Checkpoint/Checkpoint (No Flag).png", "Props/checkpoint_off.png" },
                { "Items/Checkpoints/End/End (Idle).png", "Props/goal_idle.png" },
                { "Items/Checkpoints/End/End (Pressed) (64x64).png", "Props/goal_pressed.png" },
                { "Traps/Spikes/Idle.png", "Traps/spikes.png" },
                { "Traps/Trampoline/Idle.png", "Traps/trampoline_idle.png" },
                { "Traps/Trampoline/Jump (28x28).png", "Traps/trampoline_jump.png" },
                { "Traps/Saw/On (38x38).png", "Traps/saw.png" },
                { "Other/Dust Particle.png", "VFX/dust.png" },
                { "Other/Confetti (16x16).png", "VFX/confetti.png" },
                { "Menu/Buttons/Play.png", "UI/play.png" },
                { "Menu/Buttons/Restart.png", "UI/restart.png" },
                { "Menu/Buttons/Back.png", "UI/back.png" },
                { "Menu/Buttons/Close.png", "UI/close.png" },
                { "Menu/Buttons/Levels.png", "UI/levels.png" },
                { "Menu/Buttons/Next.png", "UI/next.png" },
                { "Menu/Buttons/Previous.png", "UI/previous.png" },
                { "Menu/Buttons/Settings.png", "UI/settings.png" },
            };

            int copied = 0;
            foreach (var pair in map)
            {
                var source = Path.GetFullPath(sourceRoot + pair.Key);
                var target = Path.GetFullPath(targetRoot + pair.Value);
                if (!File.Exists(source))
                {
                    Debug.LogWarning($"[GoToHeaven] Source art is missing: {source}");
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? ".");
                File.Copy(source, target, true);
                copied++;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            FixArtImportSettings();
            Debug.Log($"[GoToHeaven] Synced {copied} art file(s) from the Material folder.");
        }

        [MenuItem("GoToHeaven/Fix Art Import Settings", priority = 21)]
        public static void FixArtImportSettings()        {
            int changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool dirty = false;
                if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
                if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
                if (!Mathf.Approximately(importer.spritePixelsPerUnit, 16f)) { importer.spritePixelsPerUnit = 16f; dirty = true; }
                if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; dirty = true; }
                if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    dirty = true;
                }
                if (importer.alphaIsTransparency == false) { importer.alphaIsTransparency = true; dirty = true; }

                if (dirty)
                {
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            Debug.Log($"[GoToHeaven] Art import settings updated on {changed} texture(s).");
        }

        [MenuItem("GoToHeaven/Reset Save Data", priority = 40)]
        public static void ResetSave()
        {
            PlayerPrefs.DeleteKey("GoToHeaven.Save.v1");
            PlayerPrefs.Save();
            Debug.Log("[GoToHeaven] Save data cleared.");
        }

        static bool HasLayer(string name) => LayerMask.NameToLayer(name) >= 0;
    }

    /// <summary>Applies pixel-art import defaults to newly added art under Resources.</summary>
    public sealed class GoToHeavenArtPostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/GoToHeaven/Art/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}

