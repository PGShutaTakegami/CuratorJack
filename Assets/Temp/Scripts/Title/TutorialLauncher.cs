using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// タイトル画面のチュートリアルボタン。押されたら、自分 + コンピューターのシングルプレイとしてチュートリアルのシーンを開く。
/// タイトルの項目の移動中・モニターへの移動中・モニター表示中・シーンの切り替え中は反応しない。
/// </summary>
public class TutorialLauncher : MonoBehaviour
{
    [SerializeField] private Button button;

    [SerializeField] private TitleSelectManager titleSelect;

    [SerializeField] private string tutorialSceneName = "TutorialScene";

    [Tooltip("自分を含む合計人数")]
    [SerializeField] private int totalPlayers = 4;

    void Awake()
    {
        button.onClick.AddListener(Launch);
    }

    void Launch()
    {
        if (SceneFader.IsFading || titleSelect.IsTitleSelectStopped || titleSelect.IsMoving)
            return;

        SinglePlayContext.Set(totalPlayers);
        SceneFader.LoadScene(tutorialSceneName);
    }
}
