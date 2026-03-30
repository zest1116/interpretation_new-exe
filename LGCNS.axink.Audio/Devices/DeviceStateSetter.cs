using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.Audio.Devices
{
    /// <summary>
    /// SetDeviceState까지 포함된 변형 인터페이스.
    /// 일부 환경에서 vtable mismatch 위험이 있어 Try + AccessViolation 방어가 필수.
    /// </summary>
    [ComImport]
    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfigWithDeviceState
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

        // ⚠️ 위험 구간(환경 편차 존재)
        [PreserveSig] int SetDeviceState([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, PolicyDeviceState state);
    }

    public static class DeviceStateSetter
    {
        public static bool TrySetEnabled(string deviceId, bool enabled, out string? error)
        {
            error = null;

            try
            {
                var obj = new CPolicyConfigClient();
                var policy = (IPolicyConfigWithDeviceState)obj;

                // 숨김 해제는 먼저 시도(실패해도 계속)
                try { _ = policy.SetEndpointVisibility(deviceId, true); } catch { /* ignore */ }

                var state = enabled ? PolicyDeviceState.Active : PolicyDeviceState.Disabled;
                int hr = policy.SetDeviceState(deviceId, state);

                if (hr != 0)
                {
                    error = $"HRESULT=0x{hr:X8}";
                    return false;
                }

                return true;
            }
            catch (AccessViolationException)
            {
                // vtable mismatch의 전형적 증상
                error = "SetDeviceState 호출이 이 환경(Windows/드라이버)과 호환되지 않아 활성/비활성 변경이 불가능합니다.";
                return false;
            }
            catch (COMException ex)
            {
                error = $"COMException 0x{ex.ErrorCode:X8}: {ex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
