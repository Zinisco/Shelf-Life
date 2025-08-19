#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

public class BookThumbPostprocessor : AssetPostprocessor
{
    // Keep in sync with your generator
    private const string OUTPUT_FOLDER = "Assets/BookPreviews";

    void OnPostprocessTexture(Texture2D texture)
    {
        if (!assetPath.StartsWith(OUTPUT_FOLDER)) return;

        // Ensure it's imported as a Sprite
        var ti = (TextureImporter)assetImporter;
        if (ti.textureType != TextureImporterType.Sprite || ti.mipmapEnabled || !ti.alphaIsTransparency)
        {
            ti.textureType = TextureImporterType.Sprite;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
        }

        // Match filename to a BookDefinition by bookID, title, or asset name
        string fileBase = Path.GetFileNameWithoutExtension(assetPath);

        // Find all BookDefinitions once; then try match
        string[] guids = AssetDatabase.FindAssets("t:BookDefinition");
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<BookDefinition>(path);
            if (def == null) continue;

            bool matches =
                string.Equals(def.bookID, fileBase, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(def.title, fileBase, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(def.name, fileBase, System.StringComparison.OrdinalIgnoreCase);

            if (!matches) continue;

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null) continue;

            // Assign if different/missing
            if (def.thumbnail != sprite)
            {
                Undo.RecordObject(def, "Assign thumbnail");
                def.thumbnail = sprite;
                EditorUtility.SetDirty(def);
                // Don't early-return: multiple defs could share same filename by accident.
            }
        }
        AssetDatabase.SaveAssets();
    }
}
#endif
