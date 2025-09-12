#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DesignItem))]
public class DesignItemInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var def = (DesignItem)target;

        // Thumbnail Preview
        if (def.itemImage != null && def.itemImage.texture != null)
        {
            GUILayout.Space(8);
            var rect = GUILayoutUtility.GetRect(1, 180, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(rect, def.itemImage.texture, ScaleMode.ScaleToFit, true);
        }

        GUILayout.Space(4);

        // Row 1
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Regenerate (Default)"))
            {
                DesignItemPreviewGenerator.GenerateForDefinition(def, orthographic: true);
            }

            if (GUILayout.Button("Top View"))
            {
                DesignItemPreviewGenerator.GenerateForDefinition(
                    def,
                    orthographic: true,
                    customCamOffset: new Vector3(0, 5, 0),
                    customCamEuler: new Vector3(90, 0, 0)
                );
            }

            if (GUILayout.Button("Bottom View"))
            {
                DesignItemPreviewGenerator.GenerateForDefinition(
                    def,
                    orthographic: true,
                    customCamOffset: new Vector3(0, -5, 0),
                    customCamEuler: new Vector3(-90, 0, 0)
                );
            }
        }

        // Row 2
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Left View"))
            {
                DesignItemPreviewGenerator.GenerateForDefinition(
                    def,
                    orthographic: true,
                    customCamOffset: new Vector3(-5, 0, 0),
                    customCamEuler: new Vector3(0, 90, 0)
                );
            }

            if (GUILayout.Button("Right View"))
            {
                DesignItemPreviewGenerator.GenerateForDefinition(
                    def,
                    orthographic: true,
                    customCamOffset: new Vector3(5, 0, 0),
                    customCamEuler: new Vector3(0, -90, 0)
                );
            }

            if (GUILayout.Button("Perspective View"))
            {
                DesignItemPreviewGenerator.GenerateForDefinition(
                    def,
                    orthographic: false, // force perspective
                    customCamOffset: new Vector3(3, 2.5f, -5), 
                    customCamEuler: new Vector3(25, -30, 0)    // tilt a bit less downward
                );
            }


            if (GUILayout.Button("Ping Thumbnail", GUILayout.Height(22)))
            {
                if (def.itemImage != null) EditorGUIUtility.PingObject(def.itemImage);
            }
        }
    }

    public override bool HasPreviewGUI()
    {
        var def = (DesignItem)target;
        return def != null && def.itemImage != null && def.itemImage.texture != null;
    }

    public override void OnPreviewGUI(Rect r, GUIStyle background)
    {
        var def = (DesignItem)target;
        if (def.itemImage != null && def.itemImage.texture != null)
            GUI.DrawTexture(r, def.itemImage.texture, ScaleMode.ScaleToFit, true);
    }
}
#endif
