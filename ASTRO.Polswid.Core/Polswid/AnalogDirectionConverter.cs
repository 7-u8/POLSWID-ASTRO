using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.Profiles;

namespace ASTRO.Polswid.Core.Polswid;

/// <summary>
/// Left-stick X -> digital Left/Right with deadzone, sensitivity, response curve, smoothing and
/// activation/release hysteresis. Going Left -> Right is a single state change (no intermediate None
/// state is ever reported when the shaped axis crosses straight to the opposite activation threshold).
/// </summary>
public sealed class AnalogDirectionConverter
{
    float _smoothed;
    SteeringDirection _dir;

    /// <summary>Shaped axis value (-1..1) after deadzone, sensitivity, curve and smoothing.</summary>
    public float Shaped { get; private set; }
    public SteeringDirection Direction => _dir;

    public void Reset()
    {
        _smoothed = 0;
        Shaped = 0;
        _dir = SteeringDirection.None;
    }

    public SteeringDirection Process(float rawX, AnalogSettings s)
    {
        float x = s.InvertX ? -rawX : rawX;
        float dz = Math.Clamp(s.Deadzone, 0f, 0.95f);
        float ax = Math.Abs(x);
        float v = ax <= dz ? 0f : MathF.Sign(x) * ((ax - dz) / (1f - dz));
        v = Math.Clamp(v * s.Sensitivity, -1f, 1f);
        if (s.ResponseCurve != 1f && v != 0f)
            v = MathF.Sign(v) * MathF.Pow(Math.Abs(v), Math.Max(0.1f, s.ResponseCurve));

        float k = Math.Clamp(s.Smoothing, 0f, 0.95f);
        _smoothed = k <= 0f ? v : _smoothed + (v - _smoothed) * (1f - k);
        float sv = _smoothed;
        Shaped = sv;

        float act = Math.Clamp(s.ActivationThreshold, 0.01f, 1f);
        float rel = Math.Clamp(s.ReleaseThreshold, 0f, act); // release can never exceed activation

        switch (_dir)
        {
            case SteeringDirection.None:
                if (sv >= act) _dir = SteeringDirection.Right;
                else if (sv <= -act) _dir = SteeringDirection.Left;
                break;
            case SteeringDirection.Right:
                if (sv <= -act) _dir = SteeringDirection.Left;       // direct reversal
                else if (sv < rel) _dir = SteeringDirection.None;
                break;
            case SteeringDirection.Left:
                if (sv >= act) _dir = SteeringDirection.Right;       // direct reversal
                else if (sv > -rel) _dir = SteeringDirection.None;
                break;
        }
        return _dir;
    }
}
