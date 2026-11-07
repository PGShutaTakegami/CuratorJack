using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤー番号から、そのプレイヤーのアイコン（タイトルで描いた絵）を引く。
/// 自分は保存しているアイコン、ネットワークの他の人はその人から届いたアイコンを使う。CPU やアイコンのない人は持たない。
/// 誰かのアイコンが届いた・変わったら Changed で知らせる。
/// </summary>
public class PlayerIconDirectory : MonoBehaviour
{
    [SerializeField] private PlayerRegistry registry;

    readonly Dictionary<int, Texture2D> lastIcons = new Dictionary<int, Texture2D>();
    readonly Dictionary<Texture2D, Sprite> sprites = new Dictionary<Texture2D, Sprite>();
    readonly List<int> removedPlayers = new List<int>();
    readonly List<Texture2D> removedIcons = new List<Texture2D>();
    Texture2D localIcon;

    /// <summary>誰かのアイコンが届いた・変わった・なくなった</summary>
    public event Action Changed;

    void OnEnable()
    {
        PlayerIconStore.Changed += ReloadLocalIcon;
        ReloadLocalIcon();
    }

    void OnDisable()
    {
        PlayerIconStore.Changed -= ReloadLocalIcon;
    }

    void OnDestroy()
    {
        foreach (Sprite sprite in sprites.Values)
        {
            if (sprite != null)
                Destroy(sprite);
        }
        if (localIcon != null)
            Destroy(localIcon);
    }

    void LateUpdate()
    {
        bool changed = false;
        foreach (int player in registry.PlayerNumbers)
        {
            TryGetIcon(player, out Texture2D icon);
            lastIcons.TryGetValue(player, out Texture2D last);
            if (icon != last)
            {
                lastIcons[player] = icon;
                changed = true;
            }
        }

        removedPlayers.Clear();
        foreach (int player in lastIcons.Keys)
        {
            if (!registry.TryGet(player, out _))
                removedPlayers.Add(player);
        }
        foreach (int player in removedPlayers)
        {
            lastIcons.Remove(player);
            changed = true;
        }

        if (!changed)
            return;

        ForgetDestroyedSprites();
        Changed?.Invoke();
    }

    void ForgetDestroyedSprites()
    {
        removedIcons.Clear();
        foreach (KeyValuePair<Texture2D, Sprite> pair in sprites)
        {
            if (pair.Key == null)
                removedIcons.Add(pair.Key);
        }
        foreach (Texture2D icon in removedIcons)
        {
            if (sprites[icon] != null)
                Destroy(sprites[icon]);
            sprites.Remove(icon);
        }
    }

    public bool TryGetIcon(int player, out Texture2D icon)
    {
        icon = null;
        if (!registry.TryGet(player, out Transform body))
            return false;

        if (player == registry.LocalPlayerNumber)
            icon = localIcon;
        else if (body.TryGetComponent(out NetworkPlayerIcon network) && network.IsSpawned)
            icon = network.Icon;

        return icon != null;
    }

    /// <summary>アイコンを、幅と高さが 1 の大きさのスプライトとして引く</summary>
    public bool TryGetSprite(int player, out Sprite sprite)
    {
        sprite = null;
        if (!TryGetIcon(player, out Texture2D icon))
            return false;

        if (!sprites.TryGetValue(icon, out sprite) || sprite == null)
        {
            sprite = Sprite.Create(icon, new Rect(0f, 0f, icon.width, icon.height), new Vector2(0.5f, 0.5f), Mathf.Max(icon.width, icon.height));
            sprites[icon] = sprite;
        }
        return true;
    }

    void ReloadLocalIcon()
    {
        if (localIcon != null)
            Destroy(localIcon);
        localIcon = PlayerIconStore.CreateTexture(PlayerIconStore.Current);
    }
}
