
using Serilog;
using System.IO;

namespace LGCNS.axink.Common
{
    public static class Logging
    {
        private static bool _initialized;

        public static void Init(string appName, string companyName)
        {
            if (_initialized) return;

            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                companyName,
                appName,
                "Logs",
                "app-.log"
            );

            Log.Logger = new Serilog.LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.File(
                    logPath,
                    rollingInterval: Serilog.RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    outputTemplate:
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();

            _initialized = true;

            Log.Information("=== Logger Initialized ===");
        }

        public static void Info(string message)
            => Log.ForContext("SourceContext", GetCaller())
                  .Information(message);

        public static void Debug(string message)
            => Log.ForContext("SourceContext", GetCaller())
                  .Debug(message);

        public static void Warn(string message)
            => Log.ForContext("SourceContext", GetCaller())
                  .Warning(message);

        public static void Error(string message)
            => Log.ForContext("SourceContext", GetCaller())
                  .Error(message);

        public static void Error(Exception ex, string message)
            => Log.ForContext("SourceContext", GetCaller())
                  .Error(ex, message);

        private static string GetCaller()
        {
            var frame = new System.Diagnostics.StackTrace()
                .GetFrame(2); // 호출자 기준
            return frame?.GetMethod()?.DeclaringType?.FullName ?? "Unknown";
        }
    }
}
