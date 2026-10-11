using Kadr.Platform.Domain.Attendance.Geo;

namespace Kadr.Platform.Tests.Attendance;

public sealed class HaversineTests
{
    // Tahrir Square approx
    private const double OriginLat = 30.0444;
    private const double OriginLon = 31.2357;

    [Fact]
    public void Same_point_is_zero()
    {
        Assert.Equal(0, Haversine.DistanceMeters(OriginLat, OriginLon, OriginLat, OriginLon), 3);
    }

    [Fact]
    public void Known_short_distance_is_near_111_meters_per_0_001_deg_lat()
    {
        // ~111.2 m per 0.001° latitude
        var d = Haversine.DistanceMeters(OriginLat, OriginLon, OriginLat + 0.001, OriginLon);
        Assert.InRange(d, 105, 120);
    }

    [Theory]
    [InlineData(60, true)]
    [InlineData(100, true)]
    [InlineData(10, false)]
    public void Radius_boundary(double radius, bool expectedInside)
    {
        // Point ~55 m north of origin
        var lat2 = OriginLat + (55d / 111_200d);
        var inside = Haversine.IsInsideRadius(OriginLat, OriginLon, lat2, OriginLon, radius);
        Assert.Equal(expectedInside, inside);
    }

    [Fact]
    public void Just_inside_and_just_outside_100m()
    {
        var justInside = OriginLat + (99d / 111_200d);
        var justOutside = OriginLat + (101d / 111_200d);
        Assert.True(Haversine.IsInsideRadius(OriginLat, OriginLon, justInside, OriginLon, 100));
        Assert.False(Haversine.IsInsideRadius(OriginLat, OriginLon, justOutside, OriginLon, 100));
    }
}
