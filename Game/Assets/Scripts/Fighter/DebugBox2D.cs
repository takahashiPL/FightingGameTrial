using System;
using UnityEngine;

namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// デバッグ用の軸平行 2D Box です（段階11A）。
    ///
    /// 何を担当するか:
    /// - 中心・半幅・半高・有効フラグを1つにまとめる
    /// - Min/Max を計算して可視化や将来の重なり判定で使う
    ///
    /// なぜ Push / Hurt / Hit を分けるか:
    /// - Push … 立ち位置の重なり防止（すり抜け防止）
    /// - Hurt … 被攻撃側の当たり領域
    /// - Hit  … 攻撃側の攻撃領域（Active 中のみ有効）
    /// 役割が違うため、同じ矩形を使い回さず種類ごとに持つ。
    ///
    /// 現段階では判定には未使用:
    /// Hit は従来どおり距離判定。この型は可視化と座標の正本化のための土台です。
    /// </summary>
    [Serializable]
    public class DebugBox2D
    {
        [Tooltip("Box 中心の X（ローカル定義なら Participant 原点からの相対、World ならワールド座標）。")]
        public float CenterX;

        [Tooltip("Box 中心の Y（ローカル定義なら Participant 原点からの相対、World ならワールド座標）。")]
        public float CenterY;

        [Tooltip("横方向の半幅（全体幅の半分）。")]
        public float HalfWidth;

        [Tooltip("縦方向の半高（全体高さの半分）。")]
        public float HalfHeight;

        [Tooltip("この Box が今有効か。Hit Box は Active 中だけ true。")]
        public bool IsActive;

        public float MinX
        {
            get { return CenterX - HalfWidth; }
        }

        public float MaxX
        {
            get { return CenterX + HalfWidth; }
        }

        public float MinY
        {
            get { return CenterY - HalfHeight; }
        }

        public float MaxY
        {
            get { return CenterY + HalfHeight; }
        }

        public DebugBox2D()
        {
            CenterX = 0f;
            CenterY = 0f;
            HalfWidth = 0f;
            HalfHeight = 0f;
            IsActive = false;
        }

        public DebugBox2D(
            float centerX,
            float centerY,
            float halfWidth,
            float halfHeight,
            bool isActive)
        {
            CenterX = centerX;
            CenterY = centerY;
            HalfWidth = halfWidth;
            HalfHeight = halfHeight;
            IsActive = isActive;
        }

        /// <summary>
        /// 値をまとめて書き込みます（毎フレームの World 再計算用）。
        /// </summary>
        public void Set(
            float centerX,
            float centerY,
            float halfWidth,
            float halfHeight,
            bool isActive)
        {
            CenterX = centerX;
            CenterY = centerY;
            HalfWidth = halfWidth;
            HalfHeight = halfHeight;
            IsActive = isActive;
        }

        /// <summary>
        /// 矩形の4隅（左下→右下→右上→左上）を World 座標で返します。
        /// </summary>
        public void GetCorners(
            out Vector3 bottomLeft,
            out Vector3 bottomRight,
            out Vector3 topRight,
            out Vector3 topLeft)
        {
            bottomLeft = new Vector3(MinX, MinY, 0f);
            bottomRight = new Vector3(MaxX, MinY, 0f);
            topRight = new Vector3(MaxX, MaxY, 0f);
            topLeft = new Vector3(MinX, MaxY, 0f);
        }
    }
}
