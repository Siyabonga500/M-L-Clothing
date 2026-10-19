namespace ClothingStore.Services;

public static class Geo
{
    // Roads are rarely straight; this turns straight-line distance into a rough road distance.
    private const double RoadFactor = 1.35;

    public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371;
        double dLat = ToRad(lat2 - lat1), dLon = ToRad(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    public static (double km, int minutes) Estimate(double fromLat, double fromLon, double toLat, double toLon, double speedKmh)
    {
        var km = HaversineKm(fromLat, fromLon, toLat, toLon) * RoadFactor;
        var minutes = (int)Math.Ceiling(km / Math.Max(5, speedKmh) * 60) + 5; // + 5 min to park and hand over
        return (Math.Round(km, 1, MidpointRounding.AwayFromZero), minutes);
    }

    private static double ToRad(double deg) => deg * Math.PI / 180;
}
