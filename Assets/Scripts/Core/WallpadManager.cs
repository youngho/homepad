using System;
using System.Collections.Generic;
using UnityEngine;

namespace Homepad.Core
{
    /// <summary>
    /// 스마트 월패드 중앙 통합 제어 및 상태 관리 싱글톤
    /// </summary>
    public class WallpadManager : MonoBehaviour
    {
        public static WallpadManager Instance { get; private set; }

        [Header("Components")]
        [SerializeField] private ArduinoConnector connector;

        [Header("Device States")]
        [SerializeField] private List<LightState> lights = new List<LightState>();
        [SerializeField] private List<HeatingState> heatingRooms = new List<HeatingState>();
        [SerializeField] private GasState gas = new GasState(false);
        [SerializeField] private VentilationState ventilation = new VentilationState();
        [SerializeField] private ElevatorState elevator = new ElevatorState();
        [SerializeField] private bool isAwayMode = false; // 외출 모드

        // State change events
        public event Action OnStateChanged;
        public event Action<LightState> OnLightChanged;
        public event Action<HeatingState> OnHeatingChanged;
        public event Action<GasState> OnGasChanged;
        public event Action<VentilationState> OnVentilationChanged;
        public event Action<ElevatorState> OnElevatorChanged;
        public event Action<bool> OnAwayModeChanged;

        public ArduinoConnector Connector => connector;
        public IReadOnlyList<LightState> Lights => lights;
        public IReadOnlyList<HeatingState> HeatingRooms => heatingRooms;
        public GasState Gas => gas;
        public VentilationState Ventilation => ventilation;
        public ElevatorState Elevator => elevator;
        public bool IsAwayMode => isAwayMode;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            if (connector == null)
            {
                connector = GetComponent<ArduinoConnector>();
                if (connector == null) connector = gameObject.AddComponent<ArduinoConnector>();
            }

            InitializeDefaultDevices();
        }

        private void InitializeDefaultDevices()
        {
            // 기본 조명 목록 (6개)
            lights.Clear();
            lights.Add(new LightState(1, "거실 조명 1", false));
            lights.Add(new LightState(2, "거실 조명 2", false));
            lights.Add(new LightState(3, "안방 조명", false));
            lights.Add(new LightState(4, "주방 조명", false));
            lights.Add(new LightState(5, "침실 1 조명", false));
            lights.Add(new LightState(6, "침실 2 조명", false));

            // 기본 난방 방 목록 (4개)
            heatingRooms.Clear();
            heatingRooms.Add(new HeatingState(1, "거실", 22.5f, 24.0f));
            heatingRooms.Add(new HeatingState(2, "안방", 23.0f, 24.5f));
            heatingRooms.Add(new HeatingState(3, "침실 1", 21.0f, 23.0f));
            heatingRooms.Add(new HeatingState(4, "침실 2", 21.5f, 23.0f));
        }

        #region 조명 제어
        public void ToggleLight(int id)
        {
            var light = lights.Find(l => l.id == id);
            if (light != null)
            {
                SetLight(id, !light.isOn);
            }
        }

        public void SetLight(int id, bool turnOn)
        {
            var light = lights.Find(l => l.id == id);
            if (light != null)
            {
                light.isOn = turnOn;
                byte[] packet = KocomProtocol.CreateLightControlPacket(id, turnOn);
                connector?.SendPacket(packet);

                OnLightChanged?.Invoke(light);
                OnStateChanged?.Invoke();
            }
        }

        /// <summary>
        /// 일괄 소등 (모든 조명 끄기)
        /// </summary>
        public void TurnOffAllLights()
        {
            foreach (var light in lights)
            {
                if (light.isOn)
                {
                    light.isOn = false;
                    byte[] packet = KocomProtocol.CreateLightControlPacket(light.id, false);
                    connector?.SendPacket(packet);
                    OnLightChanged?.Invoke(light);
                }
            }
            OnStateChanged?.Invoke();
        }
        #endregion

