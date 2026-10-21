using TMPro;
using UnityEngine;

/// <summary>
/// 上部のバーに今の周回数を表示する（ゲーム開始前は 1）。
/// </summary>
public class RoundCounterView : MonoBehaviour
{
    [SerializeField] private TurnFlowController turnFlow;

    [SerializeField] private TMP_Text label;

    void Awake()
    {
        Show(1);
        turnFlow.RoundStarted += Show;
    }

    void OnDestroy()
    {
        turnFlow.RoundStarted -= Show;
    }

    void Show(int round)
    {
        label.text = round.ToString();
    }
}
