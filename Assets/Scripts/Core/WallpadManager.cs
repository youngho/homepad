using System;
using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private bool isAwayMode;

        public event Action OnStateChanged;
        public event Action<LightState> OnLightChanged;
        public event Action<HeatingState> OnHeatingChanged;
        public event Action<GasState> OnGasChanged;
        public event Action<VentilationState> OnVentilationChanged;
        public event Action<ElevatorState> OnElevatorChanged;
        public event Action<bool> OnAwayModeChanged;

        public WallpadConfig Config => config;
        public ArduinoConnector Connector => connector;
        public IReadOnlyList<LightState> Lights => lights;
        public IReadOnlyList<HeatingState> HeatingRooms => heatingRooms;
        public GasState Gas => gas;
        public VentilationState Ventilation => ventilation;
        public ElevatorState Elevator => elevator;
        public bool IsAwayMode => isAwayMode;
        public int HouseholdFloor => config != null ? config.householdFloor : 12;

        private Coroutine elevatorRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            UnityMainThreadDispatcher.EnsureExists();

            if (config == null)
            {
                config = WallpadConfig.CreateRuntimeDefault();
            }

            if (connector == null)
            {
                connector = GetComponent<ArduinoConnector>();
                if (connector == null) connector = gameObject.AddComponent<ArduinoConnector>();
            }

            InitializeFromConfig();
            connector.OnPacketReceived += HandlePacketReceived;
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
            }
        }

        private void InitializeFromConfig()
        {
            lights.Clear();
            foreach (var definition in config.lights)
            {
                lights.Add(new LightState(definition.id, definition.name, false, definition.roomCode, definition.slot));
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
            SendHeating(room);
            OnHeatingChanged?.Invoke(room);
            RaiseStateChanged();
        }

        public void ToggleHeatingAway(int roomId)
        {
            var room = heatingRooms.Find(item => item.roomId == roomId);
            if (room == null) return;

            room.isAwayMode = !room.isAwayMode;
            SendHeating(room);
            OnHeatingChanged?.Invoke(room);
            RaiseStateChanged();
        }

        public void CloseGasValve()
        {
            gas.isOpen = false;
            connector?.SendPacket(KocomProtocol.CreateGasClosePacket());
            OnGasChanged?.Invoke(gas);
            RaiseStateChanged();
        }

        public void SetVentilationSpeed(VentilationSpeed speed)
        {
            ventilation.speed = speed;
            ventilation.isPowered = speed != VentilationSpeed.Off;
            connector?.SendPacket(KocomProtocol.CreateVentilationPacket(speed));
            OnVentilationChanged?.Invoke(ventilation);
            RaiseStateChanged();
        }

        public void CallElevator(int floor = -1)
        {
            if (floor < 1) floor = HouseholdFloor;
            elevator.isCalled = true;
            connector?.SendPacket(KocomProtocol.CreateElevatorCallPacket());
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

        private void HandlePacketReceived(byte[] raw)
        {
            if (!KocomProtocol.TryParse(raw, out var frame)) return;
            ApplyFrame(frame);
        }

        private void ApplyFrame(KocomProtocol.Frame frame)
        {
            ushort device = frame.DeviceAddress;
            switch (device)
            {
                case KocomProtocol.DeviceLight:
                    ApplyLightFrame(frame);
                    break;
                case KocomProtocol.DeviceHeating:
                    ApplyHeatingFrame(frame);
                    break;
                case KocomProtocol.DeviceGas:
                    gas.isOpen = frame.value != null && frame.value.Length > 0 && frame.value[0] != 0x00;
                    OnGasChanged?.Invoke(gas);
                    RaiseStateChanged();
                    break;
                case KocomProtocol.DeviceVentilation:
                    var speed = (VentilationSpeed)Mathf.Clamp(frame.value[0], 0, 3);
                    ventilation.speed = speed;
                    ventilation.isPowered = speed != VentilationSpeed.Off;
                    OnVentilationChanged?.Invoke(ventilation);
                    RaiseStateChanged();
                    break;
                case KocomProtocol.DeviceElevator:
                    ApplyElevatorFrame(frame);
                    break;
            }
        }

        private void ApplyLightFrame(KocomProtocol.Frame frame)
        {
            bool changed = false;
            foreach (var light in lights)
            {
                if (light.roomCode != frame.room) continue;
                if (light.slot < 0 || light.slot >= frame.value.Length) continue;
                bool isOn = frame.value[light.slot] == KocomProtocol.LightOn;
                if (light.isOn == isOn) continue;
                light.isOn = isOn;
                OnLightChanged?.Invoke(light);
                changed = true;
            }

            if (changed) RaiseStateChanged();
        }

        private void ApplyHeatingFrame(KocomProtocol.Frame frame)
        {
            var room = heatingRooms.Find(item => item.roomCode == frame.room);
            if (room == null) return;

            byte mode0 = frame.value[0];
            byte mode1 = frame.value[1];
            if (mode0 == KocomProtocol.HeatPowerOff0 && mode1 == KocomProtocol.HeatPowerOff1)
            {
                room.isPowered = false;
                room.isAwayMode = false;
            }
            else if (mode0 == KocomProtocol.HeatAway0 && mode1 == KocomProtocol.HeatAway1)
            {
                room.isPowered = true;
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

            if (frame.value[4] >= 5)
            {
                room.currentTemp = frame.value[4];
            }

            OnHeatingChanged?.Invoke(room);
            RaiseStateChanged();
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

        private void SendLightRoom(ushort room)
        {
            var roomLights = lights.FindAll(item => item.roomCode == room);
            connector?.SendPacket(KocomProtocol.CreateLightRoomPacket(room, roomLights));
        }

        private void SendHeating(HeatingState room)
        {
            connector?.SendPacket(KocomProtocol.CreateHeatingControlPacket(room.roomCode, room.isPowered, room.isAwayMode, room.targetTemp));
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
    }
}
