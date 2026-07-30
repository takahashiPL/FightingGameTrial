using System;
using UnityEngine;

namespace FightingGameTrial.Input
{
    /// <summary>
    /// 1回の SimulationTick で確定した「論理入力」を保持する入れ物です。
    ///
    /// 物理入力（キーボードの今この瞬間）とは別物です。
    /// DebugGameplayInput が Update で持つ最新状態を、
    /// SimulationSession が SimulationTick 開始時にここへコピーして確定します。
    /// </summary>
    [Serializable]
    public class SimulationInputState
    {
        [Tooltip("左方向が押されているか（確定後の論理入力）。矢印Left。")]
        public bool Left;

        [Tooltip("右方向が押されているか（確定後の論理入力）。矢印Right。")]
        public bool Right;

        [Tooltip("上方向が押されているか（確定後の論理入力）。矢印Up。")]
        public bool Up;

        [Tooltip("下方向が押されているか（確定後の論理入力）。矢印Down。")]
        public bool Down;

        [Tooltip(
            "Attack（J Punch）が押されているか（Held）。キー J。"
            + " 押した瞬間・バッファは持ちません。"
        )]
        public bool Attack;

        [Tooltip(
            "Kick（Ground Kick）が押されているか（Held）。キー K。"
            + " 押した瞬間・バッファは持ちません。"
        )]
        public bool Kick;

        [Tooltip("この入力状態を確定した SimulationTick 番号です。")]
        public int SampledAtSimulationTick;

        [Tooltip(
            "入力をサンプリングした回数です。"
            + " Pause中は増えず、Stepでは1増え、HitStop中でも SimulationTick ごとに増えます。"
        )]
        public int SampleSequence;

        public void ResetToInitialValues()
        {
            ClearGameplayHeldButtons();
            SampledAtSimulationTick = 0;
            SampleSequence = 0;
        }

        /// <summary>
        /// ゲーム操作の Held だけをニュートラルにします。
        /// Training Reset 後の入力抑制用。Kick もここに含めます。
        /// </summary>
        public void ClearGameplayHeldButtons()
        {
            Left = false;
            Right = false;
            Up = false;
            Down = false;
            Attack = false;
            Kick = false;
        }

        public bool HasAnyGameplayInputHeld()
        {
            return HasAnyGameplayInputHeld(Left, Right, Up, Down, Attack, Kick);
        }

        public static bool HasAnyGameplayInputHeld(
            bool left,
            bool right,
            bool up,
            bool down,
            bool attack,
            bool kick)
        {
            return left || right || up || down || attack || kick;
        }

        public void CopyFromPhysicalAndCommit(
            bool left,
            bool right,
            bool up,
            bool down,
            bool attack,
            bool kick,
            int simulationTick)
        {
            Left = left;
            Right = right;
            Up = up;
            Down = down;
            Attack = attack;
            Kick = kick;

            SampledAtSimulationTick = simulationTick;
            SampleSequence = SampleSequence + 1;
        }
    }
}
