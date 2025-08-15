using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.IO;

public class SceneLoader : MonoBehaviour
{
    public static string sceneToLoad; // set before loading the loading screen
    [SerializeField] private string fallbackSceneName = "GameScene";

    private void Start()
    {
        string target = string.IsNullOrEmpty(sceneToLoad) ? fallbackSceneName : sceneToLoad;
        Debug.Log($"[SceneLoader] Starting. sceneToLoad='{sceneToLoad ?? "null"}' ? target='{target}'");

        if (!IsSceneInBuild(target))
        {
            Debug.LogError($"[SceneLoader] Target scene '{target}' is NOT in Build Settings. " +
                           "Add it via File > Build Settings or fix the name.");
            return; // avoid looping on a bad scene
        }

        StartCoroutine(LoadAsync(target));
    }

    private IEnumerator LoadAsync(string target)
    {
        // short frame to let any loading UI show
        yield return null;

        Debug.Log($"[SceneLoader] Loading '{target}'...");
        AsyncOperation op = SceneManager.LoadSceneAsync(target);
        if (op == null)
        {
            Debug.LogError("[SceneLoader] SceneManager.LoadSceneAsync returned null.");
            yield break;
        }

        op.allowSceneActivation = true; // default is true, but explicit is nice

        while (!op.isDone)
        {
            // Optional: update a progress bar here using op.progress (0..0.9 until activation)
            yield return null;
        }

        Debug.Log($"[SceneLoader] Loaded '{target}'.");
    }

    private static bool IsSceneInBuild(string sceneName)
    {
        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = Path.GetFileNameWithoutExtension(path);
            if (name == sceneName) return true;
        }
        return false;
    }
}
