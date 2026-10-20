using System.Globalization;
using AquaMap.Domain.Entities;
using AColor = Android.Graphics.Color;
using ACanvas = Android.Graphics.Canvas;
using APaint = Android.Graphics.Paint;
using APaintFlags = Android.Graphics.PaintFlags;
using ATypeface = Android.Graphics.Typeface;
using ATypefaceStyle = Android.Graphics.TypefaceStyle;
using APdfDocument = Android.Graphics.Pdf.PdfDocument;

namespace AquaMap.Services
{
    /// <summary>
    /// Draws the bulletin with Android's own PdfDocument (QA INS-05: QuestPDF ships Linux-only native libraries
    /// and never worked on Android). The system font covers accents. Sizes are PDF points (A4 = 595 x 842).
    /// </summary>
    internal static class WaterReportPdfWriter
    {
        private const int PageWidth = 595, PageHeight = 842, Margin = 40;
        private const float RowHeight = 20f, TableTopFirstPage = 178f, TableTopOtherPages = 64f, FooterTop = PageHeight - 34;
        private const float ConclusionHeight = 92f;
        private static readonly float[] ColumnWeights = { 2.3f, 1.25f, 0.8f, 1.35f, 1.25f, 1.05f };

        private static readonly AColor Ink = AColor.ParseColor("#0F172A");
        private static readonly AColor Muted = AColor.ParseColor("#475569");
        private static readonly AColor Brand = AColor.ParseColor("#155E75");
        private static readonly AColor Line = AColor.ParseColor("#CBD5E1");
        private static readonly AColor OkFill = AColor.ParseColor("#ECFDF5");
        private static readonly AColor AlertFill = AColor.ParseColor("#FEF2F2");
        private static readonly AColor OkText = AColor.ParseColor("#047857");
        private static readonly AColor AlertText = AColor.ParseColor("#B91C1C");

        public static void Write(WaterReport report, string filePath)
        {
            int RowsThatFit(float top) => (int)((FooterTop - 10 - top - RowHeight) / RowHeight);
            int firstRows = RowsThatFit(TableTopFirstPage), otherRows = RowsThatFit(TableTopOtherPages);

            var pages = report.Paginate(firstRows, otherRows).ToList();
            // The conclusion needs room below the last table; otherwise it gets a page of its own.
            float lastTop = pages.Count == 1 ? TableTopFirstPage : TableTopOtherPages;
            bool conclusionOnNewPage = lastTop + RowHeight * (pages[^1].Count + 1) + ConclusionHeight > FooterTop - 10;
            int totalPages = pages.Count + (conclusionOnNewPage ? 1 : 0);

            using var document = new APdfDocument();
            for (int i = 0; i < totalPages; i++)
            {
                using var pageInfo = new APdfDocument.PageInfo.Builder(PageWidth, PageHeight, i + 1).Create();
                var page = document.StartPage(pageInfo)!;
                var canvas = page.Canvas!;

                float y = i == 0 ? DrawHeader(canvas, report) : DrawContinuationHeader(canvas, report);
                if (i < pages.Count && pages[i].Count > 0)
                    y = DrawTable(canvas, pages[i], y);
                if (i == totalPages - 1)
                    DrawConclusion(canvas, report, y + 18);
                DrawFooter(canvas, i + 1, totalPages);

                document.FinishPage(page);
            }

            using var stream = File.Create(filePath);
            document.WriteTo(stream);
            document.Close();
        }

        private static float DrawHeader(ACanvas canvas, WaterReport report)
        {
            float y = Margin + 16;
            canvas.DrawText("SAAE Alegre/ES · Serviço Autônomo de Água e Esgoto", Margin, y, TextPaint(16, Brand, bold: true));
            y += 20;
            canvas.DrawText("Boletim de Qualidade da Água", Margin, y, TextPaint(12, Muted));
            y += 16;
            canvas.DrawText($"Emissão: {report.IssuedAt}", Margin, y, TextPaint(10, Muted));
            y += 30;
            var namePaint = TextPaint(13, Ink, bold: true);
            canvas.DrawText(Fit($"Reservatório: {report.ReservoirName}", namePaint, PageWidth - 2 * Margin), Margin, y, namePaint);
            return TableTopFirstPage;
        }

        private static float DrawContinuationHeader(ACanvas canvas, WaterReport report)
        {
            var paint = TextPaint(10, Muted);
            canvas.DrawText(Fit($"Boletim de Qualidade da Água · {report.ReservoirName} (continuação)", paint, PageWidth - 2 * Margin),
                Margin, Margin + 4, paint);
            return TableTopOtherPages;
        }

