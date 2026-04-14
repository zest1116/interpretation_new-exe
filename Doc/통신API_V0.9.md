# WebView API

WPF WebView2 환경에서 SPA와 네이티브 앱 간 양방향 통신 인터페이스 명세.

---

## 개요

React SPA는 `window.chrome.webview.postMessage`로 WPF에 요청을 보내고,
`window.chrome.webview.addEventListener("message")`로 응답 및 이벤트를 수신한다.

```
[React SPA]                          [WPF Native]

  postMessage(요청 JSON)  ────────▶  처리
                                        │
  onMessage(응답 JSON)    ◀────────  응답 전송

  ---

  onMessage(이벤트 JSON)  ◀────────  OS 이벤트 발생 시 push
```


## 메시지 규격

### 공통 구조

모든 메시지(요청·응답·이벤트)는 동일한 envelope을 따른다.

```typescript
interface Message<T = unknown> {
  type: string;   // 메시지 식별자
  data?: T;       // 페이로드 (타입별로 다름, 생략 가능)
}
```

### 에러 응답

요청 처리 중 오류 발생 시 아래 형식으로 반환된다.

```json
{ "type": "error", "data": { "message": "에러 내용" } }
```

---

## 공통 타입 정의

### DeviceSnapshot

디바이스 목록 조회·변경·push 이벤트 모두에서 사용하는 공통 응답 구조.

```typescript
interface DeviceSnapshot {
  inputs: AudioDevice[];         // 입력(마이크) 장치 목록
  outputs: AudioDevice[];        // 출력(스피커) 장치 목록
  currentInputId: string | null; // 현재 기본 입력 장치 ID
  currentOutputId: string | null; // 현재 기본 출력 장치 ID
}

interface AudioDevice {
  id: string;        // 장치 고유 ID
  name: string;      // 장치 표시명 (예: "마이크 (Realtek Audio)")
  isDefault: boolean; // 해당 방향에서 기본 장치 여부
}
```

---

## API 상세

### 1. 디바이스 목록 요청

현재 시스템의 활성 입력/출력 오디오 장치 목록을 조회한다.

**요청**

```json
{ "type": "requestDevices" }
```

**응답** — `devicesResponse`

```json
{
  "type": "devicesResponse",
  "data": {
    "inputs": [
      { "id": "{0.0.1.00000000}.{e3a3...}", "name": "마이크 (Realtek Audio)", "isDefault": true },
      { "id": "{0.0.1.00000000}.{a1b2...}", "name": "마이크 (USB Audio)", "isDefault": false }
    ],
    "outputs": [
      { "id": "{0.0.0.00000000}.{f7e6...}", "name": "스피커 (Realtek Audio)", "isDefault": true }
    ],
    "currentInputId": "{0.0.1.00000000}.{e3a3...}",
    "currentOutputId": "{0.0.0.00000000}.{f7e6...}"
  }
}
```

---

### 2. 기본 디바이스 변경

Windows 시스템의 기본 오디오 장치를 변경한다.

**요청**

```json
{
  "type": "setDefaultDevice",
  "data": {
    "deviceType": "input",
    "deviceId": "{0.0.1.00000000}.{a1b2...}"
  }
}
```

| 필드 | 타입 | 필수 | 설명 |
|------|------|:----:|------|
| `deviceType` | `string` | O | `"input"` 또는 `"output"` |
| `deviceId` | `string` | O | 변경할 장치의 ID (디바이스 목록의 `id` 값) |

**응답** — `devicesResponse`

변경 후의 전체 디바이스 스냅샷이 반환된다. 형식은 1번과 동일.

**참고**: 기본 장치 변경 후 OS 이벤트가 별도로 발생하여 `deviceChanged` push도 수신될 수 있다.
SPA에서는 `devicesResponse`와 `deviceChanged`를 동일하게 처리하면 된다.

---

### 3. 디바이스 변경 이벤트 (push)

사용자가 Windows 설정에서 장치를 변경하거나, USB 장치를 연결/분리하면 WPF가 자동으로 push한다.
SPA의 요청 없이 수신되는 이벤트이므로 리스너 등록만 해두면 된다.

