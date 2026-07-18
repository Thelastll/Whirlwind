using NAudio.Wave;

namespace Whirlwind.Classes
{
    internal static class PlaySounds
    {
        private static IWavePlayer _output;
        private static AudioFileReader _audioFile;

        public static float GlobalVolume = Properties.Settings.Default.Volume;

        public static void PlayNotificationSound()
        {
            Stop();

            _output = new WaveOutEvent();
            _audioFile = new AudioFileReader("Sounds/multimedia-message-arrival-sound.wav")
            {
                Volume = GlobalVolume,
            };
            _output.Init(_audioFile);
            _output.Play();
        }

        public static void Stop()
        {
            if (_output != null)
            {
                _output.Stop();
                _output.Dispose();
                _output = null;
            }

            if (_audioFile != null)
            {
                _audioFile.Dispose();
                _audioFile = null;
            }
        }
    }
}
