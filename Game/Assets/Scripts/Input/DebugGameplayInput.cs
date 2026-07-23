using UnityEngine;
using UnityEngine.InputSystem;

namespace FightingGameTrial.Input
{
    /// <summary>
    /// ゲームプレイ用の物理キー状態を、Unity の描画フレーム（Update）ごとに保持します。
    ///
    /// 責務:
    /// - Input System から現在の物理キー状態を読む
    /// - 最新状態をフィールドに保持する
    /// - SimulationSession が SimulationTick で読み取れるように公開する
    ///
    /// やらないこと:
    /// - SimulationTick を進めない
    /// - SimulationTimeState / CurrentInput を直接変更しない
    /// - Pause / HitStop / Action を判断しない
    /// - DebugPlaybackInput（Space / . / H / A / R）と責務を混ぜない
    ///
    /// 物理入力と論理入力の違い:
    /// - 物理入力 … このクラスが Update で持つ「今キーボードがどうか」
    /// - 論理入力 … SimulationTick で確定した SimulationInputState（CurrentInput）
    ///
    /// Pause中のずれ:
    /// Pause中も Update は動くので、このクラスの物理状態は変わり続けます。
    /// 一方 SimulationTick が進まないため、HUDに出る確定入力（CurrentInput）は
    /// 最後にサンプリングした値のままです。
    /// Pause中にキーを押してから Step（.）すると、その瞬間の物理状態が論理入力へコピーされます。
    ///
    /// キー割り当て（段階6）:
    /// - 矢印キーのみを方向の正本にする（A/D/W/S は使わない）
    /// - A は既存のテスト用 Action 開始キーのまま残すため、方向には使わない
    /// - J = Attack（Held）
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class DebugGameplayInput : MonoBehaviour
    {
        /// <summary>左矢印が押されているか（物理）。</summary>
        private bool leftPressed;

        /// <summary>右矢印が押されているか（物理）。</summary>
        private bool rightPressed;

        /// <summary>上矢印が押されているか（物理）。</summary>
        private bool upPressed;

        /// <summary>下矢印が押されているか（物理）。</summary>
        private bool downPressed;

        /// <summary>J が押されているか（物理・Held）。</summary>
        private bool attackPressed;

        /// <summary>左方向の最新物理状態です。</summary>
        public bool IsLeftPressed
        {
            get { return leftPressed; }
        }

        /// <summary>右方向の最新物理状態です。</summary>
        public bool IsRightPressed
        {
            get { return rightPressed; }
        }

        /// <summary>上方向の最新物理状態です。</summary>
        public bool IsUpPressed
        {
            get { return upPressed; }
        }

        /// <summary>下方向の最新物理状態です。</summary>
        public bool IsDownPressed
        {
            get { return downPressed; }
        }

        /// <summary>Attack（J）の最新物理状態です。Heldのみ。押下エッジは持ちません。</summary>
        public bool IsAttackPressed
        {
            get { return attackPressed; }
        }

        /// <summary>
        /// Unityが描画フレームごとに呼びます。
        /// ここで読むのは「今の物理キー」だけです。論理サンプルは増やしません。
        /// </summary>
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                leftPressed = false;
                rightPressed = false;
                upPressed = false;
                downPressed = false;
                attackPressed = false;
                return;
            }

            // isPressed = 今押されているか（押しっぱなしでも true）
            // サンプリング確定は SimulationSession 側で SimulationTick ごとに行う
            leftPressed = keyboard.leftArrowKey.isPressed;
            rightPressed = keyboard.rightArrowKey.isPressed;
            upPressed = keyboard.upArrowKey.isPressed;
            downPressed = keyboard.downArrowKey.isPressed;
            attackPressed = keyboard.jKey.isPressed;
        }
    }
}
