using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace voboX.Services;

/// <summary>音频裁剪：保持原格式（WAV 直接切片，MP3 经 MediaFoundation 编码）</summary>
public static class AudioCropService
{
    /// <summary>
    /// 裁剪 [startSec, endSec) 区间并保存。
    /// </summary>
    public static void Crop(string sourcePath, double startSec, double endSec, string outputPath)
    {
        startSec = Math.Max(0, startSec);
        endSec = Math.Max(startSec + 0.05, endSec);

        using var reader = new AudioFileReader(sourcePath);
        var start = TimeSpan.FromSeconds(startSec);
        var span = TimeSpan.FromSeconds(endSec - startSec);
        reader.CurrentTime = start;

        var ext = Path.GetExtension(outputPath).ToLowerInvariant();
        if (ext == ".mp3")
        {
            // MP3：切片后经 MediaFoundation 编码为 MP3
            var sliced = reader.Skip(start).Take(span);
            MediaFoundationEncoder.EncodeToMp3(sliced.ToWaveProvider16(), outputPath, 320000);
        }
        else
        {
            // WAV / 其他：直接写 PCM 切片
            using var writer = new WaveFileWriter(outputPath, reader.WaveFormat);
            var buffer = new byte[reader.WaveFormat.AverageBytesPerSecond];
            long remaining = (long)(span.TotalSeconds * reader.WaveFormat.AverageBytesPerSecond);
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(buffer.Length, remaining);
                int read = reader.Read(buffer, 0, toRead);
                if (read == 0) break;
                writer.Write(buffer, 0, read);
                remaining -= read;
            }
        }
    }
}
