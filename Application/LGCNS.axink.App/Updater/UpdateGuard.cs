using LGCNS.axink.Common;
using Microsoft.Win32;

namespace LGCNS.axink.App.Updater
{
    /// <summary>
    /// 업데이트 실패 무한 루프를 방지하는 circuit breaker.
    /// 레지스트리에 연속 실패 횟수와 마지막 실패 시간을 기록하여
    /// MaxRetries 초과 시 CooldownPeriod 동안 업데이트를 건너뜁니다.
    /// </summary>
    public static class UpdateGuard
    {
        private const string FailCountKey = "UpdateFailCount";
        private const string LastFailTimeKey = "UpdateLastFailTime";
        private const string FailedVersionKey = "UpdateFailedVersion";

        /// <summary>연속 실패 허용 횟수</summary>
        private const int MaxRetries = 3;

        /// <summary>MaxRetries 초과 후 재시도까지 대기 시간</summary>
        private static readonly TimeSpan CooldownPeriod = TimeSpan.FromHours(6);

        /// <summary>
        /// 업데이트를 시도해도 되는지 판단합니다.
        /// </summary>
        public static bool ShouldAttemptUpdate()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryUtils.RegistryPath);
                if (key == null) return true;

                var failCount = key.GetValue(FailCountKey) is int count ? count : 0;
                if (failCount == 0) return true;

                var lastFailTicks = key.GetValue(LastFailTimeKey) is long ticks ? ticks : 0L;
                var lastFail = new DateTime(lastFailTicks, DateTimeKind.Utc);
                var elapsed = DateTime.UtcNow - lastFail;

                // 쿨다운 지나면 카운터 리셋 → 재시도 허용
                if (elapsed > CooldownPeriod)
                {
                    Logging.Info($"[UpdateGuard] 쿨다운 경과 ({elapsed:hh\\:mm}), 카운터 리셋");
                    ResetFailCount();
                    return true;
                }

                if (failCount >= MaxRetries)
                {
                    var remaining = CooldownPeriod - elapsed;
                    Logging.Warn($"[UpdateGuard] 연속 {failCount}회 실패, {remaining:hh\\:mm} 후 재시도");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logging.Error($"[UpdateGuard] ShouldAttemptUpdate 오류: {ex.Message}");
                return true; // 오류 시 안전하게 허용
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
                key.SetValue(LastFailTimeKey, DateTime.UtcNow.Ticks, RegistryValueKind.QWord);

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
        /// 실패 카운터를 초기화합니다. 업데이트 성공 시 호출합니다.
        /// </summary>
        public static void ResetFailCount()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryUtils.RegistryPath);
                key.DeleteValue(FailCountKey, throwOnMissingValue: false);
                key.DeleteValue(LastFailTimeKey, throwOnMissingValue: false);
                key.DeleteValue(FailedVersionKey, throwOnMissingValue: false);
            }
            catch (Exception ex)
            {
                Logging.Error($"[UpdateGuard] ResetFailCount 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 서버에서 새 버전이 감지되면 이전 실패 기록을 리셋합니다.
        /// (이전에 실패했던 버전과 다른 버전이면 새로 시도할 가치가 있음)
        /// </summary>
        public static void ResetIfNewVersion(string latestVersion)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryUtils.RegistryPath);
                if (key == null) return;

                var failedVersion = key.GetValue(FailedVersionKey) as string;

                if (!string.IsNullOrEmpty(failedVersion) && failedVersion != latestVersion)
                {
                    Logging.Info($"[UpdateGuard] 새 버전 감지 ({failedVersion} → {latestVersion}), 카운터 리셋");
                    ResetFailCount();
                }
            }
            catch (Exception ex)
            {
                Logging.Error($"[UpdateGuard] ResetIfNewVersion 오류: {ex.Message}");
            }
        }
    }
}