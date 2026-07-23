using System;
using UnityEngine;

namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// 論理シミュレーションの「今の時刻と状態メッセージ」だけを保持する入れ物です。
    /// 進行ロジック（いつ増やすか、HitStopかなど）は持ちません。
    /// SimulationSession が値を更新し、将来のHUDが読み取ります。
    /// </summary>
    [Serializable]
    public class SimulationTimeState
    {
        [Tooltip("入力記録用の論理tick番号。60Hzで進みます。初期値は0です。")]
        public int SimulationTick;

        [Tooltip("戦闘進行用の論理フレーム番号。HitStop中は進めません（将来）。初期値は0です。")]
        public int CombatFrame;

        [Tooltip("直近の処理結果を人間が読める日本語メッセージで保持します。")]
        public string LastStatusMessage;

        /// <summary>
        /// 初期状態へ戻します。数値は0、メッセージは空文字です。
        /// </summary>
        public void ResetToInitialValues()
        {
            SimulationTick = 0;
            CombatFrame = 0;
            LastStatusMessage = string.Empty;
        }
    }
}
