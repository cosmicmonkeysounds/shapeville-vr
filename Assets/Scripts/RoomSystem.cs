using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UltEvents;
using Meta.XR.MRUtilityKit;
using Meta.XR.BuildingBlocks;

/// <summary>
/// Central game system that hooks into MRUK scene/room lifecycle,
/// the Spatial Anchor Core building block, effect meshes, spawn positions,
/// and spatial anchor data for gameplay.
/// Assign the scene's MRUK, EffectMesh, and SpatialAnchorCore references in the inspector.
/// </summary>
public class RoomSystem : MonoBehaviour
{
    private const string TAG = "[RoomSystem]";

    public static RoomSystem Instance { get; private set; }

    [Header("References")]
    [SerializeField] private MRUK mruk;
    [SerializeField] private EffectMesh effectMesh;
    [SerializeField] private SpatialAnchorCoreBuildingBlock spatialAnchorCore;

    [Header("Spawn Settings")]
    [SerializeField] private float minEdgeDistance = 0.1f;
    [SerializeField] private float minSurfaceClearance = 0.05f;
    [SerializeField] private bool avoidVolumes = true;
    [SerializeField] private int maxSpawnAttempts = 100;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = true;

    // --- Scene Events ---
    [Header("Scene Events")]
    public UltEvent OnSceneReady;

    // --- Room Events ---
    [Header("Room Events")]
    public UltEvent<MRUKRoom> OnRoomCreated;
    public UltEvent<MRUKRoom> OnRoomUpdated;
    public UltEvent<MRUKRoom> OnRoomRemoved;

    // --- MRUK Anchor Events ---
    [Header("MRUK Anchor Events")]
    public UltEvent<MRUKAnchor> OnAnchorCreated;
    public UltEvent<MRUKAnchor> OnAnchorUpdated;
    public UltEvent<MRUKAnchor> OnAnchorRemoved;

    // --- Effect Mesh Events ---
    [Header("Effect Mesh Events")]
    public UltEvent OnEffectMeshCreated;
    public UltEvent OnEffectMeshDestroyed;

    // --- Spatial Anchor Core Events ---
    [Header("Spatial Anchor Core Events")]
    public UltEvent<OVRSpatialAnchor, OVRSpatialAnchor.OperationResult> OnSpatialAnchorCreateCompleted;
    public UltEvent<List<OVRSpatialAnchor>> OnSpatialAnchorsLoadCompleted;
    public UltEvent<OVRSpatialAnchor.OperationResult> OnSpatialAnchorsEraseAllCompleted;
    public UltEvent<OVRSpatialAnchor, OVRSpatialAnchor.OperationResult> OnSpatialAnchorEraseCompleted;

    // --- State ---
    public bool IsSceneReady { get; private set; }
    public MRUKRoom CurrentRoom { get; private set; }
    public IReadOnlyList<MRUKRoom> Rooms => mruk != null ? mruk.Rooms : null;
    public SpatialAnchorCoreBuildingBlock SpatialAnchorCore => spatialAnchorCore;

    // =========================================================================
    //  Lifecycle
    // =========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // --- MRUK ---
        if (mruk == null)
            mruk = MRUK.Instance;

        if (mruk == null)
        {
            Debug.LogError($"{TAG} No MRUK reference found. Assign it in the inspector or ensure MRUK exists in the scene.");
            return;
        }

        Log("Subscribing to MRUK events...");
        mruk.RegisterSceneLoadedCallback(HandleSceneLoaded);
        mruk.RoomCreatedEvent.AddListener(HandleRoomCreated);
        mruk.RoomUpdatedEvent.AddListener(HandleRoomUpdated);
        mruk.RoomRemovedEvent.AddListener(HandleRoomRemoved);

        // --- Spatial Anchor Core ---
        if (spatialAnchorCore == null)
            spatialAnchorCore = FindAnyObjectByType<SpatialAnchorCoreBuildingBlock>();

