#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

public static class AssignBookIDsFromTitles
{
    [MenuItem("Tools/Books/Assign bookIDs From Titles")]
    public static void Run()
    {
        string[] guids = AssetDatabase.FindAssets("t:BookDefinition");
        var seen = new HashSet<string>();     // to enforce uniqueness within this pass
        int changed = 0, skipped = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<BookDefinition>(path);
            if (def == null) continue;

            // If it already has an ID, keep it (so we don't break existing saves)
            if (!string.IsNullOrEmpty(def.bookID))
            {
                seen.Add(def.bookID);
                skipped++;
                continue;
            }

            // Build a slug from the title (fallback to filename if title is empty)
            string source = string.IsNullOrWhiteSpace(def.title)
                ? System.IO.Path.GetFileNameWithoutExtension(path)
                : def.title;

            string baseId = Slugify(source);
            string unique = MakeUnique(baseId, seen);
            def.bookID = unique;
            EditorUtility.SetDirty(def);
            changed++;
            Debug.Log($"[AssignIDs] {path} -> '{def.bookID}'");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[AssignIDs] Done. Assigned: {changed}, kept existing: {skipped}.");
    }

    // lower-case, keep letters/digits, turn spaces & separators into '-', collapse repeats
    private static string Slugify(string s)
    {
        s = s.Trim().ToLowerInvariant();
        var sb = new StringBuilder(s.Length);
        bool dash = false;

        foreach (char c in s)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                dash = false;
            }
            else
            {
                if (!dash)
                {
                    sb.Append('-');
                    dash = true;
                }
            }
        }

        // trim leading/trailing '-'
        string slug = sb.ToString().Trim('-');

        // safety: if everything got stripped, fall back to "book"
        return string.IsNullOrEmpty(slug) ? "book" : slug;
    }

    private static string MakeUnique(string baseId, HashSet<string> seen)
    {
        string id = baseId;
        int n = 2;
        while (seen.Contains(id))
            id = $"{baseId}-{n++}";
        seen.Add(id);
        return id;
    }
}
#endif
