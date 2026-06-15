using FluentAssertions;
using RotationsPlus.Common.Authorization;

namespace RotationsPlus.Api.Tests;

public class RoleNamesTests
{
    [Fact]
    public void Staff_roles_are_the_five_workforce_roles()
    {
        RoleNames.Staff.Should().BeEquivalentTo(new[]
        {
            RoleNames.Admin, RoleNames.Sales, RoleNames.Sdr, RoleNames.Institution, RoleNames.Coordinator
        });
    }

    [Fact]
    public void Customer_roles_are_student_and_preceptor()
    {
        RoleNames.Customer.Should().BeEquivalentTo(new[] { RoleNames.Student, RoleNames.Preceptor });
    }

    [Fact]
    public void Staff_and_customer_roles_do_not_overlap()
    {
        RoleNames.Staff.Should().NotIntersectWith(RoleNames.Customer);
    }
}
