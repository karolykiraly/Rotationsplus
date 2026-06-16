using FluentAssertions;
using RotationsPlus.Common.Authorization;

namespace RotationsPlus.Api.Tests;

/// <summary>
/// The cross-directory authorization boundary (staff vs customer) ultimately rests on the staff and
/// customer role-name sets being disjoint: a StaffOnly/AdminOnly policy and a CustomerOnly policy are
/// both pure RequireRole checks, so a role name shared across the two Entra directories would let a
/// token from one directory satisfy the other's policy. This test pins that invariant — if anyone
/// ever adds a role that collides across directories, CI fails here rather than silently opening a
/// privilege path. See Plan_Architecture.md §3.5 and Docs/Review_Process.md.
/// </summary>
public class RoleBoundaryTests
{
    [Fact]
    public void Staff_and_customer_role_sets_are_disjoint()
    {
        RoleNames.Staff.Intersect(RoleNames.Customer).Should().BeEmpty(
            "a role name shared across the workforce and CIAM directories would breach the staff/customer boundary");
    }

    [Fact]
    public void Role_sets_contain_no_internal_duplicates()
    {
        RoleNames.Staff.Should().OnlyHaveUniqueItems();
        RoleNames.Customer.Should().OnlyHaveUniqueItems();
    }
}
