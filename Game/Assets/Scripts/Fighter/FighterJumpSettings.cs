using System;
using UnityEngine;

namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// 1種類のジャンプ弧（Neutral / Forward / Backward）の調整値です。
    ///
    /// Min / Max を持ち、入力保持率でその間を補間します。
    /// Rigidbody 物理式へ無理に固定せず、格闘ゲーム用の調整パラメータとして使います。
    /// 将来の Character Data / ScriptableObject へ移しやすいよう、Motor から分離したデータです。
    /// </summary>
    [Serializable]
    public sealed class JumpArcSettings
    {
        [Tooltip("短押し寄りの最高到達高度（ワールド単位）。")]
        public float MinHeight = 1.8f;

        [Tooltip("長押し寄りの最高到達高度（ワールド単位）。")]
        public float MaxHeight = 2.2f;

        [Tooltip("短押し寄りの総滞空 CombatFrame 数。")]
        public int MinDurationFrames = 28;

        [Tooltip("長押し寄りの総滞空 CombatFrame 数。")]
        public int MaxDurationFrames = 34;

        [Tooltip("方向短押し寄りの水平移動距離（ワールド単位）。Neutral は 0。")]
        public float MinHorizontalDistance = 0f;

        [Tooltip("方向長押し寄りの水平移動距離（ワールド単位）。Neutral は 0。")]
        public float MaxHorizontalDistance = 0f;

        /// <summary>
        /// ゼロや逆転を避け、安全な値へ整えます（Scene 未配線時の保険）。
        /// </summary>
        public void Sanitize()
        {
            if (MinHeight < 0.1f)
            {
                MinHeight = 0.1f;
            }

            if (MaxHeight < MinHeight)
            {
                MaxHeight = MinHeight;
            }

            if (MinDurationFrames < 4)
            {
                MinDurationFrames = 4;
            }

            if (MaxDurationFrames < MinDurationFrames)
            {
                MaxDurationFrames = MinDurationFrames;
            }

            if (MinHorizontalDistance < 0f)
            {
                MinHorizontalDistance = 0f;
            }

            if (MaxHorizontalDistance < MinHorizontalDistance)
            {
                MaxHorizontalDistance = MinHorizontalDistance;
            }
        }
    }

    /// <summary>
    /// キャラクター（Motor）ごとのジャンプ性能設定です。
    ///
    /// P1/P2 で別インスタンスを持てるため、将来キャラ差を付けやすいです。
    /// ScriptableObject 化はまだせず、SerializeField のデフォルト値で安全に動きます。
    /// </summary>
    [Serializable]
    public sealed class FighterJumpSettings
    {
        [Tooltip("その場ジャンプ（上のみ）。水平距離は基本 0。")]
        public JumpArcSettings NeutralJump = CreateNeutralDefaults();

        [Tooltip("前方ジャンプ（Facing と同方向＋上）。")]
        public JumpArcSettings ForwardJump = CreateForwardDefaults();

        [Tooltip("後方ジャンプ（Facing と逆方向＋上）。")]
        public JumpArcSettings BackwardJump = CreateBackwardDefaults();

        [Tooltip("上入力を何 CombatFrame 保持すると高さ・時間が Max 寄りになるか。")]
        public int JumpHoldFramesToMax = 8;

        [Tooltip("前後方向を何 CombatFrame 保持すると水平距離が Max 寄りになるか。")]
        public int DirectionHoldFramesToMax = 10;

        [Tooltip("ジャンプ種類と逆方向入力時に、水平速度を弱く戻す量（毎 CombatFrame）。")]
        public float ReverseAirControlPerFrame = 0.02f;

        [Tooltip("着地後 Landing Visual を残す CombatFrame 数。この間は再ジャンプ不可。")]
        public int LandingFrames = 2;

        [Tooltip(
            "相手との論理 Y 差（または地上からの高さ）がこれ以上なら横 Push をスキップし飛び越え可能。"
        )]
        public float PushBoxVerticalSeparationThreshold = 0.85f;

        /// <summary>
        /// デフォルト Neutral 弧。
        /// </summary>
        public static JumpArcSettings CreateNeutralDefaults()
        {
            JumpArcSettings arc = new JumpArcSettings();
            arc.MinHeight = 1.8f;
            arc.MaxHeight = 2.2f;
            arc.MinDurationFrames = 28;
            arc.MaxDurationFrames = 34;
            arc.MinHorizontalDistance = 0f;
            arc.MaxHorizontalDistance = 0f;
            return arc;
        }

        /// <summary>
        /// デフォルト Forward 弧。
        /// </summary>
        public static JumpArcSettings CreateForwardDefaults()
        {
            JumpArcSettings arc = new JumpArcSettings();
            arc.MinHeight = 1.7f;
            arc.MaxHeight = 2.1f;
            arc.MinDurationFrames = 28;
            arc.MaxDurationFrames = 34;
            arc.MinHorizontalDistance = 2.3f;
            arc.MaxHorizontalDistance = 2.8f;
            return arc;
        }

        /// <summary>
        /// デフォルト Backward 弧。
        /// </summary>
        public static JumpArcSettings CreateBackwardDefaults()
        {
            JumpArcSettings arc = new JumpArcSettings();
            arc.MinHeight = 1.6f;
            arc.MaxHeight = 2.0f;
            arc.MinDurationFrames = 26;
            arc.MaxDurationFrames = 32;
            arc.MinHorizontalDistance = 1.7f;
            arc.MaxHorizontalDistance = 2.1f;
            return arc;
        }

        /// <summary>
        /// 参照欠落と不正値を直し、ジャンプ不能・ゼロ除算を防ぎます。
        /// </summary>
        public void EnsureValidOrFillDefaults()
        {
            if (NeutralJump == null)
            {
                NeutralJump = CreateNeutralDefaults();
            }

            if (ForwardJump == null)
            {
                ForwardJump = CreateForwardDefaults();
            }

            if (BackwardJump == null)
            {
                BackwardJump = CreateBackwardDefaults();
            }

            NeutralJump.Sanitize();
            ForwardJump.Sanitize();
            BackwardJump.Sanitize();

            if (JumpHoldFramesToMax < 1)
            {
                JumpHoldFramesToMax = 8;
            }

            if (DirectionHoldFramesToMax < 1)
            {
                DirectionHoldFramesToMax = 10;
            }

            if (ReverseAirControlPerFrame < 0f)
            {
                ReverseAirControlPerFrame = 0.02f;
            }

            if (LandingFrames < 1)
            {
                LandingFrames = 2;
            }

            if (PushBoxVerticalSeparationThreshold < 0.1f)
            {
                PushBoxVerticalSeparationThreshold = 0.85f;
            }
        }

        /// <summary>
        /// JumpType に対応する弧設定を返します。None は Neutral にフォールバック。
        /// </summary>
        public JumpArcSettings GetArc(FighterJumpType jumpType)
        {
            EnsureValidOrFillDefaults();

            if (jumpType == FighterJumpType.Forward)
            {
                return ForwardJump;
            }

            if (jumpType == FighterJumpType.Backward)
            {
                return BackwardJump;
            }

            return NeutralJump;
        }
    }
}
