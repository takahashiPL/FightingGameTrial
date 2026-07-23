using System;
using FightingGameTrial.Input;
using UnityEngine;

namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// 論理シミュレーションの「今の時刻と状態メッセージ」だけを保持する入れ物です。
    /// 進行ロジックは持ちません。
    /// </summary>
    [Serializable]
    public class SimulationTimeState
    {
        [Tooltip("入力記録用の論理tick番号。60Hzで進みます。初期値は0です。")]
        public int SimulationTick;

        [Tooltip("戦闘進行用の論理フレーム番号。HitStop中は進めません。初期値は0です。")]
        public int CombatFrame;

        [Tooltip(
            "現在のテスト用Actionが何フレーム進んだかです。"
            + " HitStop中とAction停止中（IsActionPlaying=false）は進みません。"
        )]
        public int ActionFrame;

        [Tooltip(
            "テスト用Actionが進行中かどうかです。"
            + " AキーのデバッグActionと、Jキーの時限パンチの両方で使います。"
        )]
        public bool IsActionPlaying;

        [Tooltip(
            "Jキーの時限パンチ攻撃中かどうかです。"
            + " true のときだけ ActionFrame が終了フレームに達したら自動停止します。"
        )]
        public bool IsJPunchAttack;

        [Tooltip(
            "現在のJパンチがすでにHitしたかです。"
            + " 1攻撃1Hitにするため、開始時false・Hit時true・終了/R時falseにします。"
            + " AキーのデバッグActionでは使いません。"
        )]
        public bool HasCurrentJPunchHit;

        [Tooltip(
            "直近の攻撃結果ラベルです（HUD用 ASCII）。"
            + " None / Miss / Hit。"
        )]
        public string LastAttackResult;

        [Tooltip("自動の論理進行を止めているか。初期は false（Pause解除）です。")]
        public bool IsPaused;

        [Tooltip(
            "HitStopの残り論理tickです。"
            + " 0より大きいあいだは CombatFrame と ActionFrame を進めません。"
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
            ActionFrame = 0;
            IsActionPlaying = false;
            IsJPunchAttack = false;
            HasCurrentJPunchHit = false;
            LastAttackResult = "None";
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