        if (spatialAnchorCore != null)
        {
            Log("Subscribing to Spatial Anchor Core events...");
            spatialAnchorCore.OnAnchorCreateCompleted.AddListener(HandleSpatialAnchorCreateCompleted);
            spatialAnchorCore.OnAnchorsLoadCompleted.AddListener(HandleSpatialAnchorsLoadCompleted);
            spatialAnchorCore.OnAnchorsEraseAllCompleted.AddListener(HandleSpatialAnchorsEraseAllCompleted);
            spatialAnchorCore.OnAnchorEraseCompleted.AddListener(HandleSpatialAnchorEraseCompleted);
        }
        else
        {
            Log("No SpatialAnchorCoreBuildingBlock found — spatial anchor events will not fire.", LogType.Warning);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (mruk != null)
        {
            mruk.RoomCreatedEvent.RemoveListener(HandleRoomCreated);
            mruk.RoomUpdatedEvent.RemoveListener(HandleRoomUpdated);
            mruk.RoomRemovedEvent.RemoveListener(HandleRoomRemoved);
        }

        if (spatialAnchorCore != null)
        {
            spatialAnchorCore.OnAnchorCreateCompleted.RemoveListener(HandleSpatialAnchorCreateCompleted);
            spatialAnchorCore.OnAnchorsLoadCompleted.RemoveListener(HandleSpatialAnchorsLoadCompleted);
            spatialAnchorCore.OnAnchorsEraseAllCompleted.RemoveListener(HandleSpatialAnchorsEraseAllCompleted);
            spatialAnchorCore.OnAnchorEraseCompleted.RemoveListener(HandleSpatialAnchorEraseCompleted);
        }
    }

    // =========================================================================
    //  MRUK Callbacks
    // =========================================================================

    private void HandleSceneLoaded()
    {
        CurrentRoom = mruk.GetCurrentRoom();
        IsSceneReady = true;

        Log($"Scene loaded — {mruk.Rooms.Count} room(s)");

        if (CurrentRoom != null)
        {
            LogRoomDetails(CurrentRoom, "Current room");
            SubscribeToRoomAnchors(CurrentRoom);
        }
        else
        {
            Log("No current room found after scene load.", LogType.Warning);
        }

        foreach (var room in mruk.Rooms)
        {
            if (room != CurrentRoom)
            {
                SubscribeToRoomAnchors(room);
                LogRoomDetails(room, "Additional room");
            }
        }

        OnSceneReady?.Invoke();
    }

    private void HandleRoomCreated(MRUKRoom room)
    {
        SubscribeToRoomAnchors(room);
        LogRoomDetails(room, "Room CREATED");
        OnRoomCreated?.Invoke(room);
    }

    private void HandleRoomUpdated(MRUKRoom room)
    {
        if (CurrentRoom == room)
            CurrentRoom = room;

        LogRoomDetails(room, "Room UPDATED");
        OnRoomUpdated?.Invoke(room);
    }

    private void HandleRoomRemoved(MRUKRoom room)
    {
        Log($"Room REMOVED: {room.name}");

        if (CurrentRoom == room)
        {
            CurrentRoom = mruk.GetCurrentRoom();
            Log($"  Current room reassigned to: {(CurrentRoom != null ? CurrentRoom.name : "null")}");
        }

        OnRoomRemoved?.Invoke(room);
    }

    private void SubscribeToRoomAnchors(MRUKRoom room)
    {
        room.AnchorCreatedEvent.AddListener(HandleAnchorCreated);
        room.AnchorUpdatedEvent.AddListener(HandleAnchorUpdated);
        room.AnchorRemovedEvent.AddListener(HandleAnchorRemoved);
        Log($"Subscribed to anchor events on room: {room.name}");
    }

    private void HandleAnchorCreated(MRUKAnchor anchor)
    {
        LogAnchorDetails(anchor, "Anchor CREATED");
        OnAnchorCreated?.Invoke(anchor);
    }

