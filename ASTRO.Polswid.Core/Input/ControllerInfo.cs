namespace ASTRO.Polswid.Core.Input;

/// <summary>Controller family from ids and the input API's reported type. Never from a product name.</summary>
public static class ControllerClassifier
{
    public const ushort Sony = 0x054C, Microsoft = 0x045E, Nintendo = 0x057E;

    /// <param name="sdlType">The API's own type string (e.g. PS4, PS5, XboxOne, Xbox360, Standard).</param>
    public static string Family(ushort vendor, ushort product, string sdlType)
    {
        if (vendor == Sony)
        {
            if (product is 0x0DF2) return "DualSense Edge";
            if (product is 0x0CE6) return "DualSense";
            if (product is 0x05C4 or 0x09CC or 0x0BA0) return "DualShock 4";
            return sdlType.ToUpperInvariant() switch { "PS5" => "DualSense", "PS4" => "DualShock 4", _ => "Sony gamepad" };
        }
        if (vendor == Microsoft || sdlType.StartsWith("Xbox", StringComparison.OrdinalIgnoreCase))
        {
            return product switch
            {
                0x0B13 or 0x0B12 => "Xbox Series X|S",
                0x02EA or 0x02FD or 0x02E0 or 0x02DD or 0x02D1 => "Xbox One / One S",
                0x028E or 0x028F or 0x0719 => "Xbox 360",
                _ => sdlType.Equals("Xbox360", StringComparison.OrdinalIgnoreCase) ? "Xbox 360 / XInput" : sdlType.StartsWith("XboxOne", StringComparison.OrdinalIgnoreCase) ? "Xbox One / Series" : "Xbox / XInput",
            };
        }
        if (vendor == Nintendo) return "Nintendo gamepad";
        return "Generic gamepad";
    }
}

/// <summary>
/// Pure device-selection policy. With an explicit primary only that device is ever chosen; in automatic mode the
/// active device is kept until it disappears, then the first usable one is taken.
/// </summary>
public static class DeviceSelector
{
    public static PhysicalDeviceInfo? Choose(IReadOnlyList<PhysicalDeviceInfo> devices, string? preferredKey, uint? activeId)
    {
        bool Usable(PhysicalDeviceInfo d) => d.IsSupported && !d.IsLikelyVirtual;

        if (preferredKey != null)
            return devices.FirstOrDefault(d => Usable(d) && d.Key == preferredKey);

        if (activeId is { } a)
        {
            var keep = devices.FirstOrDefault(d => d.Id == a && Usable(d));
            if (keep != null) return keep;
        }
        return devices.FirstOrDefault(Usable);
    }
}
