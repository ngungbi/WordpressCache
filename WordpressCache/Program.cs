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

var publicAddr = new Uri(config["Options:PublicAddress"]);
var backendAddr = new Uri(config["Options:BackendAddress"]);

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

var host = OperatingSystem.IsWindows() ? "https://localhost" : "http://*:80";
app.Run(host);
