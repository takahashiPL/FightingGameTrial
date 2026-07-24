using System;
using FightingGameTrial.Input;
using UnityEngine;

namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// 論理シミュレーションの共有時間状態だけを保持する入れ物です。
    /// 進行ロジックは持ちません。
    ///
    /// 何を持つか:
    /// - SimulationTick / CombatFrame
    /// - Pause / HitStop
    /// - Step・Status メッセージ
    /// - 確定済み論理入力（CurrentInput）
    ///
    /// 何を持たないか:
    /// - 攻撃 ActionFrame / IsActionPlaying / Hit 結果など
    ///   （攻撃状態の正本は各 DebugFighterParticipant.AttackState）
    /// </summary>
    [Serializable]
    public class SimulationTimeState
    {
        [Tooltip("入力記録用の論理tick番号。60Hzで進みます。初期値は0です。")]
        public int SimulationTick;

        [Tooltip("戦闘進行用の論理フレーム番号。HitStop中は進めません。初期値は0です。")]
        public int CombatFrame;

        [Tooltip("自動の論理進行を止めているか。初期は false（Pause解除）です。")]
        public bool IsPaused;

        [Tooltip(
            "HitStopの残り論理tickです。"
            + " 0より大きいあいだは CombatFrame と攻撃 Action の進行を止めます。"
            + " SimulationTick ごとに1減り、負数にはしません。"
            + " Hit成立tickでは値を設定するだけで、同じtick内では減らしません。"
        )]
        public int HitStopRemaining;

        [Tooltip("直近の Step 操作結果です。")]
        public string LastStepResult;

        [Tooltip("直近の処理結果メッセージです。HUDは欠落回避のため ASCII を優先します。")]
        public string LastStatusMessage;

        [Tooltip("最後に SimulationTick で確定した論理入力です。")]
        public SimulationInputState CurrentInput = new SimulationInputState();

        public void ResetToInitialValues()
        {
            SimulationTick = 0;
            CombatFrame = 0;
            IsPaused = false;
            HitStopRemaining = 0;
            LastStepResult = "未実行";
            LastStatusMessage = string.Empty;

            if (CurrentInput == null)
            {
                CurrentInput = new SimulationInputState();
            }

            CurrentInput.ResetToInitialValues();
        }
    }
}
