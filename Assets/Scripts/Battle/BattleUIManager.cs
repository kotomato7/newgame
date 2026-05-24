using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleUIManager : MonoBehaviour
{
    [Header("Player Status Text")]
    [SerializeField] private TextMeshProUGUI playerHpText;
    [SerializeField] private TextMeshProUGUI playerMpText;

    [Header("Enemy HP Text")]
    [SerializeField] private TextMeshProUGUI[] enemyHpTexts;

    [Header("Command Window")]
    [SerializeField] private GameObject commandWindow;      // �R�}���h�I���E�B���h�E�S��

    [Header("Target Window")]
    [SerializeField] private GameObject targetWindow;       // �^�[�Q�b�g�I���E�B���h�E�S��
    [SerializeField] private Button[] targetButtons;        // �e�G�̃^�[�Q�b�g�{�^��
    [SerializeField] private TextMeshProUGUI[] targetButtonTexts; // �{�^���̃��x��

    
    // �R�}���h�E�B���h�E��\���i�v���C���[�^�[���J�n���j
    public void ShowCommandWindow()
    {
        if (commandWindow != null) commandWindow.SetActive(true);
        if (targetWindow != null) targetWindow.SetActive(false);
    }


    // �^�[�Q�b�g�I���E�B���h�E��\��
    public void ShowTargetWindow(EnemyController[] enemies)
    {
        if (commandWindow != null) commandWindow.SetActive(false);
        if (targetWindow != null) targetWindow.SetActive(true);

        // �������Ă���G�����{�^����L����
        for (int i = 0; i < targetButtons.Length; i++)
        {
            if (targetButtons[i] == null) continue;

            bool isAlive = enemies[i] != null && !enemies[i].IsDead();
            targetButtons[i].interactable = isAlive;

            if (targetButtonTexts[i] != null)
            {
                targetButtonTexts[i].text = isAlive
                    ? enemies[i].EnemyName
                    : "---";
            }
        }
    }


    // �S�E�B���h�E���\���i�G�^�[�����Ȃǁj
    // ターゲットウィンドウ → コマンドウィンドウへ戻る
    public void ShowBackToCommand()
    {
        if (commandWindow != null) commandWindow.SetActive(true);
        if (targetWindow != null) targetWindow.SetActive(false);
    }

    public void HideAllWindows()
    {
        if (commandWindow != null) commandWindow.SetActive(false);
        if (targetWindow != null) targetWindow.SetActive(false);
    }

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
