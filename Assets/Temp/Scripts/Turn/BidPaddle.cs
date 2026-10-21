using System;
using TMPro;
using UnityEngine;

/// <summary>入札の札を上げ下げする動きの設定。位置は肩を基準に、席の向き（右・上・テーブル中央）で表す。</summary>
[Serializable]
public class BidPaddleMotion
{
    [Tooltip("札を上げきるまでの秒数")]
    public float raiseSeconds = 0.5f;

    [Tooltip("札を下ろしきるまでの秒数")]
    public float lowerSeconds = 0.35f;

    [Tooltip("上げきる直前に行き過ぎて戻る強さ（0 で行き過ぎない）")]
    public float overshoot = 1.4f;

    [Tooltip("机の縁の外側を通すための経由点（肩からの 右・上・中央 方向。プレイヤーの大きさ倍）")]
    public Vector3 liftOffset = new Vector3(-0.03f, -0.37f, 0.13f);

    [Tooltip("上げきったときの手首の位置（肩からの 右・上・中央 方向。プレイヤーの大きさ倍）")]
    public Vector3 raisedOffset = new Vector3(0f, 0.13f, 0.67f);

    [Tooltip("肘を曲げる方向（右・上・中央）")]
    public Vector3 elbowDirection = new Vector3(1f, -0.7f, 0f);

    [Tooltip("握った手の指の向き（手首から指の付け根。右・上・中央）。付け根の並びは持ち手に沿って下向き（親指が上）にそろえる")]
    public Vector3 fingerDirection = new Vector3(-1f, 0f, 0f);

    [Tooltip("指を曲げる角度（度。付け根の関節から順に）")]
    public float[] fingerCurl = { 60f, 80f, 60f, 20f };

    [Tooltip("親指を曲げる角度（度。付け根の関節から順に）")]
    public float[] thumbCurl = { 20f, 30f, 30f };

    [Tooltip("下ろしているときの札の傾き（度。正で中央側、負で手前側へ倒す）")]
    public float loweredTilt = -70f;

    [Tooltip("上げきったときの札の傾き（度）")]
    public float raisedTilt = -8f;

    [Tooltip("札の大きさ（モデルそのままが 1）")]
    public float paddleScale = 0.8f;

    [Tooltip("持ち手を握る高さ（札の下端から。モデルの大きさ 1 のとき）")]
    public float gripHeight = 0.3f;

    [Tooltip("数字を描く板の中心の高さと、板の厚みの半分（モデルの大きさ 1 のとき）")]
    public Vector2 boardCenter = new Vector2(0.81f, 0.013f);

    [Tooltip("数字の枠の大きさ（モデルの大きさ 1 のとき）")]
    public Vector2 numberSize = new Vector2(0.42f, 0.3f);

    [Tooltip("掲げたまま数字を変えたときに弾ませる秒数と高さ")]
    public Vector2 bounce = new Vector2(0.25f, 0.06f);
}

/// <summary>
/// 1 人のプレイヤーが右手で握って持つ入札の札（手の甲を机の中央側へ向け、親指を上にして持ち手を握る）。
/// 机の下から縁の外側を通って机の上へ掲げ、板の両面に数字を出す。
/// 表（机の中央側）は他のプレイヤー向け、裏は本人向け。下ろしきったら隠し、腕を元の姿勢に戻す。
/// </summary>
public class BidPaddle : MonoBehaviour
{
    BidPaddleMotion motion;
    Transform player;
    Vector3 tableCenter;
    ArmPoser arm;
    TextMeshPro front;
    TextMeshPro back;

    float progress;
    bool raising;
    float bounceElapsed = float.MaxValue;

    public bool IsShown => raising || progress > 0f;

    /// <summary>数字を描いた板の中心（ワールド）</summary>
    public Vector3 BoardCenter => transform.TransformPoint(new Vector3(0f, motion.boardCenter.x, 0f));

    public static BidPaddle Create(Transform player, Vector3 tableCenter, GameObject paddlePrefab, TMP_FontAsset font, Color numberColor, BidPaddleMotion motion, Transform parent)
    {
        var root = new GameObject($"BidPaddle_{player.name}");
        root.transform.SetParent(parent, false);
        root.transform.localScale = Vector3.one * motion.paddleScale;
        BidPaddle paddle = root.AddComponent<BidPaddle>();
        paddle.motion = motion;
        paddle.player = player;
        paddle.tableCenter = tableCenter;
        paddle.arm = ArmPoser.Find(player, "R");

        Instantiate(paddlePrefab, root.transform, false);
        paddle.front = paddle.CreateNumber("NumberFront", font, numberColor, 1f);
        paddle.back = paddle.CreateNumber("NumberBack", font, numberColor, -1f);

        root.SetActive(false);
        return paddle;
    }

