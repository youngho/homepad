#pragma once

// Config.h — RS-485 및 시리얼 통신 설정 (검증된 wallpad 설정 기반)
//
// ---------------------------------------------------------------------------
//  보드: Arduino UNO R4 WiFi / UNO WiFi Rev2 등
//  USB Serial = PC/Unity 통신 (115200)
//  Serial1 (D0 RX / D1 TX) = 월패드 RS-485 실버스 (9600 8N1)
//
//  모듈: 자동방향 TTL->RS485 (SZH-CVBE-010 등) -> BUS_DE_PIN = -1
//        수동 DE/RE 제어 MAX485 모듈 사용 시 -> BUS_DE_PIN = 해당 핀 번호 (예: 2 또는 4)
// ---------------------------------------------------------------------------

#include <Arduino.h>

#define USB_BAUD 115200

#define BUS_UART     Serial1
#define BUS_RX_PIN   0
#define BUS_TX_PIN   1
#define BUS_DE_PIN   -1    // 자동방향 모듈: -1 / DE/RE 제어 MAX485: 핀 번호 (예: 2)

#define BUS_BAUD     9600
#define BUS_CONFIG   SERIAL_8N1

// 프레임 끝: 버스가 이 시간 동안 조용하면 한 패킷으로 확정 (코콤 권장 ~50ms)
#define BUS_IDLE_GAP_MS 50

// 송신 전 버스가 비어 있어야 하는 최소 시간 (충돌 방지 핵심 타이밍)
#define BUS_SEND_IDLE_MS 20

// 송신 후 자기 에코를 버리기 위한 대기
#define BUS_TX_ECHO_MS 5

#define FRAME_MAX_LEN 64
