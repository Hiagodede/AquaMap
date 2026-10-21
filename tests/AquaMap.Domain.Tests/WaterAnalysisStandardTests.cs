using AquaMap.Domain.Entities;

namespace AquaMap.Domain.Tests;

// Portaria GM/MS 888/2021 limits as currently configured (single source: the domain). Values unchanged by this PR.
public class WaterAnalysisStandardTests
{
    private static WaterAnalysis Potable() => new()
    {
        ResidualChlorine = 1.0, Ph = 7.0, Turbidity = 1.0, Iron = 0.1, EColiAbsent = true
    };

    [Theory]
    [InlineData(0.2, true)]
    [InlineData(5.0, true)]
    [InlineData(0.19, false)]
    [InlineData(5.01, false)]
    public void Chlorine_limits(double value, bool expected)
    {
        var a = Potable();
        a.ResidualChlorine = value;
        Assert.Equal(expected, a.IsChlorineValid);
        Assert.Equal(expected, a.IsPotable);
    }

    [Theory]
    [InlineData(6.0, true)]
    [InlineData(9.5, true)]
    [InlineData(5.99, false)]
    [InlineData(9.51, false)]
    public void Ph_limits(double value, bool expected)
    {
        var a = Potable();
        a.Ph = value;
        Assert.Equal(expected, a.IsPhValid);
        Assert.Equal(expected, a.IsPotable);
    }

    [Theory]
    [InlineData(5.0, true)]
    [InlineData(5.01, false)]
    public void Turbidity_limits(double value, bool expected)
    {
        var a = Potable();
        a.Turbidity = value;
        Assert.Equal(expected, a.IsTurbidityValid);
        Assert.Equal(expected, a.IsPotable);
    }

    [Theory]
    [InlineData(0.3, true)]
    [InlineData(0.31, false)]
    public void Iron_limits(double value, bool expected)
    {
        var a = Potable();
        a.Iron = value;
        Assert.Equal(expected, a.IsIronValid);
        Assert.Equal(expected, a.IsPotable);
    }

    [Fact]
    public void Ecoli_presence_makes_water_improper()
    {
        var a = Potable();
        a.EColiAbsent = false;
        Assert.False(a.IsPotable);
    }
}
