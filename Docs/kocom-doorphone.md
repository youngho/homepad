# 코콤 월패드 현관 세대호출 RS-485 패킷 분석

현관(도어폰/카메라)에서 세대 호출 버튼을 눌렀을 때 수신되는 21바이트 RS-485 패킷의 구조와 의미를 정리한 문서입니다.

---

## 1. 캡처된 원본 패킷 목록

총 12개의 패킷이 캡처되었으며, **[호출 신호 3회] → [해제/대기 신호 3회]**의 사이클이 2번 반복된 형태입니다.

```hex
# [사이클 1]
1. AA 55 7B 9C 02 08 00 FF FF FF FF FF FF FF FF 01 01 52 19 0D 0D   (호출 시작 - 1차)
2. AA 55 7A 9D 02 08 00 FF FF FF FF FF FF FF FF 01 01 87 84 0D 0D   (호출 시작 - 재전송 1)
3. AA 55 7A 9E 02 08 00 FF FF FF FF FF FF FF FF 01 01 0A 27 0D 0D   (호출 시작 - 재전송 2)
4. AA 55 7B 9C 02 08 00 FF FF FF FF 00 00 00 00 02 00 9D BB 0D 0D   (호출 해제/대기 - 1차)
5. AA 55 7A 9D 02 08 00 FF FF FF FF 00 00 00 00 02 00 48 26 0D 0D   (호출 해제/대기 - 재전송 1)
6. AA 55 7A 9E 02 08 00 FF FF FF FF 00 00 00 00 02 00 C5 85 0D 0D   (호출 해제/대기 - 재전송 2)

# [사이클 2 - 사이클 1과 동일 (반복 전송)]
7.  AA 55 7B 9C 02 08 00 FF FF FF FF FF FF FF FF 01 01 52 19 0D 0D
8.  AA 55 7A 9D 02 08 00 FF FF FF FF FF FF FF FF 01 01 87 84 0D 0D
9.  AA 55 7A 9E 02 08 00 FF FF FF FF FF FF FF FF 01 01 0A 27 0D 0D
10. AA 55 7B 9C 02 08 00 FF FF FF FF 00 00 00 00 02 00 9D BB 0D 0D
11. AA 55 7A 9D 02 08 00 FF FF FF FF 00 00 00 00 02 00 48 26 0D 0D
12. AA 55 7A 9E 02 08 00 FF FF FF FF 00 00 00 00 02 00 C5 85 0D 0D
```

---

## 2. 패킷 프레임 레이아웃 (21 Bytes)

```text
AA 55 | SEQ (2B) | DEV (3B) | ADDR (4B) | DATA (4B) | CMD (2B) | CRC16 (2B) | 0D 0D
 0  1    2     3    4  5  6    7  8  9 10  11 12 13 14  15   16    17    18    19 20
```

| 바이트 번호 | 필드명 | 길이 | 설명 |
|:---:|:---:|:---:|:---|
| **0 ~ 1** | **Header** | 2B | 프레임 시작 프리앰블 (`AA 55`) |
| **2 ~ 3** | **Sequence / Retry** | 2B | 전송 차수 식별자 (`7B 9C` → `7A 9D` → `7A 9E`) |
| **4 ~ 6** | **Device / Sub-ID** | 3B | 기기 식별 (`02 08 00` : 현관 도어폰/카메라 0번 채널) |
| **7 ~ 10** | **Address / Target** | 4B | 대상 세대/그룹 마스크 (`FF FF FF FF` : 전체 브로드캐스트) |
| **11 ~ 14** | **State Data** | 4B | 상태 데이터 (`FF FF FF FF`: 호출 활성, `00 00 00 00`: 대기/비활성) |
| **15 ~ 16** | **Command / Status** | 2B | 동작 명령 (`01 01`: 호출 벨 울림, `02 00`: 대기/종료) |
| **17 ~ 18** | **Checksum (CRC-16)** | 2B | **CRC-16/XMODEM** (인덱스 2~16 총 15바이트 검증) |
| **19 ~ 20** | **Footer** | 2B | 프레임 종료 구분자 (`0D 0D`) |

