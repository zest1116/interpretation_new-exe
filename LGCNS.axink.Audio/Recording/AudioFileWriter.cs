using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.Audio.Recording
{
    /// <summary>
    /// 오디오 데이터를 WAV 파일로 저장하는 클래스
    /// 데이터를 저장 가능
    /// </summary>
    public class AudioFileWriter : IDisposable
    {
        private WaveFileWriter? _rawWriter;        // 데이터 저장
        private readonly object _lockRaw = new object();
        private bool _disposed;


        private readonly string _outputDirectory;
        private readonly string _streamName;

        private string? _rawFilePath;
        public string? RawFilePath => _rawFilePath;

        public AudioFileWriter(string streamName, string? outputDirRoot)
        {
            _streamName = streamName;
            string baseDir = string.IsNullOrEmpty(outputDirRoot) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : outputDirRoot;
            _outputDirectory = Path.Combine(
                baseDir,
                "AudioStreamer",
                "Recordings"
            );

            // 출력 디렉토리 생성
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
        }

        /// <summary>
        /// 녹음 시작 - 파일 생성
        /// </summary>
        public void StartRecording(WaveFormat? rawFormat)
        {
            StopRecording();

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string baseFileName = $"{_streamName}_{timestamp}";

            try
            {

                _rawFilePath = Path.Combine(_outputDirectory, $"{baseFileName}_raw.wav");
                _rawWriter = new WaveFileWriter(_rawFilePath, rawFormat);


            }
            catch (Exception ex)
            {
                StopRecording();
                throw new InvalidOperationException($"녹음 시작 실패: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 리샘플링 전 원본 데이터 저장
        /// </summary>
        public void WriteRawData(byte[] data)
        {
            if (_rawWriter == null || data == null || data.Length == 0)
                return;

            lock (_lockRaw)
            {
                try
                {
                    _rawWriter.Write(data, 0, data.Length);
                }
                catch (Exception)
                {
                    // 쓰기 오류 무시 (파일이 닫혔을 수 있음)
                }
            }
        }

        /// <summary>
        /// 녹음 중지 - 파일 닫기
        /// </summary>
        public void StopRecording()
        {
            lock (_lockRaw)
            {
                if (_rawWriter != null)
                {
                    try
                    {
                        _rawWriter.Flush();
                        _rawWriter.Dispose();
                    }
                    catch { }
                    _rawWriter = null;
                }
            }



        }

        /// <summary>
        /// 저장된 파일 경로 목록 반환
        /// </summary>
        public string? GetSavedFilePaths()
        {
            return _rawFilePath;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                StopRecording();
            }
        }

    }
}
