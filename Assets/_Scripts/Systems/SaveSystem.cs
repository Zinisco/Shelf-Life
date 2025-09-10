using UnityEngine;
using System.IO;

public static class SaveSystem
{
    private static string SavePath =>
        Path.Combine(Application.persistentDataPath, "booksave.json");

    public static bool HasSave() => File.Exists(SavePath);

    public static void Save()
    {
        // If your BookSaveManager already creates the file, this is enough:
        BookSaveManager.TriggerSave();

        // If you also stash any flags in PlayerPrefs, flush them:
        PlayerPrefs.Save();

        Debug.Log($"[SaveSystem] Saved to: {SavePath}");
    }

    public static void Load()
    {
        if (!HasSave())
        {
            Debug.Log("[SaveSystem] No save to load.");
            return;
        }
        BookSaveManager.TriggerLoad();
    }

    public static void LoadOrReset()
    {
        BookSaveManager.TriggerLoad();
        if (!HasSave())
        {
            Debug.Log("[SaveSystem] Old/corrupt save cleared during load.");
        }
    }

    public static void ClearSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log("[SaveSystem] Save file deleted.");
        }
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }
}
