using UnityEngine;

namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// 2体の Participant 間の横方向 Push Box 重なりを解消します（段階10B-3 / 13B-1）。
    ///
    /// 何をするか:
    /// - 各 Participant の PushBoxHalfWidth から最小中心距離を求める
    /// - 移動後の中心距離がそれを下回ったら、左右へ等分に押し分ける
    /// - Motor 既存の minX/maxX で片方が止まった分は、もう片方へ未消化量を移す（段階13B-1）
    /// - 実移動量は Motor.TryMoveLogicalXBy の戻り値を正本にする
    ///
    /// なぜ必要か:
    /// - P1/P2 が重なったり通り抜けたりしないようにする
    /// - Rigidbody 衝突に依存せず、60Hz SimulationTick 内で再現可能にする
    /// - P1 専用分岐ではなく、Participant 同士の共通処理にする
    /// - ステージ端で片方が動けなくても、可能な限り必要距離を確保する
    ///
    /// 処理順（呼び側＝SimulationSession）:
    /// 1. 両体の入力移動
    /// 2. ノックバック移動（あれば）
    /// 3. 本クラスで Push 補正（本メソッド）
    /// 4. Facing 更新（補正後の最終位置を基準）
    ///
    /// やらないこと:
    /// - 縦方向・高さ判定
    /// - 飛び越えによる左右関係の意図的な反転（クロスアップ）
    /// - KnockbackVelocityX の変更・壁バウンド・壁やられ
    /// - minX/maxX の値変更や新規ステージ端オブジェクト
    /// - Time.deltaTime / Rigidbody / Collider による解決
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
        /// out didWallRedistribute: 半分適用後に未解消があり再配分を試みたか（段階13B-1）。
        /// out wallLog: 壁際再配分ログ用の1行（不要なら空文字）。
        /// </summary>
        public static bool TryResolveHorizontalOverlap(
            DebugFighterParticipant participantA,
            DebugFighterParticipant participantB,
            out float centerDistance,
            out float requiredMinDistance,
            out bool wasOverlapping,
            out bool didWallRedistribute,
            out string wallLog)
        {
            centerDistance = 0f;
            requiredMinDistance = 0f;
            wasOverlapping = false;
            didWallRedistribute = false;
            wallLog = "";

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

            if (signedDelta >= 0f)
            {
                leftParticipant = participantA;
                rightParticipant = participantB;
            }
            else
            {
                leftParticipant = participantB;
                rightParticipant = participantA;
            }

            DebugFighterMotor leftMotor = leftParticipant.Motor;
            DebugFighterMotor rightMotor = rightParticipant.Motor;
            float leftX0 = leftMotor.LogicalX;
            float rightX0 = rightMotor.LogicalX;

            float separationNeeded = requiredMinDistance - (rightX0 - leftX0);
            if (separationNeeded <= OverlapEpsilon)
            {
                return false;
            }

            float halfSeparation = separationNeeded * 0.5f;

            // ------------------------------------------------------------
            // 1) 通常: 左右へ半分ずつ要求（中央付近ではこれだけで必要距離に戻る）
            // 2) 実移動量は Motor.TryMoveLogicalXBy（minX/maxX Clamp 後）
            // 3) 未消化分を反対側へ再配分（段階13B-1）
            // 4) 反対側にも余地がなければ、動ける範囲で停止
            //
            // KnockbackVelocityX は触らない（位置だけ動かす）。
            // ------------------------------------------------------------
            float leftDelta = leftMotor.TryMoveLogicalXBy(-halfSeparation);
            float leftMoved = -leftDelta;
            if (leftMoved < 0f)
            {
                leftMoved = 0f;
            }

            float rightDelta = rightMotor.TryMoveLogicalXBy(halfSeparation);
            float rightMoved = rightDelta;
            if (rightMoved < 0f)
            {
                rightMoved = 0f;
            }

            float remaining = separationNeeded - leftMoved - rightMoved;
            if (remaining < 0f)
            {
                remaining = 0f;
            }

            // 半分ずつ適用後にまだ不足があるときだけ再配分する（壁際）。
            bool neededWallRedistribute = remaining > OverlapEpsilon;

            float redistributedToRight = 0f;
            float redistributedToLeft = 0f;

            // 未解消量を右へ、次いで左へ渡す（どちらも動けなければ残る）。
            if (remaining > OverlapEpsilon)
            {
                float toRight = rightMotor.TryMoveLogicalXBy(remaining);
                if (toRight < 0f)
                {
                    toRight = 0f;
                }

                redistributedToRight = toRight;
                remaining = remaining - toRight;
                if (remaining < 0f)
                {
                    remaining = 0f;
                }
            }

            if (remaining > OverlapEpsilon)
            {
                float toLeftSigned = leftMotor.TryMoveLogicalXBy(-remaining);
                float toLeft = -toLeftSigned;
                if (toLeft < 0f)
                {
                    toLeft = 0f;
                }

                redistributedToLeft = toLeft;
                remaining = remaining - toLeft;
                if (remaining < 0f)
                {
                    remaining = 0f;
                }
            }

            didWallRedistribute = neededWallRedistribute;
            if (didWallRedistribute)
            {
                float afterDist = Mathf.Abs(
                    rightMotor.LogicalX - leftMotor.LogicalX
                );

                wallLog =
                    "left=" + leftParticipant.SlotId
                    + " right=" + rightParticipant.SlotId
                    + " overlap=" + separationNeeded.ToString("0.000")
                    + " half=" + halfSeparation.ToString("0.000")
                    + " leftMoved=" + leftMoved.ToString("0.000")
                    + " rightMoved=" + rightMoved.ToString("0.000")
                    + " reToRight=" + redistributedToRight.ToString("0.000")
                    + " reToLeft=" + redistributedToLeft.ToString("0.000")
                    + " afterDist=" + afterDist.ToString("0.000")
                    + " min=" + requiredMinDistance.ToString("0.000");
            }

            return true;
        }
    }
}
