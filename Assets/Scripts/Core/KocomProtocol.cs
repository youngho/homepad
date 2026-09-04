using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Homepad.Core
{
    /// <summary>
    /// 코콤 RS-485 21바이트 프레임.
    /// AA 55 | TYPE | SRC | DST | ROOM | VALUE[8] | CS | 0D 0D
    /// TYPE 0x30BC 월패드 송신, 0x30DC 장치 보고.
    /// CS = bytes[2..17] 합 modulo 256.
    /// </summary>
    public static class KocomProtocol
    {
        public const int PacketSize = 21;
        public const byte Header1 = 0xAA;
        public const byte Header2 = 0x55;
        public const byte Trailer = 0x0D;

        public const ushort TypeTransmit = 0x30BC;
        public const ushort TypeReport = 0x30DC;
        public const ushort AddressWallpad = 0x0001;
        public const ushort DeviceLight = 0x000E;
        public const ushort DeviceHeating = 0x0036;
        public const ushort DeviceGas = 0x002C;
        public const ushort DeviceVentilation = 0x0048;
        public const ushort DeviceElevator = 0x0044;
        public const ushort DeviceDoorLock = 0x0033;

        public const byte LightOn = 0xFF;
        public const byte LightOff = 0x00;
        public const byte HeatPowerOn0 = 0x11;
        public const byte HeatPowerOn1 = 0x00;
        public const byte HeatPowerOff0 = 0x00;
        public const byte HeatPowerOff1 = 0x01;
        public const byte HeatAway0 = 0x11;
        public const byte HeatAway1 = 0x01;

        public struct Frame
        {
            public ushort type;
            public ushort source;
            public ushort destination;
            public ushort room;
            public byte[] value;
            public byte checksum;

            public ushort DeviceAddress
            {
                get
                {
                    if (IsKnownDevice(destination)) return destination;
                    if (IsKnownDevice(source)) return source;
                    return destination;
                }
            }
        }

        public static bool IsKnownDevice(ushort address)
        {
            return address == DeviceLight
                || address == DeviceHeating
                || address == DeviceGas
                || address == DeviceVentilation
                || address == DeviceElevator
                || address == DeviceDoorLock;
        }

        public static byte[] BuildFrame(ushort destination, ushort room, byte[] value8, ushort source = AddressWallpad, ushort type = TypeTransmit)
        {
            byte[] value = new byte[8];
            if (value8 != null)
            {
                int copy = Math.Min(8, value8.Length);
                Array.Copy(value8, value, copy);
            }

            byte[] packet = new byte[PacketSize];
            packet[0] = Header1;
            packet[1] = Header2;
            WriteUInt16(packet, 2, type);
            WriteUInt16(packet, 4, source);
            WriteUInt16(packet, 6, destination);
            WriteUInt16(packet, 8, room);
            Array.Copy(value, 0, packet, 10, 8);
            packet[18] = ComputeChecksum(packet);
            packet[19] = Trailer;
            packet[20] = Trailer;
            return packet;
        }

        public static byte[] CreateLightRoomPacket(ushort room, IReadOnlyList<LightState> lightsInRoom)
        {
            byte[] value = new byte[8];
            if (lightsInRoom != null)
            {
                for (int i = 0; i < lightsInRoom.Count; i++)
                {
                    var light = lightsInRoom[i];
                    if (light.slot >= 0 && light.slot < 8)
                    {
                        value[light.slot] = light.isOn ? LightOn : LightOff;
                    }
                }
            }
            return BuildFrame(DeviceLight, room, value);
        }

        public static byte[] CreateHeatingControlPacket(ushort room, bool power, bool awayMode, float targetTemp)
        {
            byte temp = (byte)Mathf.Clamp(Mathf.RoundToInt(targetTemp), 5, 40);
            byte[] value = new byte[8];
            if (!power)
            {
                value[0] = HeatPowerOff0;
                value[1] = HeatPowerOff1;
            }
            else if (awayMode)
            {
                value[0] = HeatAway0;
                value[1] = HeatAway1;
            }
            else
            {
                value[0] = HeatPowerOn0;
                value[1] = HeatPowerOn1;
            }
            value[2] = temp;
            return BuildFrame(DeviceHeating, room, value);
        }

        public static byte[] CreateGasClosePacket()
        {
            return BuildFrame(DeviceGas, 0x0001, new byte[8]);
        }

        public static byte[] CreateVentilationPacket(VentilationSpeed speed)
        {
            return BuildFrame(DeviceVentilation, 0x0001, new byte[] { (byte)speed, 0, 0, 0, 0, 0, 0, 0 });
        }

        public static byte[] CreateElevatorCallPacket()
        {
            return BuildFrame(DeviceElevator, 0x0001, new byte[8]);
        }

        public static bool TryParse(byte[] raw, out Frame frame)
        {
            frame = default;
            if (raw == null || raw.Length < PacketSize) return false;
            if (raw[0] != Header1 || raw[1] != Header2) return false;
            if (raw[19] != Trailer || raw[20] != Trailer) return false;
            if (ComputeChecksum(raw) != raw[18]) return false;

            frame.type = ReadUInt16(raw, 2);
            frame.source = ReadUInt16(raw, 4);
            frame.destination = ReadUInt16(raw, 6);
            frame.room = ReadUInt16(raw, 8);
            frame.value = new byte[8];
            Array.Copy(raw, 10, frame.value, 0, 8);
            frame.checksum = raw[18];
            return true;
        }

        public static void ExtractFrames(List<byte> buffer, List<byte[]> output)
        {
            if (buffer == null || output == null) return;

            while (buffer.Count >= 2)
            {
                int start = -1;
                for (int i = 0; i < buffer.Count - 1; i++)
                {
                    if (buffer[i] == Header1 && buffer[i + 1] == Header2)
                    {
                        start = i;
                        break;
                    }
                }

                if (start < 0)
                {
                    buffer.Clear();
                    return;
                }

                if (start > 0)
                {
                    buffer.RemoveRange(0, start);
                }

                if (buffer.Count < PacketSize)
                {
                    return;
                }

                byte[] candidate = new byte[PacketSize];
                buffer.CopyTo(0, candidate, 0, PacketSize);
                if (TryParse(candidate, out _))
                {
                    output.Add(candidate);
                    buffer.RemoveRange(0, PacketSize);
                }
                else
                {
                    buffer.RemoveAt(0);
                }
            }
        }

        public static string ToHexString(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;
            var sb = new StringBuilder(bytes.Length * 3);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(bytes[i].ToString("X2"));
            }
            return sb.ToString();
        }

        public static string DecodeFrame(Frame frame)
        {
            string typeStr = frame.type switch
            {
                TypeTransmit => "요청(REQ)",
                TypeReport => "상태(STA)",
                0x30BD => "재전송1(30BD)",
                0x30BE => "재전송2(30BE)",
                _ => $"타입(0x{frame.type:X4})"
            };

            string roomStr = frame.room switch
            {
                0x0001 => "거실",
                0x0101 => "방1",
                0x0201 => "방2",
                0x0301 => "방3",
                _ => $"방(0x{frame.room:X4})"
            };

            ushort dev = frame.DeviceAddress;
            string devName = dev switch
            {
                DeviceLight => "조명",
                DeviceHeating => "난방",
                DeviceVentilation => "환기",
                DeviceDoorLock => "도어락",
                DeviceGas => "가스",
                DeviceElevator => "엘리베이터",
                AddressWallpad => "월패드",
                _ => $"장치(0x{dev:X4})"
            };

            var sb = new StringBuilder();
            sb.Append($"[{typeStr}] {devName} ({roomStr}) ");

            if (dev == DeviceLight)
            {
                var onLights = new List<int>();
                for (int i = 0; i < 8; i++)
                {
                    if (frame.value != null && i < frame.value.Length && frame.value[i] == LightOn)
                    {
                        onLights.Add(i + 1);
                    }
                }
                if (onLights.Count == 0) sb.Append("전체 OFF");
                else sb.Append($"스위치 ON: [{string.Join(", ", onLights)}]");
            }
            else if (dev == DeviceHeating)
            {
                if (frame.value != null && frame.value.Length >= 4)
                {
                    byte m0 = frame.value[0];
                    byte m1 = frame.value[1];
                    byte setTemp = frame.value[2];
                    byte curTemp = frame.value[3];

                    string mode = (m0 == 0x11 && m1 == 0x01) ? "외출" :
                                  (m0 == 0x11 && m1 == 0x00) ? "가동" :
                                  (m0 == 0x01 && m1 == 0x00) ? "정지" : $"모드(0x{m0:X2}{m1:X2})";

                    sb.Append($"{mode}, 설정 {setTemp}°C");
                    if (curTemp > 0) sb.Append($", 현재 {curTemp}°C");
                }
            }
            else if (dev == DeviceVentilation)
            {
                if (frame.value != null && frame.value.Length >= 3)
                {
                    byte v0 = frame.value[0];
                    byte v2 = frame.value[2];

                    if (v0 == 0x00) sb.Append("OFF (정지)");
                    else if (v0 == 0x11) sb.Append("ON (가동)");
                    else if (v0 == 0x88)
                    {
                        string speed = v2 switch
                        {
                            0x40 => "1단 (약)",
                            0x80 => "2단 (중)",
                            0xC0 => "3단 (강)",
                            _ => $"풍량(0x{v2:X2})"
                        };
                        sb.Append($"풍량 {speed}");
                    }
                }
            }
            else if (dev == DeviceDoorLock)
            {
                if (frame.source == AddressWallpad && frame.destination == DeviceDoorLock)
                    sb.Append("문열림 요청 (트리거)");
                else if (frame.source == DeviceDoorLock && frame.destination == AddressWallpad)
                    sb.Append("도어락 상태 보고");
                else
                    sb.Append("도어락 응답/신호");
            }

            return sb.ToString();
        }

        public static byte ComputeChecksum(byte[] packet)
        {
            int sum = 0;
            for (int i = 2; i <= 17; i++)
            {
                sum += packet[i];
            }
            return (byte)(sum & 0xFF);
        }

        private static void WriteUInt16(byte[] packet, int index, ushort value)
        {
            packet[index] = (byte)((value >> 8) & 0xFF);
            packet[index + 1] = (byte)(value & 0xFF);
        }

        private static ushort ReadUInt16(byte[] packet, int index)
        {
            return (ushort)((packet[index] << 8) | packet[index + 1]);
        }
    }
}
