#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public static class BookThumbRelinker
{
    private const string OUTPUT_FOLDER = "Assets/BookPreviews";

    [MenuItem("Tools/Books/Relink Thumbnails From Folder")]
    private static void RelinkAll()
    {
        if (!Directory.Exists(OUTPUT_FOLDER))
        {
            Debug.LogWarning($"Folder not found: {OUTPUT_FOLDER}");
            return;
        }

        string[] pngs = Directory.GetFiles(OUTPUT_FOLDER, "*.png", SearchOption.AllDirectories);
        int linked = 0;

        foreach (var png in pngs)
        {
            string baseName = Path.GetFileNameWithoutExtension(png);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(png);
            if (sprite == null) continue;

            foreach (string guid in AssetDatabase.FindAssets("t:BookDefinition"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var def = AssetDatabase.LoadAssetAtPath<BookDefinition>(path);
                if (def == null) continue;

                bool match =
                    string.Equals(def.bookID, baseName, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(def.title, baseName, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(def.name, baseName, System.StringComparison.OrdinalIgnoreCase);

                if (!match) continue;

                if (def.thumbnail != sprite)
                {
                    Undo.RecordObject(def, "Assign thumbnail");
                    def.thumbnail = sprite;
                    EditorUtility.SetDirty(def);
                    linked++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Relinked {linked} thumbnail assignment(s).");
    }
}
#endif
