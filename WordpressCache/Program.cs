using StackExchange.Redis;
using WordpressCache;
using WordpressCache.Services;

var builder = WebApplication.CreateBuilder(args);

var config = new GlobalConfig(builder.Configuration);

Console.WriteLine("Wordpress Cache");
Console.WriteLine($"Serve {config.PublicAddress} from {config.BackendAddress}");

var connMux = await ConnectionMultiplexer.ConnectAsync(config.RedisHost);
var services = builder.Services;
services.AddSingleton(config);
services.AddSingleton<IConnectionMultiplexer>(connMux);
services.AddSingleton<ICache, Cache>();
services.AddScoped<ServiceContainer>();

services.AddHttpClient(
    "wp", x => {
        x.BaseAddress = new Uri(config.BackendAddress);
        x.DefaultRequestHeaders.Host = config.Host;
        x.DefaultRequestHeaders.Add("x-forwarded-proto", config.Scheme);
    }
);
var app = builder.Build();

// app.MapGet("/", () => "Hello World!");
app.UseMiddleware<ProxyMiddleware>();

var host = OperatingSystem.IsWindows() ? "https://localhost" : "http://*:80";
app.Run(host);
