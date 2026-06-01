using System.Collections;
using UnityEngine;

public class BattleManager : MonoBehaviour
{
    private static readonly WaitForSeconds WaitHalf = new(0.5f);
    private static readonly WaitForSeconds WaitThird = new(0.3f);

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

    [Header("QTE")]
    [SerializeField] private QTEManager qteManager;

    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;   // 丸いオブジェクトのプレハブ
    [SerializeField] private float projectileDuration = 0.4f;

    [Header("Attack Settings")]
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private int specialDamage = 25;
    [SerializeField] private float qteSuccessMultiplier = 1.5f;

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

        if (battleUIManager != null)
            battleUIManager.UpdateAllStatus(player, enemies);

        StartPlayerTurn();
    }

    private void StartPlayerTurn()
    {
        currentState = BattleState.PlayerTurn;
        selectedEnemyIndex = -1;

        Debug.Log($"Turn {turnCount}: Player Turn");

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

        if (enemyIndex < 0 || enemyIndex >= enemies.Length) return;
        if (enemies[enemyIndex] == null || enemies[enemyIndex].IsDead()) return;

        selectedEnemyIndex = enemyIndex;
        Debug.Log($"Target selected: {enemies[enemyIndex].EnemyName}");

        if (battleUIManager != null)
            battleUIManager.HideAllWindows();

        StartCoroutine(BattleTurnCoroutine());
    }

    // ターン全体の流れ（プレイヤー攻撃 → 敵攻撃）
    private IEnumerator BattleTurnCoroutine()
    {
        // === Phase 1: QTE ===
        currentState = BattleState.QTEInput;
        bool qteSuccess = false;

        if (qteManager != null)
            yield return StartCoroutine(qteManager.RunQTE(result => qteSuccess = result));

        // === Phase 2: プレイヤー攻撃 ===
        currentState = BattleState.PlayerAction;
        EnemyController target = enemies[selectedEnemyIndex];

        int baseDamage = selectedCommand == PlayerCommand.Special ? specialDamage : attackDamage;
        int finalDamage = qteSuccess
            ? Mathf.RoundToInt(baseDamage * qteSuccessMultiplier)
            : baseDamage;

        Debug.Log($"Player → {target.EnemyName} : {finalDamage} damage (QTE: {(qteSuccess ? "SUCCESS" : "MISS")})");

        // 弾をプレイヤー → 敵へ飛ばす
        yield return StartCoroutine(LaunchProjectile(player.transform.position, target.transform.position));

        target.TakeDamage(finalDamage);

        if (battleUIManager != null)
            battleUIManager.UpdateAllStatus(player, enemies);

        yield return WaitHalf;

        selectedCommand = PlayerCommand.None;
        selectedEnemyIndex = -1;

        // === Phase 3: 勝利判定 ===
        if (AreAllEnemiesDead())
        {
            currentState = BattleState.Win;
            Debug.Log("Player Win!");
            yield break;
        }

        // === Phase 4: 敵ターン ===
        currentState = BattleState.EnemyTurn;
        yield return StartCoroutine(EnemyTurnCoroutine());

        if (currentState == BattleState.Lose)
            yield break;

        // === Phase 5: 次のプレイヤーターン ===
        turnCount++;
        StartPlayerTurn();
    }

    // 敵全員が順番に行動する
    private IEnumerator EnemyTurnCoroutine()
    {
        foreach (EnemyController enemy in enemies)
        {
            if (enemy == null || enemy.IsDead()) continue;

            EnemyActionData action = enemy.SelectAction();
            int damage = enemy.ExecuteAction(action);

            if (damage > 0)
            {
                // 弾を敵 → プレイヤーへ飛ばす
                yield return StartCoroutine(LaunchProjectile(enemy.transform.position, player.transform.position));

                player.TakeDamage(damage);

                if (battleUIManager != null)
                    battleUIManager.UpdateAllStatus(player, enemies);

                yield return WaitThird;

                if (player.IsDead())
                {
                    currentState = BattleState.Lose;
                    Debug.Log("Player Lose!");
                    yield break;
                }
            }
            else
            {
                // ガードなどダメージなし行動
                yield return WaitHalf;

                if (battleUIManager != null)
                    battleUIManager.UpdateAllStatus(player, enemies);
            }
        }
    }

    // 弾を発射して到達を待つ
    private IEnumerator LaunchProjectile(Vector3 from, Vector3 to)
    {
        bool arrived = false;
        AttackProjectile.Spawn(projectilePrefab, from, to, projectileDuration, () => arrived = true);
        yield return new WaitUntil(() => arrived);
    }

    private bool AreAllEnemiesDead()
    {
        foreach (EnemyController enemy in enemies)
        {
            if (enemy != null && !enemy.IsDead())
                return false;
        }
        return true;
    }
}
