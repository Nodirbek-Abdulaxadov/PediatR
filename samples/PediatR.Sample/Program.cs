using Microsoft.Extensions.DependencyInjection;
using PediatR;
using PediatR.Sample;

// A tiny vertical slice showing the three authoring modes flowing through one pipeline.
var services = new ServiceCollection();
services.AddSingleton<Catalog>();
services.AddTransient<ProductFeatures>();   // the generated handlers inject this host from DI
services.AddPediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Catalog).Assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehaviour<,>));
});

var sender = services.BuildServiceProvider().GetRequiredService<ISender>();

Console.WriteLine("== [Command] / [Query] (generated) ==");
var id = await sender.ProductFeatures().AddProduct("Widget", 9.99m); // grouped ISender proxy
Console.WriteLine($"Added product #{id}");

var product = await sender.Send(new GetProductQuery(id)); // explicit dispatch of a generated request
Console.WriteLine($"Fetched {product}");

Console.WriteLine("\n== [Handler] (generated, neutral) ==");
var all = await sender.ProductFeatures().ListProducts();  // grouped ISender proxy
Console.WriteLine($"Catalog holds {all.Count} product(s)");

Console.WriteLine("\n== Classic hand-written handler (side by side) ==");
Console.WriteLine(await sender.Send(new Ping("hello")));

namespace PediatR.Sample
{
    // ── Domain ────────────────────────────────────────────────────────────────
    public sealed record Product(int Id, string Name, decimal Price);

    public sealed class Catalog
    {
        private readonly List<Product> _products = new();
        private int _next = 1;

        public int Add(string name, decimal price)
        {
            var product = new Product(_next++, name, price);
            _products.Add(product);
            return product.Id;
        }

        public Product? Get(int id) => _products.FirstOrDefault(p => p.Id == id);

        public IReadOnlyList<Product> All() => _products;
    }

    // Mode 2 & 3 — one method per use case; the generator emits the request + handler.
    public sealed class ProductFeatures(Catalog catalog)
    {
        [Command] public Task<int> AddProduct(string name, decimal price)
            => Task.FromResult(catalog.Add(name, price));

        [Query] public Task<Product?> GetProduct(int id)
            => Task.FromResult(catalog.Get(id));

        [Handler] public Task<IReadOnlyList<Product>> ListProducts()
            => Task.FromResult(catalog.All());
    }

    // Mode 1 — classic, unchanged from MediatR.
    public sealed record Ping(string Message) : IRequest<string>;

    public sealed class PingHandler : IRequestHandler<Ping, string>
    {
        public Task<string> Handle(Ping request, CancellationToken cancellationToken)
            => Task.FromResult($"{request.Message} pong");
    }

    // A behavior wraps every request — generated and hand-written alike.
    public sealed class LoggingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            Console.WriteLine($"  [pipeline] {typeof(TRequest).Name}");
            return await next(cancellationToken);
        }
    }
}