    /// <summary>数字を出して札を掲げる（掲げたままなら数字だけ変えて弾ませる）</summary>
    public void Raise(string number)
    {
        bool wasRaised = raising && progress >= 1f;
        front.text = number;
        back.text = number;
        raising = true;
        gameObject.SetActive(true);
        if (wasRaised)
            bounceElapsed = 0f;
    }

    /// <summary>掲げたまま数字だけ変える（弾ませない）</summary>
    public void SetNumber(string number)
    {
        front.text = number;
        back.text = number;
    }

    public void Lower()
    {
        raising = false;
    }

    void OnDisable()
    {
        arm?.Restore();
    }

    void LateUpdate()
    {
        float seconds = raising ? motion.raiseSeconds : motion.lowerSeconds;
        progress = Mathf.MoveTowards(progress, raising ? 1f : 0f, seconds > 0f ? Time.deltaTime / seconds : 1f);
        bounceElapsed += Time.deltaTime;

        if (!raising && progress <= 0f)
        {
            gameObject.SetActive(false);
            return;
        }

        Pose(raising ? EaseOutBack(progress) : SmoothStep(progress));
    }

    /// <summary>s は 0（机の下）〜 1（掲げきった所）。1 を少し超えると行き過ぎ</summary>
    void Pose(float s)
    {
        Vector3 body = arm != null ? arm.Shoulder : PlayerBody.HeadPosition(player);
        Vector3 forward = tableCenter - body;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0f ? forward.normalized : player.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        float bodyScale = player.lossyScale.y;
        Vector3 Seat(Vector3 offset) => (right * offset.x + Vector3.up * offset.y + forward * offset.z) * bodyScale;

        Vector3 shoulder = arm != null ? body : body + right * 0.5f;
        Vector3 rest = arm != null ? arm.RestWrist : shoulder + Seat(new Vector3(0f, -0.8f, 0.6f));
        Vector3 lift = shoulder + Seat(motion.liftOffset);
        Vector3 raised = shoulder + Seat(motion.raisedOffset);

        float u = 1f - s;
        Vector3 wrist = u * u * rest + 2f * u * s * lift + s * s * raised;
        if (bounceElapsed < motion.bounce.x)
            wrist += Vector3.up * (Mathf.Sin(bounceElapsed / motion.bounce.x * Mathf.PI) * motion.bounce.y * bodyScale);

        float tilt = Mathf.Lerp(motion.loweredTilt, motion.raisedTilt, SmoothStep(Mathf.Clamp01((s - 0.1f) / 0.5f)));
        Quaternion rotation = Quaternion.AngleAxis(tilt, right) * Quaternion.LookRotation(forward, Vector3.up);

        Vector3 grip = wrist;
        if (arm != null)
        {
            Vector3 handle = rotation * Vector3.up;
            Vector3 fingers = Vector3.ProjectOnPlane(Seat(motion.fingerDirection), handle);
            arm.Reach(wrist, Seat(motion.elbowDirection), new ArmPoser.GripPose(fingers, -handle, motion.fingerCurl, motion.thumbCurl));
            grip = arm.Grip;
        }
        transform.SetPositionAndRotation(grip - rotation * Vector3.up * (motion.gripHeight * motion.paddleScale), rotation);
    }

    TextMeshPro CreateNumber(string name, TMP_FontAsset font, Color color, float side)
    {
        var textObject = new GameObject(name);
        textObject.transform.SetParent(transform, false);
        textObject.transform.localPosition = new Vector3(0f, motion.boardCenter.x, motion.boardCenter.y * side);
        textObject.transform.localRotation = side > 0f ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

        TextMeshPro text = textObject.AddComponent<TextMeshPro>();
        text.font = font;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.enableAutoSizing = true;
        text.fontSizeMin = 0.1f;
        text.fontSizeMax = 10f;
        text.rectTransform.sizeDelta = motion.numberSize;
        return text;
    }

    float EaseOutBack(float t)
    {
        float c1 = motion.overshoot;
        float c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    static float SmoothStep(float t) => t * t * (3f - 2f * t);
}
