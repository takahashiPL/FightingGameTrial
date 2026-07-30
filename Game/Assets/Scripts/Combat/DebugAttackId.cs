namespace FightingGameTrial.Combat
{
    /// <summary>
    /// 攻撃の種類を一意に識別します。
    ///
    /// 文字列比較を避け、Hit 解決や将来の技相性（Punch対Kick 等）で使うための ID です。
    /// GroundKick と将来の空中 Kick は別 ID にします（見た目素材を流用しても攻撃データは分離）。
    /// </summary>
    public enum DebugAttackId
    {
        None = 0,
        JPunch = 1,
        GroundKick = 2
    }
}
