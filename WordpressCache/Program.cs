using WordpressCache;
using WordpressCache.Config;
using WordpressCache.Services;

var builder = WebApplication.CreateBuilder(args);

// var config = new GlobalConfig(builder.Configuration);
var config = builder.Configuration;


// var connMux = await ConnectionMultiplexer.ConnectAsync(config.RedisHost);
var services = builder.Services;
services.Configure<GlobalOptions>(config.GetSection("Options"));
services.AddSingleton(config);
// services.AddSingleton<IConnectionMultiplexer>(connMux);
services.AddSingleton<ICache, MemoryCache>();
services.AddScoped<ServiceContainer>();

services.AddHostedService<ServerMonitor>();
services.AddHostedService<CacheUpdater>();

var publicAddr = new Uri(config["PublicAddress"]);
var backendAddr = new Uri(config["BackendAddress"]);

Console.WriteLine("Wordpress Cache");
Console.WriteLine($"Serve {publicAddr} from {backendAddr}");

services.AddHttpClient(
    "wp", client => {
        client.BaseAddress = backendAddr;
        client.DefaultRequestHeaders.Host = backendAddr.Host;
        client.DefaultRequestHeaders.Add("x-forwarded-proto", publicAddr.Scheme);
    }
);
var app = builder.Build();

// app.MapGet("/", () => "Hello World!");
app.UseMiddleware<ProxyMiddleware>();
// app.MapGet("/*", ProxyMiddleware.Shared.InvokeAsync);
// app.Use(ProxyMiddleware.Shared.InvokeAsync);
// app.Run(ProxyMiddleware.InvokeAsync);

// var host = OperatingSystem.IsWindows() ? "https://localhost" : "http://*:80";
app.Run();
