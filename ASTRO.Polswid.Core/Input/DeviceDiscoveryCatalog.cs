namespace ASTRO.Polswid.Core.Input;

/// <summary>Conservative de-duplication and lifetime-stable selection keys for SDL's visible inventory.</summary>
public sealed class DeviceDiscoveryCatalog
{
    readonly Dictionary<string, string> _keys = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, int> _nextSuffix = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<PhysicalDeviceInfo> Update(IEnumerable<PhysicalDeviceInfo> devices)
    {
        var result = new List<PhysicalDeviceInfo>();
        var ids = new HashSet<uint>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // If SDL exposes an alias, retain the open instance instead of switching handles during refresh.
        foreach (var d in devices.OrderByDescending(d => d.IsSelected))
        {
            if (!ids.Add(d.Id)) continue;
            bool hasPath = !string.IsNullOrWhiteSpace(d.Path);
            string signature = $"{d.VendorId:X4}:{d.ProductId:X4}";
            string identity = signature + "/" + (hasPath ? d.Path!.Trim() : "instance:" + d.Id);
            if (hasPath && !paths.Add(identity)) continue;

            // Virtual devices cannot consume the physical VID:PID slot and change a saved selection key.
            string slot = (d.IsLikelyVirtual ? "virtual:" : "") + signature;
            string cacheKey = (d.IsLikelyVirtual ? "virtual:" : "physical:") + identity;
            if (!_keys.TryGetValue(cacheKey, out var key))
            {
                _nextSuffix.TryGetValue(slot, out int suffix);
                key = suffix == 0 ? slot : $"{slot}#{suffix}";
                _nextSuffix[slot] = suffix + 1;
                _keys.Add(cacheKey, key);
            }
            result.Add(d with { Key = key });
        }
        return result;
    }
}
