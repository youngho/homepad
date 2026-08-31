using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Homepad.Core
{
    public enum ArduinoLinkMode
    {
        Simulation = 0,
        Serial = 1,
        Tcp = 2
    }

    public class ArduinoConnector : MonoBehaviour
    {
        [Header("Connection")]
        [SerializeField] private ArduinoLinkMode linkMode = ArduinoLinkMode.Simulation;
        [SerializeField] private bool autoConnect = true;

        [Header("TCP")]
        [SerializeField] private string arduinoIp = "192.168.0.100";
        [SerializeField] private int arduinoPort = 8080;

        [Header("Serial")]
        [SerializeField] private string serialPortName = "";
        [SerializeField] private int serialBaudRate = 115200;

        [Header("Status")]
        [SerializeField] private bool isConnected;

        public ArduinoLinkMode LinkMode => linkMode;
        public string ArduinoIp => arduinoIp;
        public int ArduinoPort => arduinoPort;
        public string SerialPortName => serialPortName;
        public int SerialBaudRate => serialBaudRate;
        public bool UseSimulationMode => linkMode == ArduinoLinkMode.Simulation;
        public bool IsConnected => UseSimulationMode || isConnected;

        public event Action<bool> OnConnectionStatusChanged;
        public event Action<byte[]> OnPacketReceived;
        public event Action<string, bool> OnLogMessage;

        private TcpClient tcpClient;
        private NetworkStream networkStream;
        private NativeSerialPort serialPort;
        private CancellationTokenSource cts;
        private readonly List<byte> receiveBuffer = new List<byte>(64);
        private readonly List<byte[]> extractedFrames = new List<byte[]>();
        private readonly object sendLock = new object();

        private void Awake()
        {
            UnityMainThreadDispatcher.EnsureExists();
        }

        public static string[] ListSerialPorts()
        {
            return NativeSerialPort.GetPortNames();
        }

        public void SetTarget(string ip, int port, bool simulation)
        {
            arduinoIp = ip;
            arduinoPort = port;
            linkMode = simulation ? ArduinoLinkMode.Simulation : ArduinoLinkMode.Tcp;
            OnLogMessage?.Invoke($"[시스템] 통신 대상 변경: {ip}:{port} (모드: {linkMode})", false);

            if (simulation)
            {
                Disconnect();
                isConnected = true;
                OnConnectionStatusChanged?.Invoke(true);
            }
            else
            {
                ConnectToArduino();
            }
        }

        public void SetSerialTarget(string portName, int baudRate)
        {
            serialPortName = portName;
            serialBaudRate = baudRate > 0 ? baudRate : 115200;
            linkMode = ArduinoLinkMode.Serial;
            PlayerPrefs.SetString("Homepad.SerialPort", serialPortName);
            PlayerPrefs.SetInt("Homepad.SerialBaud", serialBaudRate);
            PlayerPrefs.Save();
            ConnectToArduino();
        }

        private void Start()
        {
            if (linkMode == ArduinoLinkMode.Serial && string.IsNullOrEmpty(serialPortName))
            {
                serialPortName = PlayerPrefs.GetString("Homepad.SerialPort", "");
                serialBaudRate = PlayerPrefs.GetInt("Homepad.SerialBaud", serialBaudRate);
            }

            if (!autoConnect)
            {
                OnLogMessage?.Invoke("[시스템] 시리얼 포트를 선택한 뒤 연결하세요.", false);
                return;
            }

            if (UseSimulationMode)
            {
                isConnected = true;
                OnLogMessage?.Invoke("[아두이노] 가상 시뮬레이션 모드로 시작되었습니다.", false);
                OnConnectionStatusChanged?.Invoke(true);
                return;
            }

            ConnectToArduino();
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        public void ConnectToArduino()
        {
            if (UseSimulationMode)
            {
                isConnected = true;
                OnConnectionStatusChanged?.Invoke(true);
                return;
            }

            Disconnect();
            cts = new CancellationTokenSource();
            var token = cts.Token;

            if (linkMode == ArduinoLinkMode.Serial)
            {
                ConnectSerial(token);
            }
            else
            {
                ConnectTcp(token);
            }
        }

        public void Disconnect()
        {
            var toCancel = cts;
            cts = null;
            try { toCancel?.Cancel(); }
            catch (ObjectDisposedException) { }

            try { networkStream?.Close(); } catch { }
            try { tcpClient?.Close(); } catch { }
            try { serialPort?.Dispose(); } catch { }

            networkStream = null;
            tcpClient = null;
            serialPort = null;
            toCancel?.Dispose();

            if (isConnected)
            {
                isConnected = false;
                OnConnectionStatusChanged?.Invoke(false);
            }
        }

        public void SendPacket(byte[] packet)
        {
            if (packet == null || packet.Length == 0) return;

            string hexStr = KocomProtocol.ToHexString(packet);

            if (UseSimulationMode)
            {
                OnLogMessage?.Invoke($"[TX 시뮬레이션] {hexStr}", true);
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    lock (sendLock)
                    {
                        if (linkMode == ArduinoLinkMode.Serial)
                        {
                            if (serialPort == null || !serialPort.IsOpen)
                            {
                                throw new IOException("시리얼 포트가 연결되어 있지 않습니다.");
                            }

                            serialPort.Write(packet, 0, packet.Length);
                        }
                        else
                        {
                            if (networkStream == null || !networkStream.CanWrite)
                            {
                                throw new IOException("네트워크 스트림이 연결되어 있지 않습니다.");
                            }

                            networkStream.Write(packet, 0, packet.Length);
                        }
                    }

                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        OnLogMessage?.Invoke($"[TX] {hexStr}", true);
                    });
                }
                catch (Exception ex)
                {
                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        OnLogMessage?.Invoke($"[TX 실패] {ex.Message}", true);
                    });
                }
            });
        }

        private void ConnectSerial(CancellationToken token)
        {
            Task.Run(() =>
            {
                try
                {
                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        OnLogMessage?.Invoke($"[시리얼] {serialPortName} @ {serialBaudRate} 접속 시도...", true);
                    });

                    var port = new NativeSerialPort(serialPortName, serialBaudRate);
                    port.Open();
                    serialPort = port;
                    isConnected = true;

                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        OnConnectionStatusChanged?.Invoke(true);
                        OnLogMessage?.Invoke($"[시리얼] 연결 성공 ({serialPortName}, {serialBaudRate} baud)", false);
                    });

                    SerialReceiveLoop(token);
                }
                catch (Exception ex)
                {
                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        isConnected = false;
                        OnConnectionStatusChanged?.Invoke(false);
                        OnLogMessage?.Invoke($"[오류] 시리얼 연결 실패: {ex.Message}", false);
                    });
                }
            }, token);
        }

        private void ConnectTcp(CancellationToken token)
        {
            Task.Run(async () =>
            {
                try
                {
                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        OnLogMessage?.Invoke($"[네트워크] 아두이노 UNO WiFi({arduinoIp}:{arduinoPort}) 접속 시도 중...", true);
                    });

                    tcpClient = new TcpClient();
                    await tcpClient.ConnectAsync(arduinoIp, arduinoPort);
                    networkStream = tcpClient.GetStream();
                    isConnected = true;

                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        OnConnectionStatusChanged?.Invoke(true);
                        OnLogMessage?.Invoke($"[네트워크] 아두이노 연결 성공 ({arduinoIp}:{arduinoPort})", false);
                    });

                    await TcpReceiveLoopAsync(token);
                }
                catch (Exception ex)
                {
                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        isConnected = false;
                        OnConnectionStatusChanged?.Invoke(false);
                        OnLogMessage?.Invoke($"[오류] 아두이노 연결 실패: {ex.Message}", false);
                    });
                }
            }, token);
        }

        private void SerialReceiveLoop(CancellationToken token)
        {
            byte[] buffer = new byte[256];
            receiveBuffer.Clear();

            try
            {
                while (!token.IsCancellationRequested)
                {
                    var port = serialPort;
                    if (port == null || !port.IsOpen) break;

                    int bytesRead;
                    try
                    {
                        bytesRead = port.Read(buffer, 0, buffer.Length);
                    }
                    catch (IOException)
                    {
                        break;
                    }

                    if (bytesRead <= 0)
                    {
                        Thread.Sleep(15);
                        continue;
                    }

                    DispatchReceived(buffer, bytesRead);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
            finally
            {
                NotifyDisconnected("[시리얼] 아두이노와의 연결이 종료되었습니다.");
            }
        }

        private async Task TcpReceiveLoopAsync(CancellationToken token)
        {
            byte[] buffer = new byte[256];
            receiveBuffer.Clear();

            try
            {
                while (!token.IsCancellationRequested)
                {
                    var stream = networkStream;
                    if (stream == null) break;

                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (bytesRead <= 0) break;
                    DispatchReceived(buffer, bytesRead);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
            finally
            {
                NotifyDisconnected("[네트워크] 아두이노와의 연결이 종료되었습니다.");
            }
        }

        private void DispatchReceived(byte[] buffer, int bytesRead)
        {
            for (int i = 0; i < bytesRead; i++)
            {
                receiveBuffer.Add(buffer[i]);
            }

            extractedFrames.Clear();
            KocomProtocol.ExtractFrames(receiveBuffer, extractedFrames);
            if (extractedFrames.Count == 0) return;

            var frames = extractedFrames.ToArray();
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                foreach (var frame in frames)
                {
                    OnLogMessage?.Invoke($"[RX] {KocomProtocol.ToHexString(frame)}", false);
                    OnPacketReceived?.Invoke(frame);
                }
            });
        }

        private void NotifyDisconnected(string message)
        {
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                if (UseSimulationMode) return;
                isConnected = false;
                OnConnectionStatusChanged?.Invoke(false);
                OnLogMessage?.Invoke(message, false);
            });
        }
    }
}
