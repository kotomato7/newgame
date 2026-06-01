using System;
using System.Collections;
using UnityEngine;

// アタッカーからターゲットへ飛んでいく丸いオブジェクト
// プレハブにこのスクリプトをアタッチして使う
public class AttackProjectile : MonoBehaviour
{
    // prefabがnullの場合は即座にonArrivedを呼ぶ（演出なし扱い）
    public static void Spawn(GameObject prefab, Vector3 from, Vector3 to, float duration, Action onArrived)
    {
        if (prefab == null)
        {
            onArrived?.Invoke();
            return;
        }

        GameObject obj = Instantiate(prefab, from, Quaternion.identity);
        var proj = obj.GetComponent<AttackProjectile>();
        if (proj != null)
            proj.StartCoroutine(proj.Move(from, to, duration, onArrived));
        else
            onArrived?.Invoke();
    }

    private IEnumerator Move(Vector3 from, Vector3 to, float duration, Action onArrived)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        transform.position = to;
        onArrived?.Invoke();
        Destroy(gameObject);
    }
}
