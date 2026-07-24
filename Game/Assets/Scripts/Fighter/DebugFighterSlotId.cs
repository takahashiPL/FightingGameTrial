namespace FightingGameTrial.Fighter
{
    /// <summary>
    /// デバッグ用の参加枠 ID です（段階10B-1）。
    ///
    /// これは「誰が操作するか」の枠であり、キャラクター種類ではありません。
    /// キャラ固有の Sprite / 速度 / Box は将来の CharacterDefinition 側へ置きます。
    /// </summary>
    public enum DebugFighterSlotId
    {
        P1 = 0,
        P2 = 1
    }
}
