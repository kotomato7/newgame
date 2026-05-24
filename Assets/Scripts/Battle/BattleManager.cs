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
    private PlayerCommand selectedCommand = PlayerCommand.None;

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
        selectedEnemyIndex = -1;   //�^�[�Q�b�g�����Z�b�g

        Debug.Log("Player Turn");

        // �R�}���h�E�B���h�E��\������
        if (battleUIManager != null)
            battleUIManager.ShowCommandWindow();
    }

    // コマンドウィンドウ：攻撃ボタン
    public void OnCommandAttack()
    {
        if (currentState != BattleState.PlayerTurn) return;

        selectedCommand = PlayerCommand.Attack;
        Debug.Log("Command: Attack selected");

        if (battleUIManager != null)
            battleUIManager.ShowTargetWindow(enemies);
    }

    // コマンドウィンドウ：必殺技ボタン
    public void OnCommandSpecial()
    {
        if (currentState != BattleState.PlayerTurn) return;

        selectedCommand = PlayerCommand.Special;
        Debug.Log("Command: Special selected");

        if (battleUIManager != null)
            battleUIManager.ShowTargetWindow(enemies);
    }

    // ターゲットウィンドウ：戻るボタン
    public void OnTargetBack()
    {
        if (currentState != BattleState.PlayerTurn) return;

        selectedCommand = PlayerCommand.None;
        if (battleUIManager != null)
            battleUIManager.ShowCommandWindow();
    }

    // ターゲットウィンドウ：敵を選択
    public void SelectEnemyTarget(int enemyIndex)
    {
        if (currentState != BattleState.PlayerTurn) return;

        if (enemyIndex < 0 || enemyIndex >= enemies.Length)
        {
            Debug.Log("Invalid enemy index.");
            return;
        }

        if (enemies[enemyIndex] == null || enemies[enemyIndex].IsDead())
        {
            Debug.Log($"Enemy {enemyIndex} is not a valid target.");
            return;
        }

        selectedEnemyIndex = enemyIndex;
        Debug.Log($"Target selected: {enemies[enemyIndex].EnemyName}");

        StartPlayerCommandInput();
    }

    private void StartPlayerCommandInput()
    {
        currentState = BattleState.PlayerCommandInput;

        Debug.Log("Player Command Input Start");

        // ���͉��ŁA�����U�����������ɂ���
        ExecutePlayerAction();
    }

    private void ExecutePlayerAction()
    {
        currentState = BattleState.PlayerAction;

        EnemyController targetEnemy = enemies[selectedEnemyIndex];

        if (targetEnemy != null && !targetEnemy.IsDead())
        {
            switch (selectedCommand)
            {
                case PlayerCommand.Attack:
                    Debug.Log($"Player Attack → {targetEnemy.EnemyName}");
                    targetEnemy.TakeDamage(10);
                    break;
                case PlayerCommand.Special:
                    Debug.Log($"Player Special → {targetEnemy.EnemyName}");
                    targetEnemy.TakeDamage(25);
                    break;
            }
        }

        selectedCommand = PlayerCommand.None;

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

        // ���͉��ŁA�����G�s���ɐi��
        ExecuteEnemyAction();
    }

    private void ExecuteEnemyAction()
    {
        currentState = BattleState.EnemyAction;

        Debug.Log("Enemy Action!");

        foreach (EnemyController enemy in enemies)
        {
            if (enemy == null || enemy.IsDead())
            {
                continue;
            }

            EnemyActionData action = enemy.SelectAction();
            int damage = enemy.ExecuteAction(action);

            if (damage > 0)
            {
                player.TakeDamage(damage);
            }

            if (player.IsDead())
            {
                currentState = BattleState.Lose;
                Debug.Log("Player Lose");
                return;
            }
        }

        if (battleUIManager != null)
        {
            battleUIManager.UpdateAllStatus(player, enemies);
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
