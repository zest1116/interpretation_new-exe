using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace LGCNS.axink.Audio.Devices
{
    // =========================================================
    //  COM Interop (정확한 시그니처/순서 중요)
    // =========================================================

    [ComImport]
    [Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    internal class CPolicyConfigClient
    {
    }

    internal enum ERole
    {
        eConsole = 0,
        eMultimedia = 1,
        eCommunications = 2
    }

    internal enum PolicyDeviceState : int
    {
        Active = 1,
        Disabled = 2,
        NotPresent = 4,
        Unplugged = 8
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PropertyKey
    {
        public Guid fmtid;
        public int pid;
    }

    // 최소 PropVariant 정의(실사용 거의 없음. vtable 정렬용)
    [StructLayout(LayoutKind.Sequential)]
    internal struct PropVariant
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr p;
        public int p2;
    }

    /// <summary>
    /// 안정적으로 사용 가능한 PolicyConfig(Visibility + DefaultEndpoint까지)
    /// </summary>
    [ComImport]
    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, out IntPtr ppFormat);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, bool bDefault, out IntPtr ppFormat);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr pMixFormat);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, bool bDefault, out long pmftDefaultPeriod, out long pmftMinimumPeriod);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref long pmftDefaultPeriod, ref long pmftMinimumPeriod);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, out IntPtr pMode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref PropertyKey key, out PropVariant pv);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref PropertyKey key, ref PropVariant pv);

        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ERole role);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, bool bVisible);
    }

    // =========================================================
    //  Public Util API (여기만 쓰면 됨)
    // =========================================================

    public static class AudioDevicePolicy
    {
        /// <summary>
        /// 장치가 "숨김"이면 보이게 처리합니다.
        /// </summary>
        public static bool TrySetVisible(string deviceId, bool visible, out string? error)
        {
            error = null;
            try
            {
                var policy = (IPolicyConfig)new CPolicyConfigClient();
                int hr = policy.SetEndpointVisibility(deviceId, visible);
                if (hr != 0) Marshal.ThrowExceptionForHR(hr);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 기본 장치 설정(입/출력 동일 API). 역할 3종을 모두 설정합니다.
        /// </summary>
        public static bool TrySetDefaultAllRoles(string deviceId, out string? error)
        {
            error = null;
            try
            {
                var policy = (IPolicyConfig)new CPolicyConfigClient();

                int hr;
                hr = policy.SetDefaultEndpoint(deviceId, ERole.eConsole);
                if (hr != 0) Marshal.ThrowExceptionForHR(hr);

                hr = policy.SetDefaultEndpoint(deviceId, ERole.eMultimedia);
                if (hr != 0) Marshal.ThrowExceptionForHR(hr);

                hr = policy.SetDefaultEndpoint(deviceId, ERole.eCommunications);
                if (hr != 0) Marshal.ThrowExceptionForHR(hr);

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 기본 "출력" 장치로 설정 (실제로는 deviceId에 대해 역할 3종 설정)
        /// </summary>
        public static bool TrySetDefaultOutput(string deviceId, out string? error)
        {
            // 출력/입력 구분은 호출자(선택된 리스트)가 보장한다고 보고,
            // 실제 구현은 역할 3종 설정으로 통일합니다.
            return TrySetDefaultAllRoles(deviceId, out error);
        }

        /// <summary>
        /// 기본 "입력" 장치로 설정 (실제로는 deviceId에 대해 역할 3종 설정)
        /// </summary>
        public static bool TrySetDefaultInput(string deviceId, out string? error)
        {
            return TrySetDefaultAllRoles(deviceId, out error);
        }

        /// <summary>
        /// 활성화(Enable) 시도. 실패 가능성이 있어 Try 패턴.
        /// </summary>
        public static bool TryEnable(string deviceId, out string? error)
            => DeviceStateSetter.TrySetEnabled(deviceId, enabled: true, out error);

        /// <summary>
        /// 비활성화(Disable) 시도. 실패 가능성이 있어 Try 패턴.
        /// </summary>
        public static bool TryDisable(string deviceId, out string? error)
            => DeviceStateSetter.TrySetEnabled(deviceId, enabled: false, out error);

        /// <summary>
        /// (권장) "보이기 + 활성화 + 기본 설정"을 한 번에 처리.
        /// enable이 실패할 수 있으므로 enable 실패 시에도 default는 시도합니다.
        /// </summary>
        public static bool TryEnsureEnabledAndSetDefault(string deviceId, out string? error)
        {
            error = null;

            // 1) 보이게
            if (!TrySetVisible(deviceId, true, out var visErr))
                error = Append(error, "Visibility", visErr);

            // 2) 활성화(실패 가능)
            if (!TryEnable(deviceId, out var enErr))
                error = Append(error, "Enable", enErr);

            // 3) 기본 장치 설정(시도)
            if (!TrySetDefaultAllRoles(deviceId, out var defErr))
                error = Append(error, "Default", defErr);

            // error가 null이면 완전 성공
            return error == null;
        }

        private static string? Append(string? current, string label, string? msg)
        {
            if (string.IsNullOrWhiteSpace(msg)) return current;
            var line = $"{label}: {msg}";
            return current == null ? line : current + "\n" + line;
        }
    }
}
