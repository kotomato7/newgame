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

    [Header("Attack Status")]
    [SerializeField] private int normalAttackPower = 10;
    [SerializeField] private int strongAttackPower = 20;
    [SerializeField] private int strongAttackMpCost = 5;

    public string EnemyName => enemyName;

    public int MaxHp => maxHp;
    public int MaxMp => maxMp;
    public int CurrentHp => currentHp;
    public int CurrentMp => currentMp;

    public int NormalAttackPower => normalAttackPower;
    public int StrongAttackPower => strongAttackPower;

    private void Awake()
    {
        InitializeStatus();
    }

    public void InitializeStatus()
    {
        currentHp = maxHp;
        currentMp = maxMp;
    }

    public int NormalAttack()
    {
        Debug.Log($"{enemyName} used Normal Attack. Damage: {normalAttackPower}");
        return normalAttackPower;
    }

    public int StrongAttack()
    {
        if (currentMp < strongAttackMpCost)
        {
            Debug.Log($"{enemyName} does not have enough MP. Used Normal Attack instead.");
            return NormalAttack();
        }

        currentMp -= strongAttackMpCost;
        currentMp = Mathf.Clamp(currentMp, 0, maxMp);

        Debug.Log($"{enemyName} used Strong Attack. Damage: {strongAttackPower}, MP: {currentMp}/{maxMp}");

        return strongAttackPower;
    }

    public void TakeDamage(int damage)
    {
        if (damage < 0)
        {
            damage = 0;
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