#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BookDefinition))]
public class BookDefinitionInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var def = (BookDefinition)target;

        // Preview
        if (def.thumbnail != null && def.thumbnail.texture != null)
        {
            GUILayout.Space(8);
            var rect = GUILayoutUtility.GetRect(1, 180, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(rect, def.thumbnail.texture, ScaleMode.ScaleToFit, true);
        }

        GUILayout.Space(4);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Ping Thumbnail", GUILayout.Height(22)))
            {
                if (def.thumbnail != null) EditorGUIUtility.PingObject(def.thumbnail);
            }

            if (GUILayout.Button("Regenerate Thumbnail", GUILayout.Height(22)))
            {
                var sprite = BookPreviewGenerator.GenerateForDefinition(def); // helper below
                if (sprite != null)
                {
                    def.thumbnail = sprite;
                    EditorUtility.SetDirty(def);
                    AssetDatabase.SaveAssets();
                    Repaint();
                }
            }
        }
    }

    public override bool HasPreviewGUI()
    {
        var def = (BookDefinition)target;
        return def != null && def.thumbnail != null && def.thumbnail.texture != null;
    }

    public override void OnPreviewGUI(Rect r, GUIStyle background)
    {
        var def = (BookDefinition)target;
        if (def.thumbnail != null && def.thumbnail.texture != null)
            GUI.DrawTexture(r, def.thumbnail.texture, ScaleMode.ScaleToFit, true);
    }
}
#endif
