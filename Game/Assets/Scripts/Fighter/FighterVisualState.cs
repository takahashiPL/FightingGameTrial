namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// 戦闘状態から見た目側へ渡す Visual State です。
    ///
    /// 実装済み:
    /// - Idle / WalkForward / WalkBackward / Attack
    /// - JumpRise / JumpFall / Landing（Jump 専用 Sprite は未用意・Idle 流用）
    ///
    /// JumpType（Neutral/Forward/Backward）自体はここへ分けない。
    /// HitStun / KO は色と強制 Idle 表示で扱い、この enum には含めない。
    /// </summary>
    public enum FighterVisualState
    {
        Idle = 0,
        WalkForward = 1,
        WalkBackward = 2,
        Attack = 3,
        JumpRise = 4,
        JumpFall = 5,
        Landing = 6
    }
}
