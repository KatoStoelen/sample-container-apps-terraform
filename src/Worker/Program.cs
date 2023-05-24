using Worker;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<SomeWorker>();
    })
    .ConfigureWebHost(builder =>
    {
        builder
            .UseKestrel()
            .ConfigureServices(services => services.AddRouting())
            .Configure(app =>
            {
                app
                    .UseRouting()
                    .UseEndpoints(endpoints =>
                    {
                        endpoints.Map("/healthz", () => "Healthy");
                    });
            });
    })
    .Build();

host.Run();
