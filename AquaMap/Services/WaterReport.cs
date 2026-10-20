using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AquaMap.Domain.Entities;

namespace AquaMap.Services
{
    /// <summary>One table row of the bulletin: texts already formatted, plus which cells are out of the Portaria 888 standard.</summary>
    public sealed record WaterReportRow(
        string Date, string Chlorine, string Ph, string Turbidity, string Iron, string EColi,
        bool IsPotable, bool ChlorineOut, bool PhOut, bool TurbidityOut, bool IronOut, bool EColiOut);

    /// <summary>
    /// Content of the water quality bulletin, independent of how it is drawn. Standards come from the domain
    /// (<see cref="WaterAnalysis"/>); dates are local and numbers use pt-BR formatting.
    /// </summary>
    public sealed class WaterReport
    {
        public static readonly IReadOnlyList<string> Headers =
            new[] { "Data", "Cloro (mg/L)", "pH", "Turbidez (NTU)", "Ferro (mg/L)", "E. coli" };

        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        public string ReservoirName { get; }
        public string IssuedAt { get; }
        /// <summary>Most recent first.</summary>
        public IReadOnlyList<WaterReportRow> Rows { get; }
        /// <summary>Null when there are no analyses.</summary>
        public bool? LatestIsPotable { get; }

        public string Conclusion => LatestIsPotable switch
        {
            true => "ÁGUA PRÓPRIA PARA CONSUMO: dentro dos padrões da Portaria GM/MS nº 888/2021.",
            false => "ÁGUA IMPRÓPRIA: parâmetros fora dos limites da Portaria GM/MS nº 888/2021.",
            null => "Nenhuma análise registrada para este reservatório."
        };

        private WaterReport(string reservoirName, string issuedAt, IReadOnlyList<WaterReportRow> rows, bool? latestIsPotable)
        {
            ReservoirName = reservoirName;
            IssuedAt = issuedAt;
            Rows = rows;
            LatestIsPotable = latestIsPotable;
        }

        public static WaterReport Build(string reservoirName, IEnumerable<WaterAnalysis> history, DateTime issuedAtLocal)
        {
            var ordered = history.OrderByDescending(a => a.AnalysisDate).ToList();
            var rows = ordered.Select(a => new WaterReportRow(
                a.AnalysisDateLocal.ToString("dd/MM/yyyy HH:mm", PtBr),
                a.ResidualChlorine.ToString("0.00", PtBr),
                a.Ph.ToString("0.0#", PtBr),
                a.Turbidity.ToString("0.0#", PtBr),
                a.Iron.ToString("0.00", PtBr),
                a.EColiAbsent ? "Ausente" : "Presente",
                a.IsPotable,
                !a.IsChlorineValid, !a.IsPhValid, !a.IsTurbidityValid, !a.IsIronValid, !a.EColiAbsent)).ToList();

            return new WaterReport(
                (reservoirName ?? string.Empty).Trim(),
                issuedAtLocal.ToString("dd/MM/yyyy HH:mm", PtBr),
                rows,
                ordered.Count == 0 ? null : ordered[0].IsPotable);
        }

        /// <summary>"Boletim_&lt;nome&gt;_&lt;yyyyMMdd&gt;.pdf" with characters that are invalid in file names (on any platform) replaced.</summary>
        public static string FileName(string reservoirName, DateTime date)
        {
            var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }).ToHashSet();
            var safe = new string((reservoirName ?? string.Empty).Trim()
                .Select(c => invalid.Contains(c) || char.IsWhiteSpace(c) || char.IsControl(c) ? '_' : c).ToArray());
            if (safe.Length > 60) safe = safe[..60];
            if (safe.Trim('_').Length == 0) safe = "reservatorio";
            return $"Boletim_{safe}_{date:yyyyMMdd}.pdf";
        }

        /// <summary>Splits the rows into pages: the first page holds fewer rows because of the header block.</summary>
        public IReadOnlyList<IReadOnlyList<WaterReportRow>> Paginate(int firstPageRows, int otherPageRows)
        {
            if (firstPageRows < 1 || otherPageRows < 1) throw new ArgumentOutOfRangeException(nameof(firstPageRows));
            var pages = new List<IReadOnlyList<WaterReportRow>> { Rows.Take(firstPageRows).ToList() };
            for (int i = firstPageRows; i < Rows.Count; i += otherPageRows)
                pages.Add(Rows.Skip(i).Take(otherPageRows).ToList());
            return pages;
        }
    }
}
