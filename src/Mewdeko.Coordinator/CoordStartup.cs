﻿using Mewdeko.Coordinator.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Mewdeko.Coordinator;

public class CoordStartup(IConfiguration config)
{
    public IConfiguration Configuration { get; } = config;

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddGrpc();
        services.AddSingleton<CoordinatorRunner>();
        services.AddSingleton<IHostedService, CoordinatorRunner>(
            serviceProvider => serviceProvider.GetRequiredService<CoordinatorRunner>());
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGrpcService<CoordinatorService>();

            endpoints.MapGet("/", context => context.Response.WriteAsync(
                "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909"));
        });
    }
}