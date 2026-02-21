using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// <summary>
/// Auto-bootstraps itself into the scene via [RuntimeInitializeOnLoadMethod].
/// No need to manually add this to any GameObject.
///
/// On standalone Quest: requests USE_SCENE and USE_ANCHOR_API runtime permissions
/// early in the app lifecycle. MRUK handles its own scene loading via
/// "Load Scene On Startup" with DataSource = DeviceWithPrefabFallback.
/// </summary>
public class ScenePermissionRequester : MonoBehaviour
{
    private const string SCENE_PERMISSION = "com.oculus.permission.USE_SCENE";
    private const string ANCHOR_PERMISSION = "com.oculus.permission.USE_ANCHOR_API";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        var go = new GameObject("[ScenePermissionRequester]");
        DontDestroyOnLoad(go);
        go.AddComponent<ScenePermissionRequester>();
        Debug.Log("[ScenePermission] Auto-created ScenePermissionRequester.");
    }

    private IEnumerator Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        yield return RequestPermissionIfNeeded(SCENE_PERMISSION);
        yield return RequestPermissionIfNeeded(ANCHOR_PERMISSION);
        Debug.Log("[ScenePermission] All permissions checked. MRUK handles scene loading via LoadSceneOnStartup.");
#else
        Debug.Log("[ScenePermission] Running in Editor — no runtime permissions needed.");
        yield return null;
#endif
    }

#if UNITY_ANDROID
    private IEnumerator RequestPermissionIfNeeded(string permission)
    {
        if (Permission.HasUserAuthorizedPermission(permission))
        {
            Debug.Log($"[ScenePermission] {permission} already granted.");
            yield break;
        }

        Debug.Log($"[ScenePermission] Requesting {permission}...");
        Permission.RequestUserPermission(permission);

        float timeout = 30f;
        float elapsed = 0f;
        while (!Permission.HasUserAuthorizedPermission(permission) && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (Permission.HasUserAuthorizedPermission(permission))
            Debug.Log($"[ScenePermission] {permission} granted.");
        else
            Debug.LogWarning($"[ScenePermission] {permission} was NOT granted.");
    }
#endif
}
