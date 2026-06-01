public enum PlayerCommand
{
    None,
    Attack,
    Special,
}

public enum BattleState
{
    None,

    BattleStart,

    PlayerTurn,
    PlayerCommandInput,
    QTEInput,
    PlayerAction,

    EnemyTurn,
    EnemyCommandInput,
    EnemyAction,

    Win,
    Lose
}