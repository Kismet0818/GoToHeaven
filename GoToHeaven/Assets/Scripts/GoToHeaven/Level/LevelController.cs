using System.Collections.Generic;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// Builds the complete playable scene from a LevelData object: platforms, tiles,
    /// props, player and camera bounds. Nothing needs to be authored in a scene.
    /// </summary>
    public sealed class LevelController : MonoBehaviour
    {
        public LevelData Data { get; private set; }
        public PlayerController Player { get; private set; }
        public PlayerHealth Health { get; private set; }
        public Vector2 SpawnPoint { get; private set; }
        public int TotalCoins { get; private set; }
        public int CollectedCoins { get; private set; }
        public Rect Bounds { get; private set; }
        public bool Completed { get; private set; }

        GameManager game;
        SpriteLibrary art;
        Transform world;
        Transform platformRoot;
        Transform propRoot;
        Transform coinRoot;
        CameraFollow cameraFollow;
        readonly List<MovingPlatform> movingPlatforms = new List<MovingPlatform>();
        readonly List<Checkpoint> checkpoints = new List<Checkpoint>();

        struct TileStyle
        {
            public int topLeft;
            public int topMiddle;
            public int topRight;
            public int[] fill;
        }

        public void Initialize(LevelData data, GameManager owner)
        {
            Data = data ?? LevelData.CreateFallback();
            game = owner;
            art = owner.Art;
            if (Data.goal == null) Data.goal = new GoalDef();
            SpawnPoint = Data.spawn.V;

            var backdrop = Backdrop.Create(owner.MainCamera, Data.theme, transform);
            backdrop.name = "Backdrop";

            world = new GameObject("World").transform;
            world.SetParent(transform, false);

            platformRoot = new GameObject("Platforms").transform;
            platformRoot.SetParent(world, false);
            propRoot = new GameObject("Props").transform;
            propRoot.SetParent(world, false);
            coinRoot = new GameObject("Coins").transform;
            coinRoot.SetParent(world, false);

            BuildPlatforms();
            BuildProps();
            BuildPlayer();
            BuildBounds();
            SetupCamera();
        }

        // ---------------------------------------------------------------- platforms

        void BuildPlatforms()
        {
            foreach (var platform in Data.platforms)
            {
                if (platform == null) continue;
                CreatePlatform(platform.x, platform.y, platform.width, platform.height, platform.type, platform.oneWay);
            }

            foreach (var mover in Data.movers)
            {
                if (mover == null) continue;
                var go = CreatePlatformObject(mover.x, mover.y, mover.width, mover.height, mover.type, false);
                var platform = go.GetComponent<Platform>();
                var body = go.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                var moverComponent = go.AddComponent<MovingPlatform>();
                moverComponent.Setup(new Vector2(mover.x, mover.y), new Vector2(mover.moveX, mover.moveY), mover.speed, mover.phase);
                movingPlatforms.Add(moverComponent);
                if (platform != null) platform.OneWay = false;
            }
        }

        void CreatePlatform(float x, float y, float width, float height, string type, bool oneWay)
        {
            CreatePlatformObject(x, y, width, height, type, oneWay);
        }

        GameObject CreatePlatformObject(float x, float y, float width, float height, string type, bool oneWay)
        {
            width = Mathf.Max(0.5f, width);
            height = Mathf.Max(0.5f, height);

            var go = new GameObject($"Platform {x:0.#},{y:0.#}");
            go.transform.SetParent(platformRoot, false);
            go.transform.position = new Vector3(x, y, 0f);
            go.layer = oneWay ? GameLayers.OneWay : GameLayers.Ground;

            var marker = go.AddComponent<Platform>();
            marker.OneWay = oneWay;

            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(width, height);
            box.offset = new Vector2(width * 0.5f, height * 0.5f);

            if (oneWay)
            {
                var effector = go.AddComponent<PlatformEffector2D>();
                effector.useOneWay = true;
                effector.useOneWayGrouping = false;
                effector.useSideBounce = false;
                effector.useSideFriction = false;
                effector.surfaceArc = 150f;
                box.usedByEffector = true;
            }

            var tiles = new GameObject("Tiles").transform;
            tiles.SetParent(go.transform, false);
            RenderTiles(tiles, width, height, type);
            return go;
        }

        void RenderTiles(Transform parent, float width, float height, string type)
        {
            var style = GetStyle(type);
            int columns = Mathf.Max(1, Mathf.RoundToInt(width));
            int rows = Mathf.Max(1, Mathf.RoundToInt(height));

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int index;
                    if (row == rows - 1)
                    {
                        if (columns == 1) index = style.topMiddle;
                        else if (column == 0) index = style.topLeft;
                        else if (column == columns - 1) index = style.topRight;
                        else index = style.topMiddle;
                    }
                    else
                    {
                        index = style.fill[(column + row) % style.fill.Length];
                    }

                    var sprite = art.TerrainTile(index);
                    var tile = new GameObject("t");
                    tile.transform.SetParent(parent, false);
                    tile.transform.localPosition = new Vector3(column + 0.5f, row + 0.5f, 0f);
                    var renderer = tile.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite != null ? sprite : ProceduralArt.White;
                    renderer.color = sprite != null ? Color.white : new Color(0.36f, 0.55f, 0.36f);
                    renderer.sortingOrder = 0;
                }
            }
        }

        TileStyle GetStyle(string type)
        {
            switch ((type ?? "grass").ToLowerInvariant())
            {
                case "stone":
                    return new TileStyle { topLeft = 100, topMiddle = 101, topRight = 102, fill = new[] { 100, 101, 102 } };
                case "brick":
                    return new TileStyle { topLeft = 105, topMiddle = 106, topRight = 107, fill = new[] { 105, 106, 107 } };
                case "ice":
                    return new TileStyle { topLeft = 158, topMiddle = 159, topRight = 160, fill = new[] { 180, 181, 182 } };
                default:
                    return new TileStyle { topLeft = 6, topMiddle = 7, topRight = 8, fill = new[] { 50, 51, 52 } };
            }
        }

        // ---------------------------------------------------------------- props

        void BuildProps()
        {
            var fruitFrames = art.Frames(ArtPaths.Fruit, 32, 32, 24f, SpriteLibrary.CenterPivot);
            TotalCoins = Data.coins != null ? Data.coins.Length : 0;
            for (int i = 0; i < TotalCoins; i++)
            {
                var def = Data.coins[i];
                if (def == null) continue;
                var go = new GameObject("Coin");
                go.transform.SetParent(coinRoot, false);
                go.transform.position = new Vector3(def.x, def.y, 0f);
                go.layer = GameLayers.Collectible;

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = fruitFrames.Length > 0 ? fruitFrames[0] : ProceduralArt.Circle(12, new Color(1f, 0.78f, 0.25f));
                renderer.sortingOrder = 8;

                var animator = go.AddComponent<SpriteAnimator>();
                if (fruitFrames.Length > 1) animator.Play(fruitFrames, 18f, true);

                var circle = go.AddComponent<CircleCollider2D>();
                circle.isTrigger = true;
                circle.radius = 0.45f;

                var coin = go.AddComponent<Coin>();
                coin.Initialize(this, i * 0.7f);
            }

            var spikeSprite = art.Frames(ArtPaths.Spikes, 16, 16, 16f, SpriteLibrary.CenterPivot);
            foreach (var spike in Data.spikes)
            {
                if (spike == null) continue;
                var go = new GameObject("Spikes");
                go.transform.SetParent(propRoot, false);
                go.transform.position = new Vector3(spike.x, spike.y, 0f);
                go.layer = GameLayers.Hazard;

                int count = Mathf.Max(1, Mathf.RoundToInt(spike.width));
                for (int i = 0; i < count; i++)
                {
                    var tile = new GameObject("s");
                    tile.transform.SetParent(go.transform, false);
                    tile.transform.localPosition = new Vector3(i + 0.5f, 0.5f, 0f);
                    var renderer = tile.AddComponent<SpriteRenderer>();
                    renderer.sprite = spikeSprite.Length > 0 ? spikeSprite[0] : ProceduralArt.White;
                    renderer.sortingOrder = 5;
                }

                var trigger = go.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                trigger.size = new Vector2(count, 0.6f);
                trigger.offset = new Vector2(count * 0.5f, 0.3f);
                go.AddComponent<Hazard>();
            }

            var sawFrames = art.Frames(ArtPaths.Saw, 38, 38, 20f, SpriteLibrary.CenterPivot);
            foreach (var saw in Data.saws)
            {
                if (saw == null) continue;
                var go = new GameObject("Saw");
                go.transform.SetParent(propRoot, false);
                go.transform.position = new Vector3(saw.x, saw.y, 0f);
                go.layer = GameLayers.Hazard;

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sawFrames.Length > 0 ? sawFrames[0] : ProceduralArt.Circle(20, new Color(0.85f, 0.85f, 0.9f));
                renderer.sortingOrder = 6;

                var animator = go.AddComponent<SpriteAnimator>();
                if (sawFrames.Length > 1) animator.Play(sawFrames, 16f, true);

                var circle = go.AddComponent<CircleCollider2D>();
                circle.isTrigger = true;
                circle.radius = 0.78f;
                go.AddComponent<Hazard>();

                var trap = go.AddComponent<SawTrap>();
                trap.Setup(new Vector2(saw.x, saw.y), new Vector2(saw.moveX, saw.moveY), saw.speed, saw.phase);
            }

            var trampolineIdle = art.Frames(ArtPaths.TrampolineIdle, 28, 28, 16f, SpriteLibrary.PropPivot);
            var trampolineJump = art.Frames(ArtPaths.TrampolineJump, 28, 28, 16f, SpriteLibrary.PropPivot);
            foreach (var trampoline in Data.trampolines)
            {
                if (trampoline == null) continue;
                var go = new GameObject("Trampoline");
                go.transform.SetParent(propRoot, false);
                go.transform.position = new Vector3(trampoline.x, trampoline.y, 0f);
                go.layer = GameLayers.Hazard;

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = trampolineIdle.Length > 0 ? trampolineIdle[0] : ProceduralArt.White;
                renderer.sortingOrder = 5;

                var animator = go.AddComponent<SpriteAnimator>();
                var trigger = go.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                trigger.size = new Vector2(1.5f, 0.55f);
                trigger.offset = new Vector2(0f, 1.35f);

                var component = go.AddComponent<Trampoline>();
                component.Initialize(this, renderer.sprite, trampolineJump, trampoline.power);
                if (animator != null) animator.enabled = true;
            }

            var checkpointOff = art.Frames(ArtPaths.CheckpointOff, 64, 64, 32f, SpriteLibrary.PropPivot);
            var checkpointFlag = art.Frames(ArtPaths.CheckpointOn, 64, 64, 32f, SpriteLibrary.PropPivot);
            foreach (var checkpoint in Data.checkpoints)
            {
                if (checkpoint == null) continue;
                var go = new GameObject("Checkpoint");
                go.transform.SetParent(propRoot, false);
                go.transform.position = new Vector3(checkpoint.x, checkpoint.y, 0f);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = checkpointOff.Length > 0 ? checkpointOff[0] : ProceduralArt.White;
                renderer.sortingOrder = 5;

                go.AddComponent<SpriteAnimator>();
                var trigger = go.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                trigger.size = new Vector2(1.7f, 2.3f);
                trigger.offset = new Vector2(0f, 1.15f);

                var component = go.AddComponent<Checkpoint>();
                component.Initialize(this, renderer.sprite, checkpointFlag);
                checkpoints.Add(component);
            }

            var goalIdle = art.Frames(ArtPaths.GoalIdle, 64, 64, 28f, SpriteLibrary.PropPivot);
            var goalPressed = art.Frames(ArtPaths.GoalPressed, 64, 64, 28f, SpriteLibrary.PropPivot);
            {
                var go = new GameObject("Goal");
                go.transform.SetParent(propRoot, false);
                go.transform.position = new Vector3(Data.goal.x, Data.goal.y, 0f);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = goalIdle.Length > 0 ? goalIdle[0] : ProceduralArt.White;
                renderer.sortingOrder = 5;

                go.AddComponent<SpriteAnimator>();
                var trigger = go.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                trigger.size = new Vector2(2.2f, 3.2f);
                trigger.offset = new Vector2(0f, 1.6f);

                var component = go.AddComponent<Goal>();
                component.Initialize(this, goalPressed);
            }
        }

        // ---------------------------------------------------------------- player & camera

        void BuildPlayer()
        {
            var go = new GameObject("Player");
            go.layer = GameLayers.Player;
            go.transform.position = SpawnPoint + new Vector2(0f, 0.03f);

            var renderer = go.AddComponent<SpriteRenderer>();
            var idleFrames = art.Frames(ArtPaths.PlayerIdle, 32, 32, 16f, SpriteLibrary.PlayerPivot);
            renderer.sprite = idleFrames.Length > 0 ? idleFrames[0] : ProceduralArt.White;
            renderer.sortingOrder = 10;

            var animator = go.AddComponent<SpriteAnimator>();
            animator.Renderer = renderer;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.72f, 1.72f);
            box.offset = new Vector2(0f, 0.88f);
            box.sharedMaterial = new PhysicsMaterial2D("Player") { friction = 0f, bounciness = 0f };

            Player = go.AddComponent<PlayerController>();
            Health = go.AddComponent<PlayerHealth>();
            go.AddComponent<PlayerAnimator>();
            Health.Initialize(this);

            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(go.transform, false);
            shadow.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            shadow.transform.localScale = new Vector3(0.8f, 0.7f, 1f);
            var shadowRenderer = shadow.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = ProceduralArt.Shadow();
            shadowRenderer.sortingOrder = 9;

            foreach (var mover in movingPlatforms) mover.SetPlayer(Player);
        }

        void BuildBounds()
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;

            foreach (var platform in Data.platforms)
            {
                if (platform == null) continue;
                Encapsulate(platform.x, platform.y, ref minX, ref minY, ref maxX, ref maxY);
                Encapsulate(platform.x + platform.width, platform.y + platform.height, ref minX, ref minY, ref maxX, ref maxY);
            }
            foreach (var mover in Data.movers)
            {
                if (mover == null) continue;
                Encapsulate(mover.x, mover.y, ref minX, ref minY, ref maxX, ref maxY);
                Encapsulate(mover.x + mover.width + mover.moveX, mover.y + mover.height + mover.moveY, ref minX, ref minY, ref maxX, ref maxY);
            }

            Encapsulate(Data.spawn.x, Data.spawn.y, ref minX, ref minY, ref maxX, ref maxY);
            Encapsulate(Data.goal.x - 2f, Data.goal.y, ref minX, ref minY, ref maxX, ref maxY);
            Encapsulate(Data.goal.x + 2f, Data.goal.y + 3f, ref minX, ref minY, ref maxX, ref maxY);

            if (minX > maxX)
            {
                minX = -20f; maxX = 20f; minY = -5f; maxY = 10f;
            }

            Bounds = new Rect(minX - 3f, minY - 4f, (maxX - minX) + 6f, (maxY - minY) + 10f);
        }

        static void Encapsulate(float x, float y, ref float minX, ref float minY, ref float maxX, ref float maxY)
        {
            minX = Mathf.Min(minX, x);
            minY = Mathf.Min(minY, y);
            maxX = Mathf.Max(maxX, x);
            maxY = Mathf.Max(maxY, y);
        }

        void SetupCamera()
        {
            var camera = game.MainCamera;
            if (camera == null) return;
            cameraFollow = camera.GetComponent<CameraFollow>();
            if (cameraFollow == null) cameraFollow = camera.gameObject.AddComponent<CameraFollow>();
            cameraFollow.Configure(8f);
            cameraFollow.SetTarget(Player.transform, Bounds, true);
        }

        // ---------------------------------------------------------------- runtime events

        public void Update()
        {
            if (Player == null || Completed) return;
            if (Player.transform.position.y < Data.killY && Health != null && !Health.Depleted && !Health.IsRespawning)
                Health.FallIntoPit();
        }

        public void ActivateCheckpoint(Checkpoint checkpoint)
        {
            SpawnPoint = checkpoint.SpawnPoint;
            game?.UI?.Toast("CHECKPOINT");
        }

        public void CollectCoin(Coin coin)
        {
            CollectedCoins++;
            game?.AddRunCoin();
            GameManager.Instance?.Audio?.Play(Sfx.Coin);
            Fx.Sparkle(coin.transform.position, new Color(1f, 0.86f, 0.35f), 6);
        }

        public void RespawnPlayer()
        {
            if (Player == null) return;
            Player.Teleport(SpawnPoint);
            cameraFollow?.SnapToTarget();
        }

        public void CompleteLevel()
        {
            if (Completed) return;
            Completed = true;
            Player?.SetControl(false);
            Player?.Halt();
            GameManager.Instance?.Audio?.Play(Sfx.Victory);
            game?.OnLevelCompleted();
        }

        public void OnPlayerDefeated()
        {
            game?.OnPlayerDefeated();
        }

        public void FreezePlayer(bool freeze)
        {
            if (Player == null) return;
            Player.SetControl(!freeze);
            if (freeze) Player.Halt();
        }

        public void SetPlayerControl(bool enabled)
        {
            Player?.SetControl(enabled);
        }
    }
}