    private void HandleAnchorUpdated(MRUKAnchor anchor)
    {
        LogAnchorDetails(anchor, "Anchor UPDATED");
        OnAnchorUpdated?.Invoke(anchor);
    }

    private void HandleAnchorRemoved(MRUKAnchor anchor)
    {
        Log($"Anchor REMOVED: {anchor.name} (label: {anchor.Label})");
        OnAnchorRemoved?.Invoke(anchor);
    }

    // =========================================================================
    //  Spatial Anchor Core Callbacks
    // =========================================================================

    private void HandleSpatialAnchorCreateCompleted(OVRSpatialAnchor anchor, OVRSpatialAnchor.OperationResult result)
    {
        Log($"Spatial anchor CREATE completed: uuid={anchor.Uuid}, result={result}, pos={anchor.transform.position}");
        OnSpatialAnchorCreateCompleted?.Invoke(anchor, result);
    }

    private void HandleSpatialAnchorsLoadCompleted(List<OVRSpatialAnchor> anchors)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{TAG} Spatial anchors LOAD completed: {anchors.Count} anchor(s)");
        foreach (var a in anchors)
        {
            sb.AppendLine($"  uuid={a.Uuid}, pos={a.transform.position}, created={a.Created}");
        }
        if (debugLogging) Debug.Log(sb.ToString());

