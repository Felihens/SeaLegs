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
            if (_broken || channels <= 0 || float.IsNaN(r) || float.IsInfinity(r) || Mathf.Abs(r - 1f) < 0.001f) return;
            try { Shift(data, channels, r); }
            catch (System.Exception e)
            {
                // Never let an error repeat on every audio block: play this voice unshifted from now on.
                _broken = true;
                Debug.LogWarning("[SeaLegs] Pitch shift turned off for this voice: " + e.Message);
            }
        }

        private volatile bool _broken;

        private void Shift(float[] data, int channels, float r)
        {
            if (float.IsNaN(_phase) || _phase < 0f || _phase >= Window) _phase = 0f;
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
            // Split into a whole and a fraction first, then wrap the whole part as an int:
            // wrapping a float can round up to exactly Size and run past the buffer.
            if (float.IsNaN(delay) || float.IsInfinity(delay)) delay = 0f;
            int whole = (int)delay;
            float frac = delay - whole;
            int i = ((_write - 1 - whole) % Size + Size) % Size;
            int k = (i - 1 + Size) % Size;
            // pos = i - frac: blend from sample i back toward sample i-1
            return b[i] + (b[k] - b[i]) * frac;
        }
    }
}
