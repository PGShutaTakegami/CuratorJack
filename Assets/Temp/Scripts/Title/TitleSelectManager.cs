using UnityEngine;

public class TitleSelectManager : MonoBehaviour
{
    /// <summary>
    /// Title選択
    /// </summary>
    enum TitleSelect
    {
        /// <summary>
        /// シングルプレイ
        /// </summary>
        OnePlay,

        /// <summary>
        /// マルチプレイ(2~4人プレイ)
        /// </summary>
        MultiPlay,

        /// <summary>
        /// オプション
        /// </summary>
        Option,

        /// <summary>
        /// クレジット
        /// </summary>
        Credit,

        /// <summary>
        /// 終了
        /// </summary>
        Quit
    }

    /// <summary>
    /// Title選択項目
    /// </summary>
    [System.Serializable]
    struct SelectOption
    {
        public Vector3 position;
        public GameObject targetObject;
    }

    [SerializeField] private float slideSpeed;

    /// <summary>
    /// Title選択項目
    /// </summary>
    [SerializeField] private SelectOption[] selectOptions;
}