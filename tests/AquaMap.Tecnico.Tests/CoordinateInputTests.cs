using System.Globalization;
using AquaMap.Helpers;

namespace AquaMap.Tecnico.Tests;

// QA EST-12: after a GPS fix, coordinates typed by the technician were ignored and the GPS position was saved.
// The forms now read only the fields, which GPS and the map picker fill in the same way the technician does.
public class CoordinateInputTests
{
    [Fact]
    public void Value_typed_after_a_gps_fix_is_the_one_saved()
    {
        // QA scenario: GPS gave -20.7636/-41.5333, then -20.7700/-41.5400 was typed over it in the fields.
        var result = CoordinateInput.Parse(CoordinateInput.Sanitize("-20.7700"), CoordinateInput.Sanitize("-41.5400"));

        Assert.Equal(CoordinateInputStatus.Valid, result.Status);
        Assert.Equal(-20.77, result.Latitude, 6);
        Assert.Equal(-41.54, result.Longitude, 6);
    }

    // GPS/map values are written with at most 6 decimals: they fit the field (MaxLength 12) and the text the
    // technician sees parses back to exactly the value that is saved.
    [Theory]
    [InlineData(-20.763612345, "-20.763612")]
    [InlineData(-41.5333, "-41.5333")]
    [InlineData(-180.0, "-180")]
    [InlineData(-179.9999999, "-180")]
    public void Format_writes_what_the_field_shows_and_saves(double value, string expected)
    {
        string text = CoordinateInput.Format(value);

        Assert.Equal(expected, text);
        Assert.True(text.Length <= 12);
        Assert.Equal(text, CoordinateInput.Sanitize(text));
        Assert.Equal(double.Parse(text, CultureInfo.InvariantCulture), CoordinateInput.Parse("0", text).Longitude);
    }

    [Fact]
    public void Format_ignores_the_device_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            Assert.Equal("-20.7636", CoordinateInput.Format(-20.7636));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("-20,7636", "-41,5333")]   // pt-BR keyboard
    [InlineData(" -20.7636 ", "-41.5333")]
    public void Comma_and_spaces_are_accepted(string latitude, string longitude)
    {
        var result = CoordinateInput.Parse(latitude, longitude);

        Assert.Equal(CoordinateInputStatus.Valid, result.Status);
        Assert.Equal(-20.7636, result.Latitude, 6);
        Assert.Equal(-41.5333, result.Longitude, 6);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("  ", " ")]
    public void Both_blank_is_empty(string? latitude, string? longitude)
    {
        Assert.Equal(CoordinateInputStatus.Empty, CoordinateInput.Parse(latitude, longitude).Status);
    }

    [Theory]
    [InlineData("-20.7636", "")]   // only one field filled
    [InlineData("", "-41.5333")]
    [InlineData("-", "-41.5333")]  // typing in progress
    [InlineData(".", "-41.5333")]
    [InlineData("-90.1", "0")]     // outside the ranges the API enforces
    [InlineData("90.1", "0")]
    [InlineData("0", "-180.1")]
    [InlineData("0", "180.1")]
    [InlineData("1e400", "0")]     // infinity
    public void Partial_unparseable_or_out_of_range_is_invalid(string latitude, string longitude)
    {
        Assert.Equal(CoordinateInputStatus.Invalid, CoordinateInput.Parse(latitude, longitude).Status);
    }

    [Theory]
    [InlineData("-90", "-180")]
    [InlineData("90", "180")]
    public void Range_limits_are_valid(string latitude, string longitude)
    {
        Assert.Equal(CoordinateInputStatus.Valid, CoordinateInput.Parse(latitude, longitude).Status);
    }

    [Theory]
    [InlineData("-20,7636", "-20.7636")]
    [InlineData("-20.76361234", "-20.763612")]  // at most 6 decimals (~10 cm)
    [InlineData("a-20.7b6", "-20.76")]          // letters dropped
    [InlineData("20-.7", "20.7")]               // "-" only at the start
    [InlineData("--20..7", "-20.7")]
    public void Sanitize_keeps_a_typeable_number(string input, string expected)
    {
        Assert.Equal(expected, CoordinateInput.Sanitize(input));
    }
}