        private static float DrawTable(ACanvas canvas, IReadOnlyList<WaterReportRow> rows, float top)
        {
            float tableWidth = PageWidth - 2 * Margin;
            float totalWeight = ColumnWeights.Sum();
            var x = new float[ColumnWeights.Length + 1];
            x[0] = Margin;
            for (int c = 0; c < ColumnWeights.Length; c++) x[c + 1] = x[c] + tableWidth * ColumnWeights[c] / totalWeight;

            var headerPaint = TextPaint(9, Ink, bold: true);
            for (int c = 0; c < WaterReport.Headers.Count; c++)
                canvas.DrawText(Fit(WaterReport.Headers[c], headerPaint, x[c + 1] - x[c] - 6), x[c] + 3, top + 13, headerPaint);
            canvas.DrawLine(Margin, top + RowHeight, PageWidth - Margin, top + RowHeight, StrokePaint(Ink, 1f));

            float y = top + RowHeight;
            foreach (var row in rows)
            {
                canvas.DrawRect(Margin, y, PageWidth - Margin, y + RowHeight, FillPaint(row.IsPotable ? OkFill : AlertFill));
                string[] cells = { row.Date, row.Chlorine, row.Ph, row.Turbidity, row.Iron, row.EColi };
                bool[] outOfStandard = { false, row.ChlorineOut, row.PhOut, row.TurbidityOut, row.IronOut, row.EColiOut };
                for (int c = 0; c < cells.Length; c++)
                {
                    var paint = outOfStandard[c] ? TextPaint(9, AlertText, bold: true) : TextPaint(9, Ink);
                    canvas.DrawText(Fit(cells[c], paint, x[c + 1] - x[c] - 6), x[c] + 3, y + 13.5f, paint);
                }
                y += RowHeight;
                canvas.DrawLine(Margin, y, PageWidth - Margin, y, StrokePaint(Line, 0.5f));
            }
            return y;
        }

        private static void DrawConclusion(ACanvas canvas, WaterReport report, float top)
        {
            var pt = CultureInfo.GetCultureInfo("pt-BR");
            canvas.DrawText("Conformidade da última análise", Margin, top + 12, TextPaint(12, Ink, bold: true));

            var color = report.LatestIsPotable switch { true => OkText, false => AlertText, null => Muted };
            var conclusionPaint = TextPaint(10.5f, color, bold: report.LatestIsPotable.HasValue);
            canvas.DrawText(Fit(report.Conclusion, conclusionPaint, PageWidth - 2 * Margin), Margin, top + 32, conclusionPaint);

            // Limits come from the domain, the single source of the Portaria 888 rules.
            string limits = string.Format(pt,
                "Limites: cloro residual livre {0:0.0} a {1:0.0} mg/L · pH {2:0.0} a {3:0.0} · turbidez até {4:0.0} NTU · ferro até {5:0.0} mg/L · E. coli ausente.",
                WaterAnalysis.ChlorineMin, WaterAnalysis.ChlorineMax, WaterAnalysis.PhMin, WaterAnalysis.PhMax,
                WaterAnalysis.TurbidityMax, WaterAnalysis.IronMax);
            var limitsPaint = TextPaint(8.5f, Muted);
            canvas.DrawText(Fit(limits, limitsPaint, PageWidth - 2 * Margin), Margin, top + 52, limitsPaint);
            canvas.DrawText("Valores em vermelho estão fora do padrão.", Margin, top + 66, limitsPaint);
        }

        private static void DrawFooter(ACanvas canvas, int page, int total)
        {
            canvas.DrawLine(Margin, FooterTop, PageWidth - Margin, FooterTop, StrokePaint(Line, 0.5f));
            var paint = TextPaint(8.5f, Muted);
            canvas.DrawText("Gerado pelo AquaMap · Portaria GM/MS nº 888/2021", Margin, FooterTop + 14, paint);
            string pageText = $"Página {page} de {total}";
            canvas.DrawText(pageText, PageWidth - Margin - paint.MeasureText(pageText), FooterTop + 14, paint);
        }

        // Cuts the text with an ellipsis so it never overflows its column.
        private static string Fit(string text, APaint paint, float maxWidth)
        {
            if (paint.MeasureText(text) <= maxWidth) return text;
            const string ellipsis = "…";
            int count = paint.BreakText(text, true, maxWidth - paint.MeasureText(ellipsis), null);
            return text[..Math.Max(0, count)].TrimEnd() + ellipsis;
        }

        private static APaint TextPaint(float size, AColor color, bool bold = false)
        {
            var paint = new APaint(APaintFlags.AntiAlias) { TextSize = size, Color = color };
            paint.SetTypeface(ATypeface.Create(ATypeface.Default, bold ? ATypefaceStyle.Bold : ATypefaceStyle.Normal));
            return paint;
        }

        private static APaint StrokePaint(AColor color, float width)
        {
            var paint = new APaint(APaintFlags.AntiAlias) { Color = color, StrokeWidth = width };
            paint.SetStyle(APaint.Style.Stroke);
            return paint;
        }

        private static APaint FillPaint(AColor color)
        {
            var paint = new APaint { Color = color };
            paint.SetStyle(APaint.Style.Fill);
            return paint;
        }
    }
}
