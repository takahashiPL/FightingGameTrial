using UnityEngine;

namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// 相手ダミーの論理位置と被弾情報だけを保持します（段階9）。
    ///
    /// 責務:
    /// - 論理 X を保持し Transform へ反映する
    /// - Hit 回数・直近 Hit 情報を保持する
    /// - Session からの Hit 通知を受ける
    ///
    /// やらないこと:
    /// - 入力 / Clock / Keyboard を読まない
    /// - Update / FixedUpdate を使わない
    /// - ダメージ・ノックバック・硬直を持たない（今回は Hit 検出のみ）
    ///
    /// なぜ Player コードから分離するか:
    /// 攻撃側と被弾側を混ぜると、誰が座標を正本にしているか分かりにくくなります。
    /// ダミーは「受け取る側」として最小の入れ物にします。
    /// </summary>
    public class DebugDummyTarget : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("ダミー表示用の SpriteRenderer です。色・flipX は Scene で設定します。")]
        [SerializeField]
        private SpriteRenderer spriteRenderer;

        [Tooltip(
            "元画像が右向きのとき、左向きに見せるなら true です。"
            + " true なら Awake で flipX=true を設定します（実行中は毎tick書き換えません）。"
        )]
        [SerializeField]
        private bool facesLeft = true;

        /// <summary>
        /// 論理上の X 位置です。Awake で Transform.position.x から初期化します。
        /// </summary>
        private float logicalX;

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

        public float LogicalX
        {
            get { return logicalX; }
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
            if (spriteRenderer == null)
            {
                Debug.LogError("DebugDummyTarget: SpriteRenderer が未設定です。");
            }

            logicalX = transform.position.x;
            hitCount = 0;
            wasHitThisCombatFrame = false;
            lastHitCombatFrame = -1;

            // 向きは初期化時だけ設定（毎tick書き換えない）
            if (spriteRenderer != null && facesLeft)
            {
                spriteRenderer.flipX = true;
            }

            ApplyLogicalPositionToTransform();
        }

        /// <summary>
        /// 新しい CombatFrame の開始時に、このフレームの Hit 旗を下ろします。
        /// </summary>
        public void BeginCombatFrame()
        {
            wasHitThisCombatFrame = false;
        }

        /// <summary>
        /// パンチ Hit を受け取ります。ダメージ等はまだありません。
        /// </summary>
        public void ReceiveHit(int combatFrame)
        {
            hitCount = hitCount + 1;
            wasHitThisCombatFrame = true;
            lastHitCombatFrame = combatFrame;
        }

        private void ApplyLogicalPositionToTransform()
        {
            Vector3 position = transform.position;
            position.x = logicalX;
            transform.position = position;
        }
    }
}
