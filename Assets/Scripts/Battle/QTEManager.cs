using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum QTEResult { Miss, KeySuccess, BothSuccess }

public class QTEManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject qtePanel;
    [SerializeField] private TextMeshProUGUI keyPromptText;
    [SerializeField] private TextMeshProUGUI mousePromptText;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private Image timerBar;   // Image Type = Filled に設定すること
    [SerializeField] private RectTransform mouseCursorImage;

    [Header("Settings")]
    [SerializeField] private float timeWindow = 1.5f;
    [SerializeField] private float mouseSwipeThreshold = 150f;

    private static readonly Key[] qteKeys = { Key.Space, Key.J, Key.K, Key.F, Key.A };

    public IEnumerator RunQTE(Action<QTEResult> onComplete)
    {
        Key targetKey = qteKeys[UnityEngine.Random.Range(0, qteKeys.Length)];
        bool swipeRight = UnityEngine.Random.value > 0.5f;

        bool keySuccess = false;
        float mouseHorizontalDelta = 0f;
        float elapsed = 0f;

        qtePanel.SetActive(true);
        resultText.gameObject.SetActive(false);
        keyPromptText.text = $"Push [ {targetKey} ] !";
        if (mousePromptText != null)
            mousePromptText.text = swipeRight ? "Swipe →" : "Swipe ←";

        if (mouseCursorImage != null)
        {
            mouseCursorImage.gameObject.SetActive(true);
            Cursor.visible = false;
        }

        while (elapsed < timeWindow)
        {
            elapsed += Time.deltaTime;

            if (timerBar != null)
                timerBar.fillAmount = 1f - (elapsed / timeWindow);

            if (Keyboard.current[targetKey].wasPressedThisFrame)
                keySuccess = true;

            if (Mouse.current != null)
            {
                Vector2 pos = Mouse.current.position.ReadValue();
                mouseHorizontalDelta += Mouse.current.delta.ReadValue().x;
                if (mouseCursorImage != null)
                    mouseCursorImage.position = pos;
            }

            yield return null;
        }

        bool mouseSuccess = swipeRight
            ? mouseHorizontalDelta > mouseSwipeThreshold
            : mouseHorizontalDelta < -mouseSwipeThreshold;

        QTEResult result;
        if (keySuccess && mouseSuccess)
            result = QTEResult.BothSuccess;
        else if (keySuccess)
            result = QTEResult.KeySuccess;
        else
            result = QTEResult.Miss;

        if (timerBar != null)
            timerBar.fillAmount = 0f;

        if (mouseCursorImage != null)
        {
            mouseCursorImage.gameObject.SetActive(false);
            Cursor.visible = true;
        }

        keyPromptText.gameObject.SetActive(false);
        if (mousePromptText != null)
            mousePromptText.gameObject.SetActive(false);
        resultText.gameObject.SetActive(true);
        resultText.text = result switch
        {
            QTEResult.BothSuccess => "PERFECT!\ngreat damage UP!",
            QTEResult.KeySuccess  => "SUCCESS!\ndamage UP!",
            _                     => "MISS..."
        };

        yield return new WaitForSeconds(0.8f);

        qtePanel.SetActive(false);
        keyPromptText.gameObject.SetActive(true);
        if (mousePromptText != null)
            mousePromptText.gameObject.SetActive(true);

        onComplete?.Invoke(result);
    }
}
