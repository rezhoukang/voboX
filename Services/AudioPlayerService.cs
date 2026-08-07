using System.Windows.Threading;
using NAudio.Wave;

namespace voboX.Services;

/// <summary>音频播放服务（NAudio WaveOutEvent）</summary>
public class AudioPlayerService : IDisposable
{
    private WaveOutEvent? _output;
    private AudioFileReader? _reader;
    private DispatcherTimer? _timer;
    private bool _finished;
    private double _startSec;
    private double _endSec;

    /// <summary>当前播放的文件路径</summary>
    public string? CurrentPath { get; private set; }

    /// <summary>播放进度变化（秒，约 10ms 一次）</summary>
    public event Action<double>? PositionChanged;

    /// <summary>播放自然结束</summary>
    public event Action? PlaybackFinished;

    public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;

    /// <summary>是否已自然播放结束（用于区分"暂停在末尾"与"真正播完"）</summary>
    public bool IsFinished => _finished;

    /// <summary>本次播放的起点偏移（秒）：范围播放=选区起点，整段=0</summary>
    public double PlaybackStart => _startSec;

    /// <summary>
    /// 实际播放位置（按声卡已输出的字节换算）。
    /// 不用 reader.CurrentTime：它会包含预读缓冲，导致播放刚开始就跳到 ~300ms。
    /// </summary>
    public double Position => _output is not null && _reader is not null
        ? _output.GetPosition() / (double)_reader.WaveFormat.AverageBytesPerSecond
        : 0;
    public double Duration => _reader is null ? 0
        : _endSec > _startSec ? _endSec - _startSec       // 范围播放：总长为选区长度
        : Math.Max(0, _reader.TotalTime.TotalSeconds - _startSec);

    /// <summary>播放；startSec~endSec &gt; 0 时只播这个范围（选区），否则整段</summary>
    public void Play(string path, double startSec = 0, double endSec = 0)
    {
        Stop();
        _finished = false;
        CurrentPath = path;
        _startSec = Math.Max(0, startSec);
        _endSec = endSec > _startSec ? endSec : 0;
        _reader = new AudioFileReader(path);
        if (_startSec > 0)
            _reader.CurrentTime = TimeSpan.FromSeconds(_startSec); // 跳到选区起点
        _output = new WaveOutEvent();
        _output.DesiredLatency = 100; // 减小输出缓冲（默认 300ms），降低出声延迟
        _output.Init(_reader);
        _output.PlaybackStopped += (s, e) =>
        {
            // 设备真正停止（缓冲尾音已放完）才算自然结束；且 s 必须是当前 output
            if (e.Exception is null && ReferenceEquals(s, _output))
                FinishPlayback();
        };
        _output.Play();

        // 10ms 定时器更新进度；范围播放时到达范围末尾就结束
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        _timer.Tick += (s, e) =>
        {
            var pos = Position;
            var dur = Duration;
            if (_endSec > _startSec && dur > 0.05 && pos >= dur - 0.03)
            {
                FinishPlayback();
                return;
            }
            PositionChanged?.Invoke(pos);
        };
        _timer.Start();
    }

    /// <summary>自然结束：只停定时器并通知，不强制 Stop 输出，让设备把缓冲尾音自然放完</summary>
    private void FinishPlayback()
    {
        if (_finished) return;
        _finished = true;
        _timer?.Stop();
        _timer = null;
        if (_endSec > _startSec)
        {
            // 范围播放：主动停在范围末尾，不播选区之外
            _output?.Stop();
            _output?.Dispose();
            _output = null;
            _reader?.Dispose();
            _reader = null;
            CurrentPath = null;
        }
        // 整段播放：不 Stop/Dispose 输出：尾音仍在缓冲中，等设备自然放完；资源由下次 Play/退出时统一清理
        PlaybackFinished?.Invoke();
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
        var output = _output;
        _output = null; // 先置空，避免旧 output 的 PlaybackStopped 误触发 FinishPlayback
        var reader = _reader;
        _reader = null;
        CurrentPath = null;
        _finished = false;
        _startSec = 0;
        _endSec = 0;
        output?.Stop();
        output?.Dispose();
        reader?.Dispose();
    }

    public void Dispose() => Stop();
}
