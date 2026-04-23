using UnityEngine;
using UnityEngine.SceneManagement;

public class BackToGamescene : MonoBehaviour

{
    public void ReturnToGameScene()
    {
        SceneManager.LoadScene("GameScene1");
    }
}
