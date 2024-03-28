﻿namespace AProject.WalletApi
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;

    public class HealthMiddleware
    {
        private const string HealthCode = "DM7KDXLCNHECBDUH3J4S"; // Random string

        private readonly RequestDelegate next;

        public HealthMiddleware(RequestDelegate next)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public Task InvokeAsync(HttpContext context)
        {
            context = context ?? throw new ArgumentNullException(nameof(context));

            return context.Request.Path.ToString() switch
            {
                "/health" => WriteHtml(context),
                "/health.json" => WriteJson(context),
                _ => next?.Invoke(context) ?? Task.CompletedTask,
            };
        }

        private async Task WriteHtml(HttpContext context)
        {
            var response = context.Response;
            response.StatusCode = StatusCodes.Status200OK;
            response.ContentType = "text/html; charset=utf-8";
            response.Headers.CacheControl = "no-store,no-cache";

            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, leaveOpen: true);

            writer.WriteLine("<html><body><h1>Health check page</h1><dl>");

            foreach (var value in GetValues(context))
            {
                writer.WriteLine("<dt>" + value.key + "</dt><dd>" + value.value + "</dd>");
            }

            writer.WriteLine("</dl></html>");
            writer.Flush();

            stream.Position = 0;
            await stream.CopyToAsync(response.Body).ConfigureAwait(false);
        }

        private async Task WriteJson(HttpContext context)
        {
            var response = context.Response;
            response.StatusCode = StatusCodes.Status200OK;
            response.ContentType = "application/json";
            response.Headers.CacheControl = "no-store,no-cache";

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);

            writer.WriteStartObject();
            foreach (var value in GetValues(context))
            {
                writer.WritePropertyName(value.key);
                JsonSerializer.Serialize(writer, value.value, value.value.GetType());
            }

            writer.WriteEndObject();
            writer.Flush();

            stream.Position = 0;
            await stream.CopyToAsync(response.Body).ConfigureAwait(false);
        }

        private IEnumerable<(string key, object value)> GetValues(HttpContext context)
        {
            var allOk = true;

            var appAssembly = this.GetType().Assembly;
            var appTitle = appAssembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title ?? appAssembly.GetName().Name ?? "Unknown :(";
            var appVersion = appAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? appAssembly.GetName().Version?.ToString() ?? "Unknown";

            yield return ("App Title", appTitle);
            yield return ("App Version", appVersion);

            var dotnetVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
            yield return (".NET version", dotnetVersion);

            var aspAssembly = typeof(HttpContext).Assembly;
            var aspVersion = aspAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? aspAssembly.GetName().Version?.ToString() ?? "Unknown";
            yield return ("ASP.NET Core version", aspVersion);

            var inContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
            yield return ("In container", inContainer ?? "no");

            yield return ("Now", DateTimeOffset.UtcNow.ToString("u", CultureInfo.InvariantCulture));

            yield return ("Your IP", context.Connection.RemoteIpAddress?.ToString() ?? "-unknown-");

            foreach (var taskType in Startup.RegisteredTasks)
            {
                var name = taskType.GenericTypeArguments[0].Name;
                var task = (RecurrentTasks.ITask)context.RequestServices.GetRequiredService(taskType);
                if (task.RunStatus.LastSuccessTime.Add(task.Options.Interval).Add(task.Options.Interval) < DateTimeOffset.Now)
                {
                    allOk = false;
                    yield return (name, $"failed with {task.RunStatus.LastException?.GetType().Name}, last success {task.RunStatus.LastSuccessTime.UtcDateTime.ToString("u", CultureInfo.InvariantCulture)}");
                }
                else
                {
                    yield return (name, task.RunStatus.LastSuccessTime.UtcDateTime.ToString("u", CultureInfo.InvariantCulture));
                }
            }

            yield return ("Healthy", allOk ? $"Yes, code {HealthCode}" : "NO");
        }
    }
}
