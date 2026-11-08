using UnityEngine;

namespace SeaLegs
{
    // Shifts the pitch of the sound coming out of the AudioSource on the same object, without changing
    // its speed, so a voice layer stays in time with the song. Two read heads slide through a short
    // delay line and cross-fade, the classic "rotating tape head" trick. Runs on the audio thread.
    internal class PitchShift : MonoBehaviour
    {
        internal volatile float Ratio = 1f;   // 2^(semitones/12)

        private const int Window = 2048;      // ~46 ms at 44.1 kHz
        private const int Size = 8192;
        private float[][] _buf;
        private int _write;
        private float _phase;

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float r = Ratio;
            if (channels <= 0 || Mathf.Abs(r - 1f) < 0.001f) return;
            if (_buf == null || _buf.Length != channels)
            {
                _buf = new float[channels][];
                for (int c = 0; c < channels; c++) _buf[c] = new float[Size];
                _write = 0;
                _phase = 0f;
            }
            float step = 1f - r;
            int frames = data.Length / channels;
            for (int f = 0; f < frames; f++)
            {
                float d1 = _phase;
                float d2 = d1 + Window * 0.5f;
                if (d2 >= Window) d2 -= Window;
                float g1 = 1f - Mathf.Abs(2f * d1 / Window - 1f);
                float g2 = 1f - g1;
                for (int c = 0; c < channels; c++)
                {
                    float[] b = _buf[c];
                    b[_write] = data[f * channels + c];
                    data[f * channels + c] = Tap(b, d1) * g1 + Tap(b, d2) * g2;
                }
                _write = (_write + 1) % Size;
                _phase += step;
                if (_phase >= Window) _phase -= Window;
                else if (_phase < 0f) _phase += Window;
            }
        }

        private float Tap(float[] b, float delay)
        {
            float pos = _write - 1f - delay;
            while (pos < 0f) pos += Size;
            int i = (int)pos;
            float frac = pos - i;
            int j = (i + 1) % Size;
            return b[i] + (b[j] - b[i]) * frac;
        }
    }
}
