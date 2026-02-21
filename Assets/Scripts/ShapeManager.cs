using UnityEngine;
using System.Collections;
using Meta.XR.MRUtilityKit;

public class ShapeManager : MonoBehaviour
{
    [Header("Spawning")]
    [SerializeField] private GameObject shapePrefab;
    [SerializeField] private int spawnCount = 5;

    private void Start()
    {
        // Ensure RoomSystem exists in the scene (it may be missing)
        if (RoomSystem.Instance == null)
        {
            var rsGo = new GameObject("RoomSystem");
            rsGo.AddComponent<RoomSystem>();
            Debug.Log("[ShapeManager] Created missing RoomSystem instance.");
        }

        if (MRUK.Instance != null)
        {
            MRUK.Instance.RegisterSceneLoadedCallback(OnSceneLoaded);
        }
        else
        {
            Debug.LogWarning("[ShapeManager] MRUK.Instance is null at Start. Waiting for it...");
            StartCoroutine(WaitForMRUK());
        }
    }

    private IEnumerator WaitForMRUK()
    {
        float timeout = 15f;
        float elapsed = 0f;

        while (MRUK.Instance == null && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (MRUK.Instance != null)
        {
            Debug.Log("[ShapeManager] MRUK.Instance became available.");
            MRUK.Instance.RegisterSceneLoadedCallback(OnSceneLoaded);
        }
        else
        {
            Debug.LogError("[ShapeManager] MRUK.Instance never became available. Shapes will not spawn.");
        }
    }

    private void OnSceneLoaded()
    {
        Debug.Log("[ShapeManager] MRUK scene loaded. Spawning shapes...");
        SpawnShapes();
    }

    private void SpawnShapes()
    {
        int spawned = 0;
        for (int i = 0; i < spawnCount; i++)
        {
            var go = Instantiate(shapePrefab);
            go.name = $"Shape_{i}";

            var shape = go.GetComponent<Shape>();
            if (shape == null)
                shape = go.AddComponent<Shape>();

            if (!shape.SpawnFloating())
            {
                Destroy(go);
            }
            else
            {
                spawned++;
            }
        }
        Debug.Log($"[ShapeManager] Spawned {spawned}/{spawnCount} shapes.");
    }
}
