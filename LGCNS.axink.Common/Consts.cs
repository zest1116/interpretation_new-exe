using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.Common
{
    public static class Consts
    {
        /// <summary>
        /// 회사명
        /// </summary>
        public const string APP_COMPANY = "LGCNS";

        /// <summary>
        /// 제품명
        /// </summary>
        public const string APP_NAME = "axink";

        /// <summary>
        /// 녹음파일 Root폴더명
        /// </summary>
        public const string DIR_NAME_WAVE_ROOT = "Recording";

        /// <summary>
        /// AppSettings.json 파일명
        /// </summary>
        public const string FILE_NAME_APP_SETTINGS = "AppSettings.json";

        /// <summary>
        /// SysSettings.json 파일명
        /// </summary>
        public const string FILE_NAME_SYS_SETTINGS = "SysSettings.json";

        /// <summary>
        /// 오디오 패킷 정의
        /// </summary>
        public const byte AUDIO_PACKET_PCM16 = 1;
    }
}
