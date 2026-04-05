using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public void StartGame()
    {
        SceneManager.LoadScene("GameScene");
    }

    public void OpenOptions()
    {
        Debug.Log("Options button clicked");
    }

    public void QuitGame()
    {
        Debug.Log("Quit button clicked");

        Application.Quit();
    }
}