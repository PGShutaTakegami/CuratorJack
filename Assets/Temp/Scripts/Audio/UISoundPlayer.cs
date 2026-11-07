using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面のボタンが押されたときにクリック音を鳴らす。マウス・キーボード・コントローラーのどれで押しても Button.onClick を通るため、そこに音を足す。
/// 表示中のボタンを毎フレーム調べて未登録のものに音を足すので、後から生成されたボタン（ルーム一覧の項目など）にも鳴る。
/// ButtonSound が付いたボタンは、クリック音の代わりにその音を鳴らす。音は SoundEffectPlayer が鳴らす。
/// </summary>
public class UISoundPlayer : MonoBehaviour
{
    static UISoundPlayer instance;

    readonly HashSet<Button> boundButtons = new HashSet<Button>();
    Selectable[] selectables = new Selectable[64];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null)
            return;

        instance = new GameObject(nameof(UISoundPlayer)).AddComponent<UISoundPlayer>();
        DontDestroyOnLoad(instance.gameObject);
    }

    /// <summary>
    /// Button を通らない決定（3D のメニュー、キーで直接操作する画面など）でクリック音を鳴らす
    /// </summary>
    public static void PlayClick()
    {
        SoundEffectPlayer.Play(SoundId.UIClick);
    }

    void Update()
    {
        if (Selectable.allSelectableCount > selectables.Length)
            selectables = new Selectable[Mathf.NextPowerOfTwo(Selectable.allSelectableCount)];

        int count = Selectable.AllSelectablesNoAlloc(selectables);
        for (int i = 0; i < count; i++)
        {
            if (selectables[i] is Button button && boundButtons.Add(button))
                Bind(button);
        }
        System.Array.Clear(selectables, 0, count);

        if (Time.frameCount % 600 == 0)
            boundButtons.RemoveWhere(button => button == null);
    }

    static void Bind(Button button)
    {
        SoundId sound = button.TryGetComponent(out ButtonSound custom) ? custom.Sound : SoundId.UIClick;
        button.onClick.AddListener(() => SoundEffectPlayer.Play(sound));
    }
}
