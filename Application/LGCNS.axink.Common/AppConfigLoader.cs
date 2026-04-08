using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.IO;

namespace LGCNS.axink.Common
{
    public static class AppConfigLoader
    {
        public static IConfiguration Load()
        {
            var env = Environment.GetEnvironmentVariable("AXINK_ENVIRONMENT")
                      ?? "Production";

            Debug.WriteLine($">>> AXINK_ENVIRONMENT = {env}");
            Debug.WriteLine($">>> BaseDir = {AppContext.BaseDirectory}");
            Debug.WriteLine($">>> 파일 존재: {File.Exists(Path.Combine(AppContext.BaseDirectory, $"appsettings.{env}.json"))}");

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables(prefix: "AXINK_");

            return builder.Build();
        }

        public static T LoadSection<T>(string sectionName) where T : class, new()
        {
            var env = Environment.GetEnvironmentVariable("AXINK_ENVIRONMENT");

            var builder = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false);

            // 개발 시에만 환경별 파일 오버라이드
            if (!string.IsNullOrEmpty(env))
            {
                builder.AddJsonFile($"appsettings.{env}.json", optional: true);
            }

            var section = new T();
            builder.Build().GetSection(sectionName).Bind(section);
            return section;
        }
    }
}
