using System;
using UnityEngine;

namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// 1つの Visual State に割り当てる Sprite 列です。
    ///
    /// 1枚でも複数枚でも同じ構造で扱います。
    /// （状態ごとに単独 Sprite フィールドを増やさないための共通入れ物）
    ///
    /// 見た目専用です。Sequence の長さや終了で Gameplay State（攻撃終了・着地等）を決めません。
    /// 進行は固定 CombatFrame（elapsedFrames）基準です。Animator / Time.deltaTime は使いません。
    /// </summary>
    [Serializable]
    public sealed class FighterSpriteSequence
    {
        [Tooltip("表示する Sprite 列。1枚でも複数枚でも可。null 要素は Resolve 時に飛ばします。")]
        [SerializeField]
        private Sprite[] sprites;

        [Tooltip(
            "1枚の Sprite を何 CombatFrame 表示するか。"
            + " 1未満は 1 として扱います（除算エラー防止）。"
        )]
        [SerializeField]
        private int framesPerSprite = 6;

        [Tooltip("true なら末尾の次は先頭へ戻ります。false なら最後の index で止めます。")]
        [SerializeField]
        private bool loop = true;

        [Tooltip(
            "loop=false のとき、最後の Sprite を保持するか。"
            + " false でも配列外参照はせず、最後の index を使います。"
        )]
        [SerializeField]
        private bool holdLastFrame = true;

        /// <summary>
        /// 有効な（非 null）Sprite が1枚以上あるか。
        /// </summary>
        public bool IsValid
        {
            get { return FindFirstNonNullSprite() != null; }
        }

        /// <summary>
        /// 先頭の非 null Sprite。無ければ null。
        /// </summary>
        public Sprite FirstSprite
        {
            get { return FindFirstNonNullSprite(); }
        }

        /// <summary>
        /// 配列長（null 配列は 0）。有効枚数ではありません。
        /// </summary>
        public int FrameCount
        {
            get
            {
                if (sprites == null)
                {
                    return 0;
                }

                return sprites.Length;
            }
        }

        /// <summary>
        /// elapsed CombatFrame から表示 Sprite を選びます。
        /// 配列空・全 null・framesPerSprite 不正でも例外を出しません。
        /// </summary>
        public Sprite ResolveSprite(int elapsedFrames)
        {
            if (sprites == null || sprites.Length == 0)
            {
                return null;
            }

            int framesPer = framesPerSprite;
            if (framesPer < 1)
            {
                framesPer = 1;
            }

            int safeElapsed = elapsedFrames;
            if (safeElapsed < 0)
            {
                safeElapsed = 0;
            }

            int frameIndex = safeElapsed / framesPer;
            int lastIndex = sprites.Length - 1;

            if (loop)
            {
                frameIndex = frameIndex % sprites.Length;
                if (frameIndex < 0)
                {
                    frameIndex = 0;
                }
            }
            else
            {
                // non-loop: 最後を上限に Clamp（holdLastFrame でも同じ）
                if (frameIndex > lastIndex)
                {
                    frameIndex = lastIndex;
                }

                if (frameIndex < 0)
                {
                    frameIndex = 0;
                }

                // holdLastFrame=false でも配列外は出さない（要件どおり最後を使う）
                if (holdLastFrame == false && frameIndex > lastIndex)
                {
                    frameIndex = lastIndex;
                }
            }

            Sprite selected = sprites[frameIndex];
            if (selected != null)
            {
                return selected;
            }

            // 選んだ index が null なら、近傍の非 null を探す（毎 Frame 大量探索はしない・要素数少前提）
            return FindFirstNonNullSprite();
        }

        private Sprite FindFirstNonNullSprite()
        {
            if (sprites == null)
            {
                return null;
            }

            for (int i = 0; i < sprites.Length; i++)
            {
                Sprite sprite = sprites[i];
                if (sprite != null)
                {
                    return sprite;
                }
            }

            return null;
        }
    }
}