        #region 난방 제어
        public void SetHeatingTargetTemp(int roomId, float temp)
        {
            var room = heatingRooms.Find(r => r.roomId == roomId);
            if (room != null)
            {
                room.targetTemp = Mathf.Clamp(temp, 16f, 30f);
                byte[] packet = KocomProtocol.CreateHeatingControlPacket(roomId, room.isPowered, room.isAwayMode, room.targetTemp);
                connector?.SendPacket(packet);

                OnHeatingChanged?.Invoke(room);
                OnStateChanged?.Invoke();
            }
        }

        public void ToggleHeatingPower(int roomId)
        {
            var room = heatingRooms.Find(r => r.roomId == roomId);
            if (room != null)
            {
                room.isPowered = !room.isPowered;
                byte[] packet = KocomProtocol.CreateHeatingControlPacket(roomId, room.isPowered, room.isAwayMode, room.targetTemp);
                connector?.SendPacket(packet);

                OnHeatingChanged?.Invoke(room);
                OnStateChanged?.Invoke();
            }
        }

        public void ToggleHeatingAway(int roomId)
        {
            var room = heatingRooms.Find(r => r.roomId == roomId);
            if (room != null)
            {
                room.isAwayMode = !room.isAwayMode;
                byte[] packet = KocomProtocol.CreateHeatingControlPacket(roomId, room.isPowered, room.isAwayMode, room.targetTemp);
                connector?.SendPacket(packet);

                OnHeatingChanged?.Invoke(room);
                OnStateChanged?.Invoke();
            }
        }
        #endregion

        #region 가스 밸브 제어
        public void CloseGasValve()
        {
            gas.isOpen = false;
            byte[] packet = KocomProtocol.CreateGasClosePacket();
            connector?.SendPacket(packet);

            OnGasChanged?.Invoke(gas);
            OnStateChanged?.Invoke();
        }
        #endregion

        #region 환기 시스템 제어
        public void SetVentilationSpeed(VentilationSpeed speed)
        {
            ventilation.speed = speed;
            ventilation.isPowered = speed != VentilationSpeed.Off;

            byte[] packet = KocomProtocol.CreateVentilationPacket(speed);
            connector?.SendPacket(packet);

            OnVentilationChanged?.Invoke(ventilation);
            OnStateChanged?.Invoke();
        }
        #endregion

        #region 엘리베이터 호출
        public void CallElevator(int floor = 1)
        {
            elevator.isCalled = true;
            byte[] packet = KocomProtocol.CreateElevatorCallPacket(floor);
            connector?.SendPacket(packet);

            OnElevatorChanged?.Invoke(elevator);
            OnStateChanged?.Invoke();
        }

        public void ResetElevatorCall()
        {
            elevator.isCalled = false;
            OnElevatorChanged?.Invoke(elevator);
            OnStateChanged?.Invoke();
        }
        #endregion

        #region 외출 모드
        public void ToggleAwayMode()
        {
            SetAwayMode(!isAwayMode);
        }

        public void SetAwayMode(bool enable)
        {
            isAwayMode = enable;
            if (enable)
            {
                // 일괄 소등
                TurnOffAllLights();
                // 가스 잠금
                CloseGasValve();
                // 모든 방 외출 난방 설정
                foreach (var room in heatingRooms)
                {
                    room.isAwayMode = true;
                    byte[] packet = KocomProtocol.CreateHeatingControlPacket(room.roomId, room.isPowered, true, room.targetTemp);
                    connector?.SendPacket(packet);
                    OnHeatingChanged?.Invoke(room);
                }
                // 환기 Off
                SetVentilationSpeed(VentilationSpeed.Off);
            }
            else
            {
                foreach (var room in heatingRooms)
                {
                    room.isAwayMode = false;
                    byte[] packet = KocomProtocol.CreateHeatingControlPacket(room.roomId, room.isPowered, false, room.targetTemp);
                    connector?.SendPacket(packet);
                    OnHeatingChanged?.Invoke(room);
                }
            }

            OnAwayModeChanged?.Invoke(isAwayMode);
            OnStateChanged?.Invoke();
        }
        #endregion
    }
}
