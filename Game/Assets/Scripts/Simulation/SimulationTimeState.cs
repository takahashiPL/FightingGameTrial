using System;
using FightingGameTrial.Input;
using UnityEngine;

namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// 論理シミュレーションの「今の時刻と状態メッセージ」だけを保持する入れ物です。
    /// 進行ロジック（いつ増やすか、HitStopを減らすかなど）は持ちません。
    /// SimulationSession / SimulationClockDriver が値を更新し、DebugHudView が読み取ります。
    ///
    /// 時間軸の違い:
    /// - SimulationTick … 60Hzの論理更新回数。HitStop中も進む。
    /// - CombatFrame … 対戦処理が進んだ回数。HitStop中は止まる。
    /// - ActionFrame … 現在のテスト用Actionの進行フレーム。
    ///   HitStop中は止まり、Action停止中も止まり、再生中かつHitStopなしのときだけ進む。
    /// ActionFrameはCombatFrameの別名ではありません（行動・技の進行を別カウントするため）。
    ///
    /// 入力（段階6）:
    /// CurrentInput は「最後に SimulationTick で確定した論理入力」です。
    /// Pause中に物理キーを変えても、次の SimulationTick（または Step）までここは変わりません。
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
            + " CombatFrameとは別カウントです。初期値は0です。負数にはしません。"
        )]
        public int ActionFrame;

        [Tooltip(
            "テスト用Actionが進行中かどうかです。"
            + " trueのあいだだけ、HitStopではないCombat処理tickで ActionFrame が進みます。"
            + " AキーのデバッグActionと、Jキーの時限パンチの両方で使います。"
        )]
        public bool IsActionPlaying;

        [Tooltip(
            "Jキーの時限パンチ攻撃中かどうかです。"
            + " true のときだけ ActionFrame が終了フレームに達したら自動停止します。"
            + " AキーのデバッグAction（自動終了なし）と区別するための旗です。"
        )]
        public bool IsJPunchAttack;

        [Tooltip("自動の論理進行を止めているか。初期は false（Pause解除）です。Time.timeScale とは別物です。")]
        public bool IsPaused;

        [Tooltip(
            "HitStopの残り論理tickです。"
            + " 0より大きいあいだは CombatFrame と ActionFrame を進めません。"
            + " SimulationTick ごとに1減り、負数にはしません。"
            + " 初期値は0です。"
            + " 入力サンプリングは HitStop 中も止めません。"
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

        [Tooltip(
            "最後に SimulationTick で確定した論理入力です。"
            + " DebugGameplayInput の物理状態とは別物です。"
            + " Pause中は SimulationTick が進まないため、ここも更新されません。"
        )]
        public SimulationInputState CurrentInput = new SimulationInputState();

        /// <summary>
        /// 初期状態へ戻します。
        /// </summary>
        public void ResetToInitialValues()
        {
            SimulationTick = 0;
            CombatFrame = 0;
            ActionFrame = 0;
            IsActionPlaying = false;
            IsJPunchAttack = false;
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
