using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Homepad.Core
{
    /// <summary>
    /// 코콤 RS-485 21바이트 프레임.
    /// AA 55 | TYPE | 00 | DEST장치 DEST방 | SRC장치 SRC방 | CMD | VALUE[8] | CS | 0D 0D
    /// TYPE 0x30BC 월패드 송신, 0x30DC 장치 보고.
    /// CS = bytes[2..17] 합 modulo 256.
    /// 방 코드(0x0001 거실, 0x0101 방1 …)는 앱 안 식별자고, 버스에는 방 번호 1바이트만 실인다.
    /// </summary>
    public static class KocomProtocol
    {
        public const int PacketSize = 21;
        public const byte Header1 = 0xAA;
        public const byte Header2 = 0x55;
        public const byte Trailer = 0x0D;

        public const ushort TypeTransmit = 0x30BC;
        public const ushort TypeReport = 0x30DC;
        public const ushort TypeRetransmit1 = 0x30BD;
        public const ushort TypeRetransmit2 = 0x30BE;

        // 이 집 부팅 캡처: BC→BD 200~300ms(답 없을 때), BD→BE ~30ms.
        // HA 쪽 50ms/150ms/1s는 UART 갭·명령 간격·ACK 타임아웃이라 재전송 간격이 아니다.
        public const int RetransmitWaitBdMs = 250;
        public const int RetransmitGapBeMs = 30;
        public const ushort AddressWallpad = 0x0001;
        public const ushort DeviceLight = 0x000E;
        public const ushort DeviceHeating = 0x0036;
        public const ushort DeviceGas = 0x002C;
        public const ushort DeviceVentilation = 0x0048;
        public const ushort DeviceElevator = 0x0044;
        public const ushort DeviceDoorLock = 0x0033;

        public const byte LightOn = 0xFF;
        public const byte LightOff = 0x00;
        // 난방 VALUE[0] (바이트 10). CMD가 아니다.
        public const byte HeatRun = 0x11;
        public const byte HeatStop = 0x01;
        // 난방 VALUE[1] (바이트 11). 외출 플래그.
        public const byte HeatAwayOff = 0x00;
        public const byte HeatAwayOn = 0x01;
        public const ushort CommandControl = 0x0000;
        public const ushort CommandQuery = 0x003A;
        public const ushort CommandDoorStatus = 0x0001;
        public const ushort CommandDoorUnlock = 0x0002;
        public const byte CmdState = 0x00;
        public const byte CmdOn = 0x01;
        public const byte CmdOff = 0x02;
        public const byte CmdQuery = 0x3A;
        public const byte DeviceByteWallpad = 0x01;
        public const byte DeviceByteLight = 0x0E;
        public const byte DeviceByteHeating = 0x36;
        public const byte DeviceByteGas = 0x2C;
        public const byte DeviceByteVentilation = 0x48;
        public const byte DeviceByteElevator = 0x44;
        public const byte DeviceByteDoorLock = 0x33;

        public const byte VentCmdOff = 0x00;
        public const byte VentCmdOn = 0x11;
        public const byte VentCmdSpeed = 0x11;
        public const byte VentMarker = 0x03;
        public const byte VentFanOff = 0xFC;
        public const byte VentFanLow = 0x40;
        public const byte VentFanMid = 0x80;
        public const byte VentFanHigh = 0xC0;

        public struct Frame
        {
            public ushort type;
            public byte destDevice;
            public byte destRoom;
            public byte srcDevice;
            public byte srcRoom;
            public byte command;
            // 바이트 4–7의 16비트 묶음. 도어락 등 기존 비교용.
            public ushort source;
            public ushort destination;
            public ushort room;
            public byte[] value;
            public byte checksum;

            public ushort DeviceAddress
            {
                get
                {
                    if (IsKnownDeviceByte(destDevice)) return DeviceCode(destDevice);
                    if (IsKnownDeviceByte(srcDevice)) return DeviceCode(srcDevice);
                    return DeviceCode(destDevice);
                }
            }
        }

        public static bool IsKnownDevice(ushort address)
        {
            return IsKnownDeviceByte((byte)(address & 0xFF));
        }

        public static bool IsKnownDeviceByte(byte device)
        {
            return device == DeviceByteLight
                || device == DeviceByteHeating
                || device == DeviceByteGas
                || device == DeviceByteVentilation
                || device == DeviceByteElevator
                || device == DeviceByteDoorLock;
        }

        public static ushort DeviceCode(byte device)
        {
            return device;
        }

        public static ushort RoomCodeFromIndex(byte roomIndex)
        {
            return (ushort)((roomIndex << 8) | DeviceByteWallpad);
        }

        public static byte RoomIndexFromCode(ushort roomCode)
        {
            return (byte)((roomCode >> 8) & 0xFF);
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
            // 월패드 송신은 16비트 장치/방/명령이 버스의 pad+DEST / DEST방+SRC장치 / SRC방+CMD 와 같은 바이트가 된다.
            WriteUInt16(packet, 4, source);
            WriteUInt16(packet, 6, destination);
            WriteUInt16(packet, 8, room);
            Array.Copy(value, 0, packet, 10, 8);
            packet[18] = ComputeChecksum(packet);
            packet[19] = Trailer;
            packet[20] = Trailer;
            return packet;
        }

        // 월패드 송신: 30 BC | 장치 | 방코드 | 명령 | VALUE. 방코드 0101은 DEST방=01 + SRC=월패드.
        public static byte[] BuildRequest(ushort device, ushort room, ushort command, byte[] value8)
        {
            return BuildFrame(room, command, value8, device, TypeTransmit);
        }

        public static byte[] CreateStatusQueryPacket(ushort device, ushort room)
        {
            return BuildRequest(device, room, CommandQuery, new byte[8]);
        }

        public static bool IsRequestType(ushort type)
        {
            return type == TypeTransmit || type == TypeRetransmit1 || type == TypeRetransmit2;
        }

        public static bool IsTransmitPacket(byte[] packet)
        {
            return packet != null && packet.Length >= PacketSize && ReadUInt16(packet, 2) == TypeTransmit;
        }

        public static byte[] CloneWithType(byte[] packet, ushort type)
        {
            if (packet == null || packet.Length < PacketSize) return packet;

            byte[] copy = new byte[packet.Length];
            Array.Copy(packet, copy, packet.Length);
            WriteUInt16(copy, 2, type);
            copy[18] = ComputeChecksum(copy);
            return copy;
        }

        // 바이트 9. 조회는 0x3A, 제어/상태 보고는 0x00.
        public static ushort CommandOf(Frame frame)
        {
            return frame.command;
        }

        // TYPE × CMD.
        // 30 DC → VALUE를 메모리에 넣는다.
        // 30 BC/BD/BE + 조회(0x3A) → 넣지 않는다.
        public static bool ShouldApplyState(Frame frame)
        {
            if (frame.type == TypeReport) return true;
            if (IsRequestType(frame.type) && frame.command == CmdQuery) return false;
            return false;
        }

        public static bool IsQuery(Frame frame)
        {
            return IsRequestType(frame.type) && frame.command == CmdQuery;
        }

        public static bool IsKnownRoom(ushort address)
        {
            return address == 0x0001 || address == 0x0101 || address == 0x0201 || address == 0x0301;
        }

        public static byte RoomIndexOf(Frame frame)
        {
            if (IsKnownDeviceByte(frame.destDevice)) return frame.destRoom;
            if (IsKnownDeviceByte(frame.srcDevice)) return frame.srcRoom;
            return frame.destRoom;
        }

        public static ushort ResolveRoom(Frame frame)
        {
            return RoomCodeFromIndex(RoomIndexOf(frame));
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
            return BuildRequest(DeviceLight, room, CommandControl, value);
        }

        public static byte[] CreateHeatingControlPacket(ushort room, bool power, bool awayMode, float targetTemp)
        {
            byte temp = (byte)Mathf.Clamp(Mathf.RoundToInt(targetTemp), 5, 40);
            byte[] value = new byte[8];
            value[0] = power ? HeatRun : HeatStop;
            value[1] = awayMode ? HeatAwayOn : HeatAwayOff;
            value[2] = temp;
            return BuildRequest(DeviceHeating, room, CmdState, value);
        }

        public static byte[] CreateGasClosePacket()
        {
            return BuildRequest(DeviceGas, 0x0001, CmdOff, new byte[8]);
        }

        public static byte[] CreateVentilationPacket(VentilationSpeed speed, bool fromOff = false)
        {
            byte[] value = new byte[8];
            value[1] = VentMarker;
            if (speed == VentilationSpeed.Off)
            {
                value[0] = VentCmdOff;
                value[2] = VentFanOff;
            }
            else if (fromOff && speed == VentilationSpeed.Low)
            {
                value[0] = VentCmdOn;
                value[2] = VentFanLow;
            }
            else
            {
                value[0] = VentCmdSpeed;
                value[2] = FanByte(speed);
            }

            return BuildRequest(DeviceVentilation, 0x0001, CommandControl, value);
        }

        public static bool TryParseVentilation(Frame frame, out bool powered, out VentilationSpeed speed)
        {
            powered = false;
            speed = VentilationSpeed.Off;
            if (frame.value == null || frame.value.Length == 0) return false;

            byte v0 = frame.value[0];
            byte v2 = frame.value.Length > 2 ? frame.value[2] : (byte)0;
            if (v0 == VentCmdOff)
            {
                powered = false;
                speed = VentilationSpeed.Off;
                return true;
            }

            if (v0 == VentCmdOn || v0 == VentCmdSpeed)
            {
                powered = true;
                speed = FanSpeed(v2);
                return true;
            }

            if (v0 <= 3)
            {
                speed = (VentilationSpeed)v0;
                powered = speed != VentilationSpeed.Off;
                return true;
            }

            return false;
        }

        private static byte FanByte(VentilationSpeed speed)
        {
            return speed switch
            {
                VentilationSpeed.Medium => VentFanMid,
                VentilationSpeed.High => VentFanHigh,
                _ => VentFanLow
            };
        }

        private static VentilationSpeed FanSpeed(byte fan)
        {
            return fan switch
            {
                VentFanMid => VentilationSpeed.Medium,
                VentFanHigh => VentilationSpeed.High,
                VentFanOff => VentilationSpeed.Off,
                _ => VentilationSpeed.Low
            };
        }

        public static byte[] CreateElevatorCallPacket()
        {
            return BuildFrame(DeviceElevator, CmdOn, new byte[8], AddressWallpad, TypeTransmit);
        }

        public static byte[] CreateDoorUnlockPacket()
        {
            return BuildFrame(DeviceDoorLock, CommandDoorUnlock, new byte[8], AddressWallpad, TypeTransmit);
        }

        public static byte[] CreateDoorLockAckPacket()
        {
            return BuildFrame(DeviceDoorLock, CommandDoorStatus, new byte[8], AddressWallpad, TypeReport);
        }

        public static bool TryParse(byte[] raw, out Frame frame)
        {
            frame = default;
            if (raw == null || raw.Length < PacketSize) return false;
            if (raw[0] != Header1 || raw[1] != Header2) return false;
            if (raw[19] != Trailer || raw[20] != Trailer) return false;
            if (ComputeChecksum(raw) != raw[18]) return false;

            frame.type = ReadUInt16(raw, 2);
            frame.destDevice = raw[5];
            frame.destRoom = raw[6];
            frame.srcDevice = raw[7];
            frame.srcRoom = raw[8];
            frame.command = raw[9];
            frame.source = ReadUInt16(raw, 4);
            frame.destination = ReadUInt16(raw, 6);
            frame.room = ResolveRoom(frame);
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

        public readonly struct HexTableField
        {
            public readonly string Label;
            public readonly string Hex;
            public readonly string Hint;
            public readonly bool Expand;

            public HexTableField(string label, string hex, string hint = null, bool expand = false)
            {
                Label = label;
                Hex = hex ?? string.Empty;
                Hint = hint ?? string.Empty;
                Expand = expand;
            }

            public string Caption => string.IsNullOrEmpty(Hint) ? Label : Hint;
        }

        public static HexTableField[] GetHexTableFields(byte[] packet)
        {
            if (packet == null || packet.Length < PacketSize) return Array.Empty<HexTableField>();

            ushort type = ReadUInt16(packet, 2);
            bool checksumOk = ComputeChecksum(packet) == packet[18];

            return new[]
            {
                new HexTableField("시작", HexPair(packet, 0)),
                new HexTableField("종류", HexPair(packet, 2), DescribeType(type)),
                new HexTableField("고정", packet[4].ToString("X2")),
                new HexTableField("수신", $"{packet[5]:X2} {packet[6]:X2}", DescribePeer(packet[5], packet[6])),
                new HexTableField("송신", $"{packet[7]:X2} {packet[8]:X2}", DescribePeer(packet[7], packet[8])),
                new HexTableField("명령", packet[9].ToString("X2"), DescribeCommandByte(packet[9])),
                new HexTableField("값", ToHexSlice(packet, 10, 8), null, true),
                new HexTableField("합", packet[18].ToString("X2"), checksumOk ? string.Empty : "오류"),
                new HexTableField("끝", HexPair(packet, 19))
            };
        }

        public static string FormatHexTable(byte[] packet)
        {
            var fields = GetHexTableFields(packet);
            if (fields.Length == 0) return ToHexString(packet);

            var hexRow = new StringBuilder();
            var capRow = new StringBuilder();
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0)
                {
                    hexRow.Append(" │ ");
                    capRow.Append(" │ ");
                }

                hexRow.Append(fields[i].Hex);
                capRow.Append(fields[i].Caption);
            }

            return hexRow.Append('\n').Append(capRow).ToString();
        }

        private static string HexPair(byte[] packet, int index)
        {
            return $"{packet[index]:X2} {packet[index + 1]:X2}";
        }

        private static string ToHexSlice(byte[] packet, int start, int length)
        {
            var sb = new StringBuilder(length * 3);
            for (int i = 0; i < length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(packet[start + i].ToString("X2"));
            }
            return sb.ToString();
        }

        private static string DescribeType(ushort type)
        {
            return type switch
            {
                TypeTransmit => "요청",
                TypeReport => "상태",
                TypeRetransmit1 => "재전송1",
                TypeRetransmit2 => "재전송2",
                _ => string.Empty
            };
        }

        private static string DescribeDeviceByte(byte device)
        {
            return device switch
            {
                DeviceByteLight => "조명",
                DeviceByteHeating => "난방",
                DeviceByteVentilation => "환기",
                DeviceByteDoorLock => "도어락",
                DeviceByteGas => "가스",
                DeviceByteElevator => "엘리베이터",
                DeviceByteWallpad => "월패드",
                _ => string.Empty
            };
        }

        private static string DescribeRoomIndex(byte room)
        {
            return room switch
            {
                0 => "거실",
                1 => "방1",
                2 => "방2",
                3 => "방3",
                _ => string.Empty
            };
        }

        private static string DescribePeer(byte device, byte room)
        {
            string dev = DescribeDeviceByte(device);
            string roomName = DescribeRoomIndex(room);
            if (device == DeviceByteWallpad)
            {
                return "월패드";
            }

            if (!string.IsNullOrEmpty(dev) && !string.IsNullOrEmpty(roomName))
            {
                return dev + " " + roomName;
            }

            return dev;
        }

        private static string DescribeCommandByte(byte command)
        {
            return command switch
            {
                CmdState => "제어",
                CmdQuery => "조회",
                CmdOn => "켜기",
                CmdOff => "끄기",
                _ => string.Empty
            };
        }

        public static string DecodeFrame(Frame frame)
        {
            string typeStr = frame.type switch
            {
                TypeTransmit => "요청",
                TypeReport => "상태",
                TypeRetransmit1 => "재전송1",
                TypeRetransmit2 => "재전송2",
                _ => $"타입(0x{frame.type:X4})"
            };

            string src = DescribePeer(frame.srcDevice, frame.srcRoom);
            string dest = DescribePeer(frame.destDevice, frame.destRoom);
            if (string.IsNullOrEmpty(src)) src = $"0x{frame.srcDevice:X2}";
            if (string.IsNullOrEmpty(dest)) dest = $"0x{frame.destDevice:X2}";

            string cmd = DescribeCommandByte(frame.command);
            if (string.IsNullOrEmpty(cmd)) cmd = $"CMD 0x{frame.command:X2}";

            var sb = new StringBuilder();
            sb.Append('[').Append(typeStr).Append("] ");
            sb.Append(src).Append(" → ").Append(dest);
            sb.Append(" · ").Append(cmd);

            string payload = DescribeValue(frame);
            if (!string.IsNullOrEmpty(payload))
            {
                sb.Append(" · ").Append(payload);
            }

            return sb.ToString();
        }

        private static string DescribeValue(Frame frame)
        {
            if (frame.command == CmdQuery)
            {
                return string.Empty;
            }

            ushort dev = frame.DeviceAddress;
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

                if (onLights.Count == 0) return "전체 OFF";
                return "스위치 ON: [" + string.Join(", ", onLights) + "]";
            }

            if (dev == DeviceHeating)
            {
                if (frame.value == null || frame.value.Length < 3) return string.Empty;
                byte m0 = frame.value[0];
                byte m1 = frame.value.Length > 1 ? frame.value[1] : (byte)0;
                byte setTemp = frame.value[2];
                byte curTemp = frame.value.Length > 4 && frame.value[4] > 0
                    ? frame.value[4]
                    : (frame.value.Length > 3 ? frame.value[3] : (byte)0);

                string run = m0 == HeatRun ? "가동" : m0 == HeatStop ? "정지" : $"VALUE0(0x{m0:X2})";
                if (m1 == HeatAwayOn) run += ", 외출";
                else if (m1 != HeatAwayOff) run += $", VALUE1(0x{m1:X2})";

                string text = run + ", 설정 " + setTemp + "°C";
                if (curTemp > 0) text += ", 현재 " + curTemp + "°C";
                return text;
            }

            if (dev == DeviceVentilation)
            {
                if (frame.value == null || frame.value.Length < 3) return string.Empty;
                byte v0 = frame.value[0];
                byte v2 = frame.value[2];
                if (v0 == 0x00) return "OFF (정지)";
                if (v0 == 0x11) return "ON (가동)";
                if (v0 == 0x88)
                {
                    string speed = v2 switch
                    {
                        0x40 => "1단 (약)",
                        0x80 => "2단 (중)",
                        0xC0 => "3단 (강)",
                        _ => $"풍량(0x{v2:X2})"
                    };
                    return "풍량 " + speed;
                }

                return string.Empty;
            }

            if (dev == DeviceGas)
            {
                if (frame.command == CmdOn) return "열림";
                if (frame.command == CmdOff) return "닫힘";
                if (frame.value != null && frame.value.Length > 0 && frame.value[0] != 0x00) return "열림";
                return "닫힘";
            }

            if (dev == DeviceDoorLock)
            {
                if (frame.srcDevice == DeviceByteDoorLock && frame.destDevice == DeviceByteWallpad)
                    return "문열림 요청 (트리거)";
                if (frame.destDevice == DeviceByteDoorLock && frame.srcDevice == DeviceByteWallpad)
                    return "도어락 상태 보고";
                return "도어락 응답/신호";
            }

            if (dev == DeviceElevator)
            {
                if (frame.command == CmdOn) return "호출";
                byte marker = 0;
                if (frame.value != null && frame.value.Length > 0)
                {
                    marker = frame.value[0] != 0 ? frame.value[0] : (frame.value.Length > 2 ? frame.value[2] : (byte)0);
                }

                if (marker == 0x03) return "도착/정지";
                if (marker >= 1 && marker <= 60) return marker + "층";
                return string.Empty;
            }

            return string.Empty;
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
