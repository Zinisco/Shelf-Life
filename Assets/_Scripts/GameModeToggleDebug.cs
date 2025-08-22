using UnityEngine;
using UnityEngine.SceneManagement;

public class GameModeToggleDebug : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Z))
        {
            GameModeConfig.StartNewGame(GameMode.Zen);
            Debug.Log("Switched to Zen Mode");
            ReloadScene(); // Add this
        }

        if (Input.GetKeyDown(KeyCode.X))
        {
            GameModeConfig.StartNewGame(GameMode.Standard);
            Debug.Log("Switched to Standard Mode");
            ReloadScene(); // Add this
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            Debug.Log("Current Mode: " + GameModeConfig.CurrentMode);
        }
    }

    void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
