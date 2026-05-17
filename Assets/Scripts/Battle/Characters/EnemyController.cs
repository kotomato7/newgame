using System.Collections.Generic;
using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("Enemy Info")]
    [SerializeField] private string enemyName = "Enemy";

    [Header("Max Status")]
    [SerializeField] private int maxHp = 50;
    [SerializeField] private int maxMp = 20;

    [Header("Current Status")]
    [SerializeField] private int currentHp;
    [SerializeField] private int currentMp;

    [Header("Guard")]
    [SerializeField] private bool isGuarding = false;
    [SerializeField] private float guardDamageRate = 0.5f;

    [Header("Enemy Actions")]
    [SerializeField] private List<EnemyActionData> actions = new List<EnemyActionData>();

    public string EnemyName => enemyName;

    public int MaxHp => maxHp;
    public int MaxMp => maxMp;
    public int CurrentHp => currentHp;
    public int CurrentMp => currentMp;

    public bool IsGuarding => isGuarding;
    public float GuardDamageRate => guardDamageRate;

    private void Awake()
    {
        InitializeStatus();
        InitializeDefaultActionsIfEmpty();
    }

    public void InitializeStatus()
    {
        currentHp = maxHp;
        currentMp = maxMp;
        isGuarding = false;
    }

    private void InitializeDefaultActionsIfEmpty()
    {
        if (actions.Count > 0)
        {
            return;
        }

        actions.Add(new EnemyActionData
        {
            actionName = "Attack",
            actionType = EnemyActionType.Attack,
            power = 10,
            mpCost = 0,
            weight = 60,
            cooldownTurn = 0
        });

        actions.Add(new EnemyActionData
        {
            actionName = "Strong Attack",
            actionType = EnemyActionType.StrongAttack,
            power = 20,
            mpCost = 5,
            weight = 25,
            cooldownTurn = 0
        });

        actions.Add(new EnemyActionData
        {
            actionName = "Guard",
            actionType = EnemyActionType.Guard,
            power = 0,
            mpCost = 0,
            weight = 15,
            cooldownTurn = 0
        });
    }

    public EnemyActionData SelectAction()
    {
        List<EnemyActionData> usableActions = new List<EnemyActionData>();

        foreach (EnemyActionData action in actions)
        {
            if (action == null)
            {
                continue;
            }

            if (currentMp < action.mpCost)
            {
                continue;
            }

            if (action.weight <= 0)
            {
                continue;
            }

            usableActions.Add(action);
        }

        if (usableActions.Count == 0)
        {
            return new EnemyActionData
            {
                actionName = "Attack",
                actionType = EnemyActionType.Attack,
                power = 10,
                mpCost = 0,
                weight = 10
            };
        }

        return SelectActionByWeight(usableActions);
    }

    private EnemyActionData SelectActionByWeight(List<EnemyActionData> usableActions)
    {
        int totalWeight = 0;

        foreach (EnemyActionData action in usableActions)
        {
            totalWeight += action.weight;
        }

        int randomValue = Random.Range(0, totalWeight);
        int currentWeight = 0;

        foreach (EnemyActionData action in usableActions)
        {
            currentWeight += action.weight;

            if (randomValue < currentWeight)
            {
                return action;
            }
        }

        return usableActions[0];
    }

    public int ExecuteAction(EnemyActionData action)
    {
        isGuarding = false;

        if (action == null)
        {
            return 0;
        }

        if (action.mpCost > 0)
        {
            UseMp(action.mpCost);
        }

        switch (action.actionType)
        {
            case EnemyActionType.Attack:
                Debug.Log($"{enemyName} used {action.actionName}. Damage: {action.power}");
                return action.power;

            case EnemyActionType.StrongAttack:
                Debug.Log($"{enemyName} used {action.actionName}. Damage: {action.power}");
                return action.power;

            case EnemyActionType.Guard:
                isGuarding = true;
                Debug.Log($"{enemyName} used {action.actionName}. Guarding!");
                return 0;

            default:
                Debug.Log($"{enemyName} used unknown action.");
                return 0;
        }
    }

    public void TakeDamage(int damage)
    {
        if (damage < 0)
        {
            damage = 0;
        }

        if (isGuarding)
        {
            damage = Mathf.RoundToInt(damage * guardDamageRate);
            Debug.Log($"{enemyName} guarded. Damage reduced.");
        }

        currentHp -= damage;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);

        Debug.Log($"{enemyName} took {damage} damage. HP: {currentHp}/{maxHp}");
    }

    public void HealHp(int amount)
    {
        if (amount < 0)
        {
            amount = 0;
        }

        currentHp += amount;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);

        Debug.Log($"{enemyName} healed {amount} HP. HP: {currentHp}/{maxHp}");
    }

    public bool UseMp(int amount)
    {
        if (amount < 0)
        {
            amount = 0;
        }

        if (currentMp < amount)
        {
            Debug.Log($"{enemyName} does not have enough MP.");
            return false;
        }

        currentMp -= amount;
        currentMp = Mathf.Clamp(currentMp, 0, maxMp);

        Debug.Log($"{enemyName} used {amount} MP. MP: {currentMp}/{maxMp}");
        return true;
    }

    public void RecoverMp(int amount)
    {
        if (amount < 0)
        {
            amount = 0;
        }

        currentMp += amount;
        currentMp = Mathf.Clamp(currentMp, 0, maxMp);

        Debug.Log($"{enemyName} recovered {amount} MP. MP: {currentMp}/{maxMp}");
    }

    public bool IsDead()
    {
        return currentHp <= 0;
    }
}