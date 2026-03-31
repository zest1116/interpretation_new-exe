using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LGCNS.axink.Audio
{
    /// <summary>
    /// 오디오 데이터를 목표 포맷(16kHz, Mono, 16bit PCM)으로 리샘플링하는 클래스
    /// </summary>
    public class AudioResampler : IDisposable
    {
        private readonly WaveFormat _targetFormat;
        private readonly int _targetSampleRate;
        private readonly int _targetChannels;
        private bool _disposed;

        public AudioResampler(int targetSampleRate = 16000, int targetChannels = 1)
        {
            _targetSampleRate = targetSampleRate;
            _targetChannels = targetChannels;
            _targetFormat = new WaveFormat(targetSampleRate, 16, targetChannels);
        }

        public WaveFormat TargetFormat => _targetFormat;

        /// <summary>
        /// Float 32bit 오디오 데이터를 16bit PCM으로 변환하고 리샘플링
        /// </summary>
        public byte[] ResampleFromFloat32(byte[] floatData, WaveFormat sourceFormat)
        {
            if (floatData == null || floatData.Length == 0)
                return Array.Empty<byte>();

            int sourceChannels = sourceFormat.Channels;
            int bytesPerSample = 4; // Float32 = 4 bytes
            int totalSamples = floatData.Length / bytesPerSample;

            // Float32 데이터를 샘플 배열로 변환
            float[] floatSamples = new float[totalSamples];
            Buffer.BlockCopy(floatData, 0, floatSamples, 0, floatData.Length);

            // 다채널을 모노로 변환
            float[] monoSamples = ConvertToMono(floatSamples, sourceChannels);

            // 리샘플링 (44100Hz -> 16000Hz 등)
            float[] resampledSamples;
            if (sourceFormat.SampleRate != _targetSampleRate)
            {
                resampledSamples = Resample(monoSamples, sourceFormat.SampleRate, _targetSampleRate);
            }
            else
            {
                resampledSamples = monoSamples;
            }

            // Float를 16bit PCM으로 변환
            return ConvertFloatTo16BitPcm(resampledSamples);
        }

        /// <summary>
        /// 16bit PCM 오디오 데이터를 리샘플링
        /// </summary>
        public byte[] ResampleFrom16BitPcm(byte[] pcmData, WaveFormat sourceFormat)
        {
            if (pcmData == null || pcmData.Length == 0)
                return Array.Empty<byte>();

            int sourceChannels = sourceFormat.Channels;
            int bytesPerSample = 2; // 16bit = 2 bytes
            int totalSamples = pcmData.Length / bytesPerSample;

            // 16bit PCM을 float 샘플로 변환
            float[] floatSamples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                short sample = BitConverter.ToInt16(pcmData, i * bytesPerSample);
                floatSamples[i] = sample / 32768f;
            }

            // 다채널을 모노로 변환
            float[] monoSamples = ConvertToMono(floatSamples, sourceChannels);

            // 리샘플링
            float[] resampledSamples;
            if (sourceFormat.SampleRate != _targetSampleRate)
            {
                resampledSamples = Resample(monoSamples, sourceFormat.SampleRate, _targetSampleRate);
            }
            else
            {
                resampledSamples = monoSamples;
            }

            // Float를 16bit PCM으로 변환
            return ConvertFloatTo16BitPcm(resampledSamples);
        }

        /// <summary>
        /// 다채널 샘플을 모노로 변환 (1ch, 2ch, 4ch, 6ch, 8ch 등 모두 지원)
        /// </summary>
        private float[] ConvertToMono(float[] samples, int sourceChannels)
        {
            // 이미 모노이거나 타겟이 모노가 아니면 그대로 반환
            if (sourceChannels == 1 || _targetChannels != 1)
            {
                return samples;
            }

            // 프레임 수 계산 (총 샘플 수 / 채널 수)
            int frameCount = samples.Length / sourceChannels;
            float[] monoSamples = new float[frameCount];

            for (int frame = 0; frame < frameCount; frame++)
            {
                float sum = 0f;
                int baseIndex = frame * sourceChannels;

                // 모든 채널의 샘플을 합산
                for (int ch = 0; ch < sourceChannels; ch++)
                {
                    sum += samples[baseIndex + ch];
                }

                // 평균값으로 모노 샘플 생성
                monoSamples[frame] = sum / sourceChannels;
            }

            return monoSamples;
        }

        /// <summary>
        /// 선형 보간을 사용한 리샘플링
        /// </summary>
        private float[] Resample(float[] samples, int sourceSampleRate, int targetSampleRate)
        {
            if (sourceSampleRate == targetSampleRate)
                return samples;

            double ratio = (double)sourceSampleRate / targetSampleRate;
            int targetLength = (int)(samples.Length / ratio);
            float[] result = new float[targetLength];

            for (int i = 0; i < targetLength; i++)
            {
                double sourceIndex = i * ratio;
                int index = (int)sourceIndex;
                double fraction = sourceIndex - index;

                if (index + 1 < samples.Length)
                {
                    // 선형 보간
                    result[i] = (float)(samples[index] * (1 - fraction) + samples[index + 1] * fraction);
                }
                else if (index < samples.Length)
                {
                    result[i] = samples[index];
                }
            }

            return result;
        }

        /// <summary>
        /// Float 샘플을 16bit PCM 바이트 배열로 변환
        /// </summary>
        private byte[] ConvertFloatTo16BitPcm(float[] samples)
        {
            byte[] pcmData = new byte[samples.Length * 2];

            for (int i = 0; i < samples.Length; i++)
            {
                // 클리핑
                float sample = Math.Max(-1f, Math.Min(1f, samples[i]));
                short pcmSample = (short)(sample * 32767);
                byte[] bytes = BitConverter.GetBytes(pcmSample);
                pcmData[i * 2] = bytes[0];
                pcmData[i * 2 + 1] = bytes[1];
            }

            return pcmData;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}
