/*
  코콤 RS-485 브리지 — Arduino UNO WiFi Rev2 + MAX485
  Unity(TCP 8080) <-> Serial1 9600 8N1 <-> 월패드 버스

  배선
    MAX485 RO -> D0 (Serial1 RX)
    MAX485 DI -> D1 (Serial1 TX)
    MAX485 DE, RE -> D2 (함께 묶음)
    MAX485 A/B -> 월패드 RS-485
    접지 공통

  라이브러리: WiFiNINA
  보드: Arduino Uno WiFi Rev2
*/

#include <WiFiNINA.h>

char ssid[] = "YOUR_SSID";
char pass[] = "YOUR_PASSWORD";

const uint16_t kTcpPort = 8080;
const uint8_t kDePin = 2;
const uint32_t kRs485Baud = 9600;
const uint32_t kTxHoldMs = 2;

WiFiServer server(kTcpPort);
WiFiClient client;

void setTransmit(bool enable) {
  digitalWrite(kDePin, enable ? HIGH : LOW);
}

void setup() {
  pinMode(kDePin, OUTPUT);
  setTransmit(false);

  Serial.begin(115200);
  Serial1.begin(kRs485Baud);

  while (WiFi.begin(ssid, pass) != WL_CONNECTED) {
    delay(1500);
  }

  server.begin();
  Serial.print("Kocom RS485 bridge ");
  Serial.println(WiFi.localIP());
}

void loop() {
  if (!client || !client.connected()) {
    client = server.available();
    if (client) {
      Serial.println("Unity connected");
    }
    return;
  }

  if (client.available() > 0) {
    setTransmit(true);
    while (client.available() > 0) {
      int incoming = client.read();
      if (incoming < 0) break;
      Serial1.write((uint8_t)incoming);
    }
    Serial1.flush();
    delay(kTxHoldMs);
    setTransmit(false);
  }

  while (Serial1.available() > 0) {
    int incoming = Serial1.read();
    if (incoming < 0) break;
    client.write((uint8_t)incoming);
  }
}
