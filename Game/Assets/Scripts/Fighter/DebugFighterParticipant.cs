using UnityEngine;

namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// デバッグ用の参加枠（P1 / P2）です。キャラクター種類ではありません（段階10B-1）。
    ///
    /// 責務:
    /// - SlotId（P1/P2）を識別する
    /// - Motor / Visual / SpriteRenderer への明示参照を持つ
    /// - 表示 Tint（同キャラ色違い）を初期化時に適用する
    /// - ゲームプレイ入力を使うか（使わないなら Session が Neutral を渡す）
    /// - 相手 Participant への明示参照を持つ
    ///
    /// やらないこと:
    /// - Update で移動しない
    /// - Keyboard.current を直接読まない
    /// - SimulationTick を回さない
    /// - 相手を検索しない（Inspector で opponent を接続する）
    /// - 攻撃フレーム / CharacterDefinition を持たない（後段）
    ///
    /// なぜ Slot とキャラ種類を分けるか:
    /// 同じキャラを P1/P2 が選んでも、参加枠側の Tint だけで色違いにできる。
    /// 別キャラにするときは将来の CharacterDefinition 参照を差し替える想定で、
    /// ここでは Sprite や速度を SlotId で分岐しない。
    ///
    /// なぜ P2 が棒立ちか:
    /// Dummy 専用ロジックだからではなく、Session が Neutral 入力（全 false）を渡すから。
    /// 同じ Motor 処理を通るが、Left/Right が無いので移動しない。
    /// </summary>
    public class DebugFighterParticipant : MonoBehaviour
    {
        [Header("参加枠（キャラ種類ではない）")]
        [Tooltip("デバッグ用の参加枠 ID です。キャラクター種類の選択ではありません。")]
        [SerializeField]
        private DebugFighterSlotId slotId = DebugFighterSlotId.P1;

        [Header("参照（Inspectorで接続。自動検索はしません）")]
        [Tooltip("この参加者の移動・Facing を持つ DebugFighterMotor です。")]
        [SerializeField]
        private DebugFighterMotor motor;

        [Tooltip("この参加者の Idle/Attack Sprite 切替を持つ DebugFighterVisual です。")]
        [SerializeField]
        private DebugFighterVisual visual;

        [Tooltip("表示色（Tint）を適用する SpriteRenderer です。")]
        [SerializeField]
        private SpriteRenderer spriteRenderer;

        [Tooltip(
            "相手の DebugFighterParticipant です。"
            + " Facing 更新で相手 X を読むために使います。検索はしません。"
        )]
        [SerializeField]
        private DebugFighterParticipant opponent;

        [Header("表示バリエーション（参加枠側）")]
        [Tooltip(
            "同キャラ色違い用の表示色です。"
            + " Visual は sprite だけを切り替え、color は上書きしません。"
            + " 将来の Material / Palette 差し替え余地として、まずは Color のみ使います。"
        )]
        [SerializeField]
        private Color displayTint = Color.white;

        [Header("入力")]
        [Tooltip(
            "true なら Session が DebugGameplayInput 由来の入力を渡します。"
            + " false なら Session が Neutral 入力（全 false）を渡します（棒立ち）。"
        )]
        [SerializeField]
        private bool usesGameplayInput = true;

        public DebugFighterSlotId SlotId
        {
            get { return slotId; }
        }

        public DebugFighterMotor Motor
        {
            get { return motor; }
        }

        public DebugFighterVisual Visual
        {
            get { return visual; }
        }

        public SpriteRenderer SpriteRenderer
        {
            get { return spriteRenderer; }
        }

        public DebugFighterParticipant Opponent
        {
            get { return opponent; }
        }

        public Color DisplayTint
        {
            get { return displayTint; }
        }

        public bool UsesGameplayInput
        {
            get { return usesGameplayInput; }
        }

        private void Awake()
        {
            if (motor == null)
            {
                Debug.LogError("DebugFighterParticipant: Motor が未設定です。");
            }

            if (visual == null)
            {
                Debug.LogError("DebugFighterParticipant: Visual が未設定です。");
            }

            if (spriteRenderer == null)
            {
                Debug.LogError("DebugFighterParticipant: SpriteRenderer が未設定です。");
            }

            if (opponent == null)
            {
                Debug.LogError("DebugFighterParticipant: Opponent が未設定です。");
            }

            ApplyDisplayTint();
        }

        /// <summary>
        /// 参加枠の表示 Tint を SpriteRenderer.color へ1回適用します。
        /// Visual の毎フレーム更新では color を触らない前提です。
        /// </summary>
        public void ApplyDisplayTint()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.color = displayTint;
        }
    }
}
