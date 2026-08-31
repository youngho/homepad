using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Homepad.Core
{
    /// <summary>
    /// 아두이노 UNO WiFi 및 RS-485 통신 브릿지 클라이언트
    /// </summary>
    public class ArduinoConnector : MonoBehaviour
    {
        [Header("Connection Settings")]
        [SerializeField] private string arduinoIp = "192.168.0.100";
        [SerializeField] private int arduinoPort = 8080;
        [SerializeField] private bool useSimulationMode = true;

        [Header("Status")]
        [SerializeField] private bool isConnected = false;

        public string ArduinoIp => arduinoIp;
        public int ArduinoPort => arduinoPort;
        public bool UseSimulationMode => useSimulationMode;
        public bool IsConnected => useSimulationMode || isConnected;

        // 이벤트
        public event Action<bool> OnConnectionStatusChanged;
        public event Action<byte[]> OnPacketReceived;
        public event Action<string, bool> OnLogMessage; // msg, isTx

        private TcpClient tcpClient;
        private NetworkStream networkStream;
        private CancellationTokenSource cts;

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
                OnConnectionStatusChanged?.Invoke(true);
            }
        }

        private void Start()
        {
            if (useSimulationMode)
            {
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

            Task.Run(async () =>
            {
                try
                {
                    OnLogMessage?.Invoke($"[네트워크] 아두이노 UNO WiFi({arduinoIp}:{arduinoPort}) 접속 시도 중...", true);
                    tcpClient = new TcpClient();
                    await tcpClient.ConnectAsync(arduinoIp, arduinoPort);
                    networkStream = tcpClient.GetStream();
                    isConnected = true;

                    // Main thread callback
                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        OnConnectionStatusChanged?.Invoke(true);
                        OnLogMessage?.Invoke($"[네트워크] 아두이노 연결 성공 ({arduinoIp}:{arduinoPort})", false);
                    });

                    // Start receiving loop
                    _ = ReceiveLoopAsync(cts.Token);
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
            });
        }

        public void Disconnect()
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = null;

            networkStream?.Close();
            networkStream = null;

            tcpClient?.Close();
            tcpClient = null;

            if (isConnected)
            {
                isConnected = false;
                OnConnectionStatusChanged?.Invoke(false);
            }
        }

        /// <summary>
        /// RS-485 명령 패킷 아두이노로 송신
        /// </summary>
        public void SendPacket(byte[] packet)
        {
            if (packet == null || packet.Length == 0) return;

            string hexStr = KocomProtocol.ToHexString(packet);

            if (useSimulationMode)
            {
                OnLogMessage?.Invoke($"[TX 시뮬레이션] {hexStr}", true);
                return;
            }

            if (networkStream != null && networkStream.CanWrite)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await networkStream.WriteAsync(packet, 0, packet.Length);
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
            else
            {
                OnLogMessage?.Invoke($"[TX 실패] 네트워크 스트림이 연결되어 있지 않습니다. {hexStr}", true);
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            byte[] buffer = new byte[256];
            try
            {
                while (!token.IsCancellationRequested && networkStream != null)
                {
                    int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (bytesRead <= 0) break;

                    byte[] received = new byte[bytesRead];
                    Array.Copy(buffer, 0, received, 0, bytesRead);

                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        string hexStr = KocomProtocol.ToHexString(received);
                        OnLogMessage?.Invoke($"[RX] {hexStr}", false);
                        OnPacketReceived?.Invoke(received);
                    });
                }
            }
            catch
            {
                // Disconnected or cancelled
            }
            finally
            {
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    isConnected = false;
                    OnConnectionStatusChanged?.Invoke(false);
                    OnLogMessage?.Invoke("[네트워크] 아두이노와의 연결이 종료되었습니다.", false);
                });
            }
        }
    }
}
