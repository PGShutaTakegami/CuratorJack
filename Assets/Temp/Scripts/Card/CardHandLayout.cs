using UnityEngine;

/// <summary>
/// 手札を重ねたときの並べ方。基準位置（右端）から、一定の割合で重ねながら左へ並べる。
/// </summary>
public readonly struct CardHandLayout
{
    readonly Vector2 firstPosition;
    readonly float step;

    /// <param name="overlapRatio">隣のカードに重なる割合（0.5 で半分重なる）</param>
    public CardHandLayout(Vector2 firstPosition, float cardWidth, float overlapRatio)
    {
        this.firstPosition = firstPosition;
        step = cardWidth * (1f - Mathf.Clamp01(overlapRatio));
    }

    /// <param name="slotFromRight">右端から数えた位置（0 が右端）</param>
    public Vector2 PositionAt(int slotFromRight)
    {
        return firstPosition + Vector2.left * (step * slotFromRight);
    }
}
