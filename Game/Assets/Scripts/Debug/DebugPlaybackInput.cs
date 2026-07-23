using UnityEngine;
using UnityEngine.InputSystem;

namespace FightingGameTrial.DebugTools
{
    /// <summary>
    /// デバッグ再生用の操作要求だけを、Unityの描画フレームごとに取得します。
    ///
    /// 責務:
    /// - Space / Period(.) の「押下エッジ」を検出する
    /// - Pause切替要求・Step要求を一時保持する
    /// - SimulationClockDriver が外側で消化できるように公開する
    ///
    /// やらないこと:
    /// - SimulationTick を進めない
    /// - Pause状態そのものを持たない（状態は SimulationTimeState.IsPaused）
    /// - 格闘用の方向入力や技入力は扱わない
    ///
    /// なぜ SimulationTick の外側で入力するか:
    /// Pause中は論理 SimulationTick が止まります。
    /// それでも Pause解除キーと Step キーは受け取る必要があるため、
    /// 入力取得は ProcessOneSimulationTick の中ではなく、Unityの Update 側で行います。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class DebugPlaybackInput : MonoBehaviour
    {
        /// <summary>
        /// このUnityフレームで Pause切替が要求されたか。
        /// Consume で読み取ったあとに false へ戻します。
        /// </summary>
        private bool pauseToggleRequested;

        /// <summary>
        /// このUnityフレームで Step（1tick送り）が要求されたか。
        /// Consume で読み取ったあとに false へ戻します。
        /// </summary>
        private bool stepRequested;

        /// <summary>
        /// Unityが描画フレームごとに呼びます。
        /// ここでキーの押下エッジだけを拾い、要求フラグを立てます。
        /// 押しっぱなし（保持）では毎フレーム要求を立てません。
        /// </summary>
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            // wasPressedThisFrame = 今フレームで「押した瞬間」だけ true
            // isPressed = 押しっぱなし中も true（連打扱いになるので使わない）
            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                pauseToggleRequested = true;
            }

            if (keyboard.periodKey.wasPressedThisFrame)
            {
                stepRequested = true;
            }
        }

        /// <summary>
        /// Pause切替要求を1回分取り出し、内部フラグを下ろします。
        /// SimulationClockDriver の Update 先頭付近から呼びます。
        /// </summary>
        public bool ConsumePauseToggleRequest()
        {
            bool requested = pauseToggleRequested;
            pauseToggleRequested = false;
            return requested;
        }

        /// <summary>
        /// Step要求を1回分取り出し、内部フラグを下ろします。
        /// SimulationClockDriver の Update から呼びます。
        /// </summary>
        public bool ConsumeStepRequest()
        {
            bool requested = stepRequested;
            stepRequested = false;
            return requested;
        }
    }
}