> **중요 차이점**:  
> 일반 조명/난방/환기 패킷([kocom-hex.md](file:///Users/yoho/github/wallpad/kocom-hex.md))은 체크섬으로 **1바이트 덧셈 합(`SUM % 256`)**을 사용하지만, **현관/도어폰 패킷은 2바이트 `CRC-16/XMODEM`**을 사용합니다.

---

## 3. 필드별 상세 분석

### ① 헤더 및 푸터
- `AA 55`: 코콤 모든 RS-485 프로토콜 공통 시작 마커
- `0D 0D`: 코콤 통신 종료 마커 (`CR CR`)

### ② 전송 시퀀스 (Sequence / Retry)
코콤 RS-485 통신은 유실 방지를 위해 하나의 이벤트를 3회 연속 전송하며, 전송 회차마다 2~3번째 바이트가 바뀝니다.
- **`7B 9C`** : 1회차 전송 (최초 이벤트 발생)
- **`7A 9D`** : 2회차 전송 (1차 재전송)
- **`7A 9E`** : 3회차 전송 (2차 재전송)

### ③ 장치 및 주소 (`02 08 00 FF FF FF FF`)
- `02 08` : 현관 카메라 / 도어폰 (Door Station Unit)
- `00` : 카메라 채널 (현관 1번 카메라)
- `FF FF FF FF` : 주소 필터 없이 월패드로 직접 브로드캐스트 수신

### ④ 상태 데이터 & 명령 코드

#### 1) 호출 시작 신호 (세대 벨 울림)
```hex
AA 55 [SEQ] 02 08 00 FF FF FF FF  FF FF FF FF  01 01  [CRC] 0D 0D
```
- **데이터 (`FF FF FF FF`)**: 호출 라인 활성화 (Active)
- **명령 (`01 01`)**: 
  - `CMD = 01`: 호출(Call) 이벤트
  - `SUB = 01`: 호출 시작(Ring / Start)
- **의미**: 방문자가 현관 벨 버튼을 누르는 순간 전송되어 월패드 화면을 켜고 벨소리를 발생시킵니다.

#### 2) 호출 대기/복귀 신호
```hex
AA 55 [SEQ] 02 08 00 FF FF FF FF  00 00 00 00  02 00  [CRC] 0D 0D
```
- **데이터 (`00 00 00 00`)**: 호출 라인 비활성화 (Clear)
- **명령 (`02 00`)**:
  - `CMD = 02`: 상태 복귀 / 대기(Idle)
  - `SUB = 00`: 비활성(Inactive)
- **의미**: 벨 울림 시간 경과 또는 호출 이벤트 처리가 끝난 후 대기 상태로 복귀합니다.

---

## 4. 체크섬 검증 (CRC-16/XMODEM)

체크섬은 **Byte 2번(Sequence 시작)부터 Byte 16번(Command 끝)까지 총 15바이트**를 대상으로 계산됩니다.

- **알고리즘**: `CRC-16/XMODEM`
- **다항식 (Polynomial)**: `0x1021` ($x^{16} + x^{12} + x^5 + 1$)
- **초기값 (Initial Value)**: `0x0000`
- **반사 (Ref In / Ref Out)**: `False` / `False`
- **결과 XOR (Xor Out)**: `0x0000`
- **바이트 순서**: Big-Endian (상위 바이트가 앞, 하위 바이트가 뒤)

### 계산 검증표

| 패킷 | 계산 대상 바이트 (15B) | 계산된 CRC | 패킷 값 | 일치 여부 |
|:---:|:---|:---:|:---:|:---:|
| 1 | `7B 9C 02 08 00 FF FF FF FF FF FF FF FF 01 01` | `0x5219` | `52 19` | ✅ 일치 |
| 2 | `7A 9D 02 08 00 FF FF FF FF FF FF FF FF 01 01` | `0x8784` | `87 84` | ✅ 일치 |
| 3 | `7A 9E 02 08 00 FF FF FF FF FF FF FF FF 01 01` | `0x0A27` | `0A 27` | ✅ 일치 |
| 4 | `7B 9C 02 08 00 FF FF FF FF 00 00 00 00 02 00` | `0x9DBB` | `9D BB` | ✅ 일치 |
| 5 | `7A 9D 02 08 00 FF FF FF FF 00 00 00 00 02 00` | `0x4826` | `48 26` | ✅ 일치 |
| 6 | `7A 9E 02 08 00 FF FF FF FF 00 00 00 00 02 00` | `0xC585` | `C5 85` | ✅ 일치 |

---

## 5. 아두이노 / ESP32 구현 가이드

월패드 수신 패킷 중 현관 호출을 감지하기 위한 C/C++ 파싱 예시 코드입니다.

### CRC-16/XMODEM 계산 함수
```cpp
uint16_t crc16_xmodem(const uint8_t* data, size_t len) {
  uint16_t crc = 0x0000;
  for (size_t i = 0; i < len; ++i) {
    crc ^= (static_cast<uint16_t>(data[i]) << 8);
    for (uint8_t bit = 0; bit < 8; ++bit) {
      if (crc & 0x8000) {
        crc = ((crc << 1) ^ 0x1021) & 0xFFFF;
      } else {
        crc = (crc << 1) & 0xFFFF;
      }
    }
  }
  return crc;
}
```

### 현관 호출 감지 로직
```cpp
bool parseDoorbellPacket(const uint8_t* f, size_t len) {
  // 1. 프레임 길이 및 헤더/푸터 검사
  if (len != 21 || f[0] != 0xAA || f[1] != 0x55 || f[19] != 0x0D || f[20] != 0x0D) {
    return false;
  }

  // 2. CRC 검증 (Payload 15바이트: index 2~16)
  uint16_t calculatedCrc = crc16_xmodem(f + 2, 15);
  uint16_t packetCrc = (f[17] << 8) | f[18];
  if (calculatedCrc != packetCrc) {
    return false; // CRC 불일치
  }

  // 3. 현관 도어폰 장치(02 08) 확인
  if (f[4] == 0x02 && f[5] == 0x08) {
    // 4. 명령 코드 확인 (f[15], f[16])
    if (f[15] == 0x01 && f[16] == 0x01) {
      // 현관 세대호출 발생 (벨 울림)
      Serial.println("[EVENT] 현관 세대 호출 감지 (Ding Dong!)");
      return true;
    } else if (f[15] == 0x02 && f[16] == 0x00) {
      // 호출 대기/종료 복귀
      Serial.println("[EVENT] 현관 세대 호출 종료/대기 상태");
      return true;
    }
  }

  return false;
}
```
