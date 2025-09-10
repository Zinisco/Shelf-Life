using System.Collections;
using UnityEngine;

public class BookLookInteraction : MonoBehaviour
{
    public Camera playerCamera;
    public float interactRange = 3f;
    public LayerMask bookLayer;
    public GameObject referenceUIPrefab;

    private bool justSpawned = false;


    private ReferenceUIController activeTooltip;

    void Update()
    {
        if (referenceUIPrefab == null)
        {
            Debug.LogWarning("referenceUIPrefab is NULL!");
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, bookLayer))
        {
            BookInfo book = hit.collider.GetComponent<BookInfo>();
            if (book != null && Input.GetKeyDown(KeyCode.R))
            {
                Debug.Log("Hit Book!");

                if (activeTooltip != null)
                    Destroy(activeTooltip.gameObject);

                // Spawn prefab into the current scene (so it shows up in hierarchy)
                GameObject uiObj = Instantiate(referenceUIPrefab.gameObject);
                uiObj.transform.SetParent(null);
                uiObj.name = "REFERENCE_UI";
                uiObj.transform.position = new Vector3(0, 1.5f, 2);
                uiObj.transform.localScale = Vector3.one * 0.005f;
                uiObj.SetActive(true);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(uiObj, gameObject.scene);


                activeTooltip = uiObj.GetComponent<ReferenceUIController>();
                if (activeTooltip == null)
                {
                    Debug.LogError("ReferenceUIController missing from prefab!");
                    return;
                }

                // Make sure it's active
                uiObj.SetActive(true);

                justSpawned = true;
                StartCoroutine(ResetJustSpawnedFlag());



                StartCoroutine(ShowTooltipDelayed(book));

            }
        }

        if (!justSpawned && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.R)) && activeTooltip != null)
        {
            activeTooltip.Hide();
            Destroy(activeTooltip.gameObject);
            activeTooltip = null;
        }
    }

    private IEnumerator ResetJustSpawnedFlag()
    {
        yield return null;
        justSpawned = false;
    }

    private IEnumerator ShowTooltipDelayed(BookInfo book)
    {
        yield return null; // wait 1 frame
        if (activeTooltip != null)
        {
            activeTooltip.Show(book.definition, book.transform);
        }
    }

}
