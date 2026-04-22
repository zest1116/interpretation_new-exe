using System;
using System.Diagnostics;
using System.IO;

namespace LGCNS.axink.Updater
{
    public class UpdateLogger
    {
        public string LogFolder { get; }
        public string CurrentLogFile { get; }

        public UpdateLogger()
        {
            LogFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LGCNS", "axink Translator", "Logs");

            Directory.CreateDirectory(LogFolder);
            CurrentLogFile = Path.Combine(LogFolder, $"updater-{DateTime.Now:yyyyMMdd}.log");

            Log("========================================");
            Log($"Updater started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }

        public void Log(string message)
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            Debug.WriteLine(line);

            try
            {
                File.AppendAllText(CurrentLogFile, line + Environment.NewLine);
            }
            catch
            {
                // 로그 실패는 무시
            }
        }
    }
}