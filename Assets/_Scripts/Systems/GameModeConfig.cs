using UnityEngine;

public enum GameMode { Standard = 0, Zen = 1 }

public static class GameModeConfig
{
    private const string KEY_MODE = "GameMode_Current";   // 0/1, persisted for resume
    private const string KEY_PENDING = "GameMode_Pending"; // used only on the New Game flow

    public static GameMode CurrentMode
    {
        get => (GameMode)PlayerPrefs.GetInt(KEY_MODE, (int)GameMode.Standard);
        private set { PlayerPrefs.SetInt(KEY_MODE, (int)value); PlayerPrefs.Save(); }
    }

    // Call this on the main menu when the player clicks “New Game” (before loading the game scene)
    public static void StartNewGame(GameMode mode)
    {
        // New game wipes the save elsewhere, so we can safely set the new mode
        CurrentMode = mode;
    }

    //UI helper while choosing on the main menu (not strictly required)
    public static void SetPendingMode(GameMode mode) =>
        PlayerPrefs.SetInt(KEY_PENDING, (int)mode);

    public static GameMode GetPendingMode() =>
        (GameMode)PlayerPrefs.GetInt(KEY_PENDING, (int)GameMode.Standard);

    // Guard: you can only change modes if there is no save
    public static bool CanChooseModeNow() => !SaveSystem.HasSave();
}
