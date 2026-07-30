namespace FightingGameTrial.Combat
{
    /// <summary>
    /// 攻撃の ActionFrame 区間です（Startup / Active / Recovery）。
    /// Hit 候補に残し、将来の技相性比較で使います。
    /// </summary>
    public enum DebugAttackPhase
    {
        None = 0,
        Startup = 1,
        Active = 2,
        Recovery = 3
    }
}
