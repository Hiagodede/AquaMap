// Aliases para evitar conflito com Microsoft.Maui
using QContainer = QuestPDF.Infrastructure.IContainer;
using QColors = QuestPDF.Helpers.Colors;
using QFonts = QuestPDF.Helpers.Fonts;
using QPageSizes = QuestPDF.Helpers.PageSizes;

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using AquaMap.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace AquaMap.Services
{
    public class PdfExportService
    {
        public PdfExportService()
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        }

        public string GenerateReservoirReport(string reservoirName, IEnumerable<WaterAnalysis> history)
        {
            var historyList = history.ToList();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(QPageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(QColors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily(QFonts.Arial));

                    page.Header().Element(c => ComposeHeader(c));
                    page.Content().Element(c => ComposeContent(c, reservoirName, historyList));
                    page.Footer().Element(c => ComposeFooter(c));
                });
            });

            var docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var safeName = reservoirName.Replace(" ", "_").Replace("/", "-");
            var filePath = Path.Combine(docsPath, $"Boletim_{safeName}_{DateTime.Now:yyyyMMdd}.pdf");

            document.GeneratePdf(filePath);
            return filePath;
        }

        private void ComposeHeader(QContainer c)
        {
            c.Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("SAAE - Serviço Autônomo de Água e Esgoto")
                       .FontSize(18).SemiBold().FontColor(QColors.Blue.Darken2);
                    col.Item().Text("Boletim Técnico de Qualidade da Água")
                       .FontSize(13).FontColor(QColors.Grey.Darken2);
                    col.Item().PaddingTop(4).Text($"Emissão: {DateTime.Now:dd/MM/yyyy HH:mm}");
                });
            });
        }

        private void ComposeContent(QContainer c, string reservoirName, List<WaterAnalysis> history)
        {
            c.PaddingVertical(1, Unit.Centimetre).Column(col =>
            {
                col.Spacing(18);
                col.Item().Text($"Reservatório: {reservoirName}").FontSize(15).SemiBold();

                if (history.Count == 0)
                {
                    col.Item().Text("Nenhuma análise registrada para este reservatório.");
                    return;
                }

                var latest = history.OrderByDescending(x => x.AnalysisDate).First();

                // Tabela de histórico
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2); // Data
                        cols.RelativeColumn();  // Cloro
                        cols.RelativeColumn();  // pH
                        cols.RelativeColumn();  // Turbidez
                        cols.RelativeColumn();  // Ferro
                        cols.RelativeColumn();  // E.Coli
                    });

                    // Cabeçalho
                    table.Header(header =>
                    {
                        foreach (var label in new[] { "Data", "Cloro", "pH", "Turbidez", "Ferro (mg/L)", "E.Coli" })
                        {
                            header.Cell()
                                  .BorderBottom(1).BorderColor(QColors.Black)
                                  .PaddingVertical(5)
                                  .DefaultTextStyle(x => x.SemiBold())
                                  .Text(label);
                        }
                    });

                    // Linhas
                    foreach (var item in history.OrderByDescending(x => x.AnalysisDate))
                    {
                        var rowBorder = item.IsPotable ? QColors.Green.Lighten3 : QColors.Red.Lighten3;

                        table.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).PaddingVertical(4)
                             .Text($"{item.AnalysisDate:dd/MM/yy HH:mm}");
                        table.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).PaddingVertical(4)
                             .Text($"{item.ResidualChlorine:F2}");
                        table.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).PaddingVertical(4)
                             .Text($"{item.Ph:F1}");
                        table.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).PaddingVertical(4)
                             .Text($"{item.Turbidity:F1}");
                        table.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).PaddingVertical(4)
                             .Text($"{item.Iron:F2}");
                        table.Cell().BorderBottom(1).BorderColor(QColors.Grey.Lighten2).PaddingVertical(4)
                             .Text(item.EColiAbsent ? "Ausente" : "Presente");
                    }
                });

                // Conclusão
                col.Item().PaddingTop(20).Text("Conformidade — Última Análise:").FontSize(13).SemiBold();

                var potable = latest.IsPotable;
                col.Item()
                   .Text(potable
                       ? "✓  ÁGUA POTÁVEL — Dentro dos padrões da Portaria 888/2021"
                       : "✗  IMPRÓPRIA — Parâmetros fora dos limites da Portaria 888/2021")
                   .FontSize(12).SemiBold()
                   .FontColor(potable ? QColors.Green.Darken2 : QColors.Red.Darken2);
            });
        }

        private void ComposeFooter(QContainer c)
        {
            c.AlignCenter().Text(x =>
            {
                x.Span("Página ");
                x.CurrentPageNumber();
                x.Span(" de ");
                x.TotalPages();
            });
        }
    }
}
