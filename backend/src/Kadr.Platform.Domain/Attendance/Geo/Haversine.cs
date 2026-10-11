namespace Kadr.Platform.Domain.Attendance.Geo;

/// <summary>Great-circle distance on WGS84 (meters). Pure; no I/O.</summary>
public static class Haversine
{
    private const double EarthRadiusMeters = 6_371_000d;

    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var φ1 = DegreesToRadians(lat1);
        var φ2 = DegreesToRadians(lat2);
        var Δφ = DegreesToRadians(lat2 - lat1);
        var Δλ = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2)
                + Math.Cos(φ1) * Math.Cos(φ2) * Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    public static bool IsInsideRadius(
        double lat1,
        double lon1,
        double lat2,
        double lon2,
        double radiusMeters)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radiusMeters);
        return DistanceMeters(lat1, lon1, lat2, lon2) <= radiusMeters;
    }

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180d);
}
