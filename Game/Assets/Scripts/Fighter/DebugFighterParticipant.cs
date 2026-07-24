using UnityEngine;

namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// デバッグ用の参加枠（P1 / P2）です。キャラクター種類ではありません。
    ///
    /// 責務:
    /// - SlotId（P1/P2）を識別する
    /// - Motor / Visual / SpriteRenderer への明示参照を持つ
    /// - 表示 Tint（同キャラ色違い）を初期化時に適用する
    /// - ゲームプレイ入力を使うか（使わないなら Session が Neutral を渡す）
    /// - 相手 Participant への明示参照を持つ
    /// - 自分専用の DebugFighterAttackState を1つ所有する
    /// - 自分専用の被弾記録（HitCount 等）を所有する
    ///
    /// やらないこと:
    /// - Update で移動しない
    /// - Keyboard.current を直接読まない
    /// - SimulationTick を回さない
    /// - 相手を検索しない（Inspector で opponent を接続する）
    /// - 攻撃開始・ActionFrame進行・Hit判定を自分で回さない（状態の所有のみ。進行は Session）
    /// - attacker / defender の選択や Hit 成立判定をしない（Session の責務）
    /// - CharacterDefinition を持たない（後段）
    ///
    /// なぜ Slot とキャラ種類を分けるか:
    /// 同じキャラを P1/P2 が選んでも、参加枠側の Tint だけで色違いにできる。
    /// 別キャラにするときは将来の CharacterDefinition 参照を差し替える想定で、
    /// ここでは Sprite や速度を SlotId で分岐しない。
    ///
    /// なぜ P2 が棒立ちか:
    /// Dummy 専用ロジックだからではなく、Session が Neutral 入力（全 false）を渡すから。
    /// 同じ Motor 処理を通るが、Left/Right が無いので移動しない。
    ///
    /// 攻撃と被弾（段階10B-2）:
    /// Participant は自分の AttackState と被弾記録を所有します。
    /// SimulationSession が attacker / defender を選び Hit を解決し、
    /// 成立時に defender.ReceiveHit で被弾を記録します。
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

        [Header("攻撃状態")]
        [Tooltip(
            "この参加者自身の攻撃進行状態です。"
            + " P1/P2で同じ型を持ち、Sessionから共通処理されます。"
        )]
        [SerializeField]
        private DebugFighterAttackState attackState =
            new DebugFighterAttackState();

        // ------------------------------------------------------------
        // 被弾状態（段階10B-2）
        //
        // 試合中のランタイム状態です。Inspector から編集する設定値ではないため
        // SerializeField は付けません。参加者ごとに独立した private フィールドです。
        // ------------------------------------------------------------

        /// <summary>
        /// 累計 Hit 回数です。
        /// </summary>
        private int hitCount;

        /// <summary>
        /// 今の CombatFrame で Hit を受けたか。
        /// </summary>
        private bool wasHitThisCombatFrame;

        /// <summary>
        /// 直近で Hit した CombatFrame 番号です。未Hitは -1。
        /// </summary>
        private int lastHitCombatFrame = -1;

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

        public DebugFighterAttackState AttackState
        {
            get { return attackState; }
        }

        public int HitCount
        {
            get { return hitCount; }
        }

        public bool WasHitThisCombatFrame
        {
            get { return wasHitThisCombatFrame; }
        }

        public int LastHitCombatFrame
        {
            get { return lastHitCombatFrame; }
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

            // Serializable な通常クラスのため AddComponent は使わない。
            // 参加者ごとに別インスタンスを持ち、P1/P2 で共有しない。
            if (attackState == null)
            {
                attackState = new DebugFighterAttackState();
            }

            attackState.Reset();

            // 被弾記録も参加者ごとに独立して初期化する（共有しない）。
            hitCount = 0;
            wasHitThisCombatFrame = false;
            lastHitCombatFrame = -1;

            ApplyDisplayTint();
        }

        /// <summary>
        /// 新しい CombatFrame の開始時に、このフレームの Hit 旗を下ろします。
        /// </summary>
        public void BeginCombatFrame()
        {
            wasHitThisCombatFrame = false;
        }

        /// <summary>
        /// パンチ Hit を受け取り、被弾記録だけを更新します。
        ///
        /// 何をするか: HitCount 加算、今フレーム被弾旗、直近 CombatFrame の記録。
        /// なぜ必要か: Session が defender.ReceiveHit を呼ぶ共通口にするため。
        /// やらないこと: Hit 判定、ログ出力、ダメージ、ノックバック（Session / 後段）。
        /// </summary>
        public void ReceiveHit(int combatFrame)
        {
            hitCount = hitCount + 1;
            wasHitThisCombatFrame = true;
            lastHitCombatFrame = combatFrame;
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
