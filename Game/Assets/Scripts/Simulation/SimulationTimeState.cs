using System;
using UnityEngine;

namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// 論理シミュレーションの「今の時刻と状態メッセージ」だけを保持する入れ物です。
    /// 進行ロジック（いつ増やすか、HitStopを減らすかなど）は持ちません。
    /// SimulationSession / SimulationClockDriver が値を更新し、DebugHudView が読み取ります。
    /// </summary>
    [Serializable]
    public class SimulationTimeState
    {
        [Tooltip("入力記録用の論理tick番号。60Hzで進みます。初期値は0です。")]
        public int SimulationTick;

        [Tooltip("戦闘進行用の論理フレーム番号。HitStop中は進めません（将来）。初期値は0です。")]
        public int CombatFrame;

        [Tooltip("自動の論理進行を止めているか。初期は false（Pause解除）です。Time.timeScale とは別物です。")]
        public bool IsPaused;

        [Tooltip(
            "HitStopの残り論理tickです。"
            + " 0より大きいあいだは CombatFrame を進めません。"
            + " SimulationTick ごとに1減り、負数にはしません。"
            + " 初期値は0です。"
        )]
        public int HitStopRemaining;

        [Tooltip(
            "直近の Step 操作結果です。"
            + "例: 未実行 / 実行 / 無視（Pause解除中）。"
            + "ClockDriver が更新し、HUDが読みます。"
        )]
        public string LastStepResult;

        [Tooltip("直近の処理結果を人間が読める日本語メッセージで保持します。数値は含めません。")]
        public string LastStatusMessage;

        /// <summary>
        /// 初期状態へ戻します。
        /// </summary>
        public void ResetToInitialValues()
        {
            SimulationTick = 0;
            CombatFrame = 0;
            IsPaused = false;
            HitStopRemaining = 0;
            LastStepResult = "未実行";
            LastStatusMessage = string.Empty;
        }
    }
}
