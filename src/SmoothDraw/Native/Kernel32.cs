using System.Runtime.InteropServices;

namespace SmoothDraw.Native;

public static class Kernel32
{
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);
}
