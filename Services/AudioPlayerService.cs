using System.Windows.Threading;
using NAudio.Wave;

namespace voboX.Services;

/// <summary>音频播放服务（NAudio WaveOutEvent）</summary>
public class AudioPlayerService : IDisposable
{
    private WaveOutEvent? _output;
    private AudioFileReader? _reader;
    private DispatcherTimer? _timer;

    /// <summary>当前播放的文件路径</summary>
    public string? CurrentPath { get; private set; }

    /// <summary>播放进度变化（秒，约 10ms 一次）</summary>
    public event Action<double>? PositionChanged;

    /// <summary>播放自然结束</summary>
    public event Action? PlaybackFinished;

    public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;

    /// <summary>
    /// 实际播放位置（按声卡已输出的字节换算）。
    /// 不用 reader.CurrentTime：它会包含预读缓冲，导致播放刚开始就跳到 ~300ms。
    /// </summary>
    public double Position => _output is not null && _reader is not null
        ? _output.GetPosition() / (double)_reader.WaveFormat.AverageBytesPerSecond
        : 0;
    public double Duration => _reader?.TotalTime.TotalSeconds ?? 0;

    public void Play(string path)
    {
        Stop();
        CurrentPath = path;
        _reader = new AudioFileReader(path);
        _output = new WaveOutEvent();
        _output.DesiredLatency = 100; // 减小输出缓冲（默认 300ms），降低出声延迟
        _output.Init(_reader);
        _output.PlaybackStopped += (s, e) =>
        {
            if (e.Exception is null && _reader is not null
                && (_reader.TotalTime - _reader.CurrentTime) < TimeSpan.FromMilliseconds(500))
            {
                PlaybackFinished?.Invoke();
            }
        };
        _output.Play();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        _timer.Tick += (s, e) => PositionChanged?.Invoke(Position);
        _timer.Start();
    }

    public void Pause() => _output?.Pause();
    public void Resume() => _output?.Play();
    public void Toggle()
    {
        if (IsPlaying) Pause();
        else Resume();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
        _output?.Stop();
        _output?.Dispose();
        _output = null;
        _reader?.Dispose();
        _reader = null;
        CurrentPath = null;
    }

    public void Dispose() => Stop();
}
