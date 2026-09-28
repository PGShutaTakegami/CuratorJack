using System;
using UnityEngine;

/// <summary>
/// カメラをモニター画面の正面へ移動させ、画面全体にモニターが映る位置で停止する。
/// ズーム前の位置を記憶し、そこへ戻ることもできる。
/// </summary>
public class MonitorZoomCamera : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;

    [SerializeField] private float duration = 1.5f;

    [Tooltip("1 で画面幅・高さにぴったり合わせる。小さいほど寄って縁をはみ出させる")]
    [SerializeField, Range(0.5f, 1f)] private float fillRatio = 0.95f;

    [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    Vector3 startPosition;
    Quaternion startRotation;
    Vector3 endPosition;
    Quaternion endRotation;
    Vector3 originPosition;
    Quaternion originRotation;
    bool hasOrigin;
    float elapsed;
    Action onCompleted;

    /// <summary>
    /// ズーム中かどうか
    /// </summary>
    public bool IsPlaying { get; private set; }

    /// <summary>
    /// ズーム対象のカメラ
    /// </summary>
    public Camera TargetCamera => targetCamera;

    /// <summary>
    /// screenRenderer の正面へズームを開始し、到着後に onCompleted を呼ぶ
    /// </summary>
    public void Play(Renderer screenRenderer, Action onCompleted)
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null || screenRenderer == null || !TryCalculateEndPose(screenRenderer, out Vector3 position, out Quaternion rotation))
        {
            onCompleted?.Invoke();
            return;
        }

        originPosition = targetCamera.transform.position;
        originRotation = targetCamera.transform.rotation;
        hasOrigin = true;
        BeginMove(position, rotation, onCompleted);
    }

    /// <summary>
    /// Play 開始前の位置へ戻り、到着後に onCompleted を呼ぶ
    /// </summary>
    public void ReturnToOrigin(Action onCompleted)
    {
        if (targetCamera == null || !hasOrigin)
        {
            onCompleted?.Invoke();
            return;
        }

        BeginMove(originPosition, originRotation, onCompleted);
    }

    void BeginMove(Vector3 position, Quaternion rotation, Action completed)
    {
        Transform cameraTransform = targetCamera.transform;
        startPosition = cameraTransform.position;
        startRotation = cameraTransform.rotation;
        endPosition = position;
        endRotation = rotation;
        elapsed = 0f;
        onCompleted = completed;
        IsPlaying = true;
    }

    void Update()
    {
        if (!IsPlaying)
            return;

        elapsed += Time.deltaTime;
        float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
        float eased = easing.Evaluate(t);

        Transform cameraTransform = targetCamera.transform;
        cameraTransform.SetPositionAndRotation(
            Vector3.LerpUnclamped(startPosition, endPosition, eased),
            Quaternion.SlerpUnclamped(startRotation, endRotation, eased));

        if (t < 1f)
            return;

        IsPlaying = false;
        Action completed = onCompleted;
        onCompleted = null;
        completed?.Invoke();
    }

    /// <summary>
    /// 画面メッシュの中心・向き・大きさから、画面全体を覆うカメラ姿勢を求める
    /// </summary>
    bool TryCalculateEndPose(Renderer screenRenderer, out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = default;

        Mesh mesh = null;
        bool isBakedMesh = false;
        if (screenRenderer is SkinnedMeshRenderer skinned)
        {
            mesh = new Mesh();
            skinned.BakeMesh(mesh, true);
            isBakedMesh = true;
        }
        else if (screenRenderer.TryGetComponent(out MeshFilter meshFilter))
        {
            mesh = meshFilter.sharedMesh;
        }

        if (mesh == null || mesh.vertexCount == 0)
        {
            if (isBakedMesh)
                Destroy(mesh);
            return false;
        }

        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Matrix4x4 localToWorld = screenRenderer.transform.localToWorldMatrix;

        Vector3 center = Vector3.zero;
        Vector3 normalSum = Vector3.zero;
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = localToWorld.MultiplyPoint3x4(vertices[i]);
            center += vertices[i];
            if (i < normals.Length)
                normalSum += localToWorld.MultiplyVector(normals[i]);
        }
        center /= vertices.Length;

        if (isBakedMesh)
            Destroy(mesh);

        // メッシュの法線が裏返っていても、カメラ側を画面の正面とする
        Vector3 facing = normalSum.sqrMagnitude > 0f ? normalSum.normalized : (targetCamera.transform.position - center).normalized;
        if (Vector3.Dot(facing, targetCamera.transform.position - center) < 0f)
            facing = -facing;

        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, facing).normalized;
        if (up.sqrMagnitude < 0.0001f)
            up = Vector3.ProjectOnPlane(Vector3.forward, facing).normalized;
        Vector3 right = Vector3.Cross(up, facing);

        float halfWidth = 0f;
        float halfHeight = 0f;
        foreach (Vector3 vertex in vertices)
        {
            Vector3 offset = vertex - center;
            halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(offset, right)));
            halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(offset, up)));
        }

        float tanHalfFov = Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float distanceForHeight = halfHeight / tanHalfFov;
        float distanceForWidth = halfWidth / (tanHalfFov * targetCamera.aspect);
        float distance = Mathf.Min(distanceForHeight, distanceForWidth) * fillRatio;
        distance = Mathf.Max(distance, targetCamera.nearClipPlane * 1.1f);

        position = center + facing * distance;
        rotation = Quaternion.LookRotation(-facing, up);
        return true;
    }
}
