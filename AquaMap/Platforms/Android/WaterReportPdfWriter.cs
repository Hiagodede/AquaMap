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
    /// and never worked on Android). The system font covers accents. Sizes are PDF points (A4 = 595 x 842);
    /// the vertical layout and the pagination come from <see cref="WaterReportLayout"/>, which is unit tested.
    /// </summary>
    internal static class WaterReportPdfWriter
    {
        private const int PageWidth = 595, PageHeight = 842, Margin = 40;
        private const float FooterTop = PageHeight - 34;
        private static readonly WaterReportLayout Layout = WaterReportLayout.A4;
        private static readonly float[] ColumnWeights = { 2.3f, 1.25f, 0.8f, 1.35f, 1.25f, 1.05f };

        public static void Write(WaterReport report, string filePath)
        {
            var plan = report.Plan(Layout);
            // Created once per export: one Paint per cell meant thousands of Java objects for a long history.
            using var paints = new Paints();
            var document = new APdfDocument();
            try
            {
                for (int i = 0; i < plan.TotalPages; i++)
                {
                    using var pageInfo = new APdfDocument.PageInfo.Builder(PageWidth, PageHeight, i + 1).Create();
                    var page = document.StartPage(pageInfo)!;
                    var canvas = page.Canvas!;

                    float y = i == 0 ? DrawHeader(canvas, report, paints) : DrawContinuationHeader(canvas, report, paints);
                    if (i < plan.Pages.Count && plan.Pages[i].Count > 0)
                        y = DrawTable(canvas, plan.Pages[i], y, paints);
                    if (i == plan.TotalPages - 1)
                        DrawConclusion(canvas, report, y + 18, paints);
                    DrawFooter(canvas, i + 1, plan.TotalPages, paints);

                    document.FinishPage(page);
                }

                using var stream = File.Create(filePath);
                document.WriteTo(stream);
            }
            finally
            {
                document.Close();
                document.Dispose();
            }
        }

        private static float DrawHeader(ACanvas canvas, WaterReport report, Paints p)
        {
            float y = Margin + 16;
            canvas.DrawText("SAAE Alegre/ES · Serviço Autônomo de Água e Esgoto", Margin, y, p.Title);
            y += 20;
            canvas.DrawText("Boletim de Qualidade da Água", Margin, y, p.Subtitle);
            y += 16;
            canvas.DrawText($"Emissão: {report.IssuedAt}", Margin, y, p.Small);
            y += 30;
            canvas.DrawText(Fit($"Reservatório: {report.ReservoirName}", p.Name, PageWidth - 2 * Margin), Margin, y, p.Name);
            return Layout.TableTopFirstPage;
        }

        private static float DrawContinuationHeader(ACanvas canvas, WaterReport report, Paints p)
        {
            canvas.DrawText(Fit($"Boletim de Qualidade da Água · {report.ReservoirName} (continuação)", p.Small, PageWidth - 2 * Margin),
                Margin, Margin + 4, p.Small);
            return Layout.TableTopOtherPages;
        }

        private static float DrawTable(ACanvas canvas, IReadOnlyList<WaterReportRow> rows, float top, Paints p)
        {
            float tableWidth = PageWidth - 2 * Margin;
            float totalWeight = ColumnWeights.Sum();
            var x = new float[ColumnWeights.Length + 1];
            x[0] = Margin;
            for (int c = 0; c < ColumnWeights.Length; c++) x[c + 1] = x[c] + tableWidth * ColumnWeights[c] / totalWeight;

            for (int c = 0; c < WaterReport.Headers.Count; c++)
                canvas.DrawText(Fit(WaterReport.Headers[c], p.TableHeader, x[c + 1] - x[c] - 6), x[c] + 3, top + 13, p.TableHeader);
            canvas.DrawLine(Margin, top + Layout.RowHeight, PageWidth - Margin, top + Layout.RowHeight, p.InkLine);

            float y = top + Layout.RowHeight;
            foreach (var row in rows)
            {
                canvas.DrawRect(Margin, y, PageWidth - Margin, y + Layout.RowHeight, row.IsPotable ? p.OkFill : p.AlertFill);
                string[] cells = { row.Date, row.Chlorine, row.Ph, row.Turbidity, row.Iron, row.EColi };
                bool[] outOfStandard = { false, row.ChlorineOut, row.PhOut, row.TurbidityOut, row.IronOut, row.EColiOut };
                for (int c = 0; c < cells.Length; c++)
                {
                    var paint = outOfStandard[c] ? p.CellAlert : p.Cell;
                    canvas.DrawText(Fit(cells[c], paint, x[c + 1] - x[c] - 6), x[c] + 3, y + 13.5f, paint);
                }
                y += Layout.RowHeight;
                canvas.DrawLine(Margin, y, PageWidth - Margin, y, p.GridLine);
            }
            return y;
        }

        private static void DrawConclusion(ACanvas canvas, WaterReport report, float top, Paints p)
        {
            var pt = CultureInfo.GetCultureInfo("pt-BR");
            canvas.DrawText("Conformidade da última análise", Margin, top + 12, p.SectionTitle);

            var conclusionPaint = report.LatestIsPotable switch { true => p.ConclusionOk, false => p.ConclusionAlert, null => p.ConclusionNone };
            canvas.DrawText(Fit(report.Conclusion, conclusionPaint, PageWidth - 2 * Margin), Margin, top + 32, conclusionPaint);

            // Limits come from the domain, the single source of the Portaria 888 rules.
            string limits = string.Format(pt,
                "Limites: cloro residual livre {0:0.0#} a {1:0.0#} mg/L · pH {2:0.0#} a {3:0.0#} · turbidez até {4:0.0#} NTU · ferro até {5:0.0#} mg/L · E. coli ausente.",
                WaterAnalysis.ChlorineMin, WaterAnalysis.ChlorineMax, WaterAnalysis.PhMin, WaterAnalysis.PhMax,
                WaterAnalysis.TurbidityMax, WaterAnalysis.IronMax);
            canvas.DrawText(Fit(limits, p.Note, PageWidth - 2 * Margin), Margin, top + 52, p.Note);
            canvas.DrawText("Valores em vermelho estão fora do padrão.", Margin, top + 66, p.Note);
        }

        private static void DrawFooter(ACanvas canvas, int page, int total, Paints p)
        {
            canvas.DrawLine(Margin, FooterTop, PageWidth - Margin, FooterTop, p.GridLine);
            canvas.DrawText("Gerado pelo AquaMap · Portaria GM/MS nº 888/2021", Margin, FooterTop + 14, p.Note);
            string pageText = $"Página {page} de {total}";
            canvas.DrawText(pageText, PageWidth - Margin - p.Note.MeasureText(pageText), FooterTop + 14, p.Note);
        }

        // Cuts the text with an ellipsis so it never overflows its column.
        private static string Fit(string text, APaint paint, float maxWidth)
        {
            if (paint.MeasureText(text) <= maxWidth) return text;
            const string ellipsis = "…";
            int count = Math.Max(0, paint.BreakText(text, true, maxWidth - paint.MeasureText(ellipsis), null));
            if (count > 0 && char.IsHighSurrogate(text[count - 1])) count--; // never split an emoji
            return text[..count].TrimEnd() + ellipsis;
        }

        /// <summary>Every Paint and Typeface used by one export.</summary>
        private sealed class Paints : IDisposable
        {
            private static readonly AColor Ink = AColor.ParseColor("#0F172A");
            private static readonly AColor Muted = AColor.ParseColor("#475569");
            private static readonly AColor Brand = AColor.ParseColor("#155E75");
            private static readonly AColor Line = AColor.ParseColor("#CBD5E1");
            private static readonly AColor OkText = AColor.ParseColor("#047857");
            private static readonly AColor AlertText = AColor.ParseColor("#B91C1C");

            private readonly ATypeface _regular = ATypeface.Create(ATypeface.Default, ATypefaceStyle.Normal)!;
            private readonly ATypeface _bold = ATypeface.Create(ATypeface.Default, ATypefaceStyle.Bold)!;
            private readonly List<APaint> _all = new();

            public APaint Title { get; }
            public APaint Subtitle { get; }
            public APaint Small { get; }
            public APaint Name { get; }
            public APaint TableHeader { get; }
            public APaint Cell { get; }
            public APaint CellAlert { get; }
            public APaint SectionTitle { get; }
            public APaint ConclusionOk { get; }
            public APaint ConclusionAlert { get; }
            public APaint ConclusionNone { get; }
            public APaint Note { get; }
            public APaint OkFill { get; }
            public APaint AlertFill { get; }
            public APaint InkLine { get; }
            public APaint GridLine { get; }

            public Paints()
            {
                Title = Text(16, Brand, bold: true);
                Subtitle = Text(12, Muted);
                Small = Text(10, Muted);
                Name = Text(13, Ink, bold: true);
                TableHeader = Text(9, Ink, bold: true);
                Cell = Text(9, Ink);
                CellAlert = Text(9, AlertText, bold: true);
                SectionTitle = Text(12, Ink, bold: true);
                ConclusionOk = Text(10.5f, OkText, bold: true);
                ConclusionAlert = Text(10.5f, AlertText, bold: true);
                ConclusionNone = Text(10.5f, Muted);
                Note = Text(8.5f, Muted);
                OkFill = Fill(AColor.ParseColor("#ECFDF5"));
                AlertFill = Fill(AColor.ParseColor("#FEF2F2"));
                InkLine = Stroke(Ink, 1f);
                GridLine = Stroke(Line, 0.5f);
            }

            private APaint Text(float size, AColor color, bool bold = false)
            {
                var paint = new APaint(APaintFlags.AntiAlias) { TextSize = size, Color = color };
                paint.SetTypeface(bold ? _bold : _regular);
                _all.Add(paint);
                return paint;
            }

            private APaint Fill(AColor color)
            {
                var paint = new APaint { Color = color };
                paint.SetStyle(APaint.Style.Fill);
                _all.Add(paint);
                return paint;
            }

            private APaint Stroke(AColor color, float width)
            {
                var paint = new APaint(APaintFlags.AntiAlias) { Color = color, StrokeWidth = width };
                paint.SetStyle(APaint.Style.Stroke);
                _all.Add(paint);
                return paint;
            }

            public void Dispose()
            {
                foreach (var paint in _all) paint.Dispose();
                _regular.Dispose();
                _bold.Dispose();
            }
        }
    }
}
