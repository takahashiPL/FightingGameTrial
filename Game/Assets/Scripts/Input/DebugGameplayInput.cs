using UnityEngine;
using UnityEngine.InputSystem;

namespace FightingGameTrial.Input
{
    /// <summary>
    /// ゲームプレイ用の物理キー状態を、Unity の描画フレーム（Update）ごとに保持します。
    ///
    /// キー割り当て（Unity デバッグ）:
    /// - 矢印キー = 方向
    /// - J = Attack（地上 J Punch・Held）
    /// - K = Kick（地上 Ground Kick・Held）
    ///
    /// エッジ検出・バッファは持ちません。SimulationSession が Tick でエッジ化します。
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class DebugGameplayInput : MonoBehaviour
    {
        private bool leftPressed;
        private bool rightPressed;
        private bool upPressed;
        private bool downPressed;
        private bool attackPressed;
        private bool kickPressed;

        public bool IsLeftPressed
        {
            get { return leftPressed; }
        }

        public bool IsRightPressed
        {
            get { return rightPressed; }
        }

        public bool IsUpPressed
        {
            get { return upPressed; }
        }

        public bool IsDownPressed
        {
            get { return downPressed; }
        }

        public bool IsAttackPressed
        {
            get { return attackPressed; }
        }

        public bool IsKickPressed
        {
            get { return kickPressed; }
        }

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
                kickPressed = false;
                return;
            }

            leftPressed = keyboard.leftArrowKey.isPressed;
            rightPressed = keyboard.rightArrowKey.isPressed;
            upPressed = keyboard.upArrowKey.isPressed;
            downPressed = keyboard.downArrowKey.isPressed;
            attackPressed = keyboard.jKey.isPressed;
            kickPressed = keyboard.kKey.isPressed;
        }
    }
}
