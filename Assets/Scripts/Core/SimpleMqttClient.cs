using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Homepad.Core
{
    /// <summary>
    /// MQTT 3.1.1 QoS0 최소 클라이언트. EW-11 시리얼 브리지용.
    /// </summary>
    public sealed class SimpleMqttClient : IDisposable
    {
        private TcpClient client;
        private NetworkStream stream;
        private readonly object sendLock = new object();
        private ushort packetId = 1;

        public bool IsConnected => client != null && client.Connected && stream != null;

        public void Connect(string host, int port, string clientId, string username, string password, int keepAliveSec, CancellationToken token)
        {
            CloseSocket();
            var tcp = new TcpClient();
            var connect = tcp.ConnectAsync(host, port);
            if (!connect.Wait(8000, token))
            {
                tcp.Close();
                throw new TimeoutException($"MQTT 브로커 {host}:{port} 응답이 없습니다.");
            }

            tcp.NoDelay = true;
            tcp.ReceiveTimeout = 4000;
            tcp.SendTimeout = 5000;
            var ns = tcp.GetStream();

            byte[] connectPacket = BuildConnectPacket(clientId, username, password, keepAliveSec);
            ns.Write(connectPacket, 0, connectPacket.Length);

            MqttPacket ack = ReadPacket(ns, token);
            if (ack.Type != 2)
            {
                tcp.Close();
                throw new IOException($"MQTT CONNACK 대신 타입 {ack.Type}을 받았습니다.");
            }

            if (ack.Payload == null || ack.Payload.Length < 2 || ack.Payload[1] != 0)
            {
                byte code = ack.Payload != null && ack.Payload.Length >= 2 ? ack.Payload[1] : (byte)255;
                tcp.Close();
                throw new IOException($"MQTT 접속이 거절되었습니다. 코드 {code}");
            }

            client = tcp;
            stream = ns;
        }

        public void Subscribe(string topic, CancellationToken token)
        {
            if (!IsConnected) throw new IOException("MQTT가 연결되어 있지 않습니다.");
            ushort id = NextPacketId();
            byte[] packet = BuildSubscribePacket(id, topic);
            lock (sendLock)
            {
                stream.Write(packet, 0, packet.Length);
            }

            MqttPacket ack = ReadPacket(stream, token);
            if (ack.Type != 9)
            {
                throw new IOException($"MQTT SUBACK 대신 타입 {ack.Type}을 받았습니다.");
            }
        }

        public void Publish(string topic, byte[] payload)
        {
            if (!IsConnected) throw new IOException("MQTT가 연결되어 있지 않습니다.");
            byte[] packet = BuildPublishPacket(topic, payload ?? Array.Empty<byte>());
            lock (sendLock)
            {
                stream.Write(packet, 0, packet.Length);
            }
        }

        public void Ping()
        {
            if (!IsConnected) return;
            byte[] ping = { 0xC0, 0x00 };
            lock (sendLock)
            {
                stream.Write(ping, 0, ping.Length);
            }
        }

        /// <summary>
        /// 다음 패킷을 읽는다. PUBLISH면 topic/payload를 돌려주고, 그 외는 topic=null.
        /// </summary>
        public bool TryReadPublish(out string topic, out byte[] payload, CancellationToken token)
        {
            topic = null;
            payload = null;
            if (!IsConnected) return false;

            MqttPacket packet;
            lock (sendLock)
            {
                packet = ReadPacket(stream, token);
            }

            if (packet.Type == 3)
            {
                ParsePublish(packet, out topic, out payload);
                return topic != null;
            }

            return false;
        }

        public void Dispose()
        {
            try
            {
                if (stream != null && client != null && client.Connected)
                {
                    byte[] disconnect = { 0xE0, 0x00 };
                    stream.Write(disconnect, 0, disconnect.Length);
                }
            }
            catch
            {
            }

            CloseSocket();
        }

        private void CloseSocket()
        {
            try { stream?.Close(); } catch { }
            try { client?.Close(); } catch { }
            stream = null;
            client = null;
        }

        private ushort NextPacketId()
        {
            packetId++;
            if (packetId == 0) packetId = 1;
            return packetId;
        }

        private static byte[] BuildConnectPacket(string clientId, string username, string password, int keepAliveSec)
        {
            if (string.IsNullOrEmpty(clientId)) clientId = "homepad";
            bool hasUser = !string.IsNullOrEmpty(username);
            bool hasPass = hasUser && !string.IsNullOrEmpty(password);

            byte flags = 0x02;
            if (hasUser) flags |= 0x80;
            if (hasPass) flags |= 0x40;

            var vh = new List<byte>();
            AppendMqttString(vh, "MQTT");
            vh.Add(4);
            vh.Add(flags);
            vh.Add((byte)((keepAliveSec >> 8) & 0xFF));
            vh.Add((byte)(keepAliveSec & 0xFF));

            var payload = new List<byte>();
            AppendMqttString(payload, clientId);
            if (hasUser) AppendMqttString(payload, username);
            if (hasPass) AppendMqttString(payload, password);

            int remaining = vh.Count + payload.Count;
            var packet = new List<byte> { 0x10 };
            AppendRemainingLength(packet, remaining);
            packet.AddRange(vh);
            packet.AddRange(payload);
            return packet.ToArray();
        }

        private static byte[] BuildSubscribePacket(ushort id, string topic)
        {
            var body = new List<byte>
            {
                (byte)((id >> 8) & 0xFF),
                (byte)(id & 0xFF)
            };
            AppendMqttString(body, topic);
            body.Add(0);

            var packet = new List<byte> { 0x82 };
            AppendRemainingLength(packet, body.Count);
            packet.AddRange(body);
            return packet.ToArray();
        }

        private static byte[] BuildPublishPacket(string topic, byte[] payload)
        {
            var body = new List<byte>();
            AppendMqttString(body, topic);
            body.AddRange(payload);

            var packet = new List<byte> { 0x30 };
            AppendRemainingLength(packet, body.Count);
            packet.AddRange(body);
            return packet.ToArray();
        }

        private static void ParsePublish(MqttPacket packet, out string topic, out byte[] payload)
        {
            topic = null;
            payload = null;
            if (packet.Payload == null || packet.Payload.Length < 2) return;

            int qos = (packet.HeaderFlags >> 1) & 0x03;
            int i = 0;
            topic = ReadMqttString(packet.Payload, ref i);
            if (qos > 0) i += 2;
            if (i > packet.Payload.Length) return;

            int len = packet.Payload.Length - i;
            payload = new byte[len];
            if (len > 0) Array.Copy(packet.Payload, i, payload, 0, len);
        }

        private static MqttPacket ReadPacket(NetworkStream ns, CancellationToken token)
        {
            int header = ReadByte(ns, token);
            int remaining = ReadRemainingLength(ns, token);
            byte[] payload = remaining > 0 ? ReadExact(ns, remaining, token) : Array.Empty<byte>();
            return new MqttPacket
            {
                Type = (header >> 4) & 0x0F,
                HeaderFlags = header & 0x0F,
                Payload = payload
            };
        }

        private static int ReadByte(NetworkStream ns, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            int b = ns.ReadByte();
            if (b < 0) throw new IOException("MQTT 연결이 닫혔습니다.");
            return b;
        }

        private static int ReadRemainingLength(NetworkStream ns, CancellationToken token)
        {
            int multiplier = 1;
            int value = 0;
            int encoded;
            do
            {
                encoded = ReadByte(ns, token);
                value += (encoded & 127) * multiplier;
                multiplier *= 128;
                if (multiplier > 128 * 128 * 128) throw new IOException("MQTT remaining length가 잘못되었습니다.");
            }
            while ((encoded & 128) != 0);
            return value;
        }

        private static byte[] ReadExact(NetworkStream ns, int count, CancellationToken token)
        {
            byte[] buffer = new byte[count];
            int read = 0;
            while (read < count)
            {
                token.ThrowIfCancellationRequested();
                int n = ns.Read(buffer, read, count - read);
                if (n <= 0) throw new IOException("MQTT 패킷이 중간에 끊겼습니다.");
                read += n;
            }

            return buffer;
        }

        private static void AppendMqttString(List<byte> dest, string value)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(value ?? string.Empty);
            dest.Add((byte)((utf8.Length >> 8) & 0xFF));
            dest.Add((byte)(utf8.Length & 0xFF));
            dest.AddRange(utf8);
        }

        private static string ReadMqttString(byte[] data, ref int index)
        {
            if (index + 2 > data.Length) return string.Empty;
            int len = (data[index] << 8) | data[index + 1];
            index += 2;
            if (index + len > data.Length) return string.Empty;
            string s = Encoding.UTF8.GetString(data, index, len);
            index += len;
            return s;
        }

        private static void AppendRemainingLength(List<byte> dest, int length)
        {
            do
            {
                int encoded = length % 128;
                length /= 128;
                if (length > 0) encoded |= 128;
                dest.Add((byte)encoded);
            }
            while (length > 0);
        }

        private struct MqttPacket
        {
            public int Type;
            public int HeaderFlags;
            public byte[] Payload;
        }
    }
}
