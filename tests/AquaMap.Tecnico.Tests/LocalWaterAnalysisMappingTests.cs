using System.Reflection;
using AquaMap.Domain.Entities;
using AquaMap.Models;

namespace AquaMap.Tecnico.Tests;

// QA EST-05 / INS-04: iron and the collection GPS were dropped between the SQLite cache and the
// domain, so offline a reservoir with iron 0.8 mg/L showed "Água potável".
public class LocalWaterAnalysisMappingTests
{
    private static WaterAnalysis Sample() => new()
    {
        Id = 42,
        AnalysisDate = new DateTime(2026, 10, 14, 18, 16, 23, DateTimeKind.Utc),
        IsPendingSync = true,
        ResidualChlorine = 1.1,
        Ph = 7.3,
        Turbidity = 2.2,
        EColiAbsent = true,
        Iron = 0.8,
        CollectionLatitude = -20.7636,
        CollectionLongitude = -41.5333,
        ReservoirId = 7
    };

    // Every settable property of WaterAnalysis must survive the round-trip. A new field added to the
    // domain and forgotten in FromDomain/ToDomain makes this test fail.
    [Fact]
    public void Round_trip_preserves_every_domain_field()
    {
        var original = Sample();

        var restored = LocalWaterAnalysis.FromDomain(original, original.IsPendingSync).ToDomain();

        var fields = typeof(WaterAnalysis)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.Name != nameof(WaterAnalysis.Reservoir));
        foreach (var field in fields)
        {
            Assert.True(Equals(field.GetValue(original), field.GetValue(restored)), $"Campo perdido no cache: {field.Name}");
        }
    }

    [Fact]
    public void Iron_above_standard_keeps_the_reservoir_in_alert_offline()
    {
        var local = LocalWaterAnalysis.FromDomain(Sample());

        Assert.False(local.IsPotable);
        Assert.False(local.ToDomain().IsPotable);
    }

    [Fact]
    public void Date_read_back_from_sqlite_is_utc()
    {
        // sqlite-net returns DateTime with Kind Unspecified.
        var local = LocalWaterAnalysis.FromDomain(Sample());
        local.AnalysisDate = DateTime.SpecifyKind(local.AnalysisDate, DateTimeKind.Unspecified);

        var domain = local.ToDomain();

        Assert.Equal(DateTimeKind.Utc, domain.AnalysisDate.Kind);
        Assert.Equal(Sample().AnalysisDate, domain.AnalysisDate);
    }
}
