using UnityEngine;
using System.IO;

public static class SaveSystem
{
    private static string SavePath =>
        Path.Combine(Application.persistentDataPath, "booksave.json");

    public static bool HasSave() => File.Exists(SavePath);

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
        // If BookSaveManager found an incompatible save, it will delete it.
        // You could check here again if file still exists and, if desired, immediately Save a fresh file.
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
        // Only if you’re storing misc flags here
        PlayerPrefs.DeleteAll();
    }
}
