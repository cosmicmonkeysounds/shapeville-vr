using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Meta.XR.MRUtilityKit;

/// <summary>
/// Reads the Quest 3/3S room model via MRUK (MR Utility Kit), finds floor anchors,
/// and generates matching floor meshes with collision.
/// Works both on-device and in the Editor over Meta Horizon Link (Quest Link WiFi).
/// Falls back to a static floor only when no scene data can be loaded.
/// </summary>
public class SpatialFloorManager : MonoBehaviour
{
    [Header("Floor Rendering")]
    [Tooltip("Material applied to the generated spatial floor mesh (e.g. GridGrey).")]
    public Material floorMaterial;

    [Header("Physics")]
    [Tooltip("Layer for the generated floor. Must be included in BNGPlayerController.GroundedLayers.")]
    public int floorLayer = 0; // Default layer

    [Header("Fallback")]
    [Tooltip("Static floor object to disable when the spatial floor loads (e.g. RigidFloor).")]
    public GameObject fallbackFloor;

    [Tooltip("Size of the fallback floor generated when no scene data is available (meters).")]
    public float fallbackSize = 10f;

    readonly List<GameObject> _generatedFloors = new List<GameObject>();
    bool _sceneLoaded;

    async void Start()
    {
        var mruk = MRUK.Instance;
        if (mruk == null)
        {
            Debug.LogError("[SpatialFloor] MRUK instance not found. Add a MRUK component to the scene.");
            ActivateFallback();
            return;
        }

        // If MRUK has already loaded (e.g. another script triggered it), build immediately
        if (mruk.IsInitialized)
        {
            OnSceneLoaded();
            return;
        }

        // Listen for scene loaded event as a backup
        mruk.SceneLoadedEvent.AddListener(OnSceneLoaded);

        // Attempt to load scene from the device (works over Quest Link too)
        Debug.Log("[SpatialFloor] Loading scene from device...");
        var result = await mruk.LoadSceneFromDevice(requestSceneCaptureIfNoDataFound: true);

        switch (result)
        {
            case MRUK.LoadDeviceResult.Success:
                // OnSceneLoaded will be called via the event
                break;
            case MRUK.LoadDeviceResult.DiscoveryOngoing:
                // Scene discovery is still in progress; the SceneLoadedEvent
                // listener will handle it once discovery finishes.
                Debug.Log("[SpatialFloor] Scene discovery in progress, waiting for completion...");
                break;
            default:
                Debug.LogWarning($"[SpatialFloor] LoadSceneFromDevice returned: {result}. Using fallback floor.");
                ActivateFallback();
                break;
        }
    }

    void OnDestroy()
    {
        var mruk = MRUK.Instance;
        if (mruk != null)
        {
            mruk.SceneLoadedEvent.RemoveListener(OnSceneLoaded);
        }
    }

    void OnSceneLoaded()
    {
        if (_sceneLoaded) return;
        _sceneLoaded = true;

        Debug.Log("[SpatialFloor] Scene loaded. Building floor meshes...");
        BuildFloors();
    }

    void BuildFloors()
    {
        int floorsCreated = 0;

        foreach (var room in MRUK.Instance.Rooms)
        {
            foreach (var floorAnchor in room.FloorAnchors)
            {
                var boundary = floorAnchor.PlaneBoundary2D;
                if (boundary == null || boundary.Count < 3)
                {
                    Debug.LogWarning("[SpatialFloor] Floor anchor has insufficient boundary points.");
                    continue;
                }

                Mesh floorMesh = FloorMeshGenerator.GenerateFloorMesh(boundary.ToArray());
                if (floorMesh == null)
                    continue;

                CreateFloorObject(floorAnchor.transform, floorMesh);
                floorsCreated++;
            }
        }

        if (floorsCreated > 0)
        {
            Debug.Log($"[SpatialFloor] Created {floorsCreated} spatial floor(s).");
            DisableFallbackFloor();
        }
        else
        {
            Debug.LogWarning("[SpatialFloor] No valid floor anchors found. Using fallback.");
            ActivateFallback();
        }
    }

    void CreateFloorObject(Transform anchorTransform, Mesh mesh)
    {
        var floorObj = new GameObject("SpatialFloorMesh");
        floorObj.transform.SetParent(anchorTransform, false);
        floorObj.layer = floorLayer;

        var meshFilter = floorObj.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;

        var meshRenderer = floorObj.AddComponent<MeshRenderer>();
        meshRenderer.material = floorMaterial;

        var meshCollider = floorObj.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;

        _generatedFloors.Add(floorObj);
    }

    void DisableFallbackFloor()
    {
        if (fallbackFloor != null)
        {
            fallbackFloor.SetActive(false);
            Debug.Log("[SpatialFloor] Disabled fallback floor.");
        }
    }

    void ActivateFallback()
    {
        if (fallbackFloor != null)
        {
            fallbackFloor.SetActive(true);
            Debug.Log("[SpatialFloor] Activated fallback floor.");
            return;
        }

        // No explicit fallback assigned; generate a simple flat floor
        Debug.Log("[SpatialFloor] Generating procedural fallback floor.");
        float half = fallbackSize / 2f;
        var boundary = new Vector2[]
        {
            new Vector2(-half, -half),
            new Vector2( half, -half),
            new Vector2( half,  half),
            new Vector2(-half,  half)
        };

        Mesh mesh = FloorMeshGenerator.GenerateFloorMesh(boundary);
        if (mesh == null) return;

        CreateFloorObject(transform, mesh);
    }
}
