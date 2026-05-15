using UnityEngine;

public class BattleManager : MonoBehaviour
{
    [Header("Battle State")]
    [SerializeField] private BattleState currentState = BattleState.None;

    [Header("Players")]
    [SerializeField] private PlayerController player;

    [Header("Enemies")]
    [SerializeField] private EnemyController[] enemies;

    [Header("Target")]
    [SerializeField] private int selectedEnemyIndex = -1;

    [Header("UI")]
    [SerializeField] private BattleUIManager battleUIManager;

    [Header("Turn Count")]
    [SerializeField] private int turnCount = 0;

    private void Start()
    {
        StartBattle();
    }

    private void StartBattle()
    {
        currentState = BattleState.BattleStart;
        turnCount = 1;

        Debug.Log("Battle Start");
        Debug.Log($"Player HP: {player.CurrentHp}/{player.MaxHp}");
        Debug.Log($"Player MP: {player.CurrentMp}/{player.MaxMp}");

        if (battleUIManager != null)
        {
            battleUIManager.UpdateAllStatus(player, enemies);
        }

        StartPlayerTurn();
    }

    private void StartPlayerTurn()
    {
        currentState = BattleState.PlayerTurn;

        Debug.Log("Player Turn");
    }

    public void OnPlayerAttackButton()
    {
        if (currentState != BattleState.PlayerTurn)
        {
            return;
        }

        if (selectedEnemyIndex == -1)
        {
            Debug.Log("No enemy selected.");
            return;
        }

        if (enemies[selectedEnemyIndex] == null)
        {
            Debug.Log("Selected enemy is null.");
            return;
        }

        if (enemies[selectedEnemyIndex].IsDead())
        {
            Debug.Log("Selected enemy is already dead.");
            return;
        }

        Debug.Log($"Player selected Attack to {enemies[selectedEnemyIndex].EnemyName}");

        StartPlayerCommandInput();
    }

    public void SelectEnemyTarget(int enemyIndex)
    {
        if (currentState != BattleState.PlayerTurn)
        {
            return;
        }

        if (enemyIndex < 0 || enemyIndex >= enemies.Length)
        {
            Debug.Log("Invalid enemy index.");
            return;
        }

        if (enemies[enemyIndex] == null)
        {
            Debug.Log("Enemy is null.");
            return;
        }

        if (enemies[enemyIndex].IsDead())
        {
            Debug.Log($"{enemies[enemyIndex].EnemyName} is already dead.");
            return;
        }

        selectedEnemyIndex = enemyIndex;

        Debug.Log($"Selected target: {enemies[enemyIndex].EnemyName}");
    }

    private void StartPlayerCommandInput()
    {
        currentState = BattleState.PlayerCommandInput;

        Debug.Log("Player Command Input Start");

        // ç°ÇÕâºÇ≈ÅAÇ∑ÇÆçUåÇê¨å˜àµÇ¢Ç…Ç∑ÇÈ
        ExecutePlayerAction();
    }

    private void ExecutePlayerAction()
    {
        currentState = BattleState.PlayerAction;

        Debug.Log("Player Attack!");

        EnemyController targetEnemy = enemies[selectedEnemyIndex];

        if (targetEnemy != null && !targetEnemy.IsDead())
        {
            targetEnemy.TakeDamage(10);
        }

        if (battleUIManager != null)
        {
            battleUIManager.UpdateAllStatus(player, enemies);
        }

        if (AreAllEnemiesDead())
        {
            currentState = BattleState.Win;
            Debug.Log("Player Win");
            return;
        }

        selectedEnemyIndex = -1;

        StartEnemyTurn();
    }

    private void StartEnemyTurn()
    {
        currentState = BattleState.EnemyTurn;

        Debug.Log("Enemy Turn");

        StartEnemyCommandInput();
    }

    private void StartEnemyCommandInput()
    {
        currentState = BattleState.EnemyCommandInput;

        Debug.Log("Enemy Command Input Start");

        // ç°ÇÕâºÇ≈ÅAÇ∑ÇÆìGçsìÆÇ…êiÇﬁ
        ExecuteEnemyAction();
    }

    private void ExecuteEnemyAction()
    {
        currentState = BattleState.EnemyAction;

        Debug.Log("Enemy Attack!");

        player.TakeDamage(10);

        if (battleUIManager != null)
        {
            battleUIManager.UpdateAllStatus(player, enemies);
        }

        if (player.IsDead())
        {
            currentState = BattleState.Lose;
            Debug.Log("Player Lose");
            return;
        }

        turnCount++;
        StartPlayerTurn();
    }

    private bool AreAllEnemiesDead()
    {
        foreach (EnemyController enemy in enemies)
        {
            if (enemy != null && !enemy.IsDead())
            {
                return false;
            }
        }

        return true;
    }
}
