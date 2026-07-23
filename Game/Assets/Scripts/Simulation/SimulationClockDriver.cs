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
    /// 段階2の責務:
    /// - Pause切替要求と Step要求を SimulationTick の外側で消化する
    /// - Pause中は自動進行しない
    /// - Pause中の Step だけ ProcessOneSimulationTick を1回呼ぶ
    /// - Pause解除中は従来どおり実時間蓄積で60Hz進行する
    ///
    /// Pause中でもキー入力を受け取る必要があるため、
    /// 入力取得（DebugPlaybackInput）と Pause/Step の消化は
    /// ProcessOneSimulationTick の外（この Update）で行います。
    /// </summary>
    public class SimulationClockDriver : MonoBehaviour
    {
        private const float DefaultSecondsPerTick = 1f / 60f;

        [Header("参照")]
        [Tooltip("論理tickを実際に進める SimulationSession です。")]
        [SerializeField]
        private SimulationSession simulationSession;

        [Tooltip("Pause / Step のキー要求を取る DebugPlaybackInput です。")]
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
        /// Pause/Stepの消化 →（必要なら）論理tick進行、の順で上から追えます。
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
            // 1. Pause切替要求を取得（SimulationTickの外側）
            // ============================================================
            bool pauseToggleRequested = false;
            if (debugPlaybackInput != null)
            {
                pauseToggleRequested = debugPlaybackInput.ConsumePauseToggleRequest();
            }

            // ============================================================
            // 2. Pause切替要求を消化
            // ============================================================
            if (pauseToggleRequested)
            {
                ApplyPauseToggle(timeState);
            }

            // ============================================================
            // 3. Step要求を取得（SimulationTickの外側）
            // ============================================================
            bool stepRequested = false;
            if (debugPlaybackInput != null)
            {
                stepRequested = debugPlaybackInput.ConsumeStepRequest();
            }

            // ============================================================
            // 4. Pause中: Stepがあれば1tickだけ。なければ自動進行しない
            // ============================================================
            if (timeState.IsPaused)
            {
                // Pause中は蓄積時間を増やさない（解除直後の大量catch-up防止）
                if (stepRequested)
                {
                    simulationSession.ProcessOneSimulationTick();
                    // HUD用の短い結果（制御はここ、表示は DebugHudView）
                    timeState.LastStepResult = "実行";
                    timeState.LastStatusMessage = "Pause中に1 SimulationTick進めました";
                    Debug.Log("[FightDebug] Step executed");
                }

                return;
            }

            // ============================================================
            // 5. Pause解除中: Stepでは追加進行しない。通常の60Hz進行のみ
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
        /// Pause状態を反転し、メッセージと即時ログを更新します。
        /// Pauseへ入るときは蓄積残を0に戻します。
        /// </summary>
        private void ApplyPauseToggle(SimulationTimeState timeState)
        {
            bool willPause = (timeState.IsPaused == false);
            timeState.IsPaused = willPause;

            if (willPause)
            {
                // Pause中に実時間が溜まると解除直後に一気に進むため、残を捨てます。
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
