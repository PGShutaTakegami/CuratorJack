using UnityEngine;

/// <summary>
/// 手札を広げたときの並べ方。列の中心を基準位置に合わせて横一列に並べ、1 枚目を左端にする。
/// 収まる幅を超える場合は、間隔を詰めて幅に収める。
/// </summary>
public readonly struct CardSpreadLayout
{
    readonly Vector2 center;
    readonly float step;
    readonly int count;

    public CardSpreadLayout(Vector2 center, float cardWidth, float spacing, float maxRowWidth, int count)
    {
        this.center = center;
        this.count = count;

        float preferredStep = cardWidth + spacing;
        float fittedStep = count > 1 ? (maxRowWidth - cardWidth) / (count - 1) : preferredStep;
        step = Mathf.Max(0f, Mathf.Min(preferredStep, fittedStep));
    }

    public Vector2 PositionAt(int index)
    {
        float offsetFromCenter = (index - (count - 1) * 0.5f) * step;
        return center + Vector2.right * offsetFromCenter;
    }
}
