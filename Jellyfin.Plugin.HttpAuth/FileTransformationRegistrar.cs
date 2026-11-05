using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HttpAuth
{
    // Shape of the payload the File Transformation plugin hands to our callback (deserialized with Newtonsoft on its side).
    public class TransformationPayload
    {
        public string Contents { get; set; } = string.Empty;
    }

    // Registers the index.html transformation with the File Transformation plugin
    // (https://github.com/IAmParadox27/jellyfin-plugin-file-transformation) instead of editing the file on disk.
    public sealed class FileTransformationRegistrar : IHostedService, IDisposable
    {
        private const int MaxAttempts = 30;
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);
        private static readonly Guid TransformationId = Guid.Parse("5f3c1d0e-2a9b-4f57-9c1e-7b6a8d4e2c10");

        private readonly ILogger<FileTransformationRegistrar> _logger;
        private CancellationTokenSource _cts;
        private Task _registration;

        public FileTransformationRegistrar(ILogger<FileTransformationRegistrar> logger)
        {
            _logger = logger;
        }

        // Called by the File Transformation plugin through reflection.
        public static string InjectScript(TransformationPayload payload)
        {
            var contents = payload.Contents;
            if (contents.Contains(InjectAuthController.BlockToInject, StringComparison.Ordinal))
            {
                return contents;
            }
            const string closingBodyTag = "</body>";
            return contents.Replace(closingBodyTag, InjectAuthController.BlockToInject + closingBodyTag, StringComparison.Ordinal);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = new CancellationTokenSource();
            _registration = Task.Run(() => RegisterWithRetryAsync(_cts.Token), CancellationToken.None);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_cts == null)
            {
                return;
            }
            await _cts.CancelAsync().ConfigureAwait(false);
            try
            {
                await _registration.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        public void Dispose()
        {
            _cts?.Dispose();
        }

        private async Task RegisterWithRetryAsync(CancellationToken token)
        {
            // The File Transformation plugin may finish initializing after we start, so retry for a while.
            for (var attempt = 1; attempt <= MaxAttempts && !token.IsCancellationRequested; attempt++)
            {
                if (TryRegister())
                {
                    _logger.LogInformation("Registered the HttpAuth index.html transformation with the File Transformation plugin.");
                    return;
                }
                await Task.Delay(RetryDelay, token).ConfigureAwait(false);
            }
            _logger.LogError("Could not register with the File Transformation plugin. Install it and restart Jellyfin, otherwise the auto-login script is not injected.");
        }

        private bool TryRegister()
        {
            try
            {
                var ftAssembly = AssemblyLoadContext.All
                    .SelectMany(context => context.Assemblies)
                    .FirstOrDefault(assembly => assembly.FullName?.Contains(".FileTransformation", StringComparison.Ordinal) == true);
                var method = ftAssembly?.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface")?.GetMethod("RegisterTransformation");
                if (method == null)
                {
                    return false;
                }

                var callbackType = typeof(FileTransformationRegistrar);
                var payloadJson = JsonSerializer.Serialize(new
                {
                    id = TransformationId,
                    fileNamePattern = "index.html",
                    callbackAssembly = callbackType.Assembly.FullName,
                    callbackClass = callbackType.FullName,
                    callbackMethod = nameof(InjectScript),
                });

                // JObject comes from the File Transformation side, so build it through its own parameter type.
                var payload = method.GetParameters()[0].ParameterType
                    .GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, [typeof(string)])
                    .Invoke(null, [payloadJson]);
                method.Invoke(null, [payload]);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "File Transformation plugin is not ready yet.");
                return false;
            }
        }
    }
}
