using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Homepad.Core
{
    public sealed class NativeSerialPort : IDisposable
    {
        private FileStream unixStream;
        private IntPtr windowsHandle = new IntPtr(-1);
        private readonly string portName;
        private readonly int baudRate;
        private bool disposed;

        public string PortName => portName;
        public int BaudRate => baudRate;
        public bool IsOpen => unixStream != null || (windowsHandle != IntPtr.Zero && windowsHandle != new IntPtr(-1));

        public NativeSerialPort(string portName, int baudRate)
        {
            this.portName = portName;
            this.baudRate = baudRate;
        }

        public static bool IsWindows
        {
            get
            {
                return Environment.OSVersion.Platform == PlatformID.Win32NT;
            }
        }

        public static string[] GetPortNames()
        {
            return IsWindows ? GetWindowsPortNames() : GetUnixPortNames();
        }

        public void Open()
        {
            Close();
            if (string.IsNullOrWhiteSpace(portName))
            {
                throw new IOException("시리얼 포트가 비어 있습니다.");
            }

            if (IsWindows)
            {
                OpenWindows();
            }
            else
            {
                OpenUnix();
            }
        }

        public void Close()
        {
            if (unixStream != null)
            {
                try { unixStream.Dispose(); } catch { }
                unixStream = null;
            }

            if (windowsHandle != IntPtr.Zero && windowsHandle != new IntPtr(-1))
            {
                CloseHandle(windowsHandle);
                windowsHandle = new IntPtr(-1);
            }
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            if (unixStream != null)
            {
                unixStream.Write(buffer, offset, count);
                unixStream.Flush();
                return;
            }

            if (!IsOpen) throw new IOException("시리얼 포트가 열려 있지 않습니다.");
            var slice = new byte[count];
            Buffer.BlockCopy(buffer, offset, slice, 0, count);
            if (!WriteFile(windowsHandle, slice, count, out int written, IntPtr.Zero) || written != count)
            {
                throw new IOException("시리얼 쓰기 실패");
            }
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            if (unixStream != null)
            {
                return unixStream.Read(buffer, offset, count);
            }

            if (!IsOpen) return 0;
            var slice = new byte[count];
            if (!ReadFile(windowsHandle, slice, count, out int read, IntPtr.Zero))
            {
                return 0;
            }

            if (read > 0)
            {
                Buffer.BlockCopy(slice, 0, buffer, offset, read);
            }

            return read;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Close();
        }

        private void OpenUnix()
        {
            ConfigureUnixPort(portName, baudRate);
            unixStream = new FileStream(portName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, 1, false);
        }

        private static void ConfigureUnixPort(string path, int baud)
        {
            string args = path.StartsWith("/dev/", StringComparison.Ordinal)
                ? $"-f {path} {baud} cs8 -cstopb -parenb clocal cread raw -echo -ixon -ixoff"
                : $"{path} {baud} cs8 -cstopb -parenb clocal cread raw -echo";

            var info = new ProcessStartInfo
            {
                FileName = "/bin/stty",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(info);
            if (process == null)
            {
                throw new IOException("stty를 실행할 수 없습니다.");
            }

            if (!process.WaitForExit(2000))
            {
                try { process.Kill(); } catch { }
                throw new IOException("stty 시간 초과");
            }

            if (process.ExitCode != 0)
            {
                string err = process.StandardError.ReadToEnd();
                throw new IOException($"포트 설정 실패 ({path}): {err}");
            }
        }

        private static string[] GetUnixPortNames()
        {
            var names = new List<string>();
            if (!Directory.Exists("/dev")) return names.ToArray();

            foreach (string path in Directory.GetFiles("/dev"))
            {
                string file = Path.GetFileName(path);
                if (file.StartsWith("cu.Bluetooth", StringComparison.Ordinal)) continue;
                if (file.StartsWith("cu.usb", StringComparison.Ordinal)
                    || file.StartsWith("cu.wchusb", StringComparison.Ordinal)
                    || file.StartsWith("cu.SLAB", StringComparison.Ordinal)
                    || file.StartsWith("ttyUSB", StringComparison.Ordinal)
                    || file.StartsWith("ttyACM", StringComparison.Ordinal))
                {
                    names.Add(path);
                }
            }

            names.Sort(StringComparer.Ordinal);
            return names.ToArray();
        }

        private static string[] GetWindowsPortNames()
        {
            var names = new List<string>();
            for (int i = 1; i <= 32; i++) names.Add("COM" + i);
            return names.ToArray();
        }

        private void OpenWindows()
        {
            string winPort = portName.StartsWith(@"\\.\", StringComparison.Ordinal) ? portName : @"\\.\" + portName;
            windowsHandle = CreateFile(winPort, GenericRead | GenericWrite, 0, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (windowsHandle == IntPtr.Zero || windowsHandle == new IntPtr(-1))
            {
                throw new IOException($"포트를 열 수 없습니다: {portName}");
            }

            var dcb = new Dcb { DCBlength = (uint)Marshal.SizeOf<Dcb>() };
            if (!GetCommState(windowsHandle, ref dcb))
            {
                Close();
                throw new IOException("GetCommState 실패");
            }

            dcb.BaudRate = (uint)baudRate;
            dcb.ByteSize = 8;
            dcb.Parity = 0;
            dcb.StopBits = 0;
            dcb.Flags = 1; // fBinary
            if (!SetCommState(windowsHandle, ref dcb))
            {
                Close();
                throw new IOException("SetCommState 실패");
            }

            var timeouts = new CommTimeouts
            {
                ReadIntervalTimeout = 50,
                ReadTotalTimeoutMultiplier = 0,
                ReadTotalTimeoutConstant = 50,
                WriteTotalTimeoutMultiplier = 0,
                WriteTotalTimeoutConstant = 2000
            };
            SetCommTimeouts(windowsHandle, ref timeouts);
        }

        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint OpenExisting = 3;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer, int nNumberOfBytesToRead, out int lpNumberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, int nNumberOfBytesToWrite, out int lpNumberOfBytesWritten, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetCommState(IntPtr hFile, ref Dcb lpDCB);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetCommState(IntPtr hFile, ref Dcb lpDCB);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetCommTimeouts(IntPtr hFile, ref CommTimeouts lpCommTimeouts);

        [StructLayout(LayoutKind.Sequential)]
        private struct Dcb
        {
            public uint DCBlength;
            public uint BaudRate;
            public uint Flags;
            public ushort wReserved;
            public ushort XonLim;
            public ushort XoffLim;
            public byte ByteSize;
            public byte Parity;
            public byte StopBits;
            public byte XonChar;
            public byte XoffChar;
            public byte ErrorChar;
            public byte EofChar;
            public byte EvtChar;
            public ushort wReserved1;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CommTimeouts
        {
            public uint ReadIntervalTimeout;
            public uint ReadTotalTimeoutMultiplier;
            public uint ReadTotalTimeoutConstant;
            public uint WriteTotalTimeoutMultiplier;
            public uint WriteTotalTimeoutConstant;
        }
    }
}
