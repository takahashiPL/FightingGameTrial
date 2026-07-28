using UnityEngine;

namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// Visual State に応じて Sprite を切り替える表示専用コンポーネントです（段階8 / 10B-1＋最小 Visual State）。
    ///
    /// 責務:
    /// - Idle / Attack の Sprite 参照を保持する
    /// - SimulationSession から渡された <see cref="FighterVisualState"/> で SpriteRenderer.sprite を切り替える
    /// - HUD / ログ用に現在の Visual State 名を公開する
    ///
    /// やらないこと:
    /// - 入力を読まない
    /// - Facing / 前進後退の判定をしない（Session の責務）
    /// - 時間（Update / FixedUpdate）を使わない
    /// - 移動や flipX を扱わない（向きは DebugFighterMotor の責務）
    /// - SpriteRenderer.color を毎フレーム上書きしない（色違いは Participant.Tint）
    ///
    /// Sprite 対応（現状）:
    /// - Idle / WalkForward / WalkBackward → idleSprite（Walk 専用 Sprite は未用意）
    /// - Attack → attackSprite
    ///
    /// ActionFrame と攻撃ポーズの関係（Jパンチ）:
    /// - 1〜attackPoseEndFrame … 攻撃ポーズとして Session が Attack を渡す
    /// - それ以降 … Session が Idle / Walk を渡しうる（攻撃硬直中の見た目は Idle Sprite）
    /// </summary>
    public class DebugFighterVisual : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("切り替える対象の SpriteRenderer です。")]
        [SerializeField]
        private SpriteRenderer spriteRenderer;

        [Tooltip(
            "待機（Idle）用の Sprite です。fighter_idle_00_transparent を割り当てます。"
            + " WalkForward / WalkBackward も当面これを流用します。"
            + " SlotId では分岐しません（将来の CharacterDefinition 差し替えを阻害しない）。"
        )]
        [SerializeField]
        private Sprite idleSprite;

        [Tooltip(
            "攻撃ポーズ用の Sprite です。fighter_attack_punch_transparent を割り当てます。"
            + " SlotId では分岐しません。"
        )]
        [SerializeField]
        private Sprite attackSprite;

        [Header("Jパンチ見た目の区切り（ActionFrame）")]
        [Tooltip(
            "この ActionFrame 以下（かつ1以上）のあいだ攻撃ポーズとみなします。"
            + " 例: 6 なら 1〜6 が攻撃ポーズ。判定は Session が行い、ここは境界値を公開します。"
        )]
        [SerializeField]
        private int attackPoseEndFrame = 6;

        [Tooltip(
            "Jパンチ全体の最終 ActionFrame です。"
            + " SimulationSession がこの値に達したら Action を終了します。"
        )]
        [SerializeField]
        private int actionEndFrame = 12;

        /// <summary>
        /// いま適用している Visual State です。
        /// </summary>
        private FighterVisualState currentVisualState = FighterVisualState.Idle;

        /// <summary>
        /// HUD / ログ用。いま表示している見た目名（Idle / WalkForward / WalkBackward / Attack）。
        /// </summary>
        private string currentVisualLabel = "Idle";

        /// <summary>
        /// HUD / ログ用の現在 Visual State です。
        /// </summary>
        public FighterVisualState CurrentVisualState
        {
            get { return currentVisualState; }
        }

        /// <summary>
        /// HUD / ログ用の現在 Sprite / State 名です。
        /// </summary>
        public string CurrentVisualLabel
        {
            get { return currentVisualLabel; }
        }

        /// <summary>
        /// Jパンチ攻撃ポーズの最終 ActionFrame（1〜この値）。
        /// </summary>
        public int AttackPoseEndFrame
        {
            get { return attackPoseEndFrame; }
        }

        /// <summary>
        /// Jパンチ自動終了判定用の最終フレームです。
        /// </summary>
        public int ActionEndFrame
        {
            get { return actionEndFrame; }
        }

        /// <summary>
        /// Unity 有効化直後に1回。参照検査と Idle 初期表示だけ行います。
        /// </summary>
        private void Awake()
        {
            if (spriteRenderer == null)
            {
                Debug.LogError("DebugFighterVisual: SpriteRenderer が未設定です。");
            }

            if (idleSprite == null)
            {
                Debug.LogError("DebugFighterVisual: idleSprite が未設定です。");
            }

            if (attackSprite == null)
            {
                Debug.LogError("DebugFighterVisual: attackSprite が未設定です。");
            }

            Apply(FighterVisualState.Idle);
        }

        /// <summary>
        /// Visual State を見た目へ反映します。
        /// SimulationSession（または A/R 直後の再描画）から呼ばれます。
        ///
        /// 状態の決定（前進／後退／攻撃優先）は Session 側の責務です。
        /// このメソッドは渡された State に対応する Sprite を当てるだけです。
        /// </summary>
        public void Apply(FighterVisualState visualState)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            currentVisualState = visualState;
            currentVisualLabel = visualState.ToString();

            if (visualState == FighterVisualState.Attack)
            {
                if (attackSprite != null)
                {
                    spriteRenderer.sprite = attackSprite;
                }

                return;
            }

            // Idle / WalkForward / WalkBackward は当面 Idle Sprite を流用する。
            if (idleSprite != null)
            {
                spriteRenderer.sprite = idleSprite;
            }
        }

        /// <summary>
        /// Jパンチの攻撃ポーズ期間かどうかを返します（Session の Attack 判定用）。
        /// isJPunchAttack が false のときは常に false（Aキーのデバッグ Action 等）。
        /// </summary>
        public bool IsAttackPoseActive(bool isActionPlaying, int actionFrame, bool isJPunchAttack)
        {
            if (isJPunchAttack == false || isActionPlaying == false)
            {
                return false;
            }

            return actionFrame >= 1 && actionFrame <= attackPoseEndFrame;
        }
    }
}
