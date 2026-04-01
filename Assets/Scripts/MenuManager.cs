using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public void StartGame() //StartGame()ŠÖ”‚ğì¬
    {
        SceneManager.LoadScene("GameScene");    //Œã‚Ù‚Ç‘‚«Š·‚¦‰Â”\
    }

    public void OpenOptions()
    { 
        Debug.Log("Options button clicked");    //Œã‚Ù‚Ç‘‚«Š·‚¦‰Â”\
    }

    public void QuitGame()
    {
        Debug.Log("Quit button clicked");      //Œã‚Ù‚Ç‘‚«Š·‚¦‰Â”\

        Application.Quit();
    }
}