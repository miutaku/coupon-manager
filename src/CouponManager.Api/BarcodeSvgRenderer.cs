using System.Text;
using ZXing.Common;

namespace CouponManager.Api;

internal static class BarcodeSvgRenderer
{
    public static string Render(BitMatrix matrix)
    {
        var svg = new StringBuilder(matrix.Width * matrix.Height / 2);
        svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ")
            .Append(matrix.Width).Append(' ').Append(matrix.Height)
            .Append("\" shape-rendering=\"crispEdges\"><rect width=\"100%\" height=\"100%\" fill=\"white\"/><path d=\"");

        for (var row = 0; row < matrix.Height;)
        {
            var rowHeight = CountEqualRows(matrix, row);
            AppendDarkRuns(svg, matrix, row, rowHeight);
            row += rowHeight;
        }

        return svg.Append("\" fill=\"black\"/></svg>").ToString();
    }

    private static int CountEqualRows(BitMatrix matrix, int row)
    {
        var count = 1;
        while (row + count < matrix.Height && RowsEqual(matrix, row, row + count))
        {
            count++;
        }

        return count;
    }

    private static void AppendDarkRuns(StringBuilder svg, BitMatrix matrix, int row, int rowHeight)
    {
        for (var column = 0; column < matrix.Width;)
        {
            if (!matrix[column, row])
            {
                column++;
                continue;
            }

            var start = column;
            while (column < matrix.Width && matrix[column, row])
            {
                column++;
            }

            var width = column - start;
            svg.Append('M').Append(start).Append(' ').Append(row)
                .Append('h').Append(width)
                .Append('v').Append(rowHeight)
                .Append("h-").Append(width).Append('z');
        }
    }

    private static bool RowsEqual(BitMatrix matrix, int first, int second)
    {
        for (var column = 0; column < matrix.Width; column++)
        {
            if (matrix[column, first] != matrix[column, second])
            {
                return false;
            }
        }

        return true;
    }
}
