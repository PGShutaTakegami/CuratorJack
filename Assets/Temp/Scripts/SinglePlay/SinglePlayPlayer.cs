using UnityEngine;

/// <summary>
/// SinglePlay に参加しているプレイヤー 1 人分（自分 or コンピューター）。
/// </summary>
public class SinglePlayPlayer : MonoBehaviour
{
    public int PlayerNumber { get; private set; }

    public bool IsCpu { get; private set; }

    public void Initialize(int playerNumber, bool isCpu)
    {
        PlayerNumber = playerNumber;
        IsCpu = isCpu;
    }
}
