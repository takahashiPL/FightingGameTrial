using UnityEngine;
using UnityEngine.InputSystem;

namespace FightingGameTrial.DebugTools
{
    /// <summary>
    /// デバッグ再生用の操作要求だけを、Unityの描画フレームごとに取得します。
    ///
    /// 責務:
    /// - Space / Period(.) / H の「押下エッジ」を検出する
    /// - Pause切替・Step・テスト用HitStop要求を一時保持する
    /// - SimulationClockDriver が外側で消化できるように公開する
    ///
    /// やらないこと:
    /// - SimulationTick を進めない
    /// - Pause / HitStop の状態そのものを持たない
    /// - 格闘用の方向入力や技入力は扱わない
    ///
    /// なぜ SimulationTick の外側で入力するか:
    /// Pause中は論理 SimulationTick が止まります。
    /// それでも Pause解除・Step・HitStopテストキーは受け取る必要があるため、
    /// 入力取得は ProcessOneSimulationTick の中ではなく、Unityの Update 側で行います。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class DebugPlaybackInput : MonoBehaviour
    {
        /// <summary>
        /// このUnityフレームで Pause切替が要求されたか。
        /// </summary>
        private bool pauseToggleRequested;

        /// <summary>
        /// このUnityフレームで Step（1tick送り）が要求されたか。
        /// </summary>
        private bool stepRequested;

        /// <summary>
        /// このUnityフレームでテスト用 HitStop 発生が要求されたか。
        /// </summary>
        private bool testHitStopRequested;

        /// <summary>
        /// Unityが描画フレームごとに呼びます。
        /// 押下エッジだけを拾い、押しっぱなしでは毎フレーム要求を立てません。
        /// </summary>
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            // wasPressedThisFrame = 今フレームで「押した瞬間」だけ true
            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                pauseToggleRequested = true;
            }

            if (keyboard.periodKey.wasPressedThisFrame)
            {
                stepRequested = true;
            }

            if (keyboard.hKey.wasPressedThisFrame)
            {
                testHitStopRequested = true;
            }
        }

        /// <summary>
        /// Pause切替要求を1回分取り出し、内部フラグを下ろします。
        /// </summary>
        public bool ConsumePauseToggleRequest()
        {
            bool requested = pauseToggleRequested;
            pauseToggleRequested = false;
            return requested;
        }

        /// <summary>
        /// Step要求を1回分取り出し、内部フラグを下ろします。
        /// </summary>
        public bool ConsumeStepRequest()
        {
            bool requested = stepRequested;
            stepRequested = false;
            return requested;
        }

        /// <summary>
        /// テスト用 HitStop 発生要求を1回分取り出し、内部フラグを下ろします。
        /// </summary>
        public bool ConsumeTestHitStopRequest()
        {
            bool requested = testHitStopRequested;
            testHitStopRequested = false;
            return requested;
        }
    }
}
