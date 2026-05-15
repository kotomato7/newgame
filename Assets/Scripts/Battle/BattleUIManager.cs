using TMPro;
using UnityEngine;

public class BattleUIManager : MonoBehaviour
{
    [Header("Player Status Text")]
    [SerializeField] private TextMeshProUGUI playerHpText;
    [SerializeField] private TextMeshProUGUI playerMpText;

    [Header("Enemy HP Text")]
    [SerializeField] private TextMeshProUGUI[] enemyHpTexts;

    public void UpdatePlayerStatus(PlayerController player)
    {
        if (player == null)
        {
            return;
        }

        if (playerHpText != null)
        {
            playerHpText.text = $"HP: {player.CurrentHp} / {player.MaxHp}";
        }

        if (playerMpText != null)
        {
            playerMpText.text = $"MP: {player.CurrentMp} / {player.MaxMp}";
        }
    }

    public void UpdateEnemyStatus(EnemyController[] enemies)
    {
        if (enemies == null || enemyHpTexts == null)
        {
            return;
        }

        int count = Mathf.Min(enemies.Length, enemyHpTexts.Length);

        for (int i = 0; i < count; i++)
        {
            if (enemyHpTexts[i] == null)
            {
                continue;
            }

            if (enemies[i] == null)
            {
                enemyHpTexts[i].text = $"Enemy {i + 1}: None";
                continue;
            }

            enemyHpTexts[i].text =
                $" HP: {enemies[i].CurrentHp} / {enemies[i].MaxHp}";
        }
    }

    public void UpdateAllStatus(PlayerController player, EnemyController[] enemies)
    {
        UpdatePlayerStatus(player);
        UpdateEnemyStatus(enemies);
    }
}
