using System.Drawing;
using System.Drawing.Imaging;

namespace StableDiffusionStudio.Infrastructure.Services;

#pragma warning disable CA1416 // Windows-only System.Drawing

public static class GridCompositor
{
    public static byte[] ComposeGrid(
        IReadOnlyList<(string FilePath, int GridX, int GridY)> images,
        IReadOnlyList<string> columnLabels,
        IReadOnlyList<string> rowLabels,
        int cellSize = 256, int labelHeight = 30)
    {
        var cols = columnLabels.Count;
        var rows = Math.Max(rowLabels.Count, 1);
        var hasRowLabels = rowLabels.Count > 0;
        var labelWidth = hasRowLabels ? 120 : 0;

        var totalWidth = labelWidth + cols * cellSize;
        var totalHeight = labelHeight + rows * cellSize;

        using var bitmap = new Bitmap(totalWidth, totalHeight);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.FromArgb(30, 30, 30));

        using var font = new Font("Arial", 10, FontStyle.Bold);
        using var brush = new SolidBrush(Color.White);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        // Column labels
        for (int c = 0; c < cols; c++)
        {
            var rect = new RectangleF(labelWidth + c * cellSize, 0, cellSize, labelHeight);
            g.DrawString(columnLabels[c], font, brush, rect, sf);
        }

        // Row labels
        for (int r = 0; r < rows && hasRowLabels; r++)
        {
            var rect = new RectangleF(0, labelHeight + r * cellSize, labelWidth, cellSize);
            g.DrawString(rowLabels[r], font, brush, rect, sf);
        }

        // Images
        foreach (var (filePath, gridX, gridY) in images)
        {
            if (!File.Exists(filePath)) continue;
            try
            {
                using var img = Image.FromFile(filePath);
                var destRect = new Rectangle(labelWidth + gridX * cellSize, labelHeight + gridY * cellSize, cellSize, cellSize);
                g.DrawImage(img, destRect);
            }
            catch { /* Skip corrupt images */ }
        }

        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}

#pragma warning restore CA1416
