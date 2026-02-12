using UnityEngine;
using Meta.XR.MRUtilityKit;

public class ShapeManager : MonoBehaviour
{
    [Header("Spawning")]
    [SerializeField] private GameObject shapePrefab;
    [SerializeField] private int spawnCount = 5;

    private void Start()
    {
        MRUK.Instance.RegisterSceneLoadedCallback(OnSceneLoaded);
    }

    private void OnSceneLoaded()
    {
        var room = MRUK.Instance.GetCurrentRoom();
        if (room == null)
        {
            Debug.LogError("[ShapeManager] No room found after scene loaded.");
            return;
        }

        SpawnShapes(room);
    }

    private void SpawnShapes(MRUKRoom room)
    {
        for (int i = 0; i < spawnCount; i++)
        {
            var go = Instantiate(shapePrefab);
            go.name = $"Shape_{i}";

            var shape = go.GetComponent<Shape>();
            if (shape == null)
                shape = go.AddComponent<Shape>();

            if (!shape.FindSpawnPosition(room))
            {
                Destroy(go);
            }
        }
    }
}
