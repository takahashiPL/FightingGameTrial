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
    ///
    /// class にしている理由（学習用）:
    /// SimulationTimeState.CurrentInput が同じインスタンスを指し続け、
    /// フィールドを上書き更新する形にします。
    /// struct だと「コピーして捨てる」見え方になりやすいため、今回は class を選びます。
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
            "Attack が押されているか（Held状態の論理入力）。キー J。"
            + " 押した瞬間・離した瞬間・バッファはまだ持ちません。"
        )]
        public bool Attack;

        [Tooltip("この入力状態を確定した SimulationTick 番号です。")]
        public int SampledAtSimulationTick;

        [Tooltip(
            "入力をサンプリングした回数です。"
            + " Pause中は増えず、Stepでは1増え、HitStop中でも SimulationTick ごとに増えます。"
        )]
        public int SampleSequence;

        /// <summary>
        /// 初期状態（すべて未入力）へ戻します。
        /// </summary>
        public void ResetToInitialValues()
        {
            Left = false;
            Right = false;
            Up = false;
            Down = false;
            Attack = false;
            SampledAtSimulationTick = 0;
            SampleSequence = 0;
        }

        /// <summary>
        /// 物理入力の現在値を、この論理入力オブジェクトへコピーして確定します。
        /// 新しいインスタンスは作りません（同じ CurrentInput を更新します）。
        ///
        /// 同時方向（Left+Right など）もそのまま保持します。
        /// 左右相殺や8方向化は行いません。方向解決は後段の入力解釈責務です。
        /// </summary>
        public void CopyFromPhysicalAndCommit(
            bool left,
            bool right,
            bool up,
            bool down,
            bool attack,
            int simulationTick)
        {
            Left = left;
            Right = right;
            Up = up;
            Down = down;
            Attack = attack;

            SampledAtSimulationTick = simulationTick;
            SampleSequence = SampleSequence + 1;
        }
    }
}
