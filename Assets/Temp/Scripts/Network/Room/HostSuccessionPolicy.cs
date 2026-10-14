using System.Collections.Generic;

/// <summary>
/// ホストが抜けたときに、次のホストを誰にするかを決める。
/// クライアント ID は入室順に増えるため、残ったメンバーで最も小さい ID（ホストの次に入った人）を後継とする。
/// </summary>
public static class HostSuccessionPolicy
{
    public static bool IsSuccessor(ulong localClientId, IEnumerable<ulong> memberClientIds, ulong formerHostClientId)
    {
        foreach (ulong memberId in memberClientIds)
        {
            if (memberId != formerHostClientId && memberId < localClientId)
                return false;
        }

        return localClientId != formerHostClientId;
    }
}
