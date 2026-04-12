using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenFlash : MonoBehaviour
{
    [Header("フラッシュ設定")]
    public Image flashImage;
    public float flashDuration = 0.3f;

    public void Flash()
    {
        StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        flashImage.color = new Color(1f, 0f, 0f, 0.6f);

        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0.6f, 0f, elapsed / flashDuration);
            flashImage.color = new Color(1f, 0f, 0f, alpha);
            yield return null;
        }

        flashImage.color = new Color(1f, 0f, 0f, 0f);
    }
}
