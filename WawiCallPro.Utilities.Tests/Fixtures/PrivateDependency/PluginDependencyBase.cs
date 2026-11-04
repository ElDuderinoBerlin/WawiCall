using System.Runtime.InteropServices;

namespace PrivateDependency;

public class PluginDependencyBase
{
    [DllImport("wcp-native-probe", EntryPoint = "GetFileVersionInfoSizeW", CharSet = CharSet.Unicode)]
    private static extern uint GetVersionSize(string file, out uint handle);
    public int ProbeNative() => checked((int)GetVersionSize("wcp-native-probe-file-does-not-exist", out _));
}
