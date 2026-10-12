using Kadr.Platform.Domain.Attendance;

namespace Kadr.Platform.Tests.Attendance;

public sealed class WorkLocationTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static WorkLocation Make(
        int radius = 150,
        double lat = 30.0,
        double lon = 31.0,
        WorkLocationType type = WorkLocationType.ExternalSite,
        Guid? linkedBranchId = null,
        DateOnly? from = null,
        DateOnly? to = null,
        bool active = true) =>
        WorkLocation.Create(TenantId, "موقع", "Site", type, linkedBranchId, lat, lon, radius, null, from, to, active, null);

    [Theory]
    [InlineData(49)]
    [InlineData(1001)]
    public void Radius_outside_bounds_is_rejected(int radius) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Make(radius: radius));

    [Theory]
    [InlineData(50)]
    [InlineData(1000)]
    public void Radius_on_bounds_is_accepted(int radius) =>
        Assert.Equal(radius, Make(radius: radius).GeofenceRadiusMeters);

    [Theory]
    [InlineData(91, 31)]
    [InlineData(30, 181)]
    [InlineData(double.NaN, 31)]
    public void Invalid_coordinates_are_rejected(double lat, double lon) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Make(lat: lat, lon: lon));

    [Fact]
    public void External_site_cannot_link_to_a_branch() =>
        Assert.Throws<ArgumentException>(() => Make(linkedBranchId: Guid.NewGuid()));

    [Fact]
    public void Validity_window_is_respected()
    {
        var location = Make(from: new DateOnly(2026, 1, 10), to: new DateOnly(2026, 1, 20));
        Assert.False(location.IsValidOn(new DateOnly(2026, 1, 9)));
        Assert.True(location.IsValidOn(new DateOnly(2026, 1, 10)));
        Assert.True(location.IsValidOn(new DateOnly(2026, 1, 20)));
        Assert.False(location.IsValidOn(new DateOnly(2026, 1, 21)));
    }

    [Fact]
    public void Inactive_location_is_never_valid() =>
        Assert.False(Make(active: false).IsValidOn(new DateOnly(2026, 1, 10)));

    [Fact]
    public void Assignment_target_must_match_scope()
    {
        var location = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => WorkLocationAssignment.Create(
            TenantId, location, WorkLocationAssignmentScope.Employee, null, Guid.NewGuid(), null, null, null));
        Assert.Throws<ArgumentException>(() => WorkLocationAssignment.Create(
            TenantId, location, WorkLocationAssignmentScope.Branch, Guid.NewGuid(), null, Guid.NewGuid(), null, null));

        var ok = WorkLocationAssignment.Create(
            TenantId, location, WorkLocationAssignmentScope.Department, null, Guid.NewGuid(), null, null, null);
        Assert.Equal(WorkLocationAssignmentScope.Department, ok.Scope);
    }
}
