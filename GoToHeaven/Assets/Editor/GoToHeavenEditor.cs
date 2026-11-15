using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GoToHeaven.EditorTools
{
    /// <summary>
    /// 维护“纯运行时搭建”这套方案的编辑器工具集：
    /// 贴图导入设置、项目体检与存档管理。
    /// </summary>
    public static class GoToHeavenEditor
    {
        // 资源根与启动场景的约定路径；运行时按这些位置找美术与关卡，改名必须同步改代码。
        const string ArtRoot = "Assets/Resources/GoToHeaven/Art";
        const string LevelRoot = "Assets/Resources/GoToHeaven/Levels";
        const string BootScene = "Assets/Scenes/Boot.scene";

        // 菜单项：直接打开启动场景，省得在 Project 窗口里逐层翻目录。
        [MenuItem("GoToHeaven/Open Boot Scene", priority = 1)]
        public static void OpenBootScene()
        {
            // 先确认资源存在再打开：OpenScene 拿到不存在的路径会抛异常，打断菜单调用。
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScene);
            if (scene != null) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(BootScene);
            else Debug.LogWarning($"[GoToHeaven] Boot scene not found at {BootScene}.");
        }

        // 菜单项：一次性体检项目配置，只报告不修改，免得误碰美术资源。
        [MenuItem("GoToHeaven/Validate Project", priority = 20)]
        public static void ValidateProject()
        {
            int problems = 0;
            // 逐张检查美术贴图：必须是 Sprite 且过滤模式为 Point，否则像素画会被糊掉。
            foreach (var path in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot }))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(path);
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;
                if (importer.textureType != TextureImporterType.Sprite) problems++;
                if (importer.filterMode != FilterMode.Point) problems++;
            }

            // 场景与关卡预制体为空，通常意味着资源没导入或目录被挪走，直接计为问题。
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

            // 层名缺失会让落地、受伤与拾取判定整体失效，而运行时只表现为“踩不到地面”。
            if (!HasLayer("Ground") || !HasLayer("OneWay") || !HasLayer("Hazard") || !HasLayer("Collectible") || !HasLayer("Player"))
            {
                Debug.LogError("[GoToHeaven] Required layers are missing in ProjectSettings/TagManager.asset.");
                problems++;
            }

            // 全部通过只打普通日志；有问题时顺手给出可执行的修复菜单名，省去来回沟通。
            if (problems == 0)
                Debug.Log($"[GoToHeaven] Setup OK. {levels.Length} level(s), art under {ArtRoot}, boot scene {BootScene}.");
            else
                Debug.LogWarning($"[GoToHeaven] Validation found {problems} issue(s). Run GoToHeaven/Fix Art Import Settings if textures look blurry.");
        }

        // 菜单项：把素材包里的原始 PNG 复制到 Resources，并顺手统一导入设置。
        [MenuItem("GoToHeaven/Sync Art From Material", priority = 22)]
        public static void SyncArtFromMaterial()
        {
            const string sourceRoot = "Assets/Scenes/UI/Material/Pixel Adventure 1/Free/";
            const string targetRoot = "Assets/Resources/GoToHeaven/Art/";
            // 源文件 → Resources 内的目标名；目标名被代码里的 ArtPaths 直接引用，不能随手改。
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

            // 逐张复制：源文件缺失只警告不中断，缺一张也要把其余资源同步完。
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

            // 强制同步导入，保证紧接着调用的 FixArtImportSettings 能拿到新的 TextureImporter。
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            FixArtImportSettings();
            Debug.Log($"[GoToHeaven] Synced {copied} art file(s) from the Material folder.");
        }

        // 菜单项：把像素画导入参数一次性刷成项目标准值；新导入的图由文件末尾的后处理器接住。
        [MenuItem("GoToHeaven/Fix Art Import Settings", priority = 21)]
        public static void FixArtImportSettings()        {
            int changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                // 逐项对齐像素画规范：Sprite 类型、16 像素/单位、Point 过滤、无 mipmap、不压缩。
                // 只有值真的不对才标记 dirty，避免原封不动也触发一次全量重导入。
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

        // 菜单项：清掉存档键，用来复现“首次启动”的表现；只删本项目自己的键。
        [MenuItem("GoToHeaven/Reset Save Data", priority = 40)]
        public static void ResetSave()
        {
            // 键名必须与 SaveSystem.PrefsKey 一致，否则这里删不掉任何东西。
            PlayerPrefs.DeleteKey("GoToHeaven.Save.v1");
            PlayerPrefs.Save();
            // 注意：运行中的 GameManager 仍持有内存里的旧数据，要重进播放模式才会重读。
            Debug.Log("[GoToHeaven] Save data cleared.");
        }

        // 层是否存在只能按名字查，ProjectSettings 里改过名字就会判为缺失。
        static bool HasLayer(string name) => LayerMask.NameToLayer(name) >= 0;
    }

    /// <summary>给 Resources 下新增的美术资源自动套用像素画导入默认值，省去手动改设置。</summary>
    public sealed class GoToHeavenArtPostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            // 只拦美术目录，UI 字体图集等其它贴图保留各自的导入设置。
            if (!assetPath.StartsWith("Assets/Resources/GoToHeaven/Art/")) return;
            // 与 FixArtImportSettings 保持同一套值；导入流程中直接生效，无需再 SaveAndReimport。
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