**이벤트**

```json
{
  "type": "deviceChanged",
  "data": {
    "inputs": [ ... ],
    "outputs": [ ... ],
    "currentInputId": "...",
    "currentOutputId": "..."
  }
}
```

`data`는 `DeviceSnapshot`과 동일한 구조.

**발생 조건**: 장치 추가, 제거, 기본 장치 변경, 장치 상태 변경, 속성 변경.
내부적으로 700ms 디바운스가 적용되어 있어 빈번한 이벤트가 하나로 병합된다.

---

### 4. 디바이스 장치 Window

프론트에서 exe의 디바이스 장치 Window를 오픈한다.

```json
{
  "type": "showDeviceWindow"
}
```

### 5. 회사코드 초기화

```json
{
    "type": "initCompany"
}
```

### 6. SPA로 알림전송

```json
{
    "type": "notification",
    "data":{
        "message": "..."
    }
```

## 전체 메시지 타입 요약

### SPA → WPF (요청)

| type | data | 설명 |
|------|:----:|------|
| `requestDevices` | 없음 | 디바이스 목록 조회 |
| `setDefaultDevice` | `{ deviceType, deviceId }` | 기본 디바이스 변경 |
| `showDeviceWindow` | 없음 | 디바이스 장치 Window |
| `initCompany` | 없음 | 회사코드 초기화 |

### WPF → SPA (응답 / 이벤트)

| type | 트리거 | data |
|------|--------|------|
| `devicesResponse` | `requestDevices` / `setDefaultDevice` 응답 | `DeviceSnapshot` |
| `deviceChanged` | OS 디바이스 변경 (push) | `DeviceSnapshot` |
| `error` | 요청 처리 실패 | `{ message }` |

---

## 사용 예시

```typescript
// ── 초기화 ──

const wv = window.chrome.webview;

wv.addEventListener("message", (e) => {
  const { type, data } = e.data;

  switch (type) {
    case "devicesResponse":
    case "deviceChanged":
      // 디바이스 목록 갱신 (두 타입 모두 동일 구조)
      updateDeviceList(data);
      break;
    case "error":
      showError(data.message);
      break;
  }
});


// ── 1. 디바이스 목록 요청 ──

wv.postMessage({ type: "requestDevices" });


// ── 2. 기본 디바이스 변경 ──

wv.postMessage({
  type: "setDefaultDevice",
  data: { deviceType: "output", deviceId: selectedId },
});
```

---
## 오디오 캡처(WebSocket)
오디오 데이터 스트림 전용 채널.
WebSocket 연결 후 init 메시지를 보내면 캡처가 자동으로 시작되고,
같은 소켓으로 오디오 데이터가 수신된다.

### 연결

```typescript
const ws = new WebSocket("ws://127.0.0.1:5123/ws/audio");
```

### 캡처 시작: init 메시지

WebSocket `open` 이벤트 후 아래 JSON 텍스트 메시지를 전송하면 캡처가 시작된다.

```typescript
ws.onopen = () => {
  ws.send(JSON.stringify({
    type: "init",
    deviceType: "all"
  }));
};
```


| 필드 | 타입 | 필수 | 설명 |
|------|------|:----:|------|
| `type` | `string` | O | 반드시 `"init"` |
| `deviceType` | `string` | O | `"all"`: 마이크+스피커, `"input"`: 마이크만, `"output"`: 스피커만 |

`init` 메시지는 항상 **AudioBinary 모드**로 캡처를 시작한다.

### 캡처 중지

WebSocket 연결을 닫으면 캡처가 자동으로 중지된다.

```typescript
ws.close();
```

### 오디오 데이터 수신

캡처 시작 후 같은 WebSocket으로 오디오 데이터가 push된다.

#### AudioBinary 모드

Binary 프레임. 고정 642바이트.

```
Offset  크기    설명
──────  ──────  ──────────────────────────────
0       1 B     패킷 타입 (항상 0x01 = PCM16)
1       1 B     채널 (0x01 = Mic, 0x02 = Spk)
2~641   640 B   PCM 데이터
                16kHz, mono, 16bit signed little-endian
                320 samples = 20ms
```
