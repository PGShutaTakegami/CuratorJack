using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// EventSystem にボタンを選ばれたままにしない。
/// クリックしたボタンが選ばれたままだと、スティック・十字キーの Move で選択が隣のボタンへ移り、A（Submit）でそのボタンが押されてしまう
/// （押していないインフォメーションが開く、確定や入札が二重に効く）。決定や選択は各画面が自前で扱うため、ボタンの選択は毎フレーム外す。
/// 文字の入力欄は選ばれていないと入力できないため外さない。起動時に生成してシーンをまたいで常駐させる。
/// </summary>
public class ButtonSelectionGuard : MonoBehaviour
{
    static ButtonSelectionGuard instance;

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

        instance = new GameObject(nameof(ButtonSelectionGuard)).AddComponent<ButtonSelectionGuard>();
        DontDestroyOnLoad(instance.gameObject);
    }

    void LateUpdate()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected != null && selected.TryGetComponent(out Button _))
            eventSystem.SetSelectedGameObject(null);
    }
}
