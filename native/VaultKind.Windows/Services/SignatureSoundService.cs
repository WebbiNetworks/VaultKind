using Windows.Media.Core;
using Windows.Media.Playback;

namespace VaultKind_Windows.Services;

internal enum SignatureSound
{
    VaultOpen,
    VaultLocked,
    Warning
}

// Product rule: these are VaultKind's only sound identities. New UI events should
// remain silent unless they communicate a successful open/lock transition or a
// concrete safety warning that cannot rely on visual treatment alone.

internal enum SoundEmphasis
{
    Caution,
    Standard,
    Strong
}

internal sealed class SignatureSoundService
{
    private const int SampleRate = 44100;
    private readonly Dictionary<SignatureSound, MediaPlayer> players = [];
    private readonly Dictionary<SignatureSound, TimeSpan> durationLimits = [];
    private int playbackGeneration;

    internal bool IsEnabled { get; set; } = true;

    internal SignatureSoundService()
    {
        try
        {
            string soundDirectory = Path.Combine(
                VaultKindDataPaths.LocalApplicationDataRoot,
                "VaultKind",
                "Sounds");
            Directory.CreateDirectory(soundDirectory);

            string suppliedOpenSound = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", "vault-open.mp3");
            if (File.Exists(suppliedOpenSound))
            {
                AddFilePlayer(SignatureSound.VaultOpen, suppliedOpenSound, TimeSpan.FromSeconds(3.5));
            }
            else
            {
                AddPlayer(SignatureSound.VaultOpen, soundDirectory, "vault-open.wav", 1.35, OpenSample);
            }
            string suppliedLockedSound = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", "vault-locked.mp3");
            if (File.Exists(suppliedLockedSound))
            {
                AddFilePlayer(SignatureSound.VaultLocked, suppliedLockedSound);
            }
            else
            {
                AddPlayer(SignatureSound.VaultLocked, soundDirectory, "vault-locked.wav", 0.65, LockedSample);
            }
            string windowsWarningSound = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Media",
                "Alarm06.wav");
            if (File.Exists(windowsWarningSound))
            {
                AddFilePlayer(SignatureSound.Warning, windowsWarningSound);
            }
            else
            {
                AddPlayer(SignatureSound.Warning, soundDirectory, "warning.wav", 0.90, WarningSample);
            }
        }
        catch (Exception)
        {
            // Audio is optional and must never interrupt vault work.
        }
    }

    internal async void Play(SignatureSound sound, SoundEmphasis emphasis = SoundEmphasis.Standard)
    {
        if (!IsEnabled || !players.TryGetValue(sound, out MediaPlayer? player))
        {
            return;
        }

        try
        {
            int generation = ++playbackGeneration;
            foreach (MediaPlayer activePlayer in players.Values)
            {
                activePlayer.Pause();
            }
            player.Pause();
            double targetVolume = emphasis switch
            {
                SoundEmphasis.Caution => 0.18,
                SoundEmphasis.Strong => 0.62,
                _ => sound == SignatureSound.VaultLocked ? 0.40 : 0.34
            };
            player.Volume = targetVolume;
            player.PlaybackSession.Position = TimeSpan.Zero;
            player.Play();

            TimeSpan? playbackLimit = sound == SignatureSound.Warning && emphasis == SoundEmphasis.Caution
                ? TimeSpan.FromMilliseconds(220)
                : durationLimits.TryGetValue(sound, out TimeSpan configuredLimit) ? configuredLimit : null;
            if (playbackLimit is TimeSpan duration)
            {
                TimeSpan fadeDuration = duration <= TimeSpan.FromMilliseconds(300)
                    ? TimeSpan.FromMilliseconds(60)
                    : TimeSpan.FromMilliseconds(140);
                await Task.Delay(duration - fadeDuration);
                for (int step = 3; step >= 0; step--)
                {
                    if (generation != playbackGeneration)
                    {
                        return;
                    }

                    player.Volume = targetVolume * step / 4d;
                    await Task.Delay(TimeSpan.FromMilliseconds(35));
                }

                if (generation == playbackGeneration)
                {
                    player.Pause();
                }
            }
        }
        catch (Exception)
        {
            // A missing or unavailable audio device must remain silent and harmless.
        }
    }

    private void AddPlayer(
        SignatureSound sound,
        string directory,
        string fileName,
        double durationSeconds,
        Func<double, double> sampleFactory)
    {
        string path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, CreateWave(durationSeconds, sampleFactory));
        players[sound] = new MediaPlayer
        {
            AutoPlay = false,
            Source = MediaSource.CreateFromUri(new Uri(path))
        };
    }

    private void AddFilePlayer(SignatureSound sound, string path, TimeSpan? durationLimit = null)
    {
        players[sound] = new MediaPlayer
        {
            AutoPlay = false,
            Source = MediaSource.CreateFromUri(new Uri(path))
        };
        if (durationLimit is TimeSpan duration)
        {
            durationLimits[sound] = duration;
        }
    }

    private static byte[] CreateWave(double durationSeconds, Func<double, double> sampleFactory)
    {
        int sampleCount = (int)(SampleRate * durationSeconds);
        int dataLength = sampleCount * sizeof(short);
        using var stream = new MemoryStream(44 + dataLength);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataLength);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * sizeof(short));
        writer.Write((short)sizeof(short));
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(dataLength);

        for (int index = 0; index < sampleCount; index++)
        {
            double time = index / (double)SampleRate;
            double sample = Math.Clamp(sampleFactory(time), -1d, 1d);
            writer.Write((short)(sample * short.MaxValue));
        }

        return stream.ToArray();
    }

    private static double OpenSample(double time)
    {
        double latch = Math.Exp(-28d * time)
            * (0.48 * Math.Sin(2d * Math.PI * 410d * time)
                + 0.30 * Math.Sin(2d * Math.PI * 790d * time));
        double movementTime = Math.Max(0d, time - 0.08d);
        double movementEnvelope = movementTime <= 0d
            ? 0d
            : Math.Min(1d, movementTime / 0.12d) * Math.Exp(-1.45d * movementTime);
        double massiveDoor = movementEnvelope
            * (0.54 * Math.Sin(2d * Math.PI * 43d * movementTime)
                + 0.30 * Math.Sin(2d * Math.PI * 67d * movementTime)
                + 0.12 * Math.Sin(2d * Math.PI * 173d * movementTime)
                    * (0.65d + 0.35d * Math.Sin(2d * Math.PI * 11d * movementTime)));
        double releaseTime = time - 0.72d;
        double release = releaseTime < 0d
            ? 0d
            : Math.Exp(-9d * releaseTime)
                * (0.34 * Math.Sin(2d * Math.PI * 76d * releaseTime)
                    + 0.17 * Math.Sin(2d * Math.PI * 245d * releaseTime));
        return latch + massiveDoor + release;
    }

    private static double LockedSample(double time)
    {
        double body = Math.Exp(-16d * time)
            * (0.58 * Math.Sin(2d * Math.PI * 92d * time)
                + 0.30 * Math.Sin(2d * Math.PI * 138d * time));
        double knock = time < 0.035
            ? (1d - time / 0.035d) * 0.22 * Math.Sin(2d * Math.PI * 520d * time)
            : 0d;
        return body + knock;
    }

    private static double WarningSample(double time)
    {
        double first = WarningPulse(time, 0d);
        double second = WarningPulse(time, 0.46d);
        return first + second;
    }

    private static double WarningPulse(double time, double start)
    {
        const double duration = 0.34;
        double local = time - start;
        if (local < 0d || local > duration)
        {
            return 0d;
        }

        double normalized = local / duration;
        double envelope = Math.Pow(Math.Sin(Math.PI * normalized), 0.72d);
        double frequency = 188d - 28d * normalized;
        double phase = 2d * Math.PI * frequency * local;
        return envelope
            * (0.66 * Math.Sin(phase)
                + 0.22 * Math.Sin(2d * phase)
                + 0.09 * Math.Sin(3d * phase));
    }
}
