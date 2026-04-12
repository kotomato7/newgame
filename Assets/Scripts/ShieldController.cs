using UnityEngine;
using UnityEngine.InputSystem;

public class ShieldController : MonoBehaviour
{
    [Header("シールドオブジェクト")]
    public GameObject shieldObject;

    void Update()
    {
        Debug.Log("ShieldController Update running");
        MoveShieldToMouse();

        if (Mouse.current.rightButton.isPressed)
        {
            shieldObject.SetActive(true);
        }
        else
        {
            shieldObject.SetActive(false);
        }
    }

    void MoveShieldToMouse()
    {
        if (shieldObject == null) return;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Vector3 mouseScreenPos = new Vector3(
            mouseScreen.x,
            mouseScreen.y,
            -Camera.main.transform.position.z
        );
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(mouseScreenPos);
        worldPos.z = -7f;
        shieldObject.transform.position = worldPos;

        Debug.Log("Shield pos: " + shieldObject.transform.position);
    }
}
