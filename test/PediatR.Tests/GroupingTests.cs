namespace PediatR.Tests
{
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using PediatR.Tests.Grouping.Orders;   // BOTH feature namespaces imported at once…
    using PediatR.Tests.Grouping.Products;
    using Xunit;

    /// <summary>
    /// Two features in different namespaces both expose a <c>GetAll</c> query. With a flat
    /// <c>sender.GetAll()</c> extension this would be an ambiguous call (CS0121); grouping the
    /// methods under a per-host proxy keeps the call sites unambiguous — the point Commandor's
    /// service-grouped proxy solved, reproduced here with a classic (netstandard2.0-safe) extension.
    /// </summary>
    public class GroupingTests
    {
        [Fact]
        public async Task Same_named_queries_in_different_features_dispatch_without_ambiguity()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ProductCatalog>();
            services.AddSingleton<OrderLedger>();
            services.AddPediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<GroupingTests>());
            var sender = services.BuildServiceProvider().GetRequiredService<ISender>();

            // Both namespaces are imported, both hosts have GetAll(), yet these are unambiguous:
            var products = await sender.ProductCatalog().GetAll();
            var entries = await sender.OrderLedger().GetAll();

            Assert.Equal(11, products);
            Assert.Equal(22, entries);
        }
    }
}

namespace PediatR.Tests.Grouping.Products
{
    using System.Threading.Tasks;
    using PediatR;

    public sealed class ProductCatalog
    {
        private readonly int _count = 11;

        [Query] public Task<int> GetAll() => Task.FromResult(_count);
    }
}

namespace PediatR.Tests.Grouping.Orders
{
    using System.Threading.Tasks;
    using PediatR;

    public sealed class OrderLedger
    {
        private readonly int _count = 22;

        [Query] public Task<int> GetAll() => Task.FromResult(_count);
    }
}
