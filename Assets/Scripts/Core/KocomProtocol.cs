using System;
using System.Text;
using UnityEngine;

namespace Homepad.Core
{
    /// <summary>
    /// 코콤(KOCOM) RS-485 통신 패킷 정의 및 인코딩/디코딩 유틸리티
    /// </summary>
    public static class KocomProtocol
    {
        public const byte HEADER_1 = 0xAA;
        public const byte HEADER_2 = 0x55;

        // 장치 타입 코드 정의
        public const byte DEVICE_LIGHT = 0x0E;       // 조명
        public const byte DEVICE_HEATING = 0x36;     // 난방
        public const byte DEVICE_GAS = 0x2C;         // 가스
        public const byte DEVICE_VENTILATION = 0x48; // 환기
        public const byte DEVICE_ELEVATOR = 0x44;    // 엘리베이터

        // 명령 코드
        public const byte CMD_READ = 0x00;           // 상태 요청
        public const byte CMD_WRITE = 0x01;          // 제어 명령
        public const byte CMD_REPORT = 0x02;         // 상태 보고

        /// <summary>
        /// RS-485 송신용 패킷 생성 (헤더 + 장치타입 + ID + 명령 + 데이터 + 체크섬)
        /// </summary>
        public static byte[] BuildPacket(byte deviceType, byte targetId, byte command, byte[] data)
        {
            int dataLen = data != null ? data.Length : 0;
            byte[] packet = new byte[6 + dataLen];

            packet[0] = HEADER_1;
            packet[1] = HEADER_2;
            packet[2] = deviceType;
            packet[3] = targetId;
            packet[4] = command;

            if (data != null && dataLen > 0)
            {
                Array.Copy(data, 0, packet, 5, dataLen);
            }

            // 체크섬 계산 (Header 제외한 바이트 합 modulo 256)
            byte sum = 0;
            for (int i = 2; i < packet.Length - 1; i++)
            {
                sum += packet[i];
            }
            packet[packet.Length - 1] = sum;

            return packet;
        }

        /// <summary>
        /// 조명 On/Off 제어 패킷 생성
        /// </summary>
        public static byte[] CreateLightControlPacket(int lightId, bool turnOn)
        {
            return BuildPacket(DEVICE_LIGHT, (byte)lightId, CMD_WRITE, new byte[] { (byte)(turnOn ? 0xFF : 0x00) });
        }

        /// <summary>
        /// 난방 제어 패킷 생성 (설정온도, 전원/외출)
        /// </summary>
        public static byte[] CreateHeatingControlPacket(int roomId, bool power, bool awayMode, float targetTemp)
        {
            byte modeByte = (byte)(!power ? 0x00 : (awayMode ? 0x02 : 0x01));
            byte tempByte = (byte)Mathf.Clamp(Mathf.RoundToInt(targetTemp), 10, 40);
            return BuildPacket(DEVICE_HEATING, (byte)roomId, CMD_WRITE, new byte[] { modeByte, tempByte });
        }

        /// <summary>
        /// 가스 밸브 잠금 패킷 생성 (안전을 위해 잠금 제어만 허용)
        /// </summary>
        public static byte[] CreateGasClosePacket()
        {
            return BuildPacket(DEVICE_GAS, 0x01, CMD_WRITE, new byte[] { 0x00 }); // 0x00: 잠금
        }

        /// <summary>
        /// 환기 풍량 제어 패킷 생성
        /// </summary>
        public static byte[] CreateVentilationPacket(VentilationSpeed speed)
        {
            return BuildPacket(DEVICE_VENTILATION, 0x01, CMD_WRITE, new byte[] { (byte)speed });
        }

        /// <summary>
        /// 엘리베이터 호출 패킷 생성
        /// </summary>
        public static byte[] CreateElevatorCallPacket(int targetFloor)
        {
            return BuildPacket(DEVICE_ELEVATOR, 0x01, CMD_WRITE, new byte[] { (byte)targetFloor });
        }

        /// <summary>
        /// 바이트 배열을 16진수 문자열로 변환 (로그 및 디버깅용)
        /// </summary>
        public static string ToHexString(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;
            StringBuilder sb = new StringBuilder(bytes.Length * 3);
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("X2")).Append(" ");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
