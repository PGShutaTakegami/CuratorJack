using System.Collections;
using UnityEngine;

/// <summary>
/// 制作中の項目の画面。表示されたら、画面のフェードインが終わってから一定時間見せて、タイトルへ戻る。
/// 待っている間も、戻る入力でタイトルへ戻れる（MonitorManager が受け付ける）。
/// </summary>
public class UnderConstructionScreen : MonoBehaviour
{
    [SerializeField] private MonitorManager monitor;

    [Tooltip("フェードインが終わったかを見る、画面全体の CanvasGroup")]
    [SerializeField] private CanvasGroup screenGroup;

    [Tooltip("フェードインが終わってからタイトルへ戻るまでの秒数")]
    [SerializeField] private float displaySeconds = 2f;

    void OnEnable()
    {
        StartCoroutine(ReturnAfterDisplay());
    }

    IEnumerator ReturnAfterDisplay()
    {
        // フェードインは最初のフレームで始まるため、それを待ってから完了を見る
        yield return null;
        if (screenGroup != null)
            yield return new WaitUntil(() => screenGroup.alpha >= 1f);
        yield return new WaitForSecondsRealtime(displaySeconds);
        monitor.ReturnToTitle();
    }
}
