using LGCNS.axink.Common;
using Microsoft.Win32;

namespace LGCNS.axink.App.Updater
{
    /// <summary>
    /// 업데이트 실패 무한 루프를 방지하는 circuit breaker.
    /// 
    /// 정책:
    ///   - 실패 시 해당 세션에서는 자동 재시도 안 함 (앱 정상 실행)
    ///   - 앱 재시작 시 1회 재시도 허용
    ///   - 같은 버전 연속 3회 실패 → 자동 시도 중단, 사용자 수동 재시도만 허용
    ///   - 서버에 새 버전 배포 → 카운터 자동 리셋
    /// </summary>
    public static class UpdateGuard
    {
        private const string FailCountKey = "UpdateFailCount";
        private const string FailedVersionKey = "UpdateFailedVersion";

        /// <summary>같은 버전에 대한 자동 재시도 최대 횟수</summary>
        private const int MaxRetries = 3;

        /// <summary>
        /// 자동 업데이트를 시도해도 되는지 판단합니다.
        /// false면 사용자 수동 트리거만 허용됩니다.
        /// </summary>
        public static bool ShouldAttemptUpdate()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryUtils.RegistryPath);
                if (key == null) return true;

                var failCount = key.GetValue(FailCountKey) is int count ? count : 0;
                return failCount < MaxRetries;
            }
            catch (Exception ex)
            {
                Logging.Error($"[UpdateGuard] ShouldAttemptUpdate 오류: {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// 현재 연속 실패 횟수를 반환합니다.
        /// </summary>
        public static int GetFailCount()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryUtils.RegistryPath);
                if (key == null) return 0;
                return key.GetValue(FailCountKey) is int count ? count : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 업데이트 실패를 기록합니다.
        /// </summary>
        public static void RecordFailure(string? version = null)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryUtils.RegistryPath);
                var current = key.GetValue(FailCountKey) is int count ? count : 0;
                key.SetValue(FailCountKey, current + 1, RegistryValueKind.DWord);

                if (!string.IsNullOrEmpty(version))
                    key.SetValue(FailedVersionKey, version, RegistryValueKind.String);

                Logging.Warn($"[UpdateGuard] 실패 기록: {current + 1}회 (version={version ?? "unknown"})");
            }
            catch (Exception ex)
            {
                Logging.Error($"[UpdateGuard] RecordFailure 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 실패 카운터를 초기화합니다.
        /// 업데이트 성공 시, 또는 사용자가 수동 재시도를 요청할 때 호출합니다.
        /// </summary>
        public static void ResetFailCount()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryUtils.RegistryPath);
                key.DeleteValue(FailCountKey, throwOnMissingValue: false);
                key.DeleteValue(FailedVersionKey, throwOnMissingValue: false);

                Logging.Info("[UpdateGuard] 카운터 리셋");
            }
            catch (Exception ex)
            {
                Logging.Error($"[UpdateGuard] ResetFailCount 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 서버 최신 버전이 이전 실패 버전과 다르면 카운터를 리셋합니다.
        /// 서버 체크 직후, ShouldAttemptUpdate() 전에 호출해야 합니다.
        /// </summary>
        /// <returns>리셋이 발생했으면 true</returns>
        public static bool ResetIfNewVersion(string latestVersion)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryUtils.RegistryPath);
                if (key == null) return false;

                var failedVersion = key.GetValue(FailedVersionKey) as string;

                if (!string.IsNullOrEmpty(failedVersion) && failedVersion != latestVersion)
                {
                    Logging.Info($"[UpdateGuard] 새 버전 감지 ({failedVersion} → {latestVersion}), 카운터 리셋");
                    ResetFailCount();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logging.Error($"[UpdateGuard] ResetIfNewVersion 오류: {ex.Message}");
                return false;
            }
        }
    }
}