using UnityEngine;
using UnityEngine.SceneManagement;

public class GamesceneManager : MonoBehaviour
{
    public void Buttle()
    {
        SceneManager.LoadScene("BattleScene1");
    }

    public void Equipment()
    {
        SceneManager.LoadScene("EquipScene1");
    }

    public void Skill()
    {
        SceneManager.LoadScene("SkillScene1");
    }
}
