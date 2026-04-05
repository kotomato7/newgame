using UnityEngine;
using UnityEngine.InputSystem;

public class FireballController : MonoBehaviour
{
    [Header("移動方向")]
    public Vector3 moveDirection = new Vector3(0f, -1f, 0f);

    [Header("停止位置")]
    public Vector3 stopPosition = new Vector3(0f, -3f, 0f);

    [Header("移動速度")]
    public float moveSpeed = 3f;

    [Header("拡大速度")]
    public float scaleSpeed = 0.5f;

    [Header("入力キー順序")]
    public Key Key_1 = Key.Q;
    public Key Key_2 = Key.W;
    public Key Key_3 = Key.E;

    private bool hasStopped = false;
    private int inputStep = 0;

    void Update()
    {
        if (hasStopped)
        {
            return;
        }

        CheckSequentialInput();

        if (inputStep >= 3)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 move = moveDirection.normalized * moveSpeed * Time.deltaTime;
        Vector3 nextPosition = transform.position + move;

        Vector3 toStopNow = stopPosition - transform.position;
        Vector3 toStopNext = stopPosition - nextPosition;

        if (Vector3.Dot(toStopNow, toStopNext) <= 0f || Vector3.Distance(transform.position, stopPosition) < 0.01f)
        {
            transform.position = stopPosition;
            hasStopped = true;
            return;
        }

        transform.position = nextPosition;
        transform.localScale += new Vector3(scaleSpeed, scaleSpeed, 0f) * Time.deltaTime;
    }

    private void CheckSequentialInput()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        switch (inputStep)
        {
            case 0:
                if (Keyboard.current[Key_1].wasPressedThisFrame)
                {
                    inputStep = 1;
                    Debug.Log("Key_1 success");
                }
                break;

            case 1:
                if (Keyboard.current[Key_2].wasPressedThisFrame)
                {
                    inputStep = 2;
                    Debug.Log("Key_2 success");
                }
                break;

            case 2:
                if (Keyboard.current[Key_3].wasPressedThisFrame)
                {
                    inputStep = 3;
                    Debug.Log("Key_3 success");
                }
                break;
        }
    }
}