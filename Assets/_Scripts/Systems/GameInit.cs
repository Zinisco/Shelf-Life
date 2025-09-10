using UnityEngine;

public class GameInit : MonoBehaviour
{
    void Awake()
    {
        SettingsBootstrap.EnsureDefaultsSaved();
    }
}
