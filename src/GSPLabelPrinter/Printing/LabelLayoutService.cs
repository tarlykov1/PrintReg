using System.Drawing;
using GSPLabelPrinter.Configuration;
using GSPLabelPrinter.Models;

namespace GSPLabelPrinter.Printing;

public sealed class LabelLayoutService
{
    public void Draw(Graphics graphics, RectangleF bounds, Employee employee, PrintingSettings settings)
    {
        graphics.PageUnit = GraphicsUnit.Display;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var nameSize = FitFontSize(graphics, employee.FullName, bounds.Width, bounds.Height * 0.52f, settings.FullNameFontSize, FontStyle.Bold);
        var positionSize = FitFontSize(graphics, employee.Position, bounds.Width, bounds.Height * 0.36f, settings.PositionFontSize, FontStyle.Regular);

        using var nameFont = new Font("Arial", nameSize, FontStyle.Bold, GraphicsUnit.Point);
        using var positionFont = new Font("Arial", positionSize, FontStyle.Regular, GraphicsUnit.Point);
        using var format = new StringFormat(StringFormatFlags.LineLimit)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.Word
        };

        var nameRect = new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height * 0.56f);
        var positionRect = new RectangleF(bounds.X, bounds.Y + bounds.Height * 0.52f, bounds.Width, bounds.Height * 0.42f);
        graphics.DrawString(employee.FullName, nameFont, Brushes.Black, nameRect, format);
        graphics.DrawString(employee.Position, positionFont, Brushes.Black, positionRect, format);
    }

    private static float FitFontSize(Graphics graphics, string text, float width, float height, float desiredSize, FontStyle style)
    {
        for (var size = desiredSize; size >= 6; size -= 0.5f)
        {
            using var font = new Font("Arial", size, style, GraphicsUnit.Point);
            var measured = graphics.MeasureString(text, font, new SizeF(width, height), StringFormat.GenericTypographic);
            if (measured.Width <= width && measured.Height <= height) return size;
        }
        return 6;
    }
}
