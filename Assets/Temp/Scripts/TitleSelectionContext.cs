/// <summary>
/// タイトルで決定された選択項目を、シーンをまたいで受け渡す。
/// </summary>
public static class TitleSelectionContext
{
    /// <summary>
    /// タイトルで選択が決定されたかどうか
    /// </summary>
    public static bool HasSelection { get; private set; }

    /// <summary>
    /// タイトルで決定された選択項目
    /// </summary>
    public static TitleSelectManager.TitleSelect Selected { get; private set; }

    public static void Set(TitleSelectManager.TitleSelect select)
    {
        Selected = select;
        HasSelection = true;
    }
}
