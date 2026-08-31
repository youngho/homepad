#pragma once

// Rs485Bus.h — RS-485 반이중 충돌 및 에코 방지 버스 드라이버
// 검증된 wallpad Rs485Bus 엔진 기반

#include <Arduino.h>
#include "Config.h"

class Rs485Bus {
 public:
  void begin() {
    if (BUS_DE_PIN >= 0) {
      pinMode(BUS_DE_PIN, OUTPUT);
      digitalWrite(BUS_DE_PIN, LOW);  // 수신 모드 (RX)
    }

    BUS_UART.begin(BUS_BAUD, BUS_CONFIG);
    lastByteMs_ = millis();
  }

  int read() {
    int b = BUS_UART.read();
    if (b >= 0) {
      lastByteMs_ = millis();
      receiving_ = true;
    }
    return b;
  }

  bool available() { return BUS_UART.available() > 0; }

  uint32_t idleMs() const { return millis() - lastByteMs_; }

  bool isIdle(uint32_t gapMs) const { return idleMs() >= gapMs; }

  void noteActivity() { lastByteMs_ = millis(); }

  bool send(const uint8_t* data, size_t len) {
    if (data == nullptr || len == 0 || len > FRAME_MAX_LEN) {
      return false;
    }

    // 버스가 비어 있을 때까지 대기 (충돌 방지 타이밍)
    const uint32_t deadline = millis() + 250;
    while (!isIdle(BUS_SEND_IDLE_MS)) {
      if (millis() > deadline) {
        return false;
      }
      while (BUS_UART.available()) {
        BUS_UART.read();
        lastByteMs_ = millis();
      }
      delay(1);
    }

    transmitting_ = true;
    if (BUS_DE_PIN >= 0) {
      digitalWrite(BUS_DE_PIN, HIGH);  // 송신 모드 (TX)
    }

    BUS_UART.write(data, len);
    BUS_UART.flush();

    // 마지막 스톱 비트까지 나간 뒤 방향을 되돌림 (9600bps ≈ 104us/bit)
    delayMicroseconds(350);

    if (BUS_DE_PIN >= 0) {
      digitalWrite(BUS_DE_PIN, LOW);   // 수신 모드 (RX) 복귀
    }

    // 자기 송신 에코 버퍼 비우기
    delay(BUS_TX_ECHO_MS);
    while (BUS_UART.available()) {
      BUS_UART.read();
    }

    lastByteMs_ = millis();
    transmitting_ = false;
    receiving_ = false;
    return true;
  }

  bool isTransmitting() const { return transmitting_; }

 private:
  uint32_t lastByteMs_ = 0;
  bool transmitting_ = false;
  bool receiving_ = false;
};
