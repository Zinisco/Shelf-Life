using UnityEngine;

public static class TransformExtensions
{
    public static string GetHierarchyPath(this Transform t)
    {
        if (t == null) return "(null)";
        var names = new System.Collections.Generic.List<string>();
        while (t != null) { names.Add(t.name); t = t.parent; }
        names.Reverse();
        return string.Join("/", names);
    }
}

