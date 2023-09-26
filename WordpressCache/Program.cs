using System.Net;
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
services.AddSingleton<ServerStatus>();
services.AddSingleton<IPreloader, Preloader>();
services.AddSingleton<BackendClient>();
services.AddScoped<ServiceContainer>();

services.AddHostedService<ServerMonitor>();
services.AddHostedService<CacheUpdater>();

var publicAddr = new Uri(config["PublicAddress"]);
var backendAddr = new Uri(config["BackendAddress"]);

Console.WriteLine("Wordpress Cache");
Console.WriteLine($"Serve {publicAddr} from {backendAddr}");

var httpClientBuilder = services.AddHttpClient(
    "WP", client => {
        client.BaseAddress = backendAddr;
        client.DefaultRequestHeaders.Host = publicAddr.Host;
        client.DefaultRequestHeaders.Add("x-forwarded-proto", publicAddr.Scheme);
    }
);

if (config.GetValue<bool>("SkipCertificateValidation")) {
    httpClientBuilder.ConfigurePrimaryHttpMessageHandler(
        _ => new HttpClientHandler {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        }
    );
    // ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
}

var app = builder.Build();

using (var scope = app.Services.CreateScope()) {
    // var httpClient = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("WP");
    // var response = await httpClient.GetAsync("/");
    // if (!response.IsSuccessStatusCode) {
    //     await Task.Delay(1000);
    //     response.EnsureSuccessStatusCode();
    // }
    //
    // var content = await response.Content.ReadAsStringAsync();
    // Console.WriteLine(content);
    var preloader = scope.ServiceProvider.GetRequiredService<IPreloader>();
    await preloader.LoadAsync();
    await preloader.UpdateAllAsync();
    await preloader.SaveAsync();
}

// app.MapGet("/", () => "Hello World!");
app.UseMiddleware<ProxyMiddleware>();
app.UseMiddleware<LoggerMiddleware>();
// app.MapGet("/*", ProxyMiddleware.Shared.InvokeAsync);
// app.Use(ProxyMiddleware.Shared.InvokeAsync);
// app.Run(ProxyMiddleware.InvokeAsync);

// var host = OperatingSystem.IsWindows() ? "https://localhost" : "http://*:80";
app.Run();
