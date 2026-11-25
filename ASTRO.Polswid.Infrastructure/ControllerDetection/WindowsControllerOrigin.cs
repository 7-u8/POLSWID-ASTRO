using System.Runtime.InteropServices;
using System.Text;
using ASTRO.Polswid.Core.Input;

namespace ASTRO.Polswid.Infrastructure.ControllerDetection;

/// <summary>Read-only documented Configuration Manager queries on an interface already visible to SDL.</summary>
internal static class WindowsControllerOrigin
{
    [StructLayout(LayoutKind.Sequential)]
    struct PropertyKey { public Guid Format; public uint Id; }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern uint CM_Get_Device_Interface_PropertyW(string path, ref PropertyKey key, out uint type,
        byte[] buffer, ref uint size, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern uint CM_Locate_DevNodeW(out uint node, string? instance, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern uint CM_Get_Device_IDW(uint node, StringBuilder id, uint length, uint flags);
    [DllImport("cfgmgr32.dll", ExactSpelling = true)]
    static extern uint CM_Get_Parent(out uint parent, uint node, uint flags);

    internal static ControllerOrigin ClassifyAncestors(IEnumerable<string> ids)
    {
        bool physical = false;
        foreach (string id in ids)
        {
            if (id.StartsWith(@"ROOT\VIGEMBUS\", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith(@"VIGEM\", StringComparison.OrdinalIgnoreCase)) return ControllerOrigin.ViGEm;
            if (id.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith(@"BTHENUM\", StringComparison.OrdinalIgnoreCase)
                || id.StartsWith(@"BTHLEDEVICE\", StringComparison.OrdinalIgnoreCase)) physical = true;
        }
        return physical ? ControllerOrigin.Physical : ControllerOrigin.Unknown;
    }

    internal static ControllerOrigin Query(string? path)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(path)) return ControllerOrigin.Unknown;
        var key = new PropertyKey { Format = new Guid("78c34fc8-104a-4aca-9ea4-524d52996e57"), Id = 256 };
        var buffer = new byte[4096];
        uint size = (uint)buffer.Length;
        if (CM_Get_Device_Interface_PropertyW(path, ref key, out uint type, buffer, ref size, 0) != 0
            || type != 0x12 || size > buffer.Length) return ControllerOrigin.Unknown; // DEVPROP_TYPE_STRING
        string instance = Encoding.Unicode.GetString(buffer, 0, (int)size).TrimEnd('\0');
        if (CM_Locate_DevNodeW(out uint node, instance, 0) != 0) return ControllerOrigin.Unknown;
        if (CM_Locate_DevNodeW(out uint root, null, 0) != 0) return ControllerOrigin.Unknown;
        var ancestors = new List<string>();
        for (int depth = 0; depth < 32; depth++)
        {
            var id = new StringBuilder(1024);
            if (CM_Get_Device_IDW(node, id, (uint)id.Capacity, 0) != 0) return ControllerOrigin.Unknown;
            ancestors.Add(id.ToString());
            if (node == root) return ClassifyAncestors(ancestors);
            if (CM_Get_Parent(out uint parent, node, 0) != 0) return ControllerOrigin.Unknown;
            node = parent;
        }
        return ControllerOrigin.Unknown; // A partial chain must not certify an emulated USB child as physical.
    }
}
