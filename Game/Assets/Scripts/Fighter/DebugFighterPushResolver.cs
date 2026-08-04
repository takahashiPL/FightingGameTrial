using UnityEngine;

namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// 2体の Participant 間の横方向 Push Box 重なりを解消します（段階10B-3 / 13B-1）。
    ///
    /// 何をするか:
    /// - 各 Participant の EvaluateWorldPushBox から World 中心と半幅を取る
    /// - World 中心間距離が半幅合計を下回ったら、左右へ等分に押し分ける
    /// - Motor 既存の minX/maxX で片方が止まった分は、もう片方へ未消化量を移す（段階13B-1）
    /// - World 中心の移動量と同じ delta を Motor.TryMoveLogicalXBy へ適用する
    ///   （同一 Facing 中は worldCenter = LogicalX + signedLocalCenterX のため）
    ///
    /// なぜ必要か:
    /// - P1/P2 が重なったり通り抜けたりしないようにする
    /// - 可視化の Push Box と実押し合いを一致させる（CenterX 非0・Facing 反転対応）
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
        /// out centerDistance: 補正前の World Push 中心間距離（絶対値）。
        /// out requiredMinDistance: World Push 半幅合計。
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

            DebugBox2D worldA = participantA.EvaluateWorldPushBox();
            DebugBox2D worldB = participantB.EvaluateWorldPushBox();

            requiredMinDistance = worldA.HalfWidth + worldB.HalfWidth;

            float worldCenterA = worldA.CenterX;
            float worldCenterB = worldB.CenterX;
            float signedDelta = worldCenterB - worldCenterA;
            centerDistance = Mathf.Abs(signedDelta);

            // 最小距離以上なら重なりなし。補正不要。
            if (centerDistance + OverlapEpsilon >= requiredMinDistance)
            {
                wasOverlapping = false;
                return false;
            }

            wasOverlapping = true;

            // 左右の役割は「今の World Push 中心 X」だけで決める（P1/P2 名では分岐しない）。
            // 同中心のときは participantA を左扱い（呼び出し側が毎tick同じ順なら安定）。
            DebugFighterParticipant leftParticipant;
            DebugFighterParticipant rightParticipant;
            float leftWorldX0;
            float rightWorldX0;

            if (signedDelta >= 0f)
            {
                leftParticipant = participantA;
                rightParticipant = participantB;
                leftWorldX0 = worldCenterA;
                rightWorldX0 = worldCenterB;
            }
            else
            {
                leftParticipant = participantB;
                rightParticipant = participantA;
                leftWorldX0 = worldCenterB;
                rightWorldX0 = worldCenterA;
            }

            DebugFighterMotor leftMotor = leftParticipant.Motor;
            DebugFighterMotor rightMotor = rightParticipant.Motor;

            float separationNeeded = requiredMinDistance - (rightWorldX0 - leftWorldX0);
            if (separationNeeded <= OverlapEpsilon)
            {
                return false;
            }

            float halfSeparation = separationNeeded * 0.5f;

            // ------------------------------------------------------------
            // 1) 通常: World 中心を左右へ半分ずつ離す要求
            // 2) 同一 Facing 中は ΔworldCenter = ΔLogicalX なので TryMoveLogicalXBy へ同量
            // 3) 実移動量は Motor.TryMoveLogicalXBy（minX/maxX Clamp 後）
            // 4) 未消化分を反対側へ再配分（段階13B-1）
            // 5) 反対側にも余地がなければ、動ける範囲で停止
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
                    rightParticipant.EvaluateWorldPushBox().CenterX
                    - leftParticipant.EvaluateWorldPushBox().CenterX
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
