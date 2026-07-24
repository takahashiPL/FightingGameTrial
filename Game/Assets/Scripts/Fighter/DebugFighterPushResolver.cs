using UnityEngine;

namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// 2体の Participant 間の横方向 Push Box 重なりを解消します（段階10B-3）。
    ///
    /// 何をするか:
    /// - 各 Participant の PushBoxHalfWidth から最小中心距離を求める
    /// - 移動後の中心距離がそれを下回ったら、左右へ等分に押し分ける
    /// - Motor 既存の minX/maxX で片方が止まった分は、もう片方へ未消化量を移す
    ///
    /// なぜ必要か:
    /// - P1/P2 が重なったり通り抜けたりしないようにする
    /// - Rigidbody 衝突に依存せず、60Hz SimulationTick 内で再現可能にする
    /// - P1 専用分岐ではなく、Participant 同士の共通処理にする
    ///
    /// 処理順（呼び側＝SimulationSession）:
    /// 1. 両体の移動（ProcessOneFighterMovement）
    /// 2. 本クラスで Push 補正（本メソッド）
    /// 3. Facing 更新（補正後の最終位置を基準）
    ///
    /// やらないこと:
    /// - 縦方向・高さ判定
    /// - 飛び越えによる左右関係の意図的な反転（クロスアップ）
    /// - 攻撃ノックバック
    /// - Stage 端の新規追加（既存 Motor 端クランプのみ利用）
    /// </summary>
    public static class DebugFighterPushResolver
    {
        /// <summary>
        /// 浮動小数の誤差で「わずかに不足」と判定し続けるのを防ぐしきい値です。
        /// </summary>
        private const float OverlapEpsilon = 0.0001f;

        /// <summary>
        /// 2 Participant の横方向 Push Box 重なりを解消します。
        ///
        /// 戻り値: 補正を行ったら true。
        /// out centerDistance: 補正前の中心間距離（絶対値）。
        /// out requiredMinDistance: halfWidth 合計。
        /// out wasOverlapping: 補正前に最小距離を下回っていたか。
        /// </summary>
        public static bool TryResolveHorizontalOverlap(
            DebugFighterParticipant participantA,
            DebugFighterParticipant participantB,
            out float centerDistance,
            out float requiredMinDistance,
            out bool wasOverlapping)
        {
            centerDistance = 0f;
            requiredMinDistance = 0f;
            wasOverlapping = false;

            if (participantA == null || participantB == null)
            {
                return false;
            }

            DebugFighterMotor motorA = participantA.Motor;
            DebugFighterMotor motorB = participantB.Motor;
            if (motorA == null || motorB == null)
            {
                return false;
            }

            requiredMinDistance =
                participantA.PushBoxHalfWidth + participantB.PushBoxHalfWidth;

            float xA = motorA.LogicalX;
            float xB = motorB.LogicalX;
            float signedDelta = xB - xA;
            centerDistance = Mathf.Abs(signedDelta);

            // 最小距離以上なら重なりなし。補正不要。
            if (centerDistance + OverlapEpsilon >= requiredMinDistance)
            {
                wasOverlapping = false;
                return false;
            }

            wasOverlapping = true;

            // 左右の役割は「今の X」だけで決める（P1/P2 名では分岐しない）。
            // 同 X のときは participantA を左扱い（呼び出し側が毎tick同じ順なら安定）。
            DebugFighterParticipant leftParticipant;
            DebugFighterParticipant rightParticipant;
            float leftX0;
            float rightX0;

            if (signedDelta >= 0f)
            {
                leftParticipant = participantA;
                rightParticipant = participantB;
                leftX0 = xA;
                rightX0 = xB;
            }
            else
            {
                leftParticipant = participantB;
                rightParticipant = participantA;
                leftX0 = xB;
                rightX0 = xA;
            }

            float separationNeeded = requiredMinDistance - (rightX0 - leftX0);
            if (separationNeeded <= OverlapEpsilon)
            {
                return false;
            }

            float halfSeparation = separationNeeded * 0.5f;

            // ------------------------------------------------------------
            // 等分分離 + 端クランプで動けなかった分の転送
            //
            // なぜ等分か: 片方だけ止めず、同時接近でも双方へ補正を分けるため。
            // なぜ転送か: Motor 既存 min/max で片方が止まったとき、
            //           残りを相手へ渡さないと重なりが残るため（rules §2.2 と同趣旨）。
            // ------------------------------------------------------------
            DebugFighterMotor leftMotor = leftParticipant.Motor;
            DebugFighterMotor rightMotor = rightParticipant.Motor;

            leftMotor.SetLogicalX(leftX0 - halfSeparation);
            float leftMoved = leftX0 - leftMotor.LogicalX;
            float leftUnconsumed = halfSeparation - leftMoved;
            if (leftUnconsumed < 0f)
            {
                leftUnconsumed = 0f;
            }

            float rightWanted = halfSeparation + leftUnconsumed;
            rightMotor.SetLogicalX(rightX0 + rightWanted);
            float rightMoved = rightMotor.LogicalX - rightX0;
            float rightUnconsumed = rightWanted - rightMoved;
            if (rightUnconsumed < 0f)
            {
                rightUnconsumed = 0f;
            }

            if (rightUnconsumed > OverlapEpsilon)
            {
                leftMotor.SetLogicalX(leftMotor.LogicalX - rightUnconsumed);
            }

            return true;
        }
    }
}
