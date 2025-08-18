#if UNITY_EDITOR
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BookDefinitionEditor
{
    // Right–click a BookDefinition asset ? Generate ID (from Title)
    [MenuItem("CONTEXT/BookDefinition/Generate ID (from Title)")]
    private static void GenerateID(MenuCommand cmd)
    {
        var def = (BookDefinition)cmd.context;
        GenerateOrRegenerate(def, onlyIfEmpty: true);
    }

    // Right–click a BookDefinition asset ? Regenerate ID (force)
    [MenuItem("CONTEXT/BookDefinition/Regenerate ID (force)")]
    private static void RegenerateID(MenuCommand cmd)
    {
        var def = (BookDefinition)cmd.context;
        GenerateOrRegenerate(def, onlyIfEmpty: false);
    }

    // Optional: convenience to copy the ID
    [MenuItem("CONTEXT/BookDefinition/Copy ID to Clipboard")]
    private static void CopyID(MenuCommand cmd)
    {
        var def = (BookDefinition)cmd.context;
        EditorGUIUtility.systemCopyBuffer = def.bookID ?? "";
        Debug.Log($"[BookDefinition] Copied ID: {def.bookID}");
    }

    // Also add a top-menu to batch process the current selection
    [MenuItem("Tools/Books/Generate IDs For Selected (from Title)")]
    private static void GenerateIDsForSelected()
    {
        var defs = Selection.objects
            .OfType<BookDefinition>()
            .ToArray();

        if (defs.Length == 0)
        {
            Debug.LogWarning("No BookDefinition assets selected.");
            return;
        }

        foreach (var def in defs)
            GenerateOrRegenerate(def, onlyIfEmpty: true);

        AssetDatabase.SaveAssets();
        Debug.Log($"Processed {defs.Length} BookDefinition asset(s).");
    }

    // Core logic
    private static void GenerateOrRegenerate(BookDefinition def, bool onlyIfEmpty)
    {
        if (def == null)
            return;

        if (onlyIfEmpty && !string.IsNullOrEmpty(def.bookID))
        {
            Debug.Log($"[BookDefinition] '{def.title}' already has an ID: {def.bookID} (skipped)");
            return;
        }

        if (string.IsNullOrWhiteSpace(def.title))
        {
            Debug.LogError($"[BookDefinition] Title is empty on asset: {def.name}. Cannot generate an ID.");
            return;
        }

        // Make a stable, human-readable slug + short hash so it remains unique even if two titles collide.
        string slug = Slugify(def.title);
        string hash = ShortHash(def.title);
        string newID = $"{slug}-{hash}";

        // Ensure uniqueness across all BookDefinitions (very unlikely to collide, but we’ll be extra safe)
        newID = EnsureUniqueAcrossProject(newID, def);

        Undo.RecordObject(def, "Assign bookID");
        def.bookID = newID;
        EditorUtility.SetDirty(def);

        Debug.Log($"[BookDefinition] Assigned bookID '{def.bookID}' to '{def.title}'.");
    }

    private static string Slugify(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (char.IsWhiteSpace(c) || c == '-' || c == '_') sb.Append('-');
            // ignore other punctuation
        }

        // collapse duplicate dashes
        string slug = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "-{2,}", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "book" : slug;
    }

    private static string ShortHash(string s)
    {
        using var md5 = MD5.Create();
        var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(s));
        // 8 hex chars: good balance of short + very low collision odds for titles
        return BitConverter.ToString(bytes, 0, 4).Replace("-", "").ToLowerInvariant();
    }

    private static string EnsureUniqueAcrossProject(string baseId, BookDefinition current)
    {
        // Look up all BookDefinitions and check for duplicates
        string[] guids = AssetDatabase.FindAssets("t:BookDefinition");
        var taken = guids
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Select(p => AssetDatabase.LoadAssetAtPath<BookDefinition>(p))
            .Where(d => d != null && d != current && !string.IsNullOrEmpty(d.bookID))
            .Select(d => d.bookID)
            .ToHashSet();

        if (!taken.Contains(baseId))
            return baseId;

        // If somehow taken, append a tiny random suffix until unique
        int tries = 0;
        string candidate = baseId;
        while (taken.Contains(candidate) && tries++ < 20)
            candidate = $"{baseId}-{UnityEngine.Random.Range(1000, 9999)}";

        return candidate;
    }
}
#endif
