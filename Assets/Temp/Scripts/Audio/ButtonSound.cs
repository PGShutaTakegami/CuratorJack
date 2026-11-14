using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// このボタンを押したときに、通常のクリック音の代わりに鳴らす音（確定ボタンなど）。UISoundPlayer が読む。
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonSound : MonoBehaviour
{
    [SerializeField] private SoundId sound = SoundId.Confirm;

    public SoundId Sound => sound;
}
