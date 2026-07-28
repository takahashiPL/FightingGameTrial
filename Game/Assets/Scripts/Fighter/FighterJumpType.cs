namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// ジャンプの種類です（開始時に確定し、着地まで保持）。
    ///
    /// Facing と左右入力の組み合わせで決まります。
    /// JumpType 自体は Visual State には分けず、見た目は JumpStart / JumpRise / JumpApex / JumpFall / Landing を使います。
    /// </summary>
    public enum FighterJumpType
    {
        None = 0,
        Neutral = 1,
        Forward = 2,
        Backward = 3
    }
}
