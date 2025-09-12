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
            if (GUILayout.Button("Default"))
            {
                BookPreviewGenerator.GenerateForDefinition(def, orthographic: true);
            }

            if (GUILayout.Button("Top View"))
            {
                BookPreviewGenerator.GenerateForDefinition(
                    def, orthographic: true,
                    customCamOffset: new Vector3(0, 5, 0),
                    customCamEuler: new Vector3(90, 0, 0)
                );
            }

            if (GUILayout.Button("Bottom View"))
            {
                BookPreviewGenerator.GenerateForDefinition(
                    def, orthographic: true,
                    customCamOffset: new Vector3(0, -5, 0),
                    customCamEuler: new Vector3(-90, 0, 0)
                );
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Left View"))
            {
                BookPreviewGenerator.GenerateForDefinition(
                    def, orthographic: true,
                    customCamOffset: new Vector3(-5, 0, 0),
                    customCamEuler: new Vector3(0, 90, 0)
                );
            }

            if (GUILayout.Button("Right View"))
            {
                BookPreviewGenerator.GenerateForDefinition(
                    def, orthographic: true,
                    customCamOffset: new Vector3(5, 0, 0),
                    customCamEuler: new Vector3(0, -90, 0)
                );
            }

            if (GUILayout.Button("Perspective View"))
            {
                BookPreviewGenerator.GenerateForDefinition(
                    def, orthographic: false,
                    customCamOffset: new Vector3(3, 2.5f, -5),
                    customCamEuler: new Vector3(25, -30, 0)
                );
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
