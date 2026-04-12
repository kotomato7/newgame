using UnityEngine;

public class BombController : MonoBehaviour
{
    [Header("移動速度")]
    public float moveSpeed = 5f;

    [Header("サイズ設定")]
    public float initialScale = 0.3f;
    public float maxScale = 2f;
    public float maxDistance = 10f;

    [Header("シールド判定")]
    public GameObject shieldObject;
    public float shieldCheckRadius = 1f;
    public float shieldZ = -7f;

    private Transform target;
    private bool checkedAtShieldZ = false;

    void Start()
    {
        target = Camera.main.transform;
        transform.localScale = Vector3.one * initialScale;
    }

    void Update()
    {
        if (target == null) return;

        Vector3 direction = (target.position - transform.position).normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;

        float distance = Vector3.Distance(transform.position, target.position);
        float t = 1f - Mathf.Clamp01(distance / maxDistance);
        float scale = Mathf.Lerp(initialScale, maxScale, t);
        transform.localScale = Vector3.one * scale;

        if (!checkedAtShieldZ && transform.position.z <= shieldZ)
        {
            checkedAtShieldZ = true;
            CheckShieldBlock();
        }
    }

    void CheckShieldBlock()
    {
        if (shieldObject == null || !shieldObject.activeSelf) return;

        float distXY = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.y),
            new Vector2(shieldObject.transform.position.x, shieldObject.transform.position.y)
        );

        if (distXY <= shieldCheckRadius)
        {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("MainCamera"))
        {
            ScreenFlash flash = FindObjectOfType<ScreenFlash>();
            if (flash != null)
            {
                flash.Flash();
            }
            Debug.Log("ボムが当たった");
            Destroy(gameObject);
        }
    }
}
