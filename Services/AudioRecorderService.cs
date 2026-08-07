using NAudio.Wave;

namespace voboX.Services;

/// <summary>录音服务（WaveInEvent → WAV）</summary>
public class AudioRecorderService : IDisposable
{
    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private bool _recording;

    public bool IsRecording => _recording;
    public string? OutputPath { get; private set; }

    /// <summary>枚举所有输入设备名</summary>
    public static string[] GetDeviceNames()
    {
        var names = new List<string>();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            names.Add(WaveInEvent.GetCapabilities(i).ProductName);
        return names.ToArray();
    }

    public void Start(string outputPath, int deviceIndex)
    {
        Stop();
        OutputPath = outputPath;
        _waveIn = new WaveInEvent
        {
            DeviceNumber = deviceIndex,
            WaveFormat = new WaveFormat(44100, 16, 1),
            BufferMilliseconds = 100,
        };
        _writer = new WaveFileWriter(outputPath, _waveIn.WaveFormat);
        _waveIn.DataAvailable += (s, e) => _writer?.Write(e.Buffer, 0, e.BytesRecorded);
        _waveIn.RecordingStopped += (s, e) =>
        {
            _recording = false;
            _writer?.Dispose();
            _writer = null;
        };
        _waveIn.StartRecording();
        _recording = true;
    }

    public void Stop()
    {
        _waveIn?.StopRecording();
        _waveIn?.Dispose();
        _waveIn = null;
        _recording = false;
    }

    public void Dispose() => Stop();
}
