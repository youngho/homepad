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
        Tcp = 2,
        Mqtt = 3
    }

    public enum KocomLinkDevice
    {
        Arduino = 0,
        Ew11 = 1
    }

    public class ArduinoConnector : MonoBehaviour
    {
        [Header("Connection")]
        [SerializeField] private ArduinoLinkMode linkMode = ArduinoLinkMode.Simulation;
        [SerializeField] private bool autoConnect = true;

        [Header("TCP")]
        [SerializeField] private string arduinoIp = "192.168.0.100";
        [SerializeField] private int arduinoPort = 8080;

        [Header("MQTT")]
        [SerializeField] private string mqttHost = "192.168.0.100";
        [SerializeField] private int mqttPort = 1883;
        [SerializeField] private string mqttClientId = "homepad";
        [SerializeField] private string mqttUser = "";
        [SerializeField] private string mqttPassword = "";
        [SerializeField] private string mqttTxTopic = "kocom/tx";
        [SerializeField] private string mqttRxTopic = "kocom/rx";

        [Header("Serial")]
        [SerializeField] private string serialPortName = "";
        [SerializeField] private int serialBaudRate = 115200;

        [Header("Status")]
        [SerializeField] private bool isConnected;
        [SerializeField] private string linkLabel = "장치";

        public ArduinoLinkMode LinkMode => linkMode;
        public string ArduinoIp => arduinoIp;
        public int ArduinoPort => arduinoPort;
        public string MqttHost => mqttHost;
        public int MqttPort => mqttPort;
        public string MqttUser => mqttUser;
        public string MqttTxTopic => mqttTxTopic;
        public string MqttRxTopic => mqttRxTopic;
        public string SerialPortName => serialPortName;
        public int SerialBaudRate => serialBaudRate;
        public string LinkLabel => linkLabel;
        public bool UseSimulationMode => linkMode == ArduinoLinkMode.Simulation;
        public bool IsConnected => UseSimulationMode || isConnected;

        public event Action<bool> OnConnectionStatusChanged;
        public event Action<byte[]> OnPacketReceived;
        public event Action<string, bool> OnLogMessage;

        private TcpClient tcpClient;
        private NetworkStream networkStream;
        private NativeSerialPort serialPort;
        private SimpleMqttClient mqttClient;
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

        public void SetLinkLabel(string label)
        {
            linkLabel = string.IsNullOrEmpty(label) ? "장치" : label;
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

        public void SetTcpTarget(string ip, int port)
        {
            arduinoIp = ip;
            arduinoPort = port > 0 ? port : 8899;
            linkMode = ArduinoLinkMode.Tcp;
            PlayerPrefs.SetString("Homepad.TcpHost", arduinoIp);
            PlayerPrefs.SetInt("Homepad.TcpPort", arduinoPort);
            PlayerPrefs.Save();
            ConnectToArduino();
        }

        public void SetMqttTarget(string host, int port, string user, string password, string txTopic, string rxTopic)
        {
            mqttHost = host;
            mqttPort = port > 0 ? port : 1883;
            mqttUser = user ?? string.Empty;
            mqttPassword = password ?? string.Empty;
            mqttTxTopic = string.IsNullOrEmpty(txTopic) ? "kocom/tx" : txTopic;
            mqttRxTopic = string.IsNullOrEmpty(rxTopic) ? "kocom/rx" : rxTopic;
            mqttClientId = "homepad-" + Environment.TickCount.ToString("x");
            linkMode = ArduinoLinkMode.Mqtt;
            PlayerPrefs.SetString("Homepad.MqttHost", mqttHost);
            PlayerPrefs.SetInt("Homepad.MqttPort", mqttPort);
            PlayerPrefs.SetString("Homepad.MqttUser", mqttUser);
            PlayerPrefs.SetString("Homepad.MqttPass", mqttPassword);
            PlayerPrefs.SetString("Homepad.MqttTx", mqttTxTopic);
            PlayerPrefs.SetString("Homepad.MqttRx", mqttRxTopic);
            PlayerPrefs.Save();
            ConnectToArduino();
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
            if (cts != null || (isConnected && linkMode == ArduinoLinkMode.Serial))
            {
                return;
            }

            if (linkMode == ArduinoLinkMode.Serial && string.IsNullOrEmpty(serialPortName))
            {
                serialPortName = PlayerPrefs.GetString("Homepad.SerialPort", "");
                serialBaudRate = PlayerPrefs.GetInt("Homepad.SerialBaud", serialBaudRate);
            }

            if (!autoConnect)
            {
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
            else if (linkMode == ArduinoLinkMode.Mqtt)
            {
                ConnectMqtt(token);
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

            var stream = networkStream;
            var client = tcpClient;
            var port = serialPort;
            var mqtt = mqttClient;
            networkStream = null;
            tcpClient = null;
            serialPort = null;
            mqttClient = null;

            Task.Run(() =>
            {
                try { stream?.Close(); } catch { }
                try { client?.Close(); } catch { }
                try { port?.Dispose(); } catch { }
                try { mqtt?.Dispose(); } catch { }
                try { toCancel?.Dispose(); } catch { }
            });

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
                        else if (linkMode == ArduinoLinkMode.Mqtt)
                        {
                            if (mqttClient == null || !mqttClient.IsConnected)
                            {
                                throw new IOException("MQTT가 연결되어 있지 않습니다.");
                            }

                            mqttClient.Publish(mqttTxTopic, packet);
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
            string target = serialPortName;
            int baud = serialBaudRate;
            OnLogMessage?.Invoke($"[시리얼] {target} @ {baud} 접속 시도...", true);
            Debug.Log($"[Homepad] 시리얼 접속 시도: {target} @ {baud}");

            Task.Run(() =>
            {
                try
                {

                    var port = new NativeSerialPort(serialPortName, serialBaudRate);
                    port.Open();
                    Thread.Sleep(1200);
                    if (token.IsCancellationRequested)
                    {
                        port.Dispose();
                        return;
                    }

                    serialPort = port;
                    isConnected = true;

                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        OnConnectionStatusChanged?.Invoke(true);
                        OnLogMessage?.Invoke($"[시리얼] 연결 성공 ({serialPortName}, {serialBaudRate} baud)", false);
                        Debug.Log($"[Homepad] 시리얼 연결 성공: {serialPortName} @ {serialBaudRate}");
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
                        OnLogMessage?.Invoke($"[네트워크] {linkLabel} TCP({arduinoIp}:{arduinoPort}) 접속 시도 중...", true);
                    });

                    tcpClient = new TcpClient();
                    await tcpClient.ConnectAsync(arduinoIp, arduinoPort);
                    networkStream = tcpClient.GetStream();
                    isConnected = true;

                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        OnConnectionStatusChanged?.Invoke(true);
                        OnLogMessage?.Invoke($"[네트워크] {linkLabel} TCP 연결 성공 ({arduinoIp}:{arduinoPort})", false);
                    });

                    await TcpReceiveLoopAsync(token);
                }
                catch (Exception ex)
                {
                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        isConnected = false;
                        OnConnectionStatusChanged?.Invoke(false);
                        OnLogMessage?.Invoke($"[오류] {linkLabel} TCP 연결 실패: {ex.Message}", false);
                    });
                }
            }, token);
        }

        private void ConnectMqtt(CancellationToken token)
        {
            string host = mqttHost;
            int port = mqttPort;
            string user = mqttUser;
            string pass = mqttPassword;
            string clientId = mqttClientId;
            string rxTopic = mqttRxTopic;
            string txTopic = mqttTxTopic;
            string label = linkLabel;

            Task.Run(() =>
            {
                SimpleMqttClient mqtt = null;
                try
                {
                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        OnLogMessage?.Invoke($"[MQTT] {label} {host}:{port} 접속 시도... 송신 {txTopic} / 수신 {rxTopic}", true);
                    });

                    mqtt = new SimpleMqttClient();
                    mqtt.Connect(host, port, clientId, user, pass, 60, token);
                    mqtt.Subscribe(rxTopic, token);
                    mqttClient = mqtt;
                    isConnected = true;

                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        OnConnectionStatusChanged?.Invoke(true);
                        OnLogMessage?.Invoke($"[MQTT] {label} 연결 성공 ({host}:{port})", false);
                    });

                    MqttReceiveLoop(mqtt, token);
                }
                catch (Exception ex)
                {
                    try { mqtt?.Dispose(); } catch { }
                    UnityMainThreadDispatcher.Enqueue(() =>
                    {
                        isConnected = false;
                        OnConnectionStatusChanged?.Invoke(false);
                        OnLogMessage?.Invoke($"[오류] MQTT 연결 실패: {ex.Message}", false);
                    });
                }
            }, token);
        }

        private void MqttReceiveLoop(SimpleMqttClient mqtt, CancellationToken token)
        {
            DateTime lastPing = DateTime.UtcNow;
            try
            {
                while (!token.IsCancellationRequested && mqtt != null && mqtt.IsConnected)
                {
                    try
                    {
                        if (mqtt.TryReadPublish(out string topic, out byte[] payload, token)
                            && payload != null
                            && payload.Length > 0)
                        {
                            byte[] bytes = DecodeMqttPayload(payload);
                            if (bytes != null && bytes.Length > 0)
                            {
                                DispatchReceived(bytes, bytes.Length);
                            }
                        }
                    }
                    catch (IOException ex) when (IsTimeout(ex))
                    {
                        if ((DateTime.UtcNow - lastPing).TotalSeconds >= 30)
                        {
                            mqtt.Ping();
                            lastPing = DateTime.UtcNow;
                        }
                    }

                    if ((DateTime.UtcNow - lastPing).TotalSeconds >= 45)
                    {
                        mqtt.Ping();
                        lastPing = DateTime.UtcNow;
                    }
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
                NotifyDisconnected($"[MQTT] {linkLabel} 연결이 종료되었습니다.");
            }
        }

        private static bool IsTimeout(Exception ex)
        {
            if (ex is SocketException se && se.SocketErrorCode == SocketError.TimedOut) return true;
            string msg = ex.Message ?? string.Empty;
            return msg.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("시간", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static byte[] DecodeMqttPayload(byte[] payload)
        {
            if (payload == null || payload.Length == 0) return payload;
            if (payload.Length >= KocomProtocol.PacketSize && payload[0] == KocomProtocol.Header1)
            {
                return payload;
            }

            string text = System.Text.Encoding.ASCII.GetString(payload).Trim();
            if (text.Length < 2) return payload;
            bool looksHex = true;
            int hexChars = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ' ' || c == '-' || c == ':') continue;
                bool hex = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');
                if (!hex)
                {
                    looksHex = false;
                    break;
                }

                hexChars++;
            }

            if (!looksHex || hexChars < 4) return payload;
            return KocomHexPresets.HexStringToBytes(text);
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
                NotifyDisconnected($"[네트워크] {linkLabel} TCP 연결이 종료되었습니다.");
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
