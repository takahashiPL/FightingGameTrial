using UnityEngine;

namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// Visual State に応じて Sprite を切り替える表示専用コンポーネントです。
    ///
    /// 責務:
    /// - 各 Visual State に対応する <see cref="FighterSpriteSequence"/> を保持する
    /// - SimulationSession から渡された State で Sequence を選び、SpriteRenderer.sprite を切り替える
    /// - 固定 CombatFrame 基準でコマを進める（Animator / Time.deltaTime は使わない）
    ///
    /// やらないこと:
    /// - 入力・Facing・Jump 種類の判定（Session）
    /// - Jump 軌道計算
    /// - Gameplay State の終了判定（Attack 終了等は AttackState が正本。Sequence 長に依存しない）
    /// - flipX / color の上書き（Motor / Participant）
    ///
    /// 内部状態と表示 Sequence は別概念です。
    /// 1枚でも複数枚でも同じ Sequence 構造にし、将来コマを増やしてもこのクラス構造を変えずに済みます。
    /// WalkForward / WalkBackward は同じ walkSequence を共有します（後退の逆再生はしない）。
    /// JumpRise / JumpFall は今回別 Sequence ですが、将来同じ参照を渡せば共有もできます。
    /// </summary>
    public class DebugFighterVisual : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("切り替える対象の SpriteRenderer です。")]
        [SerializeField]
        private SpriteRenderer spriteRenderer;

        [Header("Visual Sequences（1枚でも複数枚でも同じ構造）")]
        [SerializeField]
        private FighterSpriteSequence idleSequence = new FighterSpriteSequence();

        [Tooltip("WalkForward / WalkBackward で共有します。")]
        [SerializeField]
        private FighterSpriteSequence walkSequence = new FighterSpriteSequence();

        [SerializeField]
        private FighterSpriteSequence jumpStartSequence = new FighterSpriteSequence();

        [SerializeField]
        private FighterSpriteSequence jumpRiseSequence = new FighterSpriteSequence();

        [SerializeField]
        private FighterSpriteSequence jumpApexSequence = new FighterSpriteSequence();

        [SerializeField]
        private FighterSpriteSequence jumpFallSequence = new FighterSpriteSequence();

        [SerializeField]
        private FighterSpriteSequence landingSequence = new FighterSpriteSequence();

        [SerializeField]
        private FighterSpriteSequence attackSequence = new FighterSpriteSequence();

        [Tooltip("地上 Kick 用。Attack（Punch）Sequence とは別です。")]
        [SerializeField]
        private FighterSpriteSequence kickSequence = new FighterSpriteSequence();

        [Tooltip("Ground Clash 専用。未設定なら Idle Sequence へ fallback。")]
        [SerializeField]
        private FighterSpriteSequence clashRecoilSequence = new FighterSpriteSequence();

        [Tooltip("未設定なら Idle Sequence へ fallback。")]
        [SerializeField]
        private FighterSpriteSequence hitStunSequence = new FighterSpriteSequence();

        [Tooltip("未設定なら Idle Sequence へ fallback。")]
        [SerializeField]
        private FighterSpriteSequence koSequence = new FighterSpriteSequence();

        [Header("ジャンプ見た目の長さ（CombatFrame・State 判定用）")]
        [Tooltip(
            "JumpStart を出す JumpElapsedFrames の上限です。"
            + " Session が Motor.JumpElapsedFrames と比較します。軌道計算には使いません。"
        )]
        [SerializeField]
        private int jumpStartVisualFrames = 2;

        [Tooltip(
            "Apex 通過後に JumpApex を出す CombatFrame 数です。"
            + " Session が Motor.FramesSinceJumpApex と比較します。軌道計算には使いません。"
        )]
        [SerializeField]
        private int jumpApexVisualFrames = 2;

        [Header("Jパンチ見た目の区切り（ActionFrame）")]
        [Tooltip(
            "この ActionFrame 以下（かつ1以上）のあいだ攻撃ポーズとみなします。"
            + " 判定は Session。ここは境界値の公開のみ。"
        )]
        [SerializeField]
        private int attackPoseEndFrame = 6;

        [Tooltip("Jパンチ全体の最終 ActionFrame（Session の終了判定用）。")]
        [SerializeField]
        private int actionEndFrame = 12;

        private FighterVisualState currentVisualState = FighterVisualState.Idle;

        /// <summary>
        /// 現在の Visual State に入ってからの CombatFrame 経過です。
        /// State 変化で 0。HitStop / Pause 中は Session が進めないため止まります。
        /// </summary>
        private int visualStateElapsedFrames;

        private string currentVisualLabel = "Idle";

        public FighterVisualState CurrentVisualState
        {
            get { return currentVisualState; }
        }

        public string CurrentVisualLabel
        {
            get { return currentVisualLabel; }
        }

        public int AttackPoseEndFrame
        {
            get { return attackPoseEndFrame; }
        }

        public int ActionEndFrame
        {
            get { return actionEndFrame; }
        }

        public int JumpStartVisualFrames
        {
            get
            {
                if (jumpStartVisualFrames < 0)
                {
                    return 0;
                }

                return jumpStartVisualFrames;
            }
        }

        public int JumpApexVisualFrames
        {
            get
            {
                if (jumpApexVisualFrames < 1)
                {
                    return 1;
                }

                return jumpApexVisualFrames;
            }
        }

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                Debug.LogError("DebugFighterVisual: SpriteRenderer が未設定です。");
            }

            if (idleSequence == null || idleSequence.IsValid == false)
            {
                Debug.LogError("DebugFighterVisual: idleSequence に有効な Sprite がありません。");
            }

            if (attackSequence == null || attackSequence.IsValid == false)
            {
                Debug.LogError("DebugFighterVisual: attackSequence に有効な Sprite がありません。");
            }

            if (kickSequence == null || kickSequence.IsValid == false)
            {
                Debug.LogError("DebugFighterVisual: kickSequence に有効な Sprite がありません。");
            }

            ResetToIdle();
        }

        /// <summary>
        /// Training Reset / 初期化: Idle に戻し、elapsed=0、idleSequence 先頭へ。
        /// </summary>
        public void ResetToIdle()
        {
            currentVisualState = FighterVisualState.Idle;
            visualStateElapsedFrames = 0;
            currentVisualLabel = "Idle";
            ApplySpriteForCurrentState();
        }

        /// <summary>
        /// Visual State を見た目へ反映します。
        ///
        /// advanceElapsedForCombatFrame:
        /// - true … この CombatFrame の本更新（同じ State なら elapsed++）。1 CombatFrame につき Session が最大1回 true で呼ぶ。
        /// - false … 同一 CombatFrame 内の再適用 / Reset / Aキー開始など（二重加算しない）
        ///
        /// HitStop / Pause 中は CombatFrame が進まないため Session が true で呼ばず、
        /// elapsed と Sprite コマが止まります。終了後はその位置から再開します。
        /// </summary>
        public void Apply(FighterVisualState visualState, bool advanceElapsedForCombatFrame)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (visualState != currentVisualState)
            {
                currentVisualState = visualState;
                visualStateElapsedFrames = 0;
            }
            else if (advanceElapsedForCombatFrame)
            {
                if (visualStateElapsedFrames < int.MaxValue)
                {
                    visualStateElapsedFrames = visualStateElapsedFrames + 1;
                }
            }

            currentVisualLabel = currentVisualState.ToString();
            ApplySpriteForCurrentState();
        }

        public void Apply(FighterVisualState visualState)
        {
            Apply(visualState, false);
        }

        /// <summary>
        /// J Punch の攻撃ポーズ表示窓です（既存挙動維持）。
        /// AF 1〜attackPoseEndFrame のあいだ Attack Sequence を出します。
        /// </summary>
        public bool IsAttackPoseActive(bool isActionPlaying, int actionFrame, bool isJPunchAttack)
        {
            if (isJPunchAttack == false || isActionPlaying == false)
            {
                return false;
            }

            return actionFrame >= 1 && actionFrame <= attackPoseEndFrame;
        }

        /// <summary>
        /// 地上 Kick の攻撃ポーズ表示です。
        /// AF 1 以上かつ TotalFrames 未満のあいだ Kick Sequence を出します
        /// （終了判定の正本は AttackData.TotalFrames。Sequence 長ではない）。
        /// </summary>
        public bool IsKickPoseActive(
            bool isActionPlaying,
            int actionFrame,
            bool isGroundKickAttack,
            int kickTotalFrames)
        {
            if (isGroundKickAttack == false || isActionPlaying == false)
            {
                return false;
            }

            if (actionFrame < 1)
            {
                return false;
            }

            if (kickTotalFrames <= 0)
            {
                return false;
            }

            return actionFrame < kickTotalFrames;
        }

        /// <summary>
        /// Visual State → 表示 Sequence。選択を1箇所に集約します。
        /// WalkForward / WalkBackward は同じ walkSequence を返します。
        /// </summary>
        private FighterSpriteSequence ResolveSequenceForState(FighterVisualState visualState)
        {
            switch (visualState)
            {
                case FighterVisualState.WalkForward:
                case FighterVisualState.WalkBackward:
                    return walkSequence;

                case FighterVisualState.JumpStart:
                    return jumpStartSequence;

                case FighterVisualState.JumpRise:
                    return jumpRiseSequence;

                case FighterVisualState.JumpApex:
                    return jumpApexSequence;

                case FighterVisualState.JumpFall:
                    return jumpFallSequence;

                case FighterVisualState.Landing:
                    return landingSequence;

                case FighterVisualState.Attack:
                    return attackSequence;

                case FighterVisualState.Kick:
                    return kickSequence;

                case FighterVisualState.ClashRecoil:
                    return clashRecoilSequence;

                case FighterVisualState.HitStun:
                    return hitStunSequence;

                case FighterVisualState.KO:
                    return koSequence;

                case FighterVisualState.Idle:
                default:
                    return idleSequence;
            }
        }

        private void ApplySpriteForCurrentState()
        {
            FighterSpriteSequence sequence = ResolveSequenceForState(currentVisualState);
            Sprite selected = null;

            if (sequence != null)
            {
                selected = sequence.ResolveSprite(visualStateElapsedFrames);
            }

            // fallback: idleSequence → 現在の Sprite を維持（null へ落とさない）
            if (selected == null && idleSequence != null)
            {
                selected = idleSequence.FirstSprite;
            }

            if (selected != null)
            {
                spriteRenderer.sprite = selected;
            }
        }
    }
}
