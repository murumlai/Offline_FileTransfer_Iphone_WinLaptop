using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace OfflineFileTransfer.WindowsDevices.Shell;

/// <summary>
/// Pumps the Win32 message queue on the current STA thread. The Windows shell
/// <c>Folder.CopyHere</c> operation is asynchronous and only progresses while the
/// calling STA thread dispatches messages, so a blocking wait must pump here.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class MessagePump
{
    private const uint PmRemove = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Hwnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(
        out NativeMessage message, IntPtr handle, uint filterMin, uint filterMax, uint remove);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref NativeMessage message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref NativeMessage message);

    /// <summary>
    /// Processes all currently queued messages, then returns.
    /// </summary>
    public static void DoEvents()
    {
        while (PeekMessage(out var msg, IntPtr.Zero, 0, 0, PmRemove))
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }
}
