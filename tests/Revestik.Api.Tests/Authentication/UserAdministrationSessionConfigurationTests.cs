using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Revestik.Api.Tests.Hosting;

namespace Revestik.Api.Tests.Authentication;

public sealed class UserAdministrationSessionConfigurationTests
{
    [Fact]
    public void Authentication_RevalidatesSecurityStampEveryMinute()
    {
        using var factory = new RevestikWebApplicationFactory("Development");
        var options = factory.Services.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value;
        Assert.Equal(TimeSpan.FromMinutes(1), options.ValidationInterval);
    }
}
