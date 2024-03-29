using Serilog;
using TNRD.Zeepkist.GTR.Backend.RecordMediaHandler;
using TNRD.Zeepkist.GTR.Backend.RecordMediaHandler.Google;
using TNRD.Zeepkist.GTR.Backend.RecordMediaHandler.Rabbit;
using TNRD.Zeepkist.GTR.Database;

IHost host = Host.CreateDefaultBuilder(args)
    .UseSerilog((context, configuration) =>
    {
        configuration
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Source", "MediaProcessor")
            .MinimumLevel.Debug()
            .WriteTo.Seq(context.Configuration["Seq:Url"], apiKey: context.Configuration["Seq:Key"])
            .WriteTo.Console();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddHostedService<Worker>();

        services.Configure<GoogleOptions>(context.Configuration.GetSection("Google"));
        services.AddSingleton<IGoogleUploadService, CloudStorageUploadService>();

        services.Configure<RabbitOptions>(context.Configuration.GetSection("Rabbit"));
        services.AddHostedService<RabbitWorker>();

        services.AddNpgsql<GTRContext>(context.Configuration["Database:ConnectionString"]);

        services.AddSingleton<ItemQueue>();
    })
    .Build();

host.Run();
