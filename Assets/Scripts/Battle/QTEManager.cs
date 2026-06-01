using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class QTEManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject qtePanel;
    [SerializeField] private TextMeshProUGUI keyPromptText;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Image timerBar;   // Image Type = Filled に設定すること

    [Header("Settings")]
    [SerializeField] private float timeWindow = 1.5f;

    private static readonly Key[] qteKeys = { Key.Space, Key.J, Key.K, Key.F, Key.A };

    public IEnumerator RunQTE(Action<bool> onComplete)
    {
        Key targetKey = qteKeys[UnityEngine.Random.Range(0, qteKeys.Length)];
        bool success = false;
        float elapsed = 0f;

        qtePanel.SetActive(true);
        resultText.gameObject.SetActive(false);
        keyPromptText.text = $"Push [ {targetKey} ] !";

        while (elapsed < timeWindow)
        {
            elapsed += Time.deltaTime;

            if (timerBar != null)
                timerBar.fillAmount = 1f - (elapsed / timeWindow);

            if (Keyboard.current[targetKey].wasPressedThisFrame)
            {
                success = true;
                break;
            }
            yield return null;
        }

        if (timerBar != null)
            timerBar.fillAmount = 0f;

        keyPromptText.gameObject.SetActive(false);
        resultText.gameObject.SetActive(true);
        resultText.text = success ? "SUCCESS!\ndamages UP!" : "MISS...";

        yield return new WaitForSeconds(0.8f);

        qtePanel.SetActive(false);
        keyPromptText.gameObject.SetActive(true);

        onComplete?.Invoke(success);
    }
}
