using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.Updater
{
    public class UpdateOptions
    {
        /// <summary>대기할 WPF 프로세스 ID</summary>
        public int Pid { get; set; }

        /// <summary>MSI 다운로드 URL (다운로드 모드)</summary>
        public string DownloadUrl { get; set; } = string.Empty;

        /// <summary>이미 다운로드된 MSI 경로 (직접 설치 모드)</summary>
        public string MsiPath { get; set; } = string.Empty;

        /// <summary>설치 완료 후 재시작할 앱 경로</summary>
        public string AppPath { get; set; } = string.Empty;

        /// <summary>표시할 버전 문자열</summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>SHA256 해시 (무결성 검증)</summary>
        public string FileHash { get; set; } = string.Empty;

        /// <summary>설치 후 MSI 파일 삭제 여부</summary>
        public bool Cleanup { get; set; }

        /// <summary>다운로드가 필요한지 여부</summary>
        public bool NeedsDownload => !string.IsNullOrEmpty(DownloadUrl);

        public static UpdateOptions? Parse(string[] args)
        {
            var options = new UpdateOptions();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--pid":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var pid))
                            options.Pid = pid;
                        break;
                    case "--download-url":
                        if (i + 1 < args.Length)
                            options.DownloadUrl = args[++i];
                        break;
                    case "--msi":
                        if (i + 1 < args.Length)
                            options.MsiPath = args[++i];
                        break;
                    case "--app":
                        if (i + 1 < args.Length)
                            options.AppPath = args[++i];
                        break;
                    case "--version":
                        if (i + 1 < args.Length)
                            options.Version = args[++i];
                        break;
                    case "--hash":
                        if (i + 1 < args.Length)
                            options.FileHash = args[++i];
                        break;
                    case "--cleanup":
                        options.Cleanup = true;
                        break;
                }
            }

            // 최소 필수 인자 검증
            if (options.Pid == 0 || string.IsNullOrEmpty(options.AppPath))
                return null;

            // 다운로드 URL 또는 MSI 경로 중 하나는 필수
            if (string.IsNullOrEmpty(options.DownloadUrl) && string.IsNullOrEmpty(options.MsiPath))
                return null;

            return options;
        }
    }
}
