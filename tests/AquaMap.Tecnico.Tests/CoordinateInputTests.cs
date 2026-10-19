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

    [Fact]
    public void Gps_value_keeps_its_full_precision()
    {
        var result = CoordinateInput.Parse(
            (-20.763612345).ToString(CultureInfo.InvariantCulture),
            (-41.533398765).ToString(CultureInfo.InvariantCulture));

        Assert.Equal(CoordinateInputStatus.Valid, result.Status);
        Assert.Equal(-20.763612345, result.Latitude);
        Assert.Equal(-41.533398765, result.Longitude);
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
