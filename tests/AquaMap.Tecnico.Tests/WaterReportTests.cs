using AquaMap.Domain.Entities;
using AquaMap.Services;

namespace AquaMap.Tecnico.Tests;

// QA INS-05 / TEC-11: content of the PDF bulletin (the drawing itself is Android-only and checked on a device).
public class WaterReportTests
{
    private static WaterAnalysis Analysis(DateTime utc, double chlorine = 1.0, double ph = 7.0, double turbidity = 1.0,
        double iron = 0.1, bool eColiAbsent = true) => new()
    {
        AnalysisDate = DateTime.SpecifyKind(utc, DateTimeKind.Utc),
        ResidualChlorine = chlorine, Ph = ph, Turbidity = turbidity, Iron = iron, EColiAbsent = eColiAbsent
    };

    [Fact]
    public void Rows_are_most_recent_first_with_local_dates_and_pt_br_numbers()
    {
        var older = Analysis(new DateTime(2026, 10, 1, 12, 0, 0), chlorine: 1.25, ph: 7.2);
        var newer = Analysis(new DateTime(2026, 10, 14, 15, 30, 0), iron: 0.8);

        var report = WaterReport.Build("Guararema", new[] { older, newer }, new DateTime(2026, 10, 20, 9, 0, 0));

        Assert.Equal(2, report.Rows.Count);
        Assert.Equal(newer.AnalysisDateLocal.ToString("dd/MM/yyyy HH:mm"), report.Rows[0].Date);
        Assert.Equal("1,25", report.Rows[1].Chlorine);
        Assert.Equal("7,2", report.Rows[1].Ph);
        Assert.Equal("0,80", report.Rows[0].Iron);
        Assert.Equal("20/10/2026 09:00", report.IssuedAt);
    }

    [Fact]
    public void Out_of_standard_cells_follow_the_domain_rules()
    {
        var report = WaterReport.Build("R", new[] { Analysis(DateTime.UtcNow, chlorine: 0.1, ph: 9.6, turbidity: 6, iron: 0.31, eColiAbsent: false) },
            DateTime.Now);

        var row = report.Rows[0];
        Assert.True(row.ChlorineOut && row.PhOut && row.TurbidityOut && row.IronOut && row.EColiOut);
        Assert.False(row.IsPotable);
        Assert.Equal("Presente", row.EColi);
        Assert.False(report.LatestIsPotable);
        Assert.StartsWith("ÁGUA IMPRÓPRIA", report.Conclusion);
    }

    [Fact]
    public void Within_standard_row_has_no_alert()
    {
        var report = WaterReport.Build("R", new[] { Analysis(DateTime.UtcNow, chlorine: 0.2, ph: 9.5, turbidity: 5, iron: 0.3) }, DateTime.Now);

        var row = report.Rows[0];
        Assert.False(row.ChlorineOut || row.PhOut || row.TurbidityOut || row.IronOut || row.EColiOut);
        Assert.True(report.LatestIsPotable);
        Assert.StartsWith("ÁGUA PRÓPRIA", report.Conclusion);
    }

    [Fact]
    public void Conclusion_uses_the_latest_analysis()
    {
        var badOld = Analysis(new DateTime(2026, 9, 1), iron: 0.9);
        var goodNew = Analysis(new DateTime(2026, 10, 1));

        Assert.True(WaterReport.Build("R", new[] { badOld, goodNew }, DateTime.Now).LatestIsPotable);
    }

    [Fact]
    public void No_analyses_has_no_conclusion_value()
    {
        var report = WaterReport.Build("R", Array.Empty<WaterAnalysis>(), DateTime.Now);

        Assert.Empty(report.Rows);
        Assert.Null(report.LatestIsPotable);
        Assert.Single(report.Paginate(10, 20));
    }

    [Theory]
    [InlineData(" Guararema ", "Boletim_Guararema_20261020.pdf")]
    [InlineData("Alto Universitário (Rua Felício Alcure)", "Boletim_Alto_Universitário_(Rua_Felício_Alcure)_20261020.pdf")]
    [InlineData("a/b\\c:d*e?f\"g<h>i|j", "Boletim_a_b_c_d_e_f_g_h_i_j_20261020.pdf")]
    [InlineData("../../etc", "Boletim_.._.._etc_20261020.pdf")]
    [InlineData("   ", "Boletim_reservatorio_20261020.pdf")]
    public void File_name_is_safe_on_any_platform(string name, string expected)
    {
        Assert.Equal(expected, WaterReport.FileName(name, new DateTime(2026, 10, 20)));
    }

    [Fact]
    public void Long_name_is_cut_in_the_file_name()
    {
        var fileName = WaterReport.FileName(new string('x', 100), new DateTime(2026, 10, 20));

        Assert.Equal("Boletim_".Length + 60 + "_20261020.pdf".Length, fileName.Length);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(29, 1)]
    [InlineData(30, 2)]
    [InlineData(29 + 34, 2)]
    [InlineData(29 + 34 + 1, 3)]
    public void Pagination_keeps_every_row(int rowCount, int expectedPages)
    {
        var history = Enumerable.Range(0, rowCount).Select(i => Analysis(new DateTime(2026, 1, 1).AddDays(i)));
        var report = WaterReport.Build("R", history, DateTime.Now);

        var pages = report.Paginate(29, 34);

        Assert.Equal(expectedPages, pages.Count);
        Assert.Equal(rowCount, pages.Sum(p => p.Count));
        Assert.Equal(report.Rows, pages.SelectMany(p => p));
    }
}
