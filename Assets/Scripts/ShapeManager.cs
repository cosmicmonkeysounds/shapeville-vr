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
        SpawnShapes();
    }

    private void SpawnShapes()
    {
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
        }
    }
}
