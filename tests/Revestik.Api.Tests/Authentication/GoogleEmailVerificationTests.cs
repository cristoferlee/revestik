using System.Security.Claims;
using Revestik.Api.Endpoints;

namespace Revestik.Api.Tests.Authentication;

public sealed class GoogleEmailVerificationTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("1", false)]
    [InlineData("", false)]
    public void GoogleVerification_OnlyAcceptsTrue(string value, bool expected)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("urn:google:email_verified", value)], "Google"));
        Assert.Equal(expected, GoogleEmailVerification.IsVerified(principal));
    }

    [Fact]
    public void GoogleVerification_RejectsMissingClaim()
    {
        Assert.False(GoogleEmailVerification.IsVerified(
            new ClaimsPrincipal(new ClaimsIdentity([], "Google"))));
    }
}
