using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Homepad.Core
{
    public class ArduinoConnector : MonoBehaviour
    {
        [Header("Connection Settings")]
        [SerializeField] private string arduinoIp = "192.168.0.100";
        [SerializeField] private int arduinoPort = 8080;
        [SerializeField] private bool useSimulationMode = true;

        [Header("Status")]
        [SerializeField] private bool isConnected;

        public string ArduinoIp => arduinoIp;
        public int ArduinoPort => arduinoPort;
        public bool UseSimulationMode => useSimulationMode;
        public bool IsConnected => useSimulationMode || isConnected;

        public event Action<bool> OnConnectionStatusChanged;
        public event Action<byte[]> OnPacketReceived;
        public event Action<string, bool> OnLogMessage;

        private TcpClient tcpClient;
        private NetworkStream networkStream;
        private CancellationTokenSource cts;
        private readonly List<byte> receiveBuffer = new List<byte>(64);
        private readonly List<byte[]> extractedFrames = new List<byte[]>();
        private readonly object sendLock = new object();

        private void Awake()
        {
            UnityMainThreadDispatcher.EnsureExists();
        }

        public void SetTarget(string ip, int port, bool simulation)
        {
            arduinoIp = ip;
            arduinoPort = port;
            useSimulationMode = simulation;
            OnLogMessage?.Invoke($"[시스템] 통신 대상 변경: {ip}:{port} (시뮬레이션 모드: {simulation})", false);

            if (!simulation)
            {
                ConnectToArduino();
            }
            else
            {
                Disconnect();
                isConnected = true;
                OnConnectionStatusChanged?.Invoke(true);
            }
        }

        private void Start()
        {
            if (useSimulationMode)
            {
                isConnected = true;
                OnLogMessage?.Invoke("[아두이노] 가상 시뮬레이션 모드로 시작되었습니다.", false);
                OnConnectionStatusChanged?.Invoke(true);
            }
            else
            {
                ConnectToArduino();
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        public void ConnectToArduino()
        {
            if (useSimulationMode)
            {
                isConnected = true;
                OnConnectionStatusChanged?.Invoke(true);
                return;
            }

            Disconnect();
            cts = new CancellationTokenSource();
            var token = cts.Token;

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

                    await ReceiveLoopAsync(token);
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

        public void Disconnect()
        {
            var toCancel = cts;
            cts = null;
            try
            {
                toCancel?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                networkStream?.Close();
            }
            catch
            {
            }

            try
            {
                tcpClient?.Close();
            }
            catch
            {
            }

            networkStream = null;
            tcpClient = null;
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

            if (useSimulationMode)
            {
                OnLogMessage?.Invoke($"[TX 시뮬레이션] {hexStr}", true);
                return;
            }

            var stream = networkStream;
            if (stream == null || !stream.CanWrite)
            {
                OnLogMessage?.Invoke($"[TX 실패] 네트워크 스트림이 연결되어 있지 않습니다. {hexStr}", true);
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    lock (sendLock)
                    {
                        stream.Write(packet, 0, packet.Length);
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

        private async Task ReceiveLoopAsync(CancellationToken token)
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

                    for (int i = 0; i < bytesRead; i++)
                    {
                        receiveBuffer.Add(buffer[i]);
                    }

                    extractedFrames.Clear();
                    KocomProtocol.ExtractFrames(receiveBuffer, extractedFrames);
                    if (extractedFrames.Count == 0) continue;

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
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
            finally
            {
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    if (!useSimulationMode)
                    {
                        isConnected = false;
                        OnConnectionStatusChanged?.Invoke(false);
                        OnLogMessage?.Invoke("[네트워크] 아두이노와의 연결이 종료되었습니다.", false);
                    }
                });
            }
        }
    }
}
