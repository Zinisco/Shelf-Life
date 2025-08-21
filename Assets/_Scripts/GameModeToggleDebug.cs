using UnityEngine;

public class GameModeToggleDebug : MonoBehaviour
{
    void Update()
    {
        // Press Z to switch to Zen Mode
        if (Input.GetKeyDown(KeyCode.Z))
        {
            GameModeConfig.StartNewGame(GameMode.Zen);
            Debug.Log("Switched to Zen Mode");
        }

        // Press X to switch to Standard Mode
        if (Input.GetKeyDown(KeyCode.X))
        {
            GameModeConfig.StartNewGame(GameMode.Standard);
            Debug.Log("Switched to Standard Mode");
        }

        // Show current mode
        if (Input.GetKeyDown(KeyCode.C))
        {
            Debug.Log("Current Mode: " + GameModeConfig.CurrentMode);
        }
    }
}
