using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// プレイヤーのモデルの片腕を、手首が指定した位置に来るように曲げる（上腕・前腕の 2 本で解く）。
/// 手は指定した向きに回し、指は付け根から順に手のひら側へ関節を曲げて握らせる。
/// このモデルの腕・手・指のボーンは親子になっていないため、手首から先（手・指）は手と一緒にまとめて動かす。
/// 腕のボーンは自分の軸（ローカル Y）が次の関節を向いている前提。
/// </summary>
public sealed class ArmPoser
{
    readonly struct Bone
    {
        public Bone(Transform transform)
        {
            Transform = transform;
            RestPosition = transform.localPosition;
            RestRotation = transform.localRotation;
        }

        public Transform Transform { get; }
        public Vector3 RestPosition { get; }
        public Quaternion RestRotation { get; }

        public Vector3 RestWorldPosition => Transform.parent.TransformPoint(RestPosition);
        public Quaternion RestWorldRotation => Transform.parent.rotation * RestRotation;

        public void Restore() => Transform.SetLocalPositionAndRotation(RestPosition, RestRotation);
    }

    const string ThumbPrefix = "DEF.Thumbs";
    const string FingerPrefix = "DEF.Finger";

    readonly Bone upper;
    readonly Bone lower;
    readonly Bone hand;
    readonly List<List<Bone>> fingers;
    readonly List<Bone> thumb;
    readonly bool isRight;

    /// <summary>手の握り方。向きはワールドの方向で、指の向き（手首から指の付け根）と付け根の並び（人差し指から小指）で決める</summary>
    public readonly struct GripPose
    {
        public GripPose(Vector3 fingerDirection, Vector3 knuckleLine, IReadOnlyList<float> fingerCurl, IReadOnlyList<float> thumbCurl)
        {
            FingerDirection = fingerDirection;
            KnuckleLine = knuckleLine;
            FingerCurl = fingerCurl;
            ThumbCurl = thumbCurl;
        }

        public Vector3 FingerDirection { get; }
        public Vector3 KnuckleLine { get; }

        /// <summary>指を曲げる角度（度。付け根の関節から順に、手のひら側へ）</summary>
        public IReadOnlyList<float> FingerCurl { get; }

        /// <summary>親指を曲げる角度（度。付け根の関節から順に、手のひら側へ）</summary>
        public IReadOnlyList<float> ThumbCurl { get; }
    }

    /// <summary>side は "R" か "L"</summary>
    public static ArmPoser Find(Transform player, string side)
    {
        Transform[] all = player.GetComponentsInChildren<Transform>(true);
        Transform Named(string name) => all.FirstOrDefault(t => t.name == name);

        Transform upper = Named($"DEF.UpArm.{side}");
        Transform lower = Named($"DEF.ForArm.{side}");
        Transform hand = Named($"DEF.Hand.{side}");
        if (upper == null || lower == null || hand == null)
            return null;

        string suffix = $".{side}";
        List<List<Transform>> chains = all
            .Where(t => t.name.EndsWith(suffix) && (t.name.StartsWith(FingerPrefix) || t.name.StartsWith(ThumbPrefix)))
            .GroupBy(t => ChainName(t.name, suffix))
            .Select(group => group.OrderBy(t => JointIndex(t.name, suffix)).ToList())
            .ToList();
        return new ArmPoser(upper, lower, hand, chains, side == "R");
    }

    /// <summary>"DEF.Finger2nd-01.R" → "DEF.Finger2nd"</summary>
    static string ChainName(string name, string suffix)
    {
        string body = name.Substring(0, name.Length - suffix.Length);
        int dash = body.LastIndexOf('-');
        return dash < 0 ? body : body.Substring(0, dash);
    }

    /// <summary>付け根からの順番（"DEF.Finger2nd.R" は 0、"DEF.Finger2nd-02.R" は 2）</summary>
    static int JointIndex(string name, string suffix)
    {
        string body = name.Substring(0, name.Length - suffix.Length);
        int dash = body.LastIndexOf('-');
        return dash >= 0 && int.TryParse(body.Substring(dash + 1), out int index) ? index : 0;
    }

    ArmPoser(Transform upper, Transform lower, Transform hand, List<List<Transform>> chains, bool isRight)
    {
        this.isRight = isRight;
        this.upper = new Bone(upper);
        this.lower = new Bone(lower);
        this.hand = new Bone(hand);
        fingers = chains.Where(chain => chain[0].name.StartsWith(FingerPrefix))
            .Select(chain => chain.Select(t => new Bone(t)).ToList()).ToList();
        thumb = chains.Where(chain => chain[0].name.StartsWith(ThumbPrefix))
            .Select(chain => chain.Select(t => new Bone(t)).ToList()).FirstOrDefault() ?? new List<Bone>();
    }

    public Vector3 Shoulder => upper.RestWorldPosition;

    public Vector3 RestWrist => hand.RestWorldPosition;

    /// <summary>直前の Reach で握った指の内側の中心（持ち手を通す位置）</summary>
    public Vector3 Grip { get; private set; }

    float UpperLength => Vector3.Distance(upper.RestWorldPosition, lower.RestWorldPosition);

    float LowerLength => Vector3.Distance(lower.RestWorldPosition, hand.RestWorldPosition);

