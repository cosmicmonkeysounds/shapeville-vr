using UnityEngine;
using UnityEngine.XR;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Reads the Quest 3/3S Guardian boundary and generates a 1-to-1 collidable
/// floor mesh matching the play area. No MRUK or Scene Capture required.
///
/// Boundary sources (tried in order):
///   1. OVRBoundary  — works on-device and over Quest Link
///   2. XRInputSubsystem.TryGetBoundaryPoints — standard Unity XR fallback
///   3. Simulated rectangle — editor-only, controlled by simulateInEditor
/// </summary>
public class SpatialFloorManager : MonoBehaviour
{
    [Header("Floor Rendering")]
    [Tooltip("Material applied to the generated spatial floor mesh (e.g. GridGrey).")]
    public Material floorMaterial;

    [Header("Physics")]
    [Tooltip("Layer for the generated floor. Must be included in BNGPlayerController.GroundedLayers.")]
    public int floorLayer = 0;

    [Header("Fallback")]
    [Tooltip("Static floor object to disable when the spatial floor loads.")]
    public GameObject fallbackFloor;

    [Tooltip("Size of the fallback floor generated when no boundary is available (meters).")]
    public float fallbackSize = 10f;

    [Header("Settings")]
    [Tooltip("Seconds to wait for boundary data before falling back.")]
    public float boundaryTimeout = 10f;

    [Tooltip("How often (seconds) to poll for boundary data during startup.")]
    public float pollInterval = 0.5f;

    [Header("Editor Testing")]
    [Tooltip("When true, generates a simulated rectangular boundary in the Editor so you can test the floor mesh without a headset.")]
    public bool simulateInEditor = true;

    [Tooltip("Width/depth of the simulated Guardian boundary (meters).")]
    public float simulatedSize = 3f;

    GameObject _generatedFloor;

    IEnumerator Start()
    {
        // Give XR a frame to finish initialization
        yield return null;

        List<Vector3> boundaryPoints = null;

        // --- Source 1: OVRBoundary (Oculus-specific, best Quest Link support) ---
        boundaryPoints = TryGetOVRBoundary();
        if (boundaryPoints != null)
        {
            Debug.Log($"[SpatialFloor] Guardian boundary found via OVRBoundary ({boundaryPoints.Count} points).");
            BuildFloorFromBoundary(boundaryPoints);
            yield break;
        }

        // --- Source 2: XRInputSubsystem (standard Unity XR) ---
        float elapsed = 0f;
        while (elapsed < boundaryTimeout)
        {
            boundaryPoints = TryGetXRBoundary();
            if (boundaryPoints != null)
            {
                Debug.Log($"[SpatialFloor] Guardian boundary found via XRInputSubsystem ({boundaryPoints.Count} points).");
                BuildFloorFromBoundary(boundaryPoints);
                yield break;
            }

            // Also re-check OVR each poll in case it became available
            boundaryPoints = TryGetOVRBoundary();
            if (boundaryPoints != null)
            {
                Debug.Log($"[SpatialFloor] Guardian boundary found via OVRBoundary ({boundaryPoints.Count} points).");
                BuildFloorFromBoundary(boundaryPoints);
                yield break;
            }

            yield return new WaitForSeconds(pollInterval);
            elapsed += pollInterval;
        }

        // --- Source 3: Simulated boundary (Editor only) ---
#if UNITY_EDITOR
        if (simulateInEditor)
        {
            Debug.Log($"[SpatialFloor] No headset detected. Using simulated {simulatedSize}x{simulatedSize}m boundary for editor testing.");
            float half = simulatedSize / 2f;
            boundaryPoints = new List<Vector3>
            {
                new Vector3(-half, 0f, -half),
                new Vector3( half, 0f, -half),
                new Vector3( half, 0f,  half),
                new Vector3(-half, 0f,  half)
            };
            BuildFloorFromBoundary(boundaryPoints);
            yield break;
        }
#endif

        Debug.LogWarning("[SpatialFloor] Could not get Guardian boundary. Using fallback floor.");
        ActivateFallback();
    }

    /// <summary>
    /// Try to get boundary points from OVRBoundary (Meta Oculus SDK).
    /// Returns null if OVRBoundary is unavailable or has no configured boundary.
    /// </summary>
    List<Vector3> TryGetOVRBoundary()
    {
        if (OVRManager.boundary == null)
            return null;

        if (!OVRManager.boundary.GetConfigured())
            return null;

        // GetGeometry returns the outer Guardian boundary polygon
        Vector3[] points = OVRManager.boundary.GetGeometry(OVRBoundary.BoundaryType.OuterBoundary);
        if (points == null || points.Length < 3)
            return null;

        return new List<Vector3>(points);
    }

    /// <summary>
    /// Try to get boundary points from the standard Unity XR input subsystem.
    /// Returns null if no running subsystem has boundary data.
    /// </summary>
    List<Vector3> TryGetXRBoundary()
    {
        var subsystems = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);

        var points = new List<Vector3>();
        foreach (var subsystem in subsystems)
        {
            if (subsystem.running &&
                subsystem.TryGetBoundaryPoints(points) &&
                points.Count >= 3)
            {
                return points;
            }
        }

        return null;
    }

    void BuildFloorFromBoundary(List<Vector3> boundaryPoints)
    {
        // Project world-space boundary points to 2D (XZ plane)
        var boundary2D = new Vector2[boundaryPoints.Count];
        for (int i = 0; i < boundaryPoints.Count; i++)
        {
            boundary2D[i] = new Vector2(boundaryPoints[i].x, boundaryPoints[i].z);
        }

        Mesh floorMesh = FloorMeshGenerator.GenerateFloorMesh(boundary2D);
        if (floorMesh == null)
        {
            Debug.LogWarning("[SpatialFloor] Failed to generate floor mesh from boundary.");
            ActivateFallback();
            return;
        }

        CreateFloorObject(floorMesh);
        DisableFallbackFloor();
        Debug.Log("[SpatialFloor] Spatial floor created from Guardian boundary.");
    }

    void CreateFloorObject(Mesh mesh)
    {
        _generatedFloor = new GameObject("SpatialFloorMesh");
        _generatedFloor.transform.SetParent(transform, false);
        _generatedFloor.layer = floorLayer;

        var meshFilter = _generatedFloor.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;

        var meshRenderer = _generatedFloor.AddComponent<MeshRenderer>();
        meshRenderer.material = floorMaterial;

        var meshCollider = _generatedFloor.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
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

        CreateFloorObject(mesh);
    }
}
