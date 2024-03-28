namespace AProject.WalletApi
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.HttpOverrides;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using RecurrentTasks;

    public class Startup
    {
        private readonly IConfiguration configuration;

        public Startup(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public static IReadOnlyList<Type> RegisteredTasks { get; private set; } = new List<Type>();

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<ForwardedHeadersOptions>(options => options.ForwardedHeaders = ForwardedHeaders.All);
            services.Configure<RouteOptions>(o => o.LowercaseUrls = true);
            services.AddControllers();

            services.Configure<ApiOptions>(configuration.GetSection("ApiOptions"));

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(o =>
            {
                o.SupportNonNullableReferenceTypes();
                o.SwaggerDoc("wallet", new Microsoft.OpenApi.Models.OpenApiInfo()
                {
                    Title = "Wallet API",
                    Description = "Wallet API for A-Project-Wallet.",
                    Version = "wallet",
                });
                o.EnableAnnotations();

                var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                o.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
            });

            services.AddCors(o =>
            {
                o.AddDefaultPolicy(
                    builder => builder
                        .AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod());
            });

            RegisteredTasks = new List<Type>
                {
                    //typeof(ITask<CachedData>),
                    //typeof(ITask<IndexerControlTask>),
                }
                .AsReadOnly();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseForwardedHeaders();
            app.UseStatusCodePages();

            app.UseExceptionHandler(ab => ab.Run(ctx =>
            {
                ctx.Response.ContentType = "text/plain";
                return ctx.Response.WriteAsync($"Nothing here. Please enjoy StatusCode {ctx.Response.StatusCode}.");
            }));

            app.UseMiddleware<RobotsTxtMiddleware>();
            app.UseMiddleware<HealthMiddleware>();

            app.UseSwagger();
            app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/wallet/swagger.json", "Wallet API"));

            app.UseRouting();
            app.UseCors();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}