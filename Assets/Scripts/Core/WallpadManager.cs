using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Homepad.Core
{
    [DefaultExecutionOrder(-100)]
    public class WallpadManager : MonoBehaviour
    {
        public static WallpadManager Instance { get; private set; }

        [Header("Components")]
        [SerializeField] private WallpadConfig config;
        [SerializeField] private ArduinoConnector connector;

        [Header("Device States")]
        [SerializeField] private List<LightState> lights = new List<LightState>();
        [SerializeField] private List<HeatingState> heatingRooms = new List<HeatingState>();
        [SerializeField] private GasState gas = new GasState(false);
        [SerializeField] private VentilationState ventilation = new VentilationState();
        [SerializeField] private ElevatorState elevator = new ElevatorState();
        [SerializeField] private DoorLockState doorLock = new DoorLockState();
        [SerializeField] private bool isAwayMode;

        public event Action OnStateChanged;
        public event Action<LightState> OnLightChanged;
        public event Action<HeatingState> OnHeatingChanged;
        public event Action<GasState> OnGasChanged;
        public event Action<VentilationState> OnVentilationChanged;
        public event Action<ElevatorState> OnElevatorChanged;
        public event Action<DoorLockState> OnDoorLockChanged;
        public event Action<bool> OnAwayModeChanged;

        public WallpadConfig Config => config;
        public ArduinoConnector Connector => connector;
        public IReadOnlyList<LightState> Lights => lights;
        public IReadOnlyList<HeatingState> HeatingRooms => heatingRooms;
        public GasState Gas => gas;
        public VentilationState Ventilation => ventilation;
        public ElevatorState Elevator => elevator;
        public DoorLockState DoorLock => doorLock;
        public bool IsAwayMode => isAwayMode;
        public int HouseholdFloor => config != null ? config.householdFloor : 12;

        private Coroutine elevatorRoutine;
        private Coroutine doorLockRoutine;
        private Coroutine pollRoutine;
        private Coroutine requestBurstRoutine;
        private readonly Queue<byte[]> requestQueue = new Queue<byte[]>();
        private readonly Dictionary<ushort, byte[]> lightBitmapByRoom = new Dictionary<ushort, byte[]>();
        private readonly Dictionary<ushort, HeatingState> heatingByRoom = new Dictionary<ushort, HeatingState>();
        private ushort pendingQueryRoom;
        private ushort pendingQueryDevice;
        private ushort pendingAckDevice;
        private ushort pendingAckRoom;
        private bool pendingAckSatisfied;

        private static readonly ushort[] DefaultRooms =
        {
            0x0001, 0x0101, 0x0201, 0x0301
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // 이전 씬에서 남아 있는 매니저면 버리고, 지금 씬의 월패드를 쓴다.
                Destroy(Instance.gameObject);
            }

            Instance = this;
            UnityMainThreadDispatcher.EnsureExists();

            if (config == null)
            {
                config = WallpadConfig.CreateRuntimeDefault();
            }

            EnsureConnector();
            InitializeFromConfig();
            if (connector.IsConnected)
            {
                HandleConnectionChanged(true);
            }
        }

        public ArduinoConnector EnsureConnector()
        {
            var live = ArduinoConnector.FindPreferred();
            if (live != null)
            {
                connector = live;
            }

            if (!connector)
            {
                connector = GetComponent<ArduinoConnector>();
            }

            if (!connector)
            {
                connector = gameObject.AddComponent<ArduinoConnector>();
            }

            connector.OnPacketReceived -= HandlePacketReceived;
            connector.OnConnectionStatusChanged -= HandleConnectionChanged;
            connector.OnPacketReceived += HandlePacketReceived;
            connector.OnConnectionStatusChanged += HandleConnectionChanged;
            return connector;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (connector != null)
            {
                connector.OnPacketReceived -= HandlePacketReceived;
                connector.OnConnectionStatusChanged -= HandleConnectionChanged;
            }
        }

        private void InitializeFromConfig()
        {
            lights.Clear();
            foreach (var definition in config.lights)
            {
                var light = new LightState(definition.id, definition.name, false, definition.roomCode, definition.slot);
                ApplyCachedLight(light);
                lights.Add(light);
            }

            heatingRooms.Clear();
            foreach (var definition in config.heatingRooms)
            {
                heatingRooms.Add(new HeatingState(
                    definition.roomId,
                    definition.roomName,
                    definition.currentTemp,
                    definition.targetTemp,
                    definition.roomCode));
            }

            elevator.currentFloor = 1;
        }

        public void ToggleLight(int id)
        {
            var light = lights.Find(item => item.id == id);
            if (light != null)
            {
                SetLight(id, !light.isOn);
            }
        }

        public void SetLight(int id, bool turnOn)
        {
            var light = lights.Find(item => item.id == id);
            if (light == null) return;

            light.isOn = turnOn;
            SendLightRoom(light.roomCode);
            OnLightChanged?.Invoke(light);
            RaiseStateChanged();
        }

        public void TurnOffAllLights()
        {
            var rooms = new HashSet<ushort>();
            foreach (var light in lights)
            {
                if (!light.isOn) continue;
                light.isOn = false;
                rooms.Add(light.roomCode);
                OnLightChanged?.Invoke(light);
            }

            foreach (ushort room in rooms)
            {
                SendLightRoom(room);
            }

            RaiseStateChanged();
        }

        public void TurnOffRoomLights(ushort roomCode)
        {
            bool any = false;
            foreach (var light in lights)
            {
                if (light.roomCode != roomCode || !light.isOn) continue;
                light.isOn = false;
                OnLightChanged?.Invoke(light);
                any = true;
            }

            if (!any) return;
            SendLightRoom(roomCode);
            RaiseStateChanged();
        }

        public void SetHeatingTargetTemp(int roomId, float temp)
        {
            var room = heatingRooms.Find(item => item.roomId == roomId);
            if (room == null) return;

            room.targetTemp = Mathf.Clamp(temp, 16f, 30f);
            SendHeating(room);
            OnHeatingChanged?.Invoke(room);
            RaiseStateChanged();
        }

        public void ToggleHeatingPower(int roomId)
        {
            var room = heatingRooms.Find(item => item.roomId == roomId);
            if (room == null) return;

            room.isPowered = !room.isPowered;
            if (room.isPowered) room.isAwayMode = false;
            SendHeating(room);
            OnHeatingChanged?.Invoke(room);
            RaiseStateChanged();
        }

        public void ToggleHeatingAway(int roomId)
        {
            var room = heatingRooms.Find(item => item.roomId == roomId);
            if (room == null) return;

            room.isAwayMode = !room.isAwayMode;
            if (room.isAwayMode) room.isPowered = false;
            SendHeating(room);
            OnHeatingChanged?.Invoke(room);
            RaiseStateChanged();
        }

        public void QueryDeviceStatus(ushort device, ushort room)
        {
            if (connector == null) return;
            if (!connector.IsConnected && !connector.UseSimulationMode) return;

            pendingQueryDevice = device;
            pendingQueryRoom = room;
            SendRequest(KocomProtocol.CreateStatusQueryPacket(device, room));
        }

        public void QueryHeating(int roomId)
        {
            var room = heatingRooms.Find(item => item.roomId == roomId);
            if (room == null) return;
            QueryDeviceStatus(KocomProtocol.DeviceHeating, room.roomCode);
        }

        public void CloseGasValve()
        {
            gas.isOpen = false;
            SendRequest(KocomProtocol.CreateGasClosePacket());
            OnGasChanged?.Invoke(gas);
            RaiseStateChanged();
        }

        public void SetVentilationSpeed(VentilationSpeed speed)
        {
            bool fromOff = !ventilation.isPowered || ventilation.speed == VentilationSpeed.Off;
            ventilation.speed = speed;
            ventilation.isPowered = speed != VentilationSpeed.Off;
            SendRequest(KocomProtocol.CreateVentilationPacket(speed, fromOff));
            OnVentilationChanged?.Invoke(ventilation);
            RaiseStateChanged();
        }

        public void TurnVentilationOn()
        {
            ventilation.speed = VentilationSpeed.Low;
            ventilation.isPowered = true;
            SendRequest(KocomProtocol.CreateVentilationPacket(VentilationSpeed.Low, fromOff: true));
            OnVentilationChanged?.Invoke(ventilation);
            RaiseStateChanged();
        }

        public void CallElevator(int floor = -1)
        {
            if (floor < 1) floor = HouseholdFloor;
            elevator.isCalled = true;
            SendRequest(KocomProtocol.CreateElevatorCallPacket());
            OnElevatorChanged?.Invoke(elevator);
            RaiseStateChanged();

            if (connector != null && connector.UseSimulationMode)
            {
                if (elevatorRoutine != null) StopCoroutine(elevatorRoutine);
                elevatorRoutine = StartCoroutine(SimulateElevator(floor));
            }
        }

        public void ResetElevatorCall()
        {
            elevator.isCalled = false;
            elevator.direction = ElevatorDirection.Stop;
            OnElevatorChanged?.Invoke(elevator);
            RaiseStateChanged();
        }

        public void UnlockDoor()
        {
            if (doorLock.isOpen || doorLock.isUnlocking) return;

            doorLock.isUnlocking = true;
            SendRequest(KocomProtocol.CreateDoorUnlockPacket());
            OnDoorLockChanged?.Invoke(doorLock);
            RaiseStateChanged();

            if (connector != null && connector.UseSimulationMode)
            {
                if (doorLockRoutine != null) StopCoroutine(doorLockRoutine);
                doorLockRoutine = StartCoroutine(SimulateDoorUnlock());
            }
        }

        public void SetDoorOpen(bool open)
        {
            doorLock.isUnlocking = false;
            doorLock.isOpen = open;
            OnDoorLockChanged?.Invoke(doorLock);
            RaiseStateChanged();
        }

        public LightState AddLight(string name, ushort roomCode)
        {
            int slot = 0;
            int id = 1;
            for (int i = 0; i < lights.Count; i++)
            {
                if (lights[i].id >= id) id = lights[i].id + 1;
                if (lights[i].roomCode == roomCode && lights[i].slot >= slot) slot = lights[i].slot + 1;
            }

            var light = new LightState(id, string.IsNullOrEmpty(name) ? "조명" : name, false, roomCode, slot);
            ApplyCachedLight(light);
            lights.Add(light);
            OnLightChanged?.Invoke(light);
            RaiseStateChanged();
            return light;
        }

        public HeatingState AddHeatingRoom(string roomName, ushort roomCode)
        {
            var existing = heatingRooms.Find(item => item.roomCode == roomCode);
            if (existing != null) return existing;

            int id = 1;
            for (int i = 0; i < heatingRooms.Count; i++)
            {
                if (heatingRooms[i].roomId >= id) id = heatingRooms[i].roomId + 1;
            }

            var room = new HeatingState(id, string.IsNullOrEmpty(roomName) ? "방" : roomName, 99f, 24f, roomCode);
            ApplyCachedHeating(room);
            heatingRooms.Add(room);
            OnHeatingChanged?.Invoke(room);
            RaiseStateChanged();
            return room;
        }

        public void ToggleAwayMode()
        {
            SetAwayMode(!isAwayMode);
        }

        public void SetAwayMode(bool enable)
        {
            isAwayMode = enable;
            if (enable)
            {
                TurnOffAllLights();
                CloseGasValve();
                foreach (var room in heatingRooms)
                {
                    room.isAwayMode = true;
                    SendHeating(room);
                    OnHeatingChanged?.Invoke(room);
                }
                SetVentilationSpeed(VentilationSpeed.Off);
            }
            else
            {
                foreach (var room in heatingRooms)
                {
                    room.isAwayMode = false;
                    SendHeating(room);
                    OnHeatingChanged?.Invoke(room);
                }
            }

            OnAwayModeChanged?.Invoke(isAwayMode);
            RaiseStateChanged();
        }

        private void HandleConnectionChanged(bool connected)
        {
            if (pollRoutine != null)
            {
                StopCoroutine(pollRoutine);
                pollRoutine = null;
            }

            pendingQueryRoom = 0;
            pendingQueryDevice = 0;
            if (!connected) return;
            if (connector != null && connector.UseSimulationMode) return;
            pollRoutine = StartCoroutine(PollDeviceStates());
        }

        private IEnumerator PollDeviceStates()
        {
            yield return new WaitForSeconds(0.8f);
            if (connector == null || !connector.IsConnected || connector.UseSimulationMode)
            {
                pollRoutine = null;
                yield break;
            }

            connector.NotifyLog("[시스템] 방 상태를 조회해 메모리에 맞춥니다.", false);

            var rooms = CollectRoomsToQuery();
            for (int i = 0; i < rooms.Count; i++)
            {
                yield return QueryDevice(KocomProtocol.DeviceLight, rooms[i]);
                yield return QueryDevice(KocomProtocol.DeviceHeating, rooms[i]);
            }

            connector.NotifyLog("[시스템] 방 상태 조회를 마쳤습니다.", false);
            pollRoutine = null;
        }

        private List<ushort> CollectRoomsToQuery()
        {
            var seen = new HashSet<ushort>();
            var rooms = new List<ushort>();
            for (int i = 0; i < lights.Count; i++)
            {
                AddRoom(seen, rooms, lights[i].roomCode);
            }

            for (int i = 0; i < heatingRooms.Count; i++)
            {
                AddRoom(seen, rooms, heatingRooms[i].roomCode);
            }

            if (rooms.Count == 0)
            {
                for (int i = 0; i < DefaultRooms.Length; i++)
                {
                    rooms.Add(DefaultRooms[i]);
                }
            }

            return rooms;
        }

        private static void AddRoom(HashSet<ushort> seen, List<ushort> rooms, ushort room)
        {
            if (!seen.Add(room)) return;
            rooms.Add(room);
        }

        private IEnumerator QueryDevice(ushort device, ushort room)
        {
            if (connector == null || !connector.IsConnected) yield break;

            pendingQueryDevice = device;
            pendingQueryRoom = room;
            SendRequest(KocomProtocol.CreateStatusQueryPacket(device, room));

            float elapsed = 0f;
            const float timeout = 1.0f;
            while (elapsed < timeout && pendingQueryRoom == room && pendingQueryDevice == device)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.15f);
        }

        private void HandlePacketReceived(byte[] raw)
        {
            if (!KocomProtocol.TryParse(raw, out var frame)) return;
            if (frame.type == KocomProtocol.TypeReport)
            {
                NoteRequestAcked(frame.DeviceAddress, KocomProtocol.ResolveRoom(frame));
            }

            ApplyFrame(frame);
        }

        private void SendRequest(byte[] packet)
        {
            if (packet == null || packet.Length == 0) return;
            if (connector == null) return;

            if (!KocomProtocol.IsTransmitPacket(packet))
            {
                connector.SendPacket(packet);
                return;
            }

            requestQueue.Enqueue(packet);
            if (requestBurstRoutine == null)
            {
                requestBurstRoutine = StartCoroutine(ProcessRequestQueue());
            }
        }

        private IEnumerator ProcessRequestQueue()
        {
            while (requestQueue.Count > 0)
            {
                yield return SendBcBdBe(requestQueue.Dequeue());
                if (requestQueue.Count > 0)
                {
                    yield return new WaitForSecondsRealtime(0.05f);
                }
            }

            requestBurstRoutine = null;
        }

        private IEnumerator SendBcBdBe(byte[] bc)
        {
            if (connector == null || !KocomProtocol.TryParse(bc, out var frame))
            {
                connector?.SendPacket(bc);
                yield break;
            }

            // 엘리베이터는 응답을 기다리지 못하고 BC/BD/BE를 연속으로 뿌린다.
            bool waitForReport = frame.DeviceAddress != KocomProtocol.DeviceElevator;
            pendingAckDevice = frame.DeviceAddress;
            pendingAckRoom = KocomProtocol.ResolveRoom(frame);
            pendingAckSatisfied = false;

            connector.SendPacket(bc);

            float waitBd = waitForReport
                ? KocomProtocol.RetransmitWaitBdMs / 1000f
                : KocomProtocol.RetransmitGapBeMs / 1000f;
            float waited = 0f;
            while (waited < waitBd)
            {
                if (waitForReport && pendingAckSatisfied) yield break;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (waitForReport && pendingAckSatisfied) yield break;

            connector.SendPacket(KocomProtocol.CloneWithType(bc, KocomProtocol.TypeRetransmit1));
            yield return new WaitForSecondsRealtime(KocomProtocol.RetransmitGapBeMs / 1000f);

            if (waitForReport && pendingAckSatisfied) yield break;

            connector.SendPacket(KocomProtocol.CloneWithType(bc, KocomProtocol.TypeRetransmit2));
        }

        private void NoteRequestAcked(ushort device, ushort room)
        {
            if (pendingAckDevice == device && pendingAckRoom == room)
            {
                pendingAckSatisfied = true;
            }
        }

        private void ApplyFrame(KocomProtocol.Frame frame)
        {
            if (frame.DeviceAddress == KocomProtocol.DeviceDoorLock)
            {
                ApplyDoorLockFrame(frame);
                return;
            }

            if (!KocomProtocol.ShouldApplyState(frame)) return;

            ushort device = frame.DeviceAddress;
            ushort room = KocomProtocol.ResolveRoom(frame);
            if (pendingQueryDevice == device && pendingQueryRoom == room)
            {
                pendingQueryRoom = 0;
                pendingQueryDevice = 0;
            }

            switch (device)
            {
                case KocomProtocol.DeviceLight:
                    ApplyLightFrame(frame);
                    break;
                case KocomProtocol.DeviceHeating:
                    ApplyHeatingFrame(frame);
                    break;
                case KocomProtocol.DeviceGas:
                    if (frame.command == KocomProtocol.CmdOn)
                    {
                        gas.isOpen = true;
                    }
                    else if (frame.command == KocomProtocol.CmdOff)
                    {
                        gas.isOpen = false;
                    }
                    else
                    {
                        gas.isOpen = frame.value != null && frame.value.Length > 0 && frame.value[0] != 0x00;
                    }
                    OnGasChanged?.Invoke(gas);
                    RaiseStateChanged();
                    break;
                case KocomProtocol.DeviceVentilation:
                    if (KocomProtocol.TryParseVentilation(frame, out bool ventOn, out var speed))
                    {
                        ventilation.speed = speed;
                        ventilation.isPowered = ventOn && speed != VentilationSpeed.Off;
                        OnVentilationChanged?.Invoke(ventilation);
                        RaiseStateChanged();
                    }
                    break;
                case KocomProtocol.DeviceElevator:
                    ApplyElevatorFrame(frame);
                    break;
            }
        }

        private void ApplyLightFrame(KocomProtocol.Frame frame)
        {
            if (frame.value == null || frame.value.Length == 0) return;

            ushort room = KocomProtocol.ResolveRoom(frame);
            var bitmap = new byte[8];
            int copy = Math.Min(8, frame.value.Length);
            Array.Copy(frame.value, bitmap, copy);
            lightBitmapByRoom[room] = bitmap;

            foreach (var light in lights)
            {
                if (light.roomCode != room) continue;
                if (light.slot < 0 || light.slot >= bitmap.Length) continue;
                bool isOn = bitmap[light.slot] != KocomProtocol.LightOff;
                if (light.isOn == isOn) continue;
                light.isOn = isOn;
                OnLightChanged?.Invoke(light);
            }

            RaiseStateChanged();
        }

        private void ApplyCachedLight(LightState light)
        {
            if (light == null) return;
            if (!lightBitmapByRoom.TryGetValue(light.roomCode, out var bitmap)) return;
            if (light.slot < 0 || light.slot >= bitmap.Length) return;
            light.isOn = bitmap[light.slot] != KocomProtocol.LightOff;
        }

        private void ApplyHeatingFrame(KocomProtocol.Frame frame)
        {
            ushort roomCode = KocomProtocol.ResolveRoom(frame);
            if (!heatingByRoom.TryGetValue(roomCode, out var cached))
            {
                cached = new HeatingState(0, string.Empty, 99f, 24f, roomCode);
                heatingByRoom[roomCode] = cached;
            }

            CopyHeatingFromFrame(frame, cached);

            var room = heatingRooms.Find(item => item.roomCode == roomCode);
            if (room == null) return;

            CopyHeating(cached, room);
            OnHeatingChanged?.Invoke(room);
            RaiseStateChanged();
        }

        private static void CopyHeatingFromFrame(KocomProtocol.Frame frame, HeatingState room)
        {
            if (frame.value == null || frame.value.Length < 3 || room == null) return;

            byte mode0 = frame.value[0];
            byte mode1 = frame.value[1];
            if (mode0 == KocomProtocol.HeatPowerOff0 && mode1 == KocomProtocol.HeatPowerOff1)
            {
                room.isPowered = false;
                room.isAwayMode = false;
            }
            else if (mode0 == KocomProtocol.HeatAway0 && mode1 == KocomProtocol.HeatAway1)
            {
                room.isPowered = false;
                room.isAwayMode = true;
            }
            else if (mode0 == KocomProtocol.HeatPowerOn0)
            {
                room.isPowered = true;
                room.isAwayMode = false;
            }

            if (frame.value[2] >= 5)
            {
                room.targetTemp = frame.value[2];
            }

            if (frame.value.Length > 4 && frame.value[4] >= 5)
            {
                room.currentTemp = frame.value[4];
            }
            else if (frame.value.Length > 3 && frame.value[3] >= 5)
            {
                room.currentTemp = frame.value[3];
            }
        }

        private static void CopyHeating(HeatingState from, HeatingState to)
        {
            if (from == null || to == null) return;
            to.isPowered = from.isPowered;
            to.isAwayMode = from.isAwayMode;
            to.currentTemp = from.currentTemp;
            to.targetTemp = from.targetTemp;
        }

        private void ApplyCachedHeating(HeatingState room)
        {
            if (room == null) return;
            if (!heatingByRoom.TryGetValue(room.roomCode, out var cached)) return;
            CopyHeating(cached, room);
        }

        private void ApplyElevatorFrame(KocomProtocol.Frame frame)
        {
            byte marker = frame.value[0] != 0 ? frame.value[0] : frame.value[2];
            if (marker >= 1 && marker <= 60 && marker != 0x03)
            {
                elevator.currentFloor = marker;
            }

            if (marker == 0x03)
            {
                elevator.isCalled = false;
                elevator.direction = ElevatorDirection.Stop;
                elevator.currentFloor = HouseholdFloor;
            }

            OnElevatorChanged?.Invoke(elevator);
            RaiseStateChanged();
        }

        private void ApplyDoorLockFrame(KocomProtocol.Frame frame)
        {
            if (frame.destDevice != KocomProtocol.DeviceByteDoorLock) return;

            SetDoorOpen(true);
            connector?.SendPacket(KocomProtocol.CreateDoorLockAckPacket());
            if (doorLockRoutine != null) StopCoroutine(doorLockRoutine);
            doorLockRoutine = StartCoroutine(AutoCloseDoor());
        }

        private void SendLightRoom(ushort room)
        {
            var roomLights = lights.FindAll(item => item.roomCode == room);
            SendRequest(KocomProtocol.CreateLightRoomPacket(room, roomLights));
        }

        private void SendHeating(HeatingState room)
        {
            SendRequest(KocomProtocol.CreateHeatingControlPacket(room.roomCode, room.isPowered, room.isAwayMode, room.targetTemp));
        }

        public string FormatMemoryDump()
        {
            var sb = new StringBuilder(512);
            sb.Append("외출  ").AppendLine(isAwayMode ? "외출" : "재실");

            sb.AppendLine("조명");
            if (lights.Count == 0 && lightBitmapByRoom.Count == 0)
            {
                sb.AppendLine("  없음");
            }
            else
            {
                for (int i = 0; i < lights.Count; i++)
                {
                    var light = lights[i];
                    sb.Append("  ")
                        .Append(string.IsNullOrEmpty(light.name) ? DescribeRoom(light.roomCode) : light.name)
                        .Append("  ")
                        .AppendLine(light.isOn ? "ON" : "OFF");
                }

                if (lightBitmapByRoom.Count > 0)
                {
                    sb.AppendLine("조명 비트맵");
                    var rooms = new List<ushort>(lightBitmapByRoom.Keys);
                    rooms.Sort();
                    for (int r = 0; r < rooms.Count; r++)
                    {
                        ushort roomCode = rooms[r];
                        sb.Append("  ").Append(DescribeRoom(roomCode)).Append("  ");
                        var bitmap = lightBitmapByRoom[roomCode];
                        for (int i = 0; i < bitmap.Length; i++)
                        {
                            if (i > 0) sb.Append(' ');
                            sb.Append(bitmap[i].ToString("X2"));
                        }

                        sb.AppendLine();
                    }
                }
            }

            sb.AppendLine("난방");
            if (heatingRooms.Count == 0)
            {
                sb.AppendLine("  없음");
            }
            else
            {
                for (int i = 0; i < heatingRooms.Count; i++)
                {
                    var room = heatingRooms[i];
                    string mode = room.isAwayMode ? "외출" : (room.isPowered ? "가동" : "정지");
                    sb.Append("  ")
                        .Append(string.IsNullOrEmpty(room.roomName) ? DescribeRoom(room.roomCode) : room.roomName)
                        .Append("  ")
                        .Append(mode)
                        .Append("  설정 ")
                        .Append(Mathf.RoundToInt(room.targetTemp))
                        .Append("°  현재 ")
                        .Append(Mathf.RoundToInt(room.currentTemp))
                        .AppendLine("°");
                }
            }

            sb.Append("가스  ").AppendLine(gas.isOpen ? "열림" : "잠금");
            sb.Append("환기  ").AppendLine(DescribeVentilation(ventilation));
            sb.Append("엘리베이터  ")
                .Append(elevator.currentFloor)
                .Append("층 ")
                .Append(DescribeElevator(elevator.direction));
            if (elevator.isCalled) sb.Append("  호출");
            sb.AppendLine();
            sb.Append("도어락  ").AppendLine(doorLock.isOpen ? "열림" : (doorLock.isUnlocking ? "해제 중" : "잠김"));
            return sb.ToString();
        }

        private static string DescribeRoom(ushort roomCode)
        {
            return roomCode switch
            {
                0x0001 => "거실",
                0x0101 => "방1",
                0x0201 => "방2",
                0x0301 => "방3",
                _ => $"방(0x{roomCode:X4})"
            };
        }

        private static string DescribeVentilation(VentilationState state)
        {
            if (state == null || !state.isPowered || state.speed == VentilationSpeed.Off) return "정지";
            return state.speed switch
            {
                VentilationSpeed.Low => "약",
                VentilationSpeed.Medium => "중",
                VentilationSpeed.High => "강",
                _ => "정지"
            };
        }

        private static string DescribeElevator(ElevatorDirection direction)
        {
            return direction switch
            {
                ElevatorDirection.Up => "상행",
                ElevatorDirection.Down => "하행",
                _ => "정지"
            };
        }

        private void RaiseStateChanged()
        {
            OnStateChanged?.Invoke();
        }

        private IEnumerator SimulateElevator(int targetFloor)
        {
            elevator.direction = targetFloor >= elevator.currentFloor ? ElevatorDirection.Up : ElevatorDirection.Down;
            OnElevatorChanged?.Invoke(elevator);

            while (elevator.currentFloor != targetFloor)
            {
                yield return new WaitForSeconds(0.55f);
                elevator.currentFloor += elevator.direction == ElevatorDirection.Up ? 1 : -1;
                OnElevatorChanged?.Invoke(elevator);
            }

            elevator.direction = ElevatorDirection.Stop;
            elevator.isCalled = false;
            OnElevatorChanged?.Invoke(elevator);
            RaiseStateChanged();
            elevatorRoutine = null;
        }

        private IEnumerator SimulateDoorUnlock()
        {
            yield return new WaitForSeconds(0.4f);
            SetDoorOpen(true);
            yield return AutoCloseDoor();
        }

        private IEnumerator AutoCloseDoor()
        {
            yield return new WaitForSeconds(4.5f);
            SetDoorOpen(false);
            doorLockRoutine = null;
        }
    }
}
