using UnityEngine;

public static class SaveSystem
{
    public static bool HasSave()
    {
        return System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, "booksave.json"));
    }

    public static void Load()
    {
        BookSaveManager.TriggerLoad();
    }

    public static void ClearSave()
    {
        if (System.IO.File.Exists(System.IO.Path.Combine(Application.persistentDataPath, "booksave.json")))
        {
            System.IO.File.Delete(System.IO.Path.Combine(Application.persistentDataPath, "booksave.json"));
        }

        PlayerPrefs.DeleteAll(); // Only if you're also storing other data like current day
    }
}
