using System.Runtime.InteropServices;

namespace RoletaDaDaily;

internal enum SpinSound
{
    Casino,
    Arcade8Bit,
    Bells,
    Fanfare,
    Off
}

internal sealed class SoundManager : IDisposable
{
    private const int SampleRate = 22050;
    private const uint SndAsync = 0x0001;
    private const uint SndNoDefault = 0x0002;
    private const uint SndMemory = 0x0004;
    private static readonly string[] SoundNames = ["Cassino", "Arcade 8-bit", "Sinos", "Fanfarra", "Desligado"];
    private readonly object _gate = new();
    private byte[]? _wave;
    private GCHandle _pinnedWave;
    private System.Threading.Timer? _cleanupTimer;
    private int _playbackId;
    private bool _disposed;

    public static IReadOnlyList<string> Options => SoundNames;

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySound(IntPtr sound, IntPtr module, uint flags);

    public void Play(SpinSound sound)
    {
        lock (_gate)
        {
            if (_disposed) return;
            StopLocked();
            if (sound == SpinSound.Off) return;

            byte[] wave = CreateWave(sound);
            _wave = wave;
            _pinnedWave = GCHandle.Alloc(wave, GCHandleType.Pinned);
            if (!PlaySound(_pinnedWave.AddrOfPinnedObject(), IntPtr.Zero, SndAsync | SndMemory | SndNoDefault))
            {
                _pinnedWave.Free();
                _wave = null;
                System.Media.SystemSounds.Asterisk.Play();
                return;
            }

            int playbackId = _playbackId;
            int durationMs = Math.Max(250, (wave.Length - 44) * 1000 / (SampleRate * 2) + 300);
            _cleanupTimer = new System.Threading.Timer(_ => Cleanup(playbackId), null, durationMs, Timeout.Infinite);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!_disposed) StopLocked();
        }
    }

    private void Cleanup(int playbackId)
    {
        lock (_gate)
        {
            if (_disposed || playbackId != _playbackId) return;
            _cleanupTimer?.Dispose();
            _cleanupTimer = null;
            if (_pinnedWave.IsAllocated) _pinnedWave.Free();
            _wave = null;
        }
    }

    private void StopLocked()
    {
        _playbackId++;
        _cleanupTimer?.Dispose();
        _cleanupTimer = null;
        if (_wave is not null) PlaySound(IntPtr.Zero, IntPtr.Zero, 0);
        if (_pinnedWave.IsAllocated) _pinnedWave.Free();
        _wave = null;
    }

    private static byte[] CreateWave(SpinSound sound)
    {
        const double duration = 1.45;
        int sampleCount = (int)(SampleRate * duration);
        using var stream = new MemoryStream(44 + sampleCount * 2);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8); writer.Write(36 + sampleCount * 2); writer.Write("WAVE"u8);
        writer.Write("fmt "u8); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
        writer.Write(SampleRate); writer.Write(SampleRate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write("data"u8); writer.Write(sampleCount * 2);

        var random = new Random((int)sound * 173 + 29);
        double[] notes = sound switch
        {
            SpinSound.Casino => [659.25, 783.99, 987.77, 1318.51],
            SpinSound.Arcade8Bit => [523.25, 659.25, 783.99, 1046.5],
            SpinSound.Bells => [783.99, 987.77, 1174.66, 1567.98],
            _ => [523.25, 659.25, 783.99, 1046.5]
        };
        for (int i = 0; i < sampleCount; i++)
        {
            double t = i / (double)SampleRate;
            double envelope = Math.Min(1, t * 14) * Math.Min(1, (duration - t) * 8);
            double value;
            if (sound == SpinSound.Casino)
            {
                double click = i % (SampleRate / 8) < 120 ? (random.NextDouble() - 0.5) * 0.36 : 0;
                value = Math.Sin(2 * Math.PI * notes[(int)(t * 3.2) % notes.Length] * t) * 0.25 + click;
            }
            else if (sound == SpinSound.Arcade8Bit)
            {
                double frequency = notes[Math.Min(notes.Length - 1, (int)(t * 3))];
                value = Math.Sign(Math.Sin(2 * Math.PI * frequency * t)) * 0.22;
            }
            else if (sound == SpinSound.Bells)
            {
                double frequency = notes[Math.Min(notes.Length - 1, (int)(t * 2.6))];
                value = (Math.Sin(2 * Math.PI * frequency * t) * 0.28 + Math.Sin(4 * Math.PI * frequency * t) * 0.10) * Math.Exp(-(t % 0.38) * 3.2);
            }
            else
            {
                double frequency = notes[Math.Min(notes.Length - 1, (int)(t * 2.2))];
                value = (Math.Sin(2 * Math.PI * frequency * t) * 0.22 + Math.Sin(2 * Math.PI * frequency * 1.5 * t) * 0.08);
            }
            short sample = (short)(Math.Clamp(value * envelope, -0.95, 0.95) * short.MaxValue);
            writer.Write(sample);
        }
        return stream.ToArray();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            StopLocked();
            _disposed = true;
        }
    }
}

