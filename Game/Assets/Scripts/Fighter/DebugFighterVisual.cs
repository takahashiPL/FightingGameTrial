using UnityEngine;

namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// ActionFrame に応じて Sprite を切り替える表示専用コンポーネントです（段階8）。
    ///
    /// 責務:
    /// - Idle / Attack の Sprite 参照を保持する
    /// - SimulationSession から渡された Action 状態で SpriteRenderer.sprite を切り替える
    ///
    /// やらないこと:
    /// - 入力を読まない
    /// - 時間（Update / FixedUpdate）を使わない
    /// - 移動や flipX を扱わない（向きは DebugFighterMotor の責務）
    ///
    /// なぜ Motor から分離するか:
    /// 移動と見た目のコマ送りは別の関心です。
    /// 混ぜると「なぜこのフレームでスプライトが変わったか」が追いづらくなります。
    ///
    /// なぜ Animator をまだ使わないか:
    /// 学習用に、ActionFrame と Sprite の対応をコード上で直接追えるようにするためです。
    /// 演出やブレンドは後段で Animator に寄せられます。
    ///
    /// ActionFrame と見た目の関係（Jパンチ）:
    /// - 1〜attackPoseEndFrame … 攻撃 Sprite
    /// - それ以降〜actionEndFrame … Idle Sprite（硬直の見た目）
    /// - 終了後 … Idle、Action 停止
    /// </summary>
    public class DebugFighterVisual : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("切り替える対象の SpriteRenderer です（DebugPlayer 上）。")]
        [SerializeField]
        private SpriteRenderer spriteRenderer;

        [Tooltip("待機（Idle）用の Sprite です。fighter_idle_00_transparent を割り当てます。")]
        [SerializeField]
        private Sprite idleSprite;

        [Tooltip("攻撃ポーズ用の Sprite です。fighter_attack_punch_transparent を割り当てます。")]
        [SerializeField]
        private Sprite attackSprite;

        [Header("Jパンチ見た目の区切り（ActionFrame）")]
        [Tooltip(
            "この ActionFrame 以下（かつ1以上）のあいだ攻撃 Sprite を表示します。"
            + " 例: 6 なら 1〜6 が攻撃ポーズ。"
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
        /// HUD / ログ用。いま表示している見た目名（Idle / Attack）。
        /// </summary>
        private string currentVisualLabel = "Idle";

        /// <summary>
        /// HUD / ログ用の現在 Sprite 名です。
        /// </summary>
        public string CurrentVisualLabel
        {
            get { return currentVisualLabel; }
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

            // 開始時は Idle
            Apply(false, 0, false);
        }

        /// <summary>
        /// Action 状態を見た目へ反映します。
        /// SimulationSession（または A/R 直後の再描画）から呼ばれます。
        ///
        /// isJPunchAttack が false のとき（AキーのデバッグActionなど）は常に Idle を出します。
        /// ActionFrame の数値確認用であり、攻撃ポーズへは切り替えません。
        /// </summary>
        public void Apply(bool isActionPlaying, int actionFrame, bool isJPunchAttack)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            bool showAttackPose = false;

            if (isJPunchAttack && isActionPlaying)
            {
                // ActionFrame 1〜attackPoseEndFrame だけ攻撃ポーズ
                if (actionFrame >= 1 && actionFrame <= attackPoseEndFrame)
                {
                    showAttackPose = true;
                }
            }

            if (showAttackPose)
            {
                if (attackSprite != null)
                {
                    spriteRenderer.sprite = attackSprite;
                }

                currentVisualLabel = "Attack";
            }
            else
            {
                if (idleSprite != null)
                {
                    spriteRenderer.sprite = idleSprite;
                }

                currentVisualLabel = "Idle";
            }
        }
    }
}
