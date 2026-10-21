using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AquaMap.Domain.Entities
{
    public enum WaterStatus { NoData, Potable, Alert }

    /// <summary>One measured parameter as shown to people: formatted value, the Portaria 888 standard and whether it complies.</summary>
    public sealed record ParameterReading(string Name, string Value, string Standard, bool WithinStandard)
    {
        /// <summary>"Ferro: 0,80 mg/L (padrão: até 0,3 mg/L)".</summary>
        public string Summary => $"{Name}: {Value} (padrão: {Standard})";
    }

    /// <summary>
    /// Status and explanation of a reservoir's water, for every screen and both apps. The rules themselves live in
    /// <see cref="WaterAnalysis"/>; this only puts them into words (pt-BR).
    /// </summary>
    public static class WaterQuality
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        public static WaterAnalysis? Latest(IEnumerable<WaterAnalysis>? analyses) =>
            analyses?.OrderByDescending(a => a.AnalysisDate).FirstOrDefault();

        public static WaterStatus Evaluate(WaterAnalysis? latest) =>
            latest == null ? WaterStatus.NoData : latest.IsPotable ? WaterStatus.Potable : WaterStatus.Alert;

        public static string StatusText(WaterStatus status) => status switch
        {
            WaterStatus.Potable => "Própria para consumo",
            WaterStatus.Alert => "Em alerta",
            _ => "Sem análise"
        };

        /// <summary>All parameters of one analysis, in the order they are shown.</summary>
        public static IReadOnlyList<ParameterReading> Readings(WaterAnalysis a) => new[]
        {
            new ParameterReading("Cloro residual livre", $"{a.ResidualChlorine.ToString("0.00", PtBr)} mg/L", WaterAnalysisInput.ChlorineStandardText, a.IsChlorineValid),
            new ParameterReading("pH", a.Ph.ToString("0.0#", PtBr), WaterAnalysisInput.PhStandardText, a.IsPhValid),
            new ParameterReading("Turbidez", $"{a.Turbidity.ToString("0.0#", PtBr)} NTU", WaterAnalysisInput.TurbidityStandardText, a.IsTurbidityValid),
            new ParameterReading("Ferro", $"{a.Iron.ToString("0.00", PtBr)} mg/L", WaterAnalysisInput.IronStandardText, a.IsIronValid),
            new ParameterReading("E. coli", a.EColiAbsent ? "Ausente" : "Presente", "ausente", a.EColiAbsent),
        };

        /// <summary>Only the parameters that put the water in alert; empty when it complies.</summary>
        public static IReadOnlyList<ParameterReading> OutOfStandard(WaterAnalysis a) =>
            Readings(a).Where(r => !r.WithinStandard).ToList();

        /// <summary>"14/10/2026" in local time, or null without analyses.</summary>
        public static string? LastCollectionDate(WaterAnalysis? latest) =>
            latest?.AnalysisDateLocal.ToString("dd/MM/yyyy", PtBr);
    }
}
