using AquaMap.Domain.Entities;

namespace AquaMap.Domain.Tests;

// QA CID-07 / INS-14: the citizen saw a reservoir in alert on the map and could not find out why.
public class WaterQualityTests
{
    private static WaterAnalysis Analysis(double chlorine = 1.0, double ph = 7.0, double turbidity = 1.0, double iron = 0.1,
        bool eColiAbsent = true, DateTime? utc = null) => new()
    {
        ResidualChlorine = chlorine, Ph = ph, Turbidity = turbidity, Iron = iron, EColiAbsent = eColiAbsent,
        AnalysisDate = DateTime.SpecifyKind(utc ?? new DateTime(2026, 10, 14, 15, 0, 0), DateTimeKind.Utc)
    };

    [Fact]
    public void No_analysis_is_no_data()
    {
        Assert.Equal(WaterStatus.NoData, WaterQuality.Evaluate(WaterQuality.Latest(null)));
        Assert.Equal(WaterStatus.NoData, WaterQuality.Evaluate(WaterQuality.Latest(Array.Empty<WaterAnalysis>())));
        Assert.Null(WaterQuality.LastCollectionDate(null));
        Assert.Equal("Sem análise", WaterQuality.StatusText(WaterStatus.NoData));
    }

    [Fact]
    public void Status_comes_from_the_latest_analysis()
    {
        var oldAlert = Analysis(iron: 0.8, utc: new DateTime(2026, 9, 1));
        var newOk = Analysis(utc: new DateTime(2026, 10, 1));

        Assert.Equal(WaterStatus.Potable, WaterQuality.Evaluate(WaterQuality.Latest(new[] { newOk, oldAlert })));
        Assert.Equal(WaterStatus.Alert, WaterQuality.Evaluate(WaterQuality.Latest(new[] { oldAlert, Analysis(ph: 9.6, utc: new DateTime(2026, 10, 2)) })));
        Assert.Equal("Própria para consumo", WaterQuality.StatusText(WaterStatus.Potable));
        Assert.Equal("Em alerta", WaterQuality.StatusText(WaterStatus.Alert));
    }

    [Fact]
    public void Iron_alert_explains_the_parameter_and_its_limit()
    {
        var issues = WaterQuality.OutOfStandard(Analysis(iron: 0.8));

        var iron = Assert.Single(issues);
        Assert.Equal("Ferro", iron.Name);
        Assert.Equal("0,80 mg/L", iron.Value);
        Assert.Equal("até 0,3 mg/L", iron.Standard);
        Assert.Equal("Ferro: 0,80 mg/L (padrão: até 0,3 mg/L)", iron.Summary);
    }

    [Fact]
    public void Every_out_of_standard_parameter_is_listed()
    {
        var issues = WaterQuality.OutOfStandard(Analysis(chlorine: 0.1, ph: 9.8, turbidity: 6, iron: 0.31, eColiAbsent: false));

        Assert.Equal(new[] { "Cloro residual livre", "pH", "Turbidez", "Ferro", "E. coli" }, issues.Select(i => i.Name));
        Assert.Equal("Presente", issues[^1].Value);
    }

    [Fact]
    public void Compliant_analysis_has_no_issues_and_all_readings()
    {
        var a = Analysis(chlorine: 0.2, ph: 9.5, turbidity: 5, iron: 0.3);

        Assert.Empty(WaterQuality.OutOfStandard(a));
        Assert.Equal(5, WaterQuality.Readings(a).Count);
        Assert.All(WaterQuality.Readings(a), r => Assert.True(r.WithinStandard));
    }

    [Fact]
    public void Standards_shown_come_from_the_domain_limits()
    {
        var readings = WaterQuality.Readings(Analysis());

        Assert.Equal(WaterAnalysisInput.ChlorineStandardText, readings[0].Standard);
        Assert.Equal(WaterAnalysisInput.PhStandardText, readings[1].Standard);
        Assert.Equal(WaterAnalysisInput.TurbidityStandardText, readings[2].Standard);
        Assert.Equal(WaterAnalysisInput.IronStandardText, readings[3].Standard);
    }

    [Fact]
    public void Last_collection_date_is_local()
    {
        var a = Analysis(utc: new DateTime(2026, 10, 14, 1, 30, 0));

        Assert.Equal(a.AnalysisDateLocal.ToString("dd/MM/yyyy"), WaterQuality.LastCollectionDate(a));
    }
}
