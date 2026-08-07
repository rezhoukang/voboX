using System.IO;
using NAudio.Wave;

namespace voboX.Services;

/// <summary>音频裁剪：统一输出 WAV（源支持 MP3/WAV，经 AudioFileReader 转 PCM 后直接写切片）</summary>
public static class AudioCropService
{
    /// <summary>
    /// 裁剪 [startSec, endSec) 区间并保存为 WAV。
    /// </summary>
    public static void Crop(string sourcePath, double startSec, double endSec, string outputPath)
    {
        startSec = Math.Max(0, startSec);
        endSec = Math.Max(startSec + 0.05, endSec);

        using var reader = new AudioFileReader(sourcePath);
        var start = TimeSpan.FromSeconds(startSec);
        var span = TimeSpan.FromSeconds(endSec - startSec);
        reader.CurrentTime = start;

        using var writer = new WaveFileWriter(outputPath, reader.WaveFormat);
        var buffer = new byte[reader.WaveFormat.AverageBytesPerSecond];
        long remaining = (long)(span.TotalSeconds * reader.WaveFormat.AverageBytesPerSecond);
        // 对齐到块边界：AudioFileReader 内部的 BlockAlignReductionStream 要求
        // 每次读取必须是 block align 的整数倍，否则抛 "Must read complete blocks"
        remaining -= remaining % reader.WaveFormat.BlockAlign;
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
