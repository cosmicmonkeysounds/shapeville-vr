using UnityEngine;
using BNG;
using Meta.XR.MRUtilityKit;

[RequireComponent(typeof(Rigidbody))]
public class Shape : GrabbableEvents
{
    [Header("Reset Settings")]
    [SerializeField] private float resetDelay = 10f;

    private Rigidbody rb;
    private bool hasBeenThrown;
    private float resetTimer;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// Places this shape at a random floating position in the room with no gravity.
    /// Returns false if no valid position was found.
    /// </summary>
    public bool SpawnFloating()
    {
        if (RoomSystem.Instance == null)
        {
            Debug.LogWarning($"[Shape] RoomSystem.Instance is null — cannot spawn {gameObject.name}");
            return false;
        }

        var pos = RoomSystem.Instance.GetRandomFloatingSpawn();
        if (!pos.HasValue)
        {
            Debug.LogWarning($"[Shape] Could not find a floating spawn for {gameObject.name}");
            return false;
        }

        transform.position = pos.Value;
        transform.rotation = Random.rotation;
        MakeFloating();
        return true;
    }

    /// <summary>
    /// Finds a random spawn position on the floor using the MRUK room data
    /// and moves this shape there.
    /// </summary>
    public bool FindSpawnPosition(MRUKRoom room)
    {
        if (room.GenerateRandomPositionOnSurface(
                MRUK.SurfaceType.FACING_UP,
                0.1f,
                new LabelFilter(MRUKAnchor.SceneLabels.FLOOR),
                out Vector3 position,
                out Vector3 normal))
        {
            transform.position = position;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
            return true;
        }

        Debug.LogWarning($"[Shape] Could not find a spawn position for {gameObject.name}");
        return false;
    }

    public override void OnGrab(Grabber grabber)
    {
        base.OnGrab(grabber);

        // Cancel any pending reset
        hasBeenThrown = false;
        resetTimer = 0f;

        // Re-enable gravity so physics feels natural while held
        rb.useGravity = true;
    }

    public override void OnRelease()
    {
        base.OnRelease();

        // Shape has been thrown — start the reset countdown
        hasBeenThrown = true;
        resetTimer = resetDelay;
    }

    private void Update()
    {
        if (!hasBeenThrown) return;

        resetTimer -= Time.deltaTime;
        if (resetTimer <= 0f)
        {
            hasBeenThrown = false;
            SpawnFloating();
        }
    }

    private void MakeFloating()
    {
        rb.useGravity = false;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}
