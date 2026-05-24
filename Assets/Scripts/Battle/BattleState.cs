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
    PlayerAction,

    EnemyTurn,
    EnemyCommandInput,
    EnemyAction,

    Win,
    Lose
}