using NAudio.Wave;

namespace voboX.Services;

/// <summary>波形峰值提取（后台线程使用）</summary>
public static class WaveformExtractor
{
    /// <summary>
    /// 提取归一化峰值（0~1）。buckets 为采样点数量，同时返回时长（秒）。
    /// </summary>
    public static (double[] Peaks, double DurationSeconds) Extract(string path, int buckets)
    {
        using var reader = new AudioFileReader(path);
        int channels = reader.WaveFormat.Channels;
        long frameCount = reader.Length / Math.Max(1, reader.WaveFormat.BlockAlign);
        double duration = reader.TotalTime.TotalSeconds;
        if (frameCount <= 0) return (Array.Empty<double>(), 0);

        var peaks = new double[buckets];
        var buffer = new float[reader.WaveFormat.AverageBytesPerSecond];
        double maxAbs = 0;
        long frame = 0;
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            int frames = read / Math.Max(1, channels);
            for (int i = 0; i < frames; i++)
            {
                double peak = 0;
                for (int c = 0; c < channels; c++)
                    peak = Math.Max(peak, Math.Abs(buffer[i * channels + c]));
                long idx = frame * buckets / frameCount;
                idx = Math.Min(idx, buckets - 1);
                peaks[idx] = Math.Max(peaks[idx], peak);
                maxAbs = Math.Max(maxAbs, peak);
                frame++;
            }
        }

        if (maxAbs > 0.0001)
            for (int i = 0; i < peaks.Length; i++)
                peaks[i] /= maxAbs;

        return (peaks, duration);
    }
}
