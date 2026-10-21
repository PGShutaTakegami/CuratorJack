using UnityEngine;

/// <summary>
/// プレイヤー番号から、画面に出す名前を引く。
/// ネットワークではそのプレイヤーが送った名前、ネットワークを使わないときは自分だけロビーで入力した名前を使う。
/// 名前がなければ「Player + 番号」にする。
/// </summary>
public class PlayerNameDirectory : MonoBehaviour
{
    [SerializeField] private PlayerRegistry registry;

    public static string FallbackOf(int player) => $"Player{player}";

    public string NameOf(int player)
    {
        string name = EnteredNameOf(player);
        return string.IsNullOrEmpty(name) ? FallbackOf(player) : name;
    }

    string EnteredNameOf(int player)
    {
        if (!registry.TryGet(player, out Transform body))
            return string.Empty;

        if (body.TryGetComponent(out NetworkSessionPlayer session) && session.IsSpawned)
            return session.DisplayName;

        return player == registry.LocalPlayerNumber ? PlayerNameContext.Current : string.Empty;
    }
}
