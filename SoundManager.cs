using System.Media;

namespace RoletaDaDaily;

internal enum SpinSound { Casino, Arcade8Bit, Bells, Fanfare, Off }

internal sealed class SoundManager : IDisposable
{
    private readonly object _gate = new();
    private SoundPlayer? _player;
    private MemoryStream? _stream;
    private System.Threading.Timer? _cleanup;

    public static IReadOnlyList<string> Options { get; } = ["Cassino", "Arcade 8-bit", "Sinos", "Fanfarra", "Desligado"];

    public void Play(SpinSound sound)
    {
        if (sound == SpinSound.Off) return;
        StopCurrent();
        byte[] wave = CreateWave(sound);
        var stream = new MemoryStream(wave, writable: false);
        var player = new SoundPlayer(stream);
        lock (_gate)
        {
            _stream = stream;
            _player = player;
            player.Play();
            int durationMs = wave.Length / (22050 * 2) + 250;
            _cleanup = new System.Threading.Timer(_ => StopCurrent(), null, durationMs, Timeout.Infinite);
        }
    }

    private void StopCurrent()
    {
        lock (_gate)
        {
            _cleanup?.Dispose();
            _cleanup = null;
            _player?.Stop();
            _player?.Dispose();
            _player = null;
            _stream?.Dispose();
            _stream = null;
        }
    }

    private static byte[] CreateWave(SpinSound sound)
    {
        const int sampleRate = 22050;
        (double Frequency, int Milliseconds, double Volume)[] notes = sound switch
        {
            SpinSound.Casino => [(523.25, 105, .38), (659.25, 105, .38), (783.99, 105, .38), (1046.5, 260, .42)],
            SpinSound.Arcade8Bit => [(659.25, 85, .32), (783.99, 85, .32), (987.77, 85, .32), (1318.5, 220, .36)],
            SpinSound.Bells => [(880, 150, .34), (1174.66, 180, .34), (1567.98, 360, .38)],
            SpinSound.Fanfare => [(392, 130, .34), (523.25, 130, .34), (659.25, 130, .34), (783.99, 380, .4)],
            _ => []
        };

        int samples = notes.Sum(n => sampleRate * n.Milliseconds / 1000);
        using var stream = new MemoryStream(44 + samples * 2);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8); writer.Write(36 + samples * 2); writer.Write("WAVE"u8);
        writer.Write("fmt "u8); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
        writer.Write(sampleRate); writer.Write(sampleRate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write("data"u8); writer.Write(samples * 2);

        foreach ((double frequency, int milliseconds, double volume) in notes)
        {
            int count = sampleRate * milliseconds / 1000;
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / sampleRate;
                double attack = Math.Min(1, i / (sampleRate * 0.012));
                double release = Math.Min(1, (count - i) / (sampleRate * 0.05));
                double envelope = Math.Min(attack, release);
                double fundamental = Math.Sin(2 * Math.PI * frequency * t);
                double tone = sound == SpinSound.Arcade8Bit
                    ? Math.Sign(fundamental) * 0.55 + Math.Sin(2 * Math.PI * frequency * 2 * t) * 0.18
                    : fundamental * 0.76 + Math.Sin(2 * Math.PI * frequency * 2 * t) * 0.16 + Math.Sin(2 * Math.PI * frequency * 3 * t) * 0.08;
                short sample = (short)Math.Clamp(tone * envelope * volume * short.MaxValue, short.MinValue, short.MaxValue);
                writer.Write(sample);
            }
        }
        return stream.ToArray();
    }

    public void Dispose() => StopCurrent();
}
