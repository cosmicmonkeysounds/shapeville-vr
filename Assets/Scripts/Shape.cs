using UnityEngine;
using Meta.XR.MRUtilityKit;

public class Shape : MonoBehaviour
{
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
}
