using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureAppConfiguration((context, config) =>
    {
        if (context.HostingEnvironment.IsDevelopment())
        {
            config.AddUserSecrets<Program>(optional: true);
        }
    })
    .ConfigureFunctionsWorkerDefaults()
    .Build();

host.Run();
