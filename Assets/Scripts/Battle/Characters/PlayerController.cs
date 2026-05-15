using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Max Status")]
    [SerializeField] private int maxHp = 100;
    [SerializeField] private int maxMp = 50;

    [Header("Current Status")]
    [SerializeField] private int currentHp;
    [SerializeField] private int currentMp;

    public int MaxHp => maxHp;
    public int MaxMp => maxMp;
    public int CurrentHp => currentHp;
    public int CurrentMp => currentMp;

    private void Awake()
    {
        InitializeStatus();
    }

    public void InitializeStatus()
    {
        currentHp = maxHp;
        currentMp = maxMp;
    }

    public void TakeDamage(int damage)
    {
        if (damage < 0)
        {
            damage = 0;
        }

        currentHp -= damage;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);

        Debug.Log($"Player took {damage} damage. HP: {currentHp}/{maxHp}");
    }

    public void HealHp(int amount)
    {
        if (amount < 0)
        {
            amount = 0;
        }

        currentHp += amount;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);

        Debug.Log($"Player healed {amount} HP. HP: {currentHp}/{maxHp}");
    }

    public bool UseMp(int amount)
    {
        if (amount < 0)
        {
            amount = 0;
        }

        if (currentMp < amount)
        {
            Debug.Log("Not enough MP.");
            return false;
        }

        currentMp -= amount;
        currentMp = Mathf.Clamp(currentMp, 0, maxMp);

        Debug.Log($"Player used {amount} MP. MP: {currentMp}/{maxMp}");
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

        Debug.Log($"Player recovered {amount} MP. MP: {currentMp}/{maxMp}");
    }

    public bool IsDead()
    {
        return currentHp <= 0;
    }
}