    /// <summary>
    /// 手首を wrist へ（届かなければ届く所まで）伸ばし、肘を pole の方へ曲げ、手を grip の形で握らせる。
    /// </summary>
    public void Reach(Vector3 wrist, Vector3 pole, GripPose grip)
    {
        Vector3 shoulder = Shoulder;
        float a = UpperLength;
        float b = LowerLength;

        Vector3 toWrist = wrist - shoulder;
        float distance = Mathf.Clamp(toWrist.magnitude, Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
        Vector3 direction = toWrist.sqrMagnitude > 0f ? toWrist.normalized : Vector3.down;

        Vector3 bend = Vector3.ProjectOnPlane(pole, direction);
        if (bend.sqrMagnitude < 1e-6f)
            bend = Vector3.ProjectOnPlane(Vector3.down, direction);
        bend.Normalize();

        float cos = Mathf.Clamp((a * a + distance * distance - b * b) / (2f * a * distance), -1f, 1f);
        float sin = Mathf.Sqrt(1f - cos * cos);
        Vector3 elbow = shoulder + direction * (a * cos) + bend * (a * sin);
        Vector3 reached = shoulder + direction * distance;

        Quaternion upperTurn = Quaternion.FromToRotation(upper.RestWorldRotation * Vector3.up, elbow - shoulder);
        upper.Transform.SetPositionAndRotation(shoulder, upperTurn * upper.RestWorldRotation);

        Quaternion lowerTurn = Quaternion.FromToRotation(lower.RestWorldRotation * Vector3.up, reached - elbow);
        lower.Transform.SetPositionAndRotation(elbow, lowerTurn * lower.RestWorldRotation);

        Vector3 restWrist = hand.RestWorldPosition;
        RestHandFrame(out Vector3 fingerDirection, out Vector3 knuckleLine, out Vector3 palm);
        Quaternion handTurn = Quaternion.LookRotation(grip.FingerDirection, grip.KnuckleLine)
            * Quaternion.Inverse(Quaternion.LookRotation(fingerDirection, knuckleLine));
        hand.Transform.SetPositionAndRotation(reached, handTurn * hand.RestWorldRotation);

        Vector3 gripSum = Vector3.zero;
        int gripCount = 0;
        foreach (List<Bone> finger in fingers)
        {
            foreach (Vector3 joint in Curl(finger, grip.FingerCurl, palm, handTurn, reached, restWrist))
            {
                gripSum += joint;
                gripCount++;
            }
        }
        Curl(thumb, grip.ThumbCurl, palm, handTurn, reached, restWrist);
        Grip = gripCount > 0 ? gripSum / gripCount : reached;
    }

    /// <summary>
    /// 元の姿勢での手の向き（ワールド）。指の向き＝手首から指の付け根、付け根の並び＝人差し指から小指、
    /// 手のひら＝この 2 つに垂直で、右手と左手で向きが逆になる側。
    /// </summary>
    void RestHandFrame(out Vector3 fingerDirection, out Vector3 knuckleLine, out Vector3 palm)
    {
        Vector3 wrist = hand.RestWorldPosition;
        Vector3 rotationUp = hand.RestWorldRotation * Vector3.up;
        fingerDirection = fingers.Count > 0
            ? (fingers.Aggregate(Vector3.zero, (sum, finger) => sum + finger[0].RestWorldPosition) / fingers.Count - wrist).normalized
            : rotationUp;

        List<Bone> index = fingers.FirstOrDefault(finger => finger[0].Transform.name.StartsWith(FingerPrefix + "1st"));
        List<Bone> pinky = fingers.FirstOrDefault(finger => finger[0].Transform.name.StartsWith(FingerPrefix + "4th"));
        knuckleLine = index != null && pinky != null
            ? Vector3.ProjectOnPlane(pinky[0].RestWorldPosition - index[0].RestWorldPosition, fingerDirection).normalized
            : hand.RestWorldRotation * Vector3.right;

        palm = Vector3.Cross(fingerDirection, knuckleLine).normalized * (isRight ? -1f : 1f);
    }

    /// <summary>
    /// 指の関節を付け根から順に手のひら側へ曲げる（曲げた分は先の関節にも引き継ぐ）。曲げた後の関節の位置を返す。
    /// 曲げる軸は指ごとに元の姿勢から決めて固定する（関節が進んでも反り返らない）。
    /// </summary>
    static List<Vector3> Curl(List<Bone> chain, IReadOnlyList<float> angles, Vector3 restPalm, Quaternion handTurn, Vector3 wrist, Vector3 restWrist)
    {
        var joints = new List<Vector3>(chain.Count);
        if (chain.Count == 0)
            return joints;

        Vector3 restDirection = chain.Count > 1
            ? chain[1].RestWorldPosition - chain[0].RestWorldPosition
            : chain[0].RestWorldRotation * Vector3.up;
        Vector3 axis = handTurn * Vector3.Cross(restDirection, restPalm).normalized;

        Quaternion turn = handTurn;
        Vector3 joint = Vector3.zero;
        for (int i = 0; i < chain.Count; i++)
        {
            Bone bone = chain[i];
            joint = i == 0
                ? wrist + handTurn * (bone.RestWorldPosition - restWrist)
                : joint + turn * (bone.RestWorldPosition - chain[i - 1].RestWorldPosition);

            float angle = i < angles.Count ? angles[i] : 0f;
            turn = Quaternion.AngleAxis(angle, axis) * turn;
            bone.Transform.SetPositionAndRotation(joint, turn * bone.RestWorldRotation);
            joints.Add(joint);
        }
        return joints;
    }

    public void Restore()
    {
        upper.Restore();
        lower.Restore();
        hand.Restore();
        foreach (Bone bone in fingers.SelectMany(finger => finger).Concat(thumb))
            bone.Restore();
    }
}
