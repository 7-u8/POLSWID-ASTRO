namespace ASTRO.Polswid.Core.Input;

/// <summary>Controller-neutral digital controls (PlayStation naming). L2/R2 are the *digital* trigger bits.</summary>
[Flags]
public enum PadButton : uint
{
    None = 0,
    Cross = 1u << 0,
    Circle = 1u << 1,
    Square = 1u << 2,
    Triangle = 1u << 3,
    L1 = 1u << 4,
    R1 = 1u << 5,
    L2 = 1u << 6,
    R2 = 1u << 7,
    Share = 1u << 8,
    Options = 1u << 9,
    L3 = 1u << 10,
    R3 = 1u << 11,
    Guide = 1u << 12,
    Touchpad = 1u << 13,
    DpadUp = 1u << 14,
    DpadDown = 1u << 15,
    DpadLeft = 1u << 16,
    DpadRight = 1u << 17,
}

public static class PadButtons
{
    /// <summary>Every single-bit button, in a stable order.</summary>
    public static readonly PadButton[] All =
    {
        PadButton.Cross, PadButton.Circle, PadButton.Square, PadButton.Triangle,
        PadButton.L1, PadButton.R1, PadButton.L2, PadButton.R2,
        PadButton.Share, PadButton.Options, PadButton.L3, PadButton.R3,
        PadButton.Guide, PadButton.Touchpad,
        PadButton.DpadUp, PadButton.DpadDown, PadButton.DpadLeft, PadButton.DpadRight,
    };

    public const PadButton DpadAll = PadButton.DpadUp | PadButton.DpadDown | PadButton.DpadLeft | PadButton.DpadRight;
    public const PadButton Steering = PadButton.DpadLeft | PadButton.DpadRight;

    public static string Display(PadButton b) => b switch
    {
        PadButton.None => "None",
        PadButton.Cross => "Cross (X)",
        PadButton.Circle => "Circle",
        PadButton.Square => "Square",
        PadButton.Triangle => "Triangle",
        PadButton.L1 => "L1",
        PadButton.R1 => "R1",
        PadButton.L2 => "L2",
        PadButton.R2 => "R2",
        PadButton.Share => "Share / Back",
        PadButton.Options => "Options / Start",
        PadButton.L3 => "L3",
        PadButton.R3 => "R3",
        PadButton.Guide => "PS / Guide",
        PadButton.Touchpad => "Touchpad",
        PadButton.DpadUp => "D-Pad Up",
        PadButton.DpadDown => "D-Pad Down",
        PadButton.DpadLeft => "D-Pad Left",
        PadButton.DpadRight => "D-Pad Right",
        _ => b.ToString(),
    };
}

/// <summary>
/// One complete controller state. Sticks use the XInput convention: X right = +1, Y up = +1.
/// Triggers are 0..1. Submitted to the virtual pad as a whole, which is what makes direction changes atomic.
/// </summary>
public struct GamepadState : IEquatable<GamepadState>
{
    public PadButton Buttons;
    public float LX, LY, RX, RY, L2, R2;

    public readonly bool Has(PadButton b) => b != PadButton.None && (Buttons & b) == b;
    public readonly bool HasAny(PadButton b) => (Buttons & b) != 0;

    public void Set(PadButton b, bool down)
    {
        if (down) Buttons |= b; else Buttons &= ~b;
    }

    public readonly bool Equals(GamepadState o) =>
        Buttons == o.Buttons && LX == o.LX && LY == o.LY && RX == o.RX && RY == o.RY && L2 == o.L2 && R2 == o.R2;

    public override readonly bool Equals(object? obj) => obj is GamepadState s && Equals(s);
    public override readonly int GetHashCode() => HashCode.Combine(Buttons, LX, LY, RX, RY, L2, R2);
    public static bool operator ==(GamepadState a, GamepadState b) => a.Equals(b);
    public static bool operator !=(GamepadState a, GamepadState b) => !a.Equals(b);

    public override readonly string ToString() => $"{Buttons} LS({LX:0.00},{LY:0.00}) RS({RX:0.00},{RY:0.00}) L2={L2:0.00} R2={R2:0.00}";
}

public enum SteeringDirection { None = 0, Left = 1, Right = 2, Both = 3 }

public static class SteeringExtensions
{
    public static SteeringDirection Opposite(this SteeringDirection d) => d switch
    {
        SteeringDirection.Left => SteeringDirection.Right,
        SteeringDirection.Right => SteeringDirection.Left,
        _ => d,
    };

    public static SteeringDirection FromButtons(PadButton b)
    {
        bool l = (b & PadButton.DpadLeft) != 0, r = (b & PadButton.DpadRight) != 0;
        return l && r ? SteeringDirection.Both : l ? SteeringDirection.Left : r ? SteeringDirection.Right : SteeringDirection.None;
    }

    public static PadButton ToButtons(this SteeringDirection d) => d switch
    {
        SteeringDirection.Left => PadButton.DpadLeft,
        SteeringDirection.Right => PadButton.DpadRight,
        SteeringDirection.Both => PadButton.DpadLeft | PadButton.DpadRight,
        _ => PadButton.None,
    };
}
