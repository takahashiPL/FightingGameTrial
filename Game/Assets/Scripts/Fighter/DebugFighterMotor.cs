using FightingGameTrial.Input;
using UnityEngine;

namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// Fighter の左右移動と見た目の向きを担当します（段階10 / 10B-1）。
    ///
    /// 責務:
    /// - 論理位置 logicalX を保持する（位置の正本）
    /// - Scene 開始時の初期論理 X を保持し、Training Reset で戻せるようにする
    /// - 1 CombatFrame 分の左右移動を行う（ワールド X のみ）
    /// - 外部から指定された Facing を SpriteRenderer.flipX へ反映する
    /// - Transform へ表示位置を反映する
    /// - Push Box 補正後の論理 X を外部から書き戻す（SetLogicalX）
    ///
    /// やらないこと:
    /// - 入力 Left/Right から Facing を決めない（移動入力と Facing の分離）
    /// - 相手を直接探さない（Session が Facing を渡す）
    /// - Push 重なり計算をしない（DebugFighterPushResolver / Session の責務）
    /// - Unity の Update / FixedUpdate で独自に移動しない
    /// - Input System（Keyboard.current）を直接読まない
    /// - Pause / Step / HitStop の判断をしない（呼ぶ側＝SimulationSession の責務）
    /// - SpriteRenderer.color を触らない（色違いは Participant の Tint）
    /// - Training Reset の Facing 再適用をしない（Session が位置復帰後に行う）
    ///
    /// なぜ P1/P2 で同じ Motor を使うか:
    /// 戦闘上の位置・Facing は参加枠に依存しない共通処理だからです。
    /// P2 が棒立ちなのは Neutral 入力が渡されるだけで、専用移動コードはありません。
    ///
    /// なぜ入力で向きを変えないか（正式方針）:
    /// Fighter は基本的に相手と向き合う。
    /// Left/Right はワールド X の移動だけを決め、前進／後退は相手との位置関係で解釈する。
    ///
    /// なぜ Update ではなく SimulationTick 側で動かすか:
    /// ゲーム仕様の正本は 60Hz の論理進行です。
    /// Pause中は自動の SimulationTick が止まるため、ここも自動では呼ばれず移動しません。
    /// Pause中の Step では ProcessOneSimulationTick が1回だけ呼ばれ、その結果として
    /// この Motor も1 CombatFrame 分だけ進みます。
    ///
    /// なぜ HitStop中に移動しないか:
    /// HitStop中は CombatFrame が進まないため、Session がこの Motor を呼びません。
    /// </summary>
    public class DebugFighterMotor : MonoBehaviour
    {
        [Header("移動量（CombatFrame単位）")]
        [Tooltip("CombatFrame が1回進むごとの左右移動量（ワールド単位）です。")]
        [SerializeField]
        private float moveUnitsPerCombatFrame = 0.05f;

        [Tooltip("論理位置 X の左端です。これより左へは行きません。")]
        [SerializeField]
        private float minX = -7f;

        [Tooltip("論理位置 X の右端です。これより右へは行きません。")]
        [SerializeField]
        private float maxX = 7f;

        [Header("見た目")]
        [Tooltip("向き変更に使う SpriteRenderer です。Transform.scale ではなく flipX を使います。")]
        [SerializeField]
        private SpriteRenderer spriteRenderer;

        [Tooltip(
            "元画像が右向きなら true、左向きなら false です。"
            + " この値で素材の初期向きを吸収し、SetFacingRight の見た目へ合わせます。"
            + " SlotId（P1/P2）では分岐しません。"
        )]
        [SerializeField]
        private bool facesRightByDefault = true;

        /// <summary>
        /// 論理上の X 位置です。
        /// Transform.position を毎フレーム加算し続けるのではなく、ここに正本を持ちます。
        /// </summary>
        private float logicalX;

        /// <summary>
        /// Scene 開始時（Awake）に取り込んだ論理 X の初期値です。
        /// Training Reset（R）でここへ戻します。Transform を直接戻す正本にはしません。
        /// </summary>
        private float initialLogicalX;

        /// <summary>
        /// 見た目として右を向いているか。
        /// 入力では変えず、SimulationSession が相手位置から SetFacingRight で設定します。
        /// </summary>
        private bool facingRight = true;

        /// <summary>
        /// HUD / ログ用: 現在の論理 X。
        /// </summary>
        public float LogicalX
        {
            get { return logicalX; }
        }

        /// <summary>
        /// HUD / ログ用: 見た目が右向きか。
        /// </summary>
        public bool FacingRight
        {
            get { return facingRight; }
        }

        /// <summary>
        /// Unity 有効化直後に1回。論理位置を Scene 上の初期 Transform から取ります。
        /// </summary>
        private void Awake()
        {
            if (spriteRenderer == null)
            {
                Debug.LogError("DebugFighterMotor: SpriteRenderer が未設定です。");
            }

            // Scene配置の X を論理位置の初期値にする（Y/Zは以後も Transform の値を維持）
            logicalX = transform.position.x;
            initialLogicalX = logicalX;
            facingRight = true;
            ApplyFacingToSprite();
            ApplyLogicalPositionToTransform();
        }

        /// <summary>
        /// Training Reset 用: 論理 X を Scene 開始時の初期値へ戻します。
        ///
        /// SetLogicalX 経由で minX/maxX クランプと Transform 反映を行います。
        /// Facing は変更しません（Session が位置復帰後に向き合いを再適用します）。
        /// </summary>
        public void ResetLogicalXToInitial()
        {
            SetLogicalX(initialLogicalX);
        }

        /// <summary>
        /// 1 CombatFrame 分だけ左右移動を行います（Facing は変えません）。
        /// SimulationSession が HitStop なしのときだけ呼びます。
        ///
        /// Right → logicalX を増やすだけ
        /// Left  → logicalX を減らすだけ
        /// Neutral（Left/Right とも false）→ 移動なし（P2 棒立ち）
        /// 向きは Session が Push 補正後に SetFacingRight で決めます。
        /// </summary>
        public void ProcessOneCombatFrame(SimulationInputState input)
        {
            if (input == null)
            {
                ApplyLogicalPositionToTransform();
                return;
            }

            bool left = input.Left;
            bool right = input.Right;

            // ------------------------------------------------------------
            // 左右・同時入力の解決（移動のみ）
            // Left+Right 同時は「どちらにも動かない」。
            // Facing はここでは触らない（段階10: 移動入力と Facing の分離）。
            // 上下入力は今回の移動には使わない。
            // ------------------------------------------------------------
            if (left && right)
            {
                // 移動なし
            }
            else if (right)
            {
                logicalX = logicalX + moveUnitsPerCombatFrame;
            }
            else if (left)
            {
                logicalX = logicalX - moveUnitsPerCombatFrame;
            }
            else
            {
                // どちらもなし: 移動なし（Neutral 入力の棒立ち）
            }

            // 端で止める（画面外へ出さない）
            if (logicalX < minX)
            {
                logicalX = minX;
            }

            if (logicalX > maxX)
            {
                logicalX = maxX;
            }

            ApplyLogicalPositionToTransform();
        }

        /// <summary>
        /// Facing だけを外部から設定します（段階10）。
        ///
        /// 呼び出し側（SimulationSession）が、Push 補正後の selfX と opponentX から
        /// 「相手と向き合う向き」を決めて渡します。
        /// 入力の Left/Right からは呼びません。
        /// </summary>
        public void SetFacingRight(bool faceRight)
        {
            facingRight = faceRight;
            ApplyFacingToSprite();
        }

        /// <summary>
        /// Push Box 補正やノックバックなどで、論理 X だけを外部から書き戻します。
        ///
        /// 何をするか: logicalX を更新し、既存の minX/maxX でクランプして Transform へ反映。
        /// なぜ必要か: 重なり解消・ノックバックは Session が計算し、位置の正本は Motor が持つため。
        /// Facing は変更しません。minX/maxX の値自体は変更しません。
        /// 実移動量を知りたいときは TryMoveLogicalXBy を使います（段階13B-1）。
        /// </summary>
        public void SetLogicalX(float newX)
        {
            logicalX = newX;

            if (logicalX < minX)
            {
                logicalX = minX;
            }

            if (logicalX > maxX)
            {
                logicalX = maxX;
            }

            ApplyLogicalPositionToTransform();
        }

        /// <summary>
        /// 論理 X を deltaX だけ動かそうとし、minX/maxX クランプ後の実移動量を返します（段階13B-1）。
        ///
        /// 戻り値: 実際に変わった量（signed）。要求どおりなら deltaX に近い値。
        /// 端で止まった場合は |戻り値| &lt; |deltaX| になる。
        ///
        /// なぜ必要か:
        /// Push が「要求した補正」と「実際に動けた量」の差（未消化）を測り、
        /// 反対側へ再配分するため。KnockbackVelocityX は触らない。
        /// Transform へは既存どおりここ経由でのみ書く。
        /// </summary>
        public float TryMoveLogicalXBy(float deltaX)
        {
            float beforeX = logicalX;
            SetLogicalX(logicalX + deltaX);
            return logicalX - beforeX;
        }

        /// <summary>
        /// SpriteRenderer.flipX で見た目の左右を合わせます。
        /// Transform.localScale の X 符号反転は使いません。
        /// color は変更しません（Tint は Participant 側）。
        /// </summary>
        private void ApplyFacingToSprite()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            // facesRightByDefault=true（元画像が右向き）のとき:
            //   右を見せたい → flipX=false
            //   左を見せたい → flipX=true
            bool shouldFlipX;
            if (facesRightByDefault)
            {
                shouldFlipX = (facingRight == false);
            }
            else
            {
                shouldFlipX = facingRight;
            }

            spriteRenderer.flipX = shouldFlipX;
        }

        /// <summary>
        /// 論理 X を Transform の表示位置へ写します。Y/Z は変更しません。
        /// </summary>
        private void ApplyLogicalPositionToTransform()
        {
            Vector3 position = transform.position;
            position.x = logicalX;
            transform.position = position;
        }
    }
}
