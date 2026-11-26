using System.Security.Claims;
using Revestik.Api.Authorization;
using Revestik.Shared.Authentication;

namespace Revestik.Api.Tests.Authentication;

public sealed class PublicSessionClassificationTests
{
    private static ClaimsPrincipal Principal(
        bool authenticated,
        params string[] roles) =>
        new(new ClaimsIdentity(
            roles.Select(role =>
                new Claim(ClaimTypes.Role, role)),
            authenticated ? "Test" : null));

    [Theory]
    [InlineData("Administrator")]
    [InlineData("Accountant")]
    [InlineData("Sales")]
    [InlineData("Warehouse")]
    public void Employee_IsNotPublicCustomer(string role)
    {
        var principal = Principal(true, role);

        Assert.True(
            PublicAuthenticationRules.IsEmployee(principal));

        Assert.False(
            PublicAuthenticationRules.IsPublicCustomer(principal));
    }

    [Fact]
    public void PublicCustomer_IsNotEmployee()
    {
        var principal = Principal(
            true, RoleNames.PublicCustomer);

        Assert.True(
            PublicAuthenticationRules.IsPublicCustomer(principal));

        Assert.False(
            PublicAuthenticationRules.IsEmployee(principal));
    }

    [Fact]
    public void MixedRoles_AreNeitherPublicNorEmployee()
    {
        var principal = Principal(
            true,
            RoleNames.PublicCustomer,
            RoleNames.Administrator);

        Assert.False(
            PublicAuthenticationRules.IsEmployee(principal));

        Assert.False(
            PublicAuthenticationRules.IsPublicCustomer(principal));
    }

    [Fact]
    public void MultipleInternalRoles_AreNotValidEmployee()
    {
        var principal = Principal(
            true,
            RoleNames.Accountant,
            RoleNames.Sales);

        Assert.False(
            PublicAuthenticationRules.IsEmployee(principal));

        Assert.False(
            PublicAuthenticationRules.IsPublicCustomer(principal));
    }

    [Theory]
    [InlineData("UnknownRole")]
    [InlineData("PublicCustomer", "UnknownRole")]
    [InlineData("Administrator", "UnknownRole")]
    public void UnknownRoles_AreRejected(
        string first,
        string? second = null)
    {
        var principal = second is null
            ? Principal(true, first)
            : Principal(true, first, second);

        Assert.False(
            PublicAuthenticationRules.IsEmployee(principal));

        Assert.False(
            PublicAuthenticationRules.IsPublicCustomer(principal));
    }

    [Fact]
    public void AnonymousAndRolelessAccounts_AreNotClassified()
    {
        foreach (var principal in new ClaimsPrincipal?[]
        {
            null,
            Principal(false, RoleNames.Administrator),
            Principal(false, RoleNames.PublicCustomer),
            Principal(true)
        })
        {
            Assert.False(
                PublicAuthenticationRules.IsEmployee(principal));

            Assert.False(
                PublicAuthenticationRules.IsPublicCustomer(principal));
        }
    }

    [Fact]
    public void SharedEmployeeClassification_MatchesApiRoles()
    {
        foreach (var role in RoleNames.All)
        {
            Assert.True(
                PublicAuthenticationRules.IsEmployee(
                    Principal(true, role)));
        }

        Assert.False(
            PublicAuthenticationRules.IsEmployee(
                Principal(true, RoleNames.PublicCustomer)));
    }
}
