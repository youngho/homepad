using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Homepad.Core
{
    public sealed class NativeSerialPort : IDisposable
    {
        private FileStream unixStream;
        private int unixFd = -1;
        private IntPtr windowsHandle = new IntPtr(-1);
        private readonly string portName;
        private readonly int baudRate;
        private bool disposed;

        public string PortName => portName;
        public int BaudRate => baudRate;
        public bool IsOpen => unixFd >= 0 || unixStream != null || (windowsHandle != IntPtr.Zero && windowsHandle != new IntPtr(-1));

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

        private static string ResolveUnixPortPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("/dev/tty.", StringComparison.Ordinal))
            {
                return path;
            }

            string callout = "/dev/cu." + path.Substring("/dev/tty.".Length);
            try
            {
                if (File.Exists(callout)) return callout;
            }
            catch
            {
            }

            return path;
        }

        public void Close()
        {
            if (unixFd >= 0)
            {
                try { UnixClose(unixFd); } catch { }
                unixFd = -1;
            }

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
            if (unixFd >= 0)
            {
                byte[] slice = buffer;
                if (offset != 0 || count != buffer.Length)
                {
                    slice = new byte[count];
                    Buffer.BlockCopy(buffer, offset, slice, 0, count);
                }

                int written = UnixWrite(unixFd, slice, count);
                if (written < 0) throw new IOException("시리얼 쓰기 실패");
                return;
            }

            if (unixStream != null)
            {
                unixStream.Write(buffer, offset, count);
                unixStream.Flush();
                return;
            }

            if (!IsOpen) throw new IOException("시리얼 포트가 열려 있지 않습니다.");
            var winSlice = new byte[count];
            Buffer.BlockCopy(buffer, offset, winSlice, 0, count);
            if (!WriteFile(windowsHandle, winSlice, count, out int winWritten, IntPtr.Zero) || winWritten != count)
            {
                throw new IOException("시리얼 쓰기 실패");
            }
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            if (unixFd >= 0)
            {
                byte[] dest = buffer;
                if (offset != 0)
                {
                    dest = new byte[count];
                }

                int n = UnixRead(unixFd, dest, count);
                if (n <= 0) return 0;
                if (offset != 0)
                {
                    Buffer.BlockCopy(dest, 0, buffer, offset, n);
                }

                return n;
            }

            if (unixStream != null)
            {
                try
                {
                    return unixStream.Read(buffer, offset, count);
                }
                catch (IOException)
                {
                    return 0;
                }
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
            string path = ResolveUnixPortPath(portName);
            Exception lastError = null;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    ConfigureUnixPort(path, baudRate);
                    int fd = UnixOpen(path, UnixOpenFlags);
                    if (fd >= 0)
                    {
                        unixFd = fd;
                        return;
                    }

                    lastError = new IOException($"open 실패 errno={Marshal.GetLastWin32Error()}");
                    unixStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, 1, false);
                    return;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    lastError = ex;
                    Close();
                    Thread.Sleep(500);
                }
            }

            int errno = Marshal.GetLastWin32Error();
            throw new IOException($"포트를 열 수 없습니다: {path} ({lastError?.Message ?? "errno " + errno})");
        }

        private static void ConfigureUnixPort(string path, int baud)
        {
            string args = path.StartsWith("/dev/", StringComparison.Ordinal)
                ? $"-f {path} {baud} cs8 -cstopb -parenb clocal cread -hupcl raw -echo -ixon -ixoff min 0 time 1"
                : $"{path} {baud} cs8 -cstopb -parenb clocal cread raw -echo min 0 time 1";

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
            var found = new HashSet<string>(StringComparer.Ordinal);
            AddUnixPortsFromEntries(found);
            AddUnixPortsFromGlobs(found);
            AddUnixPortsFromShell(found);

            var names = new List<string>();
            foreach (string path in found)
            {
                if (IsUnixSerialCandidate(Path.GetFileName(path)))
                {
                    names.Add(path);
                }
            }

            names.Sort(CompareUnixPorts);
            return names.ToArray();
        }

        private static bool IsUnixSerialCandidate(string file)
        {
            if (string.IsNullOrEmpty(file)) return false;
            if (file.StartsWith("cu.Bluetooth", StringComparison.Ordinal)) return false;
            if (file.StartsWith("tty.Bluetooth", StringComparison.Ordinal)) return false;

            return file.StartsWith("cu.usb", StringComparison.Ordinal)
                || file.StartsWith("cu.wch", StringComparison.Ordinal)
                || file.StartsWith("cu.SLAB", StringComparison.Ordinal)
                || file.StartsWith("ttyUSB", StringComparison.Ordinal)
                || file.StartsWith("ttyACM", StringComparison.Ordinal);
        }

        private static int CompareUnixPorts(string a, string b)
        {
            int cmp = UnixPortRank(a).CompareTo(UnixPortRank(b));
            return cmp != 0 ? cmp : string.CompareOrdinal(a, b);
        }

        private static int UnixPortRank(string path)
        {
            string file = Path.GetFileName(path);
            if (file.StartsWith("cu.usbmodem", StringComparison.Ordinal)) return 0;
            if (file.StartsWith("cu.usbserial", StringComparison.Ordinal)) return 1;
            if (file.StartsWith("cu.usb", StringComparison.Ordinal)) return 2;
            if (file.StartsWith("cu.wch", StringComparison.Ordinal)) return 3;
            if (file.StartsWith("cu.SLAB", StringComparison.Ordinal)) return 4;
            return 10;
        }

        private static void AddUnixPortsFromEntries(HashSet<string> names)
        {
            try
            {
                if (!Directory.Exists("/dev")) return;
                foreach (string path in Directory.GetFileSystemEntries("/dev"))
                {
                    names.Add(path);
                }
            }
            catch
            {
            }
        }

        private static void AddUnixPortsFromGlobs(HashSet<string> names)
        {
            string[] patterns =
            {
                "cu.usb*",
                "cu.wch*",
                "cu.SLAB*",
                "ttyUSB*",
                "ttyACM*"
            };

            foreach (string pattern in patterns)
            {
                try
                {
                    foreach (string path in Directory.GetFileSystemEntries("/dev", pattern))
                    {
                        names.Add(path);
                    }
                }
                catch
                {
                }
            }
        }

        private static void AddUnixPortsFromShell(HashSet<string> names)
        {
            try
            {
                var info = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"shopt -s nullglob; ls -1 /dev/cu.usb* /dev/cu.wch* /dev/cu.SLAB* /dev/ttyUSB* /dev/ttyACM* 2>/dev/null\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(info);
                if (process == null) return;

                string output = process.StandardOutput.ReadToEnd();
                if (!process.WaitForExit(1500))
                {
                    try { process.Kill(); } catch { }
                    return;
                }

                foreach (string line in output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string path = line.Trim();
                    if (path.IndexOf('*') >= 0) continue;
                    names.Add(path);
                }
            }
            catch
            {
            }
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

        private static readonly int UnixOpenFlags = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? 0x0002 | 0x20000
            : 0x0002 | 0x0100;

        [DllImport("libc", EntryPoint = "open", SetLastError = true)]
        private static extern int UnixOpen(string pathname, int flags);

        [DllImport("libc", EntryPoint = "close", SetLastError = true)]
        private static extern int UnixClose(int fd);

        [DllImport("libc", EntryPoint = "read", SetLastError = true)]
        private static extern int UnixRead(int fd, byte[] buffer, int count);

        [DllImport("libc", EntryPoint = "write", SetLastError = true)]
        private static extern int UnixWrite(int fd, byte[] buffer, int count);

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
