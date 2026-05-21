using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.Common
{
    public static class LogCleaner
    {
        private const int RetentionDays = 30;

        /// <summary>
        /// 지정된 디렉터리에서 보존기간(기본 30일)을 초과한 로그 파일을 삭제합니다.
        /// </summary>
        /// <param name="logDirectory">로그 디렉터리 경로. null이면 기본 경로 사용</param>
        /// <param name="searchPatterns">대상 파일 패턴 (기본: *.log, *.txt)</param>
        public static void CleanOldLogs(string logDirectory = null, params string[] searchPatterns)
        {
            try
            {
                logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Consts.APP_COMPANY,
                Consts.APP_NAME,
                "Logs");

                if (!Directory.Exists(logDirectory))
                {
                    Log.Debug("Log directory not found, skipping cleanup: {Dir}", logDirectory);
                    return;
                }

                if (searchPatterns == null || searchPatterns.Length == 0)
                    searchPatterns = new[] { "*.log", "*.txt" };

                var cutoff = DateTime.Now.AddDays(-RetentionDays);
                int deleted = 0, failed = 0;
                long bytesFreed = 0;

                foreach (var pattern in searchPatterns)
                {
                    foreach (var file in Directory.EnumerateFiles(logDirectory, pattern, SearchOption.AllDirectories))
                    {
                        try
                        {
                            var fi = new FileInfo(file);
                            if (fi.LastWriteTime < cutoff)
                            {
                                var size = fi.Length;
                                fi.Delete();
                                deleted++;
                                bytesFreed += size;
                            }
                        }
                        catch (IOException ex)
                        {
                            // 사용 중인 파일 (오늘자 로그 등) — 무시
                            failed++;
                            Log.Debug(ex, "Skipped locked log file: {File}", file);
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            failed++;
                            Log.Warning(ex, "Access denied for log file: {File}", file);
                        }
                    }
                }

                if (deleted > 0)
                {
                    Log.Information(
                        "Log cleanup complete: deleted={Deleted}, failed={Failed}, freed={MB:F2} MB, retention={Days}d",
                        deleted, failed, bytesFreed / 1024.0 / 1024.0, RetentionDays);
                }
            }
            catch (Exception ex)
            {
                // 로그 정리 실패가 앱 시작을 막아서는 안 됨
                Log.Warning(ex, "Log cleanup failed");
            }
        }
    }
}
