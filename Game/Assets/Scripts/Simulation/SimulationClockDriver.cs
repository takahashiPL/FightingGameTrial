using UnityEngine;

namespace FightingGameTrial.Simulation
{
    /// <summary>
    /// Unityの実時間（Update）を受け皿にして、60Hzの論理 SimulationTick 進行を依頼します。
    ///
    /// 重要:
    /// - Unity の Update / FixedUpdate はゲーム仕様の正本ではありません。
    /// - 正本は docs/rules.md の 60Hz 論理 SimulationTick です。
    /// - このクラスは「実時間が 1/60 秒たまったら Session に1回進めさせる」だけを担当します。
    ///
    /// 今回（段階1）は Pause / Step を実装しません。
    /// 将来、Pause中は自動進行を止め、Stepは外側から Session を1回呼ぶ想定です。
    /// </summary>
    public class SimulationClockDriver : MonoBehaviour
    {
        private const float DefaultSecondsPerTick = 1f / 60f;

        [Header("参照")]
        [Tooltip("論理tickを実際に進める SimulationSession です。同じ SimulationRoot 配下を指定します。")]
        [SerializeField]
        private SimulationSession simulationSession;

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
        /// 1/60以上たまったら Session へ進行を依頼し、その分を減らします。
        /// </summary>
        private float accumulatedSeconds;

        /// <summary>
        /// Unityがこのコンポーネントを有効化した直後に1回呼びます。
        /// Session参照の欠落を早く気づけるように検査します。
        /// </summary>
        private void Awake()
        {
            if (simulationSession == null)
            {
                Debug.LogError(
                    "SimulationClockDriver: SimulationSession が未設定です。"
                    + " Inspector で SimulationRoot 配下の SimulationSession を割り当ててください。"
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
        /// Unityが描画フレームごとに呼びます（実時間の受け皿）。
        /// ここで格闘判定や CombatFrame の仕様分岐は行いません。
        /// たまった実時間に応じて ProcessOneSimulationTick を依頼するだけです。
        /// </summary>
        private void Update()
        {
            if (simulationSession == null)
            {
                return;
            }

            // Time.timeScale の影響を受けにくいよう unscaled を使います。
            // （将来の Pause は自前フラグで止める想定。今回は Pause未実装）
            float deltaSeconds = Time.unscaledDeltaTime;
            accumulatedSeconds = accumulatedSeconds + deltaSeconds;

            int processedCatchUpCount = 0;

            // フレーム落ち時は while で複数tick追いつきます。
            // ただし無制限にはせず、maxCatchUpTicksPerUpdate で打ち切ります。
            while (accumulatedSeconds >= secondsPerSimulationTick
                   && processedCatchUpCount < maxCatchUpTicksPerUpdate)
            {
                accumulatedSeconds = accumulatedSeconds - secondsPerSimulationTick;
                processedCatchUpCount = processedCatchUpCount + 1;

                // 論理進行の本体は Session 側（ゲーム仕様に近い入口）
                simulationSession.ProcessOneSimulationTick();
            }

            // 上限で打ち切った場合、余り時間を捨てすぎないよう残します。
            // ただし余りが極端に大きいと次フレームも上限に当たるため、
            // 学習用には「遅れを一気に消化しすぎない」ことを優先します。
            if (processedCatchUpCount >= maxCatchUpTicksPerUpdate
                && accumulatedSeconds >= secondsPerSimulationTick)
            {
                // 溢れ分を1tickぶん未満に抑え、永久に遅れが膨らみ続けるのを防ぎます。
                accumulatedSeconds = secondsPerSimulationTick * 0.99f;
            }
        }
    }
}
