using UnityEngine;

/// <summary>
/// 手札を広げたときの並べ方。1 ページに最大 columns 枚 × rowsPerPage 列を並べ、入りきらない分は次のページにする。
/// ページ内の列は上から古い順に埋め、各列の枚数はできるだけそろえる（各列は中央揃え）。
/// 一番下の列の中心を基準位置に合わせ、表示中のページ以外は横に pageWidth ずつずらした画面の外に置く。
/// 列の幅が収まる幅を超える場合は、間隔を詰めて幅に収める。
/// </summary>
public readonly struct CardSpreadLayout
{
    readonly Vector2 bottomRowCenter;
    readonly Vector2 cardSize;
    readonly float stepX;
    readonly float stepY;
    readonly int columns;
    readonly int rowsPerPage;
    readonly int count;
    readonly int page;
    readonly float pageWidth;

    public CardSpreadLayout(Vector2 bottomRowCenter, Vector2 cardSize, float spacing, float maxRowWidth,
        int columns, int rowsPerPage, int count, int page, float pageWidth)
    {
        this.bottomRowCenter = bottomRowCenter;
        this.cardSize = cardSize;
        this.columns = Mathf.Max(1, columns);
        this.rowsPerPage = Mathf.Max(1, rowsPerPage);
        this.count = count;
        this.pageWidth = pageWidth;

        float preferredStep = cardSize.x + spacing;
        float fittedStep = this.columns > 1 ? (maxRowWidth - cardSize.x) / (this.columns - 1) : preferredStep;
        stepX = Mathf.Max(0f, Mathf.Min(preferredStep, fittedStep));
        stepY = cardSize.y + spacing;

        int perPage = this.columns * this.rowsPerPage;
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(count / (float)perPage));
        this.page = Mathf.Clamp(page, 0, pageCount - 1);
    }

    int PerPage => columns * rowsPerPage;

    public int PageCount => Mathf.Max(1, Mathf.CeilToInt(count / (float)PerPage));

    /// <summary>表示中のページ（範囲内に収めたもの）</summary>
    public int Page => page;

    public Vector2 PositionAt(int index)
    {
        int cardPage = index / PerPage;
        int slot = index % PerPage;
        int onPage = Mathf.Min(PerPage, count - cardPage * PerPage);
        int rows = RowsFor(onPage);
        int perRow = PerRowFor(onPage, rows);
        int row = slot / perRow;
        int column = slot % perRow;
        int inRow = Mathf.Min(perRow, onPage - row * perRow);

        float x = (column - (inRow - 1) * 0.5f) * stepX + (cardPage - page) * pageWidth;
        float y = (rows - 1 - row) * stepY;
        return bottomRowCenter + new Vector2(x, y);
    }

    /// <summary>index のカードの下に、同じページの別の列があるか</summary>
    public bool HasRowBelow(int index)
    {
        int cardPage = index / PerPage;
        int onPage = Mathf.Min(PerPage, count - cardPage * PerPage);
        int rows = RowsFor(onPage);
        return index % PerPage / PerRowFor(onPage, rows) < rows - 1;
    }

    /// <summary>表示中のページの一番上の列の右上</summary>
    public Vector2 TopRight()
    {
        int onPage = Mathf.Max(1, Mathf.Min(PerPage, count - page * PerPage));
        int rows = RowsFor(onPage);
        int inTopRow = Mathf.Min(PerRowFor(onPage, rows), onPage);
        float x = (inTopRow - 1) * 0.5f * stepX;
        float y = (rows - 1) * stepY;
        return bottomRowCenter + new Vector2(x, y) + cardSize * 0.5f;
    }

    /// <summary>count 枚を並べたときの列の数（rowsPerPage まで）</summary>
    public static int RowsFor(int count, int columns, int rowsPerPage)
    {
        return Mathf.Clamp(Mathf.CeilToInt(count / (float)Mathf.Max(1, columns)), 1, Mathf.Max(1, rowsPerPage));
    }

    int RowsFor(int onPage) => RowsFor(onPage, columns, rowsPerPage);

    static int PerRowFor(int onPage, int rows) => Mathf.Max(1, Mathf.CeilToInt(onPage / (float)rows));
}
