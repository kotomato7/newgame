using UnityEngine;

[System.Serializable]
public class EnemyActionData
{
    [Header("Action Info")]
    public string actionName = "Attack";
    public EnemyActionType actionType = EnemyActionType.Attack;

    [Header("Power")]
    public int power = 10;

    [Header("MP Cost")]
    public int mpCost = 0;

    [Header("Selection")]
    public int weight = 10;

    [Header("Condition")]
    public int cooldownTurn = 0;
}