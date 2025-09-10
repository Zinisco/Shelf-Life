using System;
using UnityEngine;

public class BookDisplay : MonoBehaviour
{
    public GameObject attachedBook;

    [SerializeField] private string objectID;

    public string GetID()
    {
        if (string.IsNullOrEmpty(objectID))
            objectID = Guid.NewGuid().ToString();
        return objectID;
    }

    public void SetID(string id) => objectID = id;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(objectID))
            objectID = Guid.NewGuid().ToString();
    }
#endif
}
