using AquaMap.Domain.Entities;

namespace AquaMap.Domain.Tests;

public class WaterAnalysisInputTests
{
    private static WaterAnalysisInputResult Validate(
        string chlorine = "1.0", string ph = "7.2", string turbidity = "2.0", string iron = "0.1", bool? eColiAbsent = true)
        => WaterAnalysisInput.Validate(chlorine, ph, turbidity, iron, eColiAbsent);

    [Fact]
    public void Normal_reading_is_valid_and_potable()
    {
        var result = Validate();

        Assert.True(result.IsValid);
        Assert.Empty(result.OutOfStandard);
        Assert.True(result.Analysis!.IsPotable);
    }

    // QA EST-01 / INS-01: these readings were blocked by the app, so improper water was never recorded.
    [Theory]
    [InlineData("0.05", "7.2", "2.0", "0.1", true, "Cloro")]
    [InlineData("1.0", "10", "2.0", "0.1", true, "pH")]
    [InlineData("1.0", "7.2", "8", "0.1", true, "Turbidez")]
    [InlineData("1.0", "7.2", "2.0", "0.8", true, "Ferro")]
    [InlineData("1.0", "7.2", "2.0", "0.1", false, "E. coli")]
    public void Out_of_standard_reading_is_accepted_and_flagged(string chlorine, string ph, string turbidity, string iron, bool eColiAbsent, string flagged)
    {
        var result = Validate(chlorine, ph, turbidity, iron, eColiAbsent);

        Assert.True(result.IsValid);
        Assert.False(result.Analysis!.IsPotable);
        var issue = Assert.Single(result.OutOfStandard);
        Assert.StartsWith(flagged, issue);
    }

    // QA EST-06 / INS-08: an empty iron field became 0 and counted as potable.
    [Fact]
    public void Missing_iron_is_rejected_instead_of_becoming_zero()
    {
        var result = Validate(iron: "");

        Assert.False(result.IsValid);
        Assert.Contains("ferro", result.Error!);
    }

    // QA EST-06 / INS-09: E. coli defaulted to "absent".
    [Fact]
    public void Missing_ecoli_result_is_rejected()
    {
        var result = Validate(eColiAbsent: null);

        Assert.False(result.IsValid);
        Assert.Contains("E. coli", result.Error!);
    }

    // QA INS-03: iron -5 was accepted locally, rejected by the API and stuck as "pending" forever.
    [Theory]
    [InlineData("-0.1", "7.2", "2.0", "0.1")]
    [InlineData("1.0", "15", "2.0", "0.1")]
    [InlineData("1.0", "-1", "2.0", "0.1")]
    [InlineData("1.0", "7.2", "-1", "0.1")]
    [InlineData("1.0", "7.2", "2.0", "-5")]
    public void Physically_impossible_reading_is_rejected(string chlorine, string ph, string turbidity, string iron)
    {
        var result = Validate(chlorine, ph, turbidity, iron);

        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Physical_limits_are_inclusive()
    {
        var result = Validate(chlorine: "20", ph: "14", turbidity: "1000", iron: "100");

        Assert.True(result.IsValid);
        Assert.Equal(4, result.OutOfStandard.Count);
    }

    [Fact]
    public void Every_out_of_standard_parameter_is_listed()
    {
        var result = Validate("0.05", "10", "8", "0.8", eColiAbsent: false);

        Assert.True(result.IsValid);
        Assert.Equal(5, result.OutOfStandard.Count);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1e3")]
    [InlineData("1 000")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData(" ")]
    public void Non_numeric_reading_is_rejected(string chlorine)
    {
        Assert.False(Validate(chlorine: chlorine).IsValid);
    }

    [Fact]
    public void Comma_and_dot_decimal_separators_are_equivalent()
    {
        var comma = Validate(chlorine: "1,5", ph: "7,2");
        var dot = Validate(chlorine: "1.5", ph: "7.2");

        Assert.Equal(dot.Analysis!.ResidualChlorine, comma.Analysis!.ResidualChlorine);
        Assert.Equal(1.5, comma.Analysis.ResidualChlorine);
        Assert.Equal(7.2, comma.Analysis.Ph);
    }

    [Theory]
    [InlineData(1.0, null)]
    [InlineData(0.05, "Fora do padrão da Portaria 888 (0,2 a 5,0 mg/L). Será registrado como alerta.")]
    [InlineData(-1.0, "Cloro residual deve estar entre 0 e 20 mg/L.")]
    public void Live_hint_distinguishes_alert_from_impossible_value(double chlorine, string? expected)
    {
        Assert.Equal(expected, WaterAnalysisInput.DescribeChlorine(chlorine));
    }

    [Fact]
    public void Physical_limits_match_the_api_messages()
    {
        Assert.Equal("pH deve estar entre 0 e 14.", WaterAnalysisInput.GetPhysicalLimitError(1, 14.1, 0, 0));
        Assert.Equal("Ferro deve estar entre 0 e 100 mg/L.", WaterAnalysisInput.GetPhysicalLimitError(1, 7, 0, -5));
        Assert.Null(WaterAnalysisInput.GetPhysicalLimitError(0, 0, 0, 0));
    }
}
