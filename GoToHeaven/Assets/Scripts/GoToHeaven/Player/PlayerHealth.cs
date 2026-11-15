using System.Collections;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>玩家生命：心数、无敌帧、受击反馈与重生交接。</summary>
    public sealed class PlayerHealth : MonoBehaviour
    {
        public const int MaxHearts = 3; // 满血只有 3 颗心，容错刻意做得很低

        /// <summary>当前剩余心数。</summary>
        public int Current { get; private set; } = MaxHearts;
        /// <summary>心数是否已归零（进入死亡流程）。</summary>
        public bool Depleted { get; private set; }
        /// <summary>是否正在受击/重生的过渡流程中，此期间不再接受新的伤害。</summary>
        public bool IsRespawning { get; private set; }

        LevelController level;
        PlayerController controller;
        PlayerAnimator animator;
        SpriteRenderer[] renderers; // 缓存的全部渲染器，闪烁时逐个开关
        float invulnerableTimer;
        Coroutine blinkRoutine;

        /// <summary>无敌帧是否仍然有效。</summary>
        public bool IsInvulnerable => invulnerableTimer > 0f;

        /// <summary>由关卡在生成玩家时调用，取齐组件引用并重置到满血状态。</summary>
        public void Initialize(LevelController owner)
        {
            level = owner;
            controller = GetComponent<PlayerController>();
            animator = GetComponent<PlayerAnimator>();
            renderers = GetComponentsInChildren<SpriteRenderer>(true); // 含隐藏的子物体，闪烁才能覆盖到全部部件
            Current = MaxHearts;
            Depleted = false;
            IsRespawning = false;
            invulnerableTimer = 0f;
        }

        /// <summary>扣血并进入受击流程；无敌帧或过渡中调用会被直接忽略。</summary>
        public void TakeDamage(int amount = 1, bool knockedBack = true, float respawnDelay = 0.38f)
        {
            // 先做守卫、再改状态：若先上锁再 return，角色会永久停在过渡态里无法再受伤。
            if (Depleted || IsRespawning || invulnerableTimer > 0f) return;

            IsRespawning = true; // 立刻上锁，防止同一帧内多个伤害源重复结算
            Current = Mathf.Max(0, Current - amount);
            GameManager.Instance?.Audio?.Play(Sfx.Hurt);
            CameraFollow.Shake(0.3f, 0.25f);

            if (controller != null)
            {
                controller.SetControl(false);
                if (knockedBack)
                {
                    // 先 Teleport 到原地再给冲量：Teleport 会清速度，顺序反了击退就被抹掉。
                    controller.Teleport(transform.position);
                    controller.AddVelocity(new Vector2(-controller.Facing * 5f, 9f));
                }
            }

            if (Current <= 0)
            {
                Depleted = true;
                StartCoroutine(DeathRoutine());
            }
            else
            {
                StartCoroutine(HitRoutine(respawnDelay));
            }
        }

        /// <summary>掉进坑里时的专用入口：不击退，且用极短延迟快速重生。</summary>
        public void FallIntoPit()
        {
            if (Depleted || IsRespawning) return;
            TakeDamage(1, knockedBack: false, respawnDelay: 0.12f);
        }

        IEnumerator HitRoutine(float delay)
        {
            animator?.PlayHit();
            // 等受击动画的起手帧，玩家才看得清“是自己撞上去的”，而不是瞬间闪回检查点。
            if (delay > 0f) yield return new WaitForSeconds(delay);

            level?.RespawnPlayer();
            if (controller != null)
            {
                controller.ResetForRespawn();
                controller.SetControl(true);
            }

            SetInvulnerable(1.2f); // 重生后给足无敌时间，避免立刻被同一处陷阱连击
            IsRespawning = false;
        }

        IEnumerator DeathRoutine()
        {
            animator?.PlayHit();
            yield return new WaitForSeconds(0.85f); // 留出受击动画与音效收尾，再把控制权交回关卡
            level?.OnPlayerDefeated();
        }

        /// <summary>叠加一段无敌时间并启动闪烁；多次调用取较长者，短的无敌不会覆盖长的。</summary>
        public void SetInvulnerable(float duration)
        {
            invulnerableTimer = Mathf.Max(invulnerableTimer, duration);
            if (blinkRoutine != null) StopCoroutine(blinkRoutine); // 重启前先停旧的，否则两条协程会抢着改可见性
            blinkRoutine = StartCoroutine(BlinkRoutine());
        }

        IEnumerator BlinkRoutine()
        {
            float elapsed = 0f;
            // 多给 0.01 秒余量：抵消 Update 与协程在同一帧的先后差，闪烁才能盖满整段无敌时间。
            while (elapsed < invulnerableTimer + 0.01f)
            {
                bool visible = Mathf.Repeat(elapsed, 0.16f) < 0.09f; // 0.16 秒一个周期，亮 0.09 秒，约为 6Hz 闪烁
                SetRenderersVisible(visible);
                elapsed += Time.deltaTime;
                yield return null;
            }
            SetRenderersVisible(true);
        }

        void SetRenderersVisible(bool visible)
        {
            if (renderers == null) return;
            foreach (var renderer in renderers)
                if (renderer != null) renderer.enabled = visible; // 判空：子部件可能在运行时被销毁
        }

        void Update()
        {
            // 无敌时间在 Update 里走，和闪烁协程共用 Time.deltaTime，两者不会渐渐错位。
            if (invulnerableTimer > 0f) invulnerableTimer = Mathf.Max(0f, invulnerableTimer - Time.deltaTime);
        }

        /// <summary>重置为满血、解除死亡与重生锁，并把渲染器恢复为可见。</summary>
        public void RestoreAll()
        {
            Current = MaxHearts;
            Depleted = false;
            IsRespawning = false;
            invulnerableTimer = 0f;
            SetRenderersVisible(true);
        }
    }
}