        OnSpatialAnchorsLoadCompleted?.Invoke(anchors);
    }

    private void HandleSpatialAnchorsEraseAllCompleted(OVRSpatialAnchor.OperationResult result)
    {
        Log($"Spatial anchors ERASE ALL completed: result={result}");
        OnSpatialAnchorsEraseAllCompleted?.Invoke(result);
    }

    private void HandleSpatialAnchorEraseCompleted(OVRSpatialAnchor anchor, OVRSpatialAnchor.OperationResult result)
    {
        Log($"Spatial anchor ERASE completed: uuid={anchor.Uuid}, result={result}");
        OnSpatialAnchorEraseCompleted?.Invoke(anchor, result);
    }

    // =========================================================================
    //  Spatial Anchor Core — Passthrough API
    // =========================================================================

    /// <summary>
    /// Creates and saves a spatial anchor at the given world pose via SpatialAnchorCore.
    /// Listen to <see cref="OnSpatialAnchorCreateCompleted"/> for the result.
    /// </summary>
    public void CreateSpatialAnchor(Vector3 position, Quaternion rotation, GameObject prefab = null)
    {
        if (spatialAnchorCore == null)
        {
            Log("Cannot create spatial anchor — SpatialAnchorCore not assigned.", LogType.Error);
            return;
        }
        Log($"Creating spatial anchor at pos={position}, rot={rotation.eulerAngles}");
        spatialAnchorCore.InstantiateSpatialAnchor(prefab, position, rotation);
    }

    /// <summary>
    /// Loads previously saved spatial anchors by their UUIDs.
    /// Listen to <see cref="OnSpatialAnchorsLoadCompleted"/> for the result.
    /// </summary>
    public void LoadSpatialAnchors(List<Guid> uuids, GameObject prefab = null)
    {
        if (spatialAnchorCore == null)
        {
            Log("Cannot load spatial anchors — SpatialAnchorCore not assigned.", LogType.Error);
            return;
        }
        Log($"Loading {uuids.Count} spatial anchor(s)...");
        spatialAnchorCore.LoadAndInstantiateAnchors(prefab, uuids);
    }

    /// <summary>
    /// Erases all instantiated spatial anchors.
    /// Listen to <see cref="OnSpatialAnchorsEraseAllCompleted"/> for the result.
    /// </summary>
    public void EraseAllSpatialAnchors()
    {
        if (spatialAnchorCore == null)
        {
            Log("Cannot erase spatial anchors — SpatialAnchorCore not assigned.", LogType.Error);
            return;
        }
        Log("Erasing all spatial anchors...");
        spatialAnchorCore.EraseAllAnchors();
    }

    /// <summary>
    /// Erases a single spatial anchor by UUID.
    /// Listen to <see cref="OnSpatialAnchorEraseCompleted"/> for the result.
    /// </summary>
    public void EraseSpatialAnchor(Guid uuid)
    {
        if (spatialAnchorCore == null)
        {
            Log("Cannot erase spatial anchor — SpatialAnchorCore not assigned.", LogType.Error);
            return;
        }
        Log($"Erasing spatial anchor: {uuid}");
        spatialAnchorCore.EraseAnchorByUuid(uuid);
    }

    // =========================================================================
    //  Effect Mesh
    // =========================================================================

    public void CreateEffectMesh()
    {
        if (effectMesh == null)
        {
            Log("No EffectMesh assigned — cannot create.", LogType.Warning);
            return;
        }
        Log("Creating effect mesh (all rooms)...");
        effectMesh.CreateMesh();
        Log("Effect mesh created.");
        OnEffectMeshCreated?.Invoke();
    }

    public void CreateEffectMesh(MRUKRoom room)
    {
        if (effectMesh == null) return;
        Log($"Creating effect mesh for room: {room.name}");
        effectMesh.CreateMesh(room);
        OnEffectMeshCreated?.Invoke();
    }

    public void DestroyEffectMesh()
    {
        if (effectMesh == null) return;
        Log("Destroying effect mesh.");
        effectMesh.DestroyMesh();
        OnEffectMeshDestroyed?.Invoke();
    }

    public void SetEffectMeshVisible(bool visible, Material overrideMat = null)
    {
        Log($"Effect mesh visibility: {visible}");
        effectMesh?.ToggleEffectMeshVisibility(visible, new LabelFilter(), overrideMat);
    }

    public void SetEffectMeshColliders(bool enabled)
    {
        Log($"Effect mesh colliders: {enabled}");
        effectMesh?.ToggleEffectMeshColliders(enabled);
    }

    public void SetEffectMeshMaterial(Material material)
    {
        Log($"Effect mesh material override: {(material != null ? material.name : "null")}");
        effectMesh?.OverrideEffectMaterial(material);
    }

    public void SetEffectMeshShadows(bool cast)
    {
        Log($"Effect mesh shadows: {cast}");
        effectMesh?.ToggleShadowCasting(cast);
    }

    // =========================================================================
    //  Spawn Positions
    // =========================================================================

    public Pose? GetRandomSurfaceSpawn(
        MRUK.SurfaceType surfaceType,
        MRUKAnchor.SceneLabels labels = MRUKAnchor.SceneLabels.FLOOR,
        MRUKRoom room = null)
    {
        room ??= CurrentRoom;
        if (room == null)
        {
            Log("GetRandomSurfaceSpawn failed — no room available.", LogType.Warning);
            return null;
        }

        if (room.GenerateRandomPositionOnSurface(
                surfaceType,
                minEdgeDistance,
                new LabelFilter(labels),
                out Vector3 pos,
                out Vector3 normal))
        {
            var rot = Quaternion.FromToRotation(Vector3.up, normal);
            Log($"Surface spawn found: pos={pos}, normal={normal}, surface={surfaceType}, labels={labels}");
            return new Pose(pos, rot);
        }

        Log($"Surface spawn failed: surface={surfaceType}, labels={labels}", LogType.Warning);
        return null;
    }

    public Vector3? GetRandomFloatingSpawn(MRUKRoom room = null)
    {
        room ??= CurrentRoom;
        if (room == null)
        {
            Log("GetRandomFloatingSpawn failed — no room available.", LogType.Warning);
            return null;
        }

        var result = room.GenerateRandomPositionInRoom(minSurfaceClearance, avoidVolumes);
        if (result.HasValue)
            Log($"Floating spawn found: {result.Value}");
        else
            Log("Floating spawn failed — could not find valid position.", LogType.Warning);

        return result;
    }

    public List<Pose> GetSpawnPositions(
        int count,
        MRUK.SurfaceType surfaceType,
        MRUKAnchor.SceneLabels labels = MRUKAnchor.SceneLabels.FLOOR,
        MRUKRoom room = null)
    {
        var results = new List<Pose>(count);
        int attempts = 0;
        int maxTotal = count * maxSpawnAttempts;

        for (int i = 0; i < maxTotal && results.Count < count; i++)
        {
            attempts++;
            var pose = GetRandomSurfaceSpawn(surfaceType, labels, room);
            if (pose.HasValue)
                results.Add(pose.Value);
        }

        Log($"GetSpawnPositions: requested={count}, found={results.Count}, attempts={attempts}, surface={surfaceType}, labels={labels}");
        return results;
    }

    // =========================================================================
    //  Spatial Anchor Queries (MRUK)
    // =========================================================================

    public List<MRUKAnchor> GetAnchors(
        MRUKAnchor.SceneLabels labels,
        MRUKRoom room = null)
    {
        room ??= CurrentRoom;
        if (room == null) return new List<MRUKAnchor>();

        var result = new List<MRUKAnchor>();
        foreach (var anchor in room.Anchors)
        {
            if (anchor.HasAnyLabel(labels))
                result.Add(anchor);
        }

        Log($"GetAnchors: labels={labels}, found={result.Count} in room {room.name}");
        return result;
    }

    public MRUKAnchor GetLargestSurface(
        MRUKAnchor.SceneLabels labels,
        MRUKRoom room = null)
    {
        room ??= CurrentRoom;
        var anchor = room?.FindLargestSurface(labels);
        Log($"GetLargestSurface: labels={labels}, result={anchor?.name ?? "null"}");
        return anchor;
    }

    public List<MRUKAnchor> GetFloors(MRUKRoom room = null)
    {
        room ??= CurrentRoom;
        return room?.FloorAnchors ?? new List<MRUKAnchor>();
    }

    public List<MRUKAnchor> GetWalls(MRUKRoom room = null)
    {
        room ??= CurrentRoom;
        return room?.WallAnchors ?? new List<MRUKAnchor>();
    }

    public List<MRUKAnchor> GetCeilings(MRUKRoom room = null)
    {
        room ??= CurrentRoom;
        return room?.CeilingAnchors ?? new List<MRUKAnchor>();
    }

    public MRUKAnchor GetGlobalMesh(MRUKRoom room = null)
    {
        room ??= CurrentRoom;
        return room?.GlobalMeshAnchor;
    }

    public Vector3 GetAnchorCenter(MRUKAnchor anchor)
    {
        return anchor.GetAnchorCenter();
    }

    public List<Vector3> GetRoomOutline(MRUKRoom room = null)
    {
        room ??= CurrentRoom;
        return room?.GetRoomOutline() ?? new List<Vector3>();
    }

    public Bounds GetRoomBounds(MRUKRoom room = null)
    {
        room ??= CurrentRoom;
        return room?.GetRoomBounds() ?? new Bounds();
    }

    // =========================================================================
    //  Spatial Queries
    // =========================================================================

    public bool IsInRoom(Vector3 worldPosition, bool testVertical = true, MRUKRoom room = null)
    {
        room ??= CurrentRoom;
        return room != null && room.IsPositionInRoom(worldPosition, testVertical);
    }

    public bool IsInsideFurniture(Vector3 worldPosition, float buffer = 0f, MRUKRoom room = null)
    {
        room ??= CurrentRoom;
        return room != null && room.IsPositionInSceneVolume(worldPosition, buffer);
    }

    public bool RoomRaycast(
        Ray ray,
        float maxDist,
        out RaycastHit hit,
        out MRUKAnchor anchor,
        MRUKAnchor.SceneLabels labels = (MRUKAnchor.SceneLabels)(~0),
        MRUKRoom room = null)
    {
        room ??= CurrentRoom;
        if (room == null)
        {
            hit = default;
            anchor = null;
            return false;
        }
        return room.Raycast(ray, maxDist, new LabelFilter(labels), out hit, out anchor);
    }

    public Pose? GetPlacementPose(
        Ray ray,
        float maxDist,
        MRUKAnchor.SceneLabels labels = (MRUKAnchor.SceneLabels)(~0),
        MRUK.PositioningMethod positioning = MRUK.PositioningMethod.DEFAULT,
        MRUKRoom room = null)
    {
        room ??= CurrentRoom;
        if (room == null) return null;

        var pose = room.GetBestPoseFromRaycast(
            ray, maxDist,
            new LabelFilter(labels),
            out _,
            out _,
            positioning);

        return pose.position == Vector3.zero && pose.rotation == Quaternion.identity
            ? null
            : pose;
    }

    public MRUKAnchor GetKeyWall(out Vector2 wallScale, MRUKRoom room = null)
    {
        room ??= CurrentRoom;
        wallScale = Vector2.zero;
        return room?.GetKeyWall(out wallScale);
    }

    public Vector3 GetFacingDirection(MRUKAnchor anchor, MRUKRoom room = null)
    {
        room ??= CurrentRoom;
        if (room == null) return Vector3.forward;
        return room.GetFacingDirection(anchor);
    }

    // =========================================================================
    //  Debug Logging
    // =========================================================================

    private void Log(string message, LogType type = LogType.Log)
    {
        if (!debugLogging) return;

        switch (type)
        {
            case LogType.Warning:
                Debug.LogWarning($"{TAG} {message}");
                break;
            case LogType.Error:
                Debug.LogError($"{TAG} {message}");
                break;
            default:
                Debug.Log($"{TAG} {message}");
                break;
        }
    }

    private void LogRoomDetails(MRUKRoom room, string context)
    {
        if (!debugLogging) return;

        var sb = new StringBuilder();
        sb.AppendLine($"{TAG} {context}: {room.name}");
        sb.AppendLine($"  Anchors: {room.Anchors.Count}");
        sb.AppendLine($"  Walls: {room.WallAnchors.Count}");
        sb.AppendLine($"  Floors: {room.FloorAnchors.Count}");
        sb.AppendLine($"  Ceilings: {room.CeilingAnchors.Count}");
        sb.AppendLine($"  GlobalMesh: {(room.GlobalMeshAnchor != null ? "yes" : "no")}");
        sb.AppendLine($"  Bounds: {room.GetRoomBounds()}");

        foreach (var anchor in room.Anchors)
        {
            sb.AppendLine($"  [{anchor.Label}] {anchor.name} — pos:{anchor.transform.position} center:{anchor.GetAnchorCenter()}");
        }

        Debug.Log(sb.ToString());
    }

    private void LogAnchorDetails(MRUKAnchor anchor, string context)
    {
        if (!debugLogging) return;

        var sb = new StringBuilder();
        sb.AppendLine($"{TAG} {context}: {anchor.name}");
        sb.AppendLine($"  Label: {anchor.Label}");
        sb.AppendLine($"  Position: {anchor.transform.position}");
        sb.AppendLine($"  Center: {anchor.GetAnchorCenter()}");
        sb.AppendLine($"  Room: {anchor.Room?.name ?? "null"}");

        if (anchor.PlaneRect.HasValue)
            sb.AppendLine($"  PlaneRect: {anchor.PlaneRect.Value}");
        if (anchor.VolumeBounds.HasValue)
            sb.AppendLine($"  VolumeBounds: {anchor.VolumeBounds.Value}");

        Debug.Log(sb.ToString());
    }
}
