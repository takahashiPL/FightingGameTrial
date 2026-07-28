using FightingGameTrial.DebugTools;
using UnityEngine;

namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// Unityの実時間（Update）を受け皿にして、60Hzの論理 SimulationTick 進行を依頼します。
    ///
    /// 重要:
    /// - Unity の Update / FixedUpdate はゲーム仕様の正本ではありません。
    /// - 正本は docs/rules.md の 60Hz 論理 SimulationTick です。
    ///
    /// 責務:
    /// - Pause / Step / テスト用HitStop / A・R のデバッグ操作を SimulationTick の外側で消化する
    /// - H / A / R キーは状態を設定するだけで、追加の SimulationTick は進めない
    /// - Pause中でも H / A / R / Space / . を受理する
    /// - ゲーム入力（矢印 / J）の内容は判断しない（DebugGameplayInput + Session の責務）
    ///
    /// A / R（段階10B-2）:
    /// - P1 AttackState のデバッグ操作（Session.StartTestActionForP1 / ResetTestActionForP1）
    /// - ClockDriver は Participant を検索せず、既存の Session 参照だけを使う
    /// - 攻撃進行は通常の Participant 共通 tick 経路を使用（専用進行は持たない）
    /// - Pause + Step によるフレーム確認に使う
    ///
    /// 混同しない3つの「止まる」:
    /// - Pause … 自動の SimulationTick 進行を止める（手動Stepは可）。入力サンプルも増えない
    /// - HitStop … CombatFrame と Action 進行だけ止める（SimulationTickと入力サンプルは進む）
    /// - Action停止 … ActionFrame だけ進まない（CombatFrameは進む）
    ///
    /// ProcessOneSimulationTick は「1tick進めると決まったあと」だけを担当します。
    /// </summary>
    public class SimulationClockDriver : MonoBehaviour
    {
        private const float DefaultSecondsPerTick = 1f / 60f;

        [Header("参照")]
        [Tooltip("論理tickを実際に進める SimulationSession です。")]
        [SerializeField]
        private SimulationSession simulationSession;

        [Tooltip("Pause / Step / HitStop / Actionテストのキー要求を取る DebugPlaybackInput です。")]
        [SerializeField]
        private DebugPlaybackInput debugPlaybackInput;

        [Header("60Hz設定")]
        [Tooltip("1論理tickあたりの実時間（秒）。通常は 1/60 です。")]
        [SerializeField]
        private float secondsPerSimulationTick = DefaultSecondsPerTick;

        [Tooltip(
            "1回のUpdateでまとめて進められる最大tick数です。"
            + "フレーム落ちで実時間が溜まりすぎたとき、"
            + "無限に追いつかず上限で打ち切るための安全弁です。"
        )]
        [SerializeField]
        private int maxCatchUpTicksPerUpdate = 5;

        [Header("HitStopテスト（段階4）")]
        [Tooltip(
            "Hキーで設定するテスト用 HitStop の長さ（SimulationTick数）です。"
            + " 0以下なら HitStop を発生させません。"
            + " HitStop中に再度Hを押すと、この値で単純に上書きします（加算しません）。"
        )]
        [SerializeField]
        private int testHitStopFrames = 6;

        /// <summary>
        /// まだ消費していない実時間（秒）です。
        /// Pause中は増やさず、Pause突入時に0へ戻して解除直後の大量catch-upを防ぎます。
        /// </summary>
        private float accumulatedSeconds;

        /// <summary>
        /// Unityがこのコンポーネントを有効化した直後に1回呼びます。
        /// </summary>
        private void Awake()
        {
            if (simulationSession == null)
            {
                Debug.LogError(
                    "SimulationClockDriver: SimulationSession が未設定です。"
                );
            }

            if (debugPlaybackInput == null)
            {
                Debug.LogError(
                    "SimulationClockDriver: DebugPlaybackInput が未設定です。"
                );
            }

            if (secondsPerSimulationTick <= 0f)
            {
                secondsPerSimulationTick = DefaultSecondsPerTick;
            }

            if (maxCatchUpTicksPerUpdate < 1)
            {
                maxCatchUpTicksPerUpdate = 1;
            }

            accumulatedSeconds = 0f;
        }

        /// <summary>
        /// Unityが描画フレームごとに呼びます。
        /// Pause / HitStop / Action / Step の消化 →（必要なら）論理tick進行、の順です。
        /// </summary>
        private void Update()
        {
            if (simulationSession == null)
            {
                return;
            }

            SimulationTimeState timeState = simulationSession.TimeState;
            if (timeState == null)
            {
                return;
            }

            // ============================================================
            // 1. Pause切替要求を取得・消化（SimulationTickの外側）
            // ============================================================
            bool pauseToggleRequested = false;
            if (debugPlaybackInput != null)
            {
                pauseToggleRequested = debugPlaybackInput.ConsumePauseToggleRequest();
            }

            if (pauseToggleRequested)
            {
                ApplyPauseToggle(timeState);
            }

            // ============================================================
            // 2. テスト用 HitStop 要求を取得・消化（SimulationTickの外側）
            //    Pause中でも受理する。Hだけでは tick を進めない。
            // ============================================================
            bool testHitStopRequested = false;
            if (debugPlaybackInput != null)
            {
                testHitStopRequested = debugPlaybackInput.ConsumeTestHitStopRequest();
            }

            if (testHitStopRequested)
            {
                ApplyTestHitStop(timeState);
            }

            // ============================================================
            // 3. テスト用 Action 開始 / Reset（SimulationTickの外側）
            //    Pause中でも受理する。A/Rだけでは tick を進めない。
            //    Pause状態と Action再生状態は別物です。
            // ============================================================
            bool testActionStartRequested = false;
            bool testActionResetRequested = false;
            if (debugPlaybackInput != null)
            {
                testActionStartRequested = debugPlaybackInput.ConsumeTestActionStartRequest();
                testActionResetRequested = debugPlaybackInput.ConsumeTestActionResetRequest();
            }

            // 同じUnityフレームで A と R が両方来た場合は、後勝ちではなく
            // 「開始→Reset」の順で適用する（Resetが最終状態になる）。
            // 通常は同時押しを想定しないが、処理順を明示しておく。
            if (testActionStartRequested)
            {
                ApplyTestActionStart();
            }

            if (testActionResetRequested)
            {
                ApplyTestActionReset();

                // ------------------------------------------------------------
                // Reset を受理した Unity フレームでは、以降の通常 SimulationTick へ進まない。
                //
                // なぜか:
                // Reset 直後に押しっぱなしのゲーム操作を同一フレームの tick で再処理すると、
                // 意図しない移動・ジャンプ・攻撃が始まるため。
                //
                // このフレームは Reset（位置・戦闘状態・共通 release gate 開始）だけを行い、
                // 次の Unity フレーム以降の tick で gate 判定を続ける。
                // 有効入力は、全ゲーム操作（Left/Right/Up/Down/Attack）を一度離すまで
                // ニュートラルのまま（R は解除条件に含めない）。
                // ------------------------------------------------------------
                return;
            }

            // ============================================================
            // 4. Step要求を取得（SimulationTickの外側）
            // ============================================================
            bool stepRequested = false;
            if (debugPlaybackInput != null)
            {
                stepRequested = debugPlaybackInput.ConsumeStepRequest();
            }

            // ============================================================
            // 5. Pause中: Stepがあれば1tickだけ。なければ自動進行しない
            // ============================================================
            if (timeState.IsPaused)
            {
                // Pause中は蓄積時間を増やさない（解除直後の大量catch-up防止）
                if (stepRequested)
                {
                    // HitStop / Action再生の有無は Session 内で判断する。
                    // 状態メッセージは Session が書く。
                    simulationSession.ProcessOneSimulationTick();
                    timeState.LastStepResult = "実行";
                    Debug.Log("[FightDebug] Step executed");
                }

                return;
            }

            // ============================================================
            // 6. Pause解除中: Stepでは追加進行しない。通常の60Hz進行のみ
            // ============================================================
            if (stepRequested)
            {
                timeState.LastStepResult = "無視（Pause解除中）";
                timeState.LastStatusMessage = "Pause解除中のためStep要求を無視しました";
                Debug.Log("[FightDebug] Step ignored (not paused)");
            }

            // Time.timeScale は変更しません。unscaled で実時間を受け取ります。
            float deltaSeconds = Time.unscaledDeltaTime;
            accumulatedSeconds = accumulatedSeconds + deltaSeconds;

            AdvanceByAccumulatedTime();
        }

        /// <summary>
        /// Pause状態を反転します。Pauseへ入るときは蓄積残を0に戻します。
        /// Pauseは自動進行を止めます。HitStop（CombatFrameだけ止める）とは別物です。
        /// </summary>
        private void ApplyPauseToggle(SimulationTimeState timeState)
        {
            bool willPause = (timeState.IsPaused == false);
            timeState.IsPaused = willPause;

            if (willPause)
            {
                accumulatedSeconds = 0f;
                timeState.LastStatusMessage = "Pauseに入りました";
                Debug.Log("[FightDebug] Pause ON");
            }
            else
            {
                timeState.LastStatusMessage = "Pauseを解除しました";
                Debug.Log("[FightDebug] Pause OFF");
            }
        }

        /// <summary>
        /// Hキーによるテスト用 HitStop を設定します。
        /// SimulationTickは進めません。残りの上書きのみです。
        ///
        /// 再入力仕様（今回）:
        /// HitStop中に再度Hを押した場合も、testHitStopFrames で単純上書きします。
        /// 加算・最大値比較・延長規則はまだ実装しません。
        /// </summary>
        private void ApplyTestHitStop(SimulationTimeState timeState)
        {
            if (testHitStopFrames <= 0)
            {
                timeState.LastStatusMessage =
                    "HitStop skipped: frames<=0";
                return;
            }

            // 単純上書き（加算しない）
            timeState.HitStopRemaining = testHitStopFrames;
            // HUD用文言は TMP 既存グリフのみ（欠落回避のため ASCII）
            timeState.LastStatusMessage =
                "HitStop start: " + testHitStopFrames;
            Debug.Log("[FightDebug] Test HitStop started: " + testHitStopFrames);
        }

        /// <summary>
        /// Aキー: P1 AttackState のテスト用パンチ開始を Session に依頼します。
        ///
        /// SimulationTick は進めません。Pause 中でも受理します。
        /// </summary>
        private void ApplyTestActionStart()
        {
            if (simulationSession == null)
            {
                return;
            }

            simulationSession.StartTestActionForP1();
        }

        /// <summary>
        /// Rキー: 練習モードの Training Reset を Session に依頼します。
        ///
        /// SimulationTick は進めません。Pause 中でも受理します。
        /// 呼び出し元（Update）は Reset 受理フレームで通常 tick 進行へ進まないこと。
        /// </summary>
        private void ApplyTestActionReset()
        {
            if (simulationSession == null)
            {
                return;
            }

            simulationSession.ResetTestActionForP1();
        }

        /// <summary>
        /// 蓄積した実時間から、上限付きで SimulationTick を進めます。
        /// Pause解除中だけ呼ばれます。
        /// </summary>
        private void AdvanceByAccumulatedTime()
        {
            int processedCatchUpCount = 0;

            while (accumulatedSeconds >= secondsPerSimulationTick
                   && processedCatchUpCount < maxCatchUpTicksPerUpdate)
            {
                accumulatedSeconds = accumulatedSeconds - secondsPerSimulationTick;
                processedCatchUpCount = processedCatchUpCount + 1;
                simulationSession.ProcessOneSimulationTick();
            }

            if (processedCatchUpCount >= maxCatchUpTicksPerUpdate
                && accumulatedSeconds >= secondsPerSimulationTick)
            {
                accumulatedSeconds = secondsPerSimulationTick * 0.99f;
            }
        }
    }
}
