namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// 戦闘状態から見た目側へ渡す Visual State です（最小実装）。
    ///
    /// 現時点の用途:
    /// - Idle / WalkForward / WalkBackward / Attack を区別する
    /// - Walk 専用 Sprite / Animator はまだ無い（Walk も Idle Sprite を流用）
    ///
    /// HitStun / KO は色と強制 Idle 表示で扱い、この enum には含めない。
    /// </summary>
    public enum FighterVisualState
    {
        Idle = 0,
        WalkForward = 1,
        WalkBackward = 2,
        Attack = 3
    }
}
