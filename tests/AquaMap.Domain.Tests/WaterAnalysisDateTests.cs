using AquaMap.Domain.Entities;

namespace AquaMap.Domain.Tests;

// QA EST-10 / INS-16: dates were stored in UTC but displayed without conversion (+3 h in Alegre-ES).
public class WaterAnalysisDateTests
{
    private static readonly DateTime UtcNoon = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Utc_date_is_displayed_in_local_time()
    {
        var a = new WaterAnalysis { AnalysisDate = UtcNoon };

        Assert.Equal(UtcNoon.ToLocalTime(), a.AnalysisDateLocal);
        Assert.Equal(DateTimeKind.Local, a.AnalysisDateLocal.Kind);
    }

    [Fact]
    public void Unspecified_date_from_sqlite_is_treated_as_utc()
    {
        var a = new WaterAnalysis { AnalysisDate = DateTime.SpecifyKind(UtcNoon, DateTimeKind.Unspecified) };

        Assert.Equal(UtcNoon.ToLocalTime(), a.AnalysisDateLocal);
        Assert.Equal(DateTimeKind.Local, a.AnalysisDateLocal.Kind);
    }

    [Fact]
    public void Local_display_date_is_not_serialized_by_the_api()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new WaterAnalysis { AnalysisDate = UtcNoon });

        Assert.DoesNotContain("AnalysisDateLocal", json);
    }
}
