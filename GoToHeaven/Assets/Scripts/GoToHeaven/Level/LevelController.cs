using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// Runtime controller placed on every level prefab. The world (Tilemap + props)
    /// is authored in the prefab; this class only spawns the player, binds entities,
    /// computes camera bounds and owns checkpoint/complete/death flow.
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
        CameraFollow cameraFollow;

        public void Initialize(LevelData data, GameManager owner)
        {
            Data = data;
            game = owner;
            art = owner.Art;

            var spawn = GetComponentInChildren<PlayerSpawn>(true);
            SpawnPoint = spawn != null ? (Vector2)spawn.transform.position : data.spawn.V;

            var coins = GetComponentsInChildren<Coin>(true);
            TotalCoins = coins.Length;
            for (int i = 0; i < coins.Length; i++) coins[i].Bind(this, i * 0.7f);

            foreach (var checkpoint in GetComponentsInChildren<Checkpoint>(true)) checkpoint.Bind(this);
            foreach (var goal in GetComponentsInChildren<Goal>(true)) goal.Bind(this);
            foreach (var trampoline in GetComponentsInChildren<Trampoline>(true)) trampoline.Bind(this);
            foreach (var saw in GetComponentsInChildren<SawTrap>(true)) saw.Bind();

            BuildPlayer();

            foreach (var mover in GetComponentsInChildren<MovingPlatform>(true)) mover.Bind(Player);

            BuildBounds();
            SetupCamera();
            Backdrop.Create(owner.MainCamera, Data.theme, transform);
        }

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
        }

        void BuildBounds()
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            bool any = false;

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer) continue;
                var b = renderer.bounds;
                minX = Mathf.Min(minX, b.min.x); maxX = Mathf.Max(maxX, b.max.x);
                minY = Mathf.Min(minY, b.min.y); maxY = Mathf.Max(maxY, b.max.y);
                any = true;
            }
            foreach (var collider in GetComponentsInChildren<Collider2D>(true))
            {
                var b = collider.bounds;
                minX = Mathf.Min(minX, b.min.x); maxX = Mathf.Max(maxX, b.max.x);
                minY = Mathf.Min(minY, b.min.y); maxY = Mathf.Max(maxY, b.max.y);
                any = true;
            }

            if (!any)
            {
                minX = SpawnPoint.x - 10f; maxX = SpawnPoint.x + 10f;
                minY = SpawnPoint.y - 4f; maxY = SpawnPoint.y + 8f;
            }

            Bounds = new Rect(minX - 3f, minY - 4f, (maxX - minX) + 6f, (maxY - minY) + 10f);
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

        void Update()
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
