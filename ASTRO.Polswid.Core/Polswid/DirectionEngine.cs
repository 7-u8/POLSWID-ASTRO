using ASTRO.Polswid.Core.Input;
using ASTRO.Polswid.Core.Profiles;

namespace ASTRO.Polswid.Core.Polswid;

public enum DirectionOwner { None, DPad, Analog, Continuous }

/// <summary>
/// Decides the one steering direction the virtual pad receives each frame, from the complete next state of all
/// sources. Old -> New therefore is a single update. Neutral is only produced when no source requests a direction.
/// </summary>
public sealed class DirectionEngine
{
    SteeringDirection _current;
    long _changedAtUs;
    SteeringDirection _padDir, _padPrev, _analogPrev;
    bool _padLeftPrev, _padRightPrev;
    DirectionOwner _lastActive;
    SteeringDirection _flipOverride, _srcAtFlip;
    bool _flipPendingCapture;

    public SteeringDirection Current => _current;
    public DirectionOwner Owner { get; private set; }
    public bool Changed { get; private set; }

    public void Reset()
    {
        _current = _padDir = _padPrev = _analogPrev = SteeringDirection.None;
        _padLeftPrev = _padRightPrev = false;
        _lastActive = DirectionOwner.None;
        _flipOverride = _srcAtFlip = SteeringDirection.None;
        _flipPendingCapture = false;
        _changedAtUs = 0;
        Changed = false;
        Owner = DirectionOwner.None;
    }

    /// <summary>One-shot flip (air switch). Holds until the physical source changes.</summary>
    public void Flip()
    {
        if (_current is SteeringDirection.None or SteeringDirection.Both) return;
        _flipOverride = _current.Opposite();
        _flipPendingCapture = true;
    }

    public SteeringDirection Resolve(bool padLeft, bool padRight, SteeringDirection analog,
        DirectionSettings s, int minDwellMs, long nowUs)
    {
        SteeringDirection pad;
        if (padLeft && padRight)
        {
            if (s.AllowContradictory) pad = SteeringDirection.Both;
            else if (!_padLeftPrev && _padRightPrev) pad = SteeringDirection.Left;      // newest press wins
            else if (!_padRightPrev && _padLeftPrev) pad = SteeringDirection.Right;
            else pad = _padDir is SteeringDirection.Left or SteeringDirection.Right ? _padDir : SteeringDirection.Left;
        }
        else pad = padLeft ? SteeringDirection.Left : padRight ? SteeringDirection.Right : SteeringDirection.None;
        _padLeftPrev = padLeft; _padRightPrev = padRight;
        _padDir = pad;

        if (analog != _analogPrev) { if (analog != SteeringDirection.None) _lastActive = DirectionOwner.Analog; _analogPrev = analog; }
        if (pad != _padPrev) { if (pad != SteeringDirection.None) _lastActive = DirectionOwner.DPad; _padPrev = pad; }

        DirectionOwner owner;
        SteeringDirection src;
        switch (s.Priority)
        {
            case PriorityMode.DPadFirst:
                if (pad != SteeringDirection.None) { src = pad; owner = DirectionOwner.DPad; }
                else { src = analog; owner = analog != SteeringDirection.None ? DirectionOwner.Analog : DirectionOwner.None; }
                break;
            case PriorityMode.LastInputWins:
                if (_lastActive == DirectionOwner.Analog && analog != SteeringDirection.None) { src = analog; owner = DirectionOwner.Analog; }
                else if (_lastActive == DirectionOwner.DPad && pad != SteeringDirection.None) { src = pad; owner = DirectionOwner.DPad; }
                else if (analog != SteeringDirection.None) { src = analog; owner = DirectionOwner.Analog; }
                else { src = pad; owner = pad != SteeringDirection.None ? DirectionOwner.DPad : DirectionOwner.None; }
                break;
            default: // AnalogFirst: analog owns steering while active, otherwise it returns to the D-Pad
                if (analog != SteeringDirection.None) { src = analog; owner = DirectionOwner.Analog; }
                else { src = pad; owner = pad != SteeringDirection.None ? DirectionOwner.DPad : DirectionOwner.None; }
                break;
        }

        if (_flipPendingCapture) { _srcAtFlip = src; _flipPendingCapture = false; }
        if (_flipOverride != SteeringDirection.None)
        {
            if (src != _srcAtFlip) _flipOverride = SteeringDirection.None;
            else src = _flipOverride;
        }

        SteeringDirection next = src;
        if (minDwellMs > 0 && _current != SteeringDirection.None && next != _current &&
            nowUs - _changedAtUs < minDwellMs * 1000L)
            next = _current;

        Changed = next != _current;
        if (Changed) _changedAtUs = nowUs;
        _current = next;
        Owner = next == SteeringDirection.None ? DirectionOwner.None : owner;
        return next;
    }
}
