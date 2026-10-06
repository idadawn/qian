using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Runtime.Errors.Tests;

public sealed class QianErrorsExtensionsTests
{
    [Fact]
    public void Repeated_calls_share_one_catalog_and_one_exception_handler()
    {
        var services = new ServiceCollection();
        services.AddQianErrors(c => c.Register(ErrorCatalogTests.QuotaExceeded));
        services.AddQianErrors();

        using var provider = services.BuildServiceProvider();
        var catalog = Assert.Single(provider.GetServices<ErrorCatalog>());
        Assert.True(catalog.Contains(ErrorCatalogTests.QuotaExceeded));
        Assert.True(catalog.Contains(PlatformErrors.NotFound));
        Assert.Single(services, d => d.ServiceType == typeof(IExceptionHandler));
    }

    [Fact]
    public void A_catalog_registered_by_factory_is_rejected()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_ => new ErrorCatalog());

        Assert.Throws<InvalidOperationException>(() => services.AddQianErrors());
    }
}
