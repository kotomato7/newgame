using UnityEngine;

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