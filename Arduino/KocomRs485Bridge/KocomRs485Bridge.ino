/*
  Kocom RS-485 Bridge for Unity Wallpad
  - 검증된 wallpad Rs485Bus 엔진(충돌 방지 타이밍, 에코 억제, 자동/수동 방향 제어) 기반
  - PC Unity (USB Serial 115200) <-> RS-485 버스 (Serial1 9600 8N1) 고속 투명 바이너리 브릿지
*/

#include "Config.h"
#include "Rs485Bus.h"

Rs485Bus bus;

// USB 수신 버퍼
uint8_t usbBuf[FRAME_MAX_LEN];
size_t usbLen = 0;
uint32_t lastUsbByteMs = 0;

void setup() {
  Serial.begin(USB_BAUD);
  
  // USB 시리얼 초기화 대기 (네이티브 USB 보드 대응, 최대 2초)
  const uint32_t waitStart = millis();
  while (!Serial && (millis() - waitStart < 2000)) {
  }
  
  bus.begin();
}

void loop() {
  // 1. RS-485 버스 -> PC (Unity)
  while (bus.available()) {
    int b = bus.read();
    if (b >= 0) {
      Serial.write((uint8_t)b);
    }
  }

  // 2. PC (Unity) -> RS-485 버스
  while (Serial.available()) {
    int b = Serial.read();
    if (b < 0) break;

    if (usbLen < FRAME_MAX_LEN) {
      usbBuf[usbLen++] = (uint8_t)b;
      lastUsbByteMs = millis();
    }

    // 코콤 21바이트 표준 프레임 완성 감지 (AA 55 ... 0D 0D)
    if (usbLen >= 21 && usbBuf[0] == 0xAA && usbBuf[1] == 0x55 && 
        usbBuf[usbLen - 2] == 0x0D && usbBuf[usbLen - 1] == 0x0D) {
      bus.send(usbBuf, usbLen);
      usbLen = 0;
    }
  }

  // 패킷 조각이 남아있고 일정 시간(10ms) 동안 추가 데이터가 없으면 전송 시도
  if (usbLen > 0 && (millis() - lastUsbByteMs > 10)) {
    bus.send(usbBuf, usbLen);
    usbLen = 0;
  }
}
