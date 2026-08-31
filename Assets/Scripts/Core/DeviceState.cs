using System;
using System.Collections.Generic;

namespace Homepad.Core
{
    /// <summary>
    /// 개별 조명 상태 데이터 모델
    /// </summary>
    [Serializable]
    public class LightState
    {
        public int id;
        public string name;
        public bool isOn;

        public LightState(int id, string name, bool isOn = false)
        {
            this.id = id;
            this.name = name;
            this.isOn = isOn;
        }
    }

    /// <summary>
    /// 개별 방 난방 상태 데이터 모델
    /// </summary>
    [Serializable]
    public class HeatingState
    {
        public int roomId;
        public string roomName;
        public bool isPowered;
        public bool isAwayMode; // 외출 모드
        public float currentTemp;
        public float targetTemp;

        public HeatingState(int roomId, string roomName, float currentTemp = 22f, float targetTemp = 24f)
        {
            this.roomId = roomId;
            this.roomName = roomName;
            this.isPowered = true;
            this.isAwayMode = false;
            this.currentTemp = currentTemp;
            this.targetTemp = targetTemp;
        }
    }

    /// <summary>
    /// 가스 밸브 상태
    /// </summary>
    [Serializable]
    public class GasState
    {
        public bool isOpen;

        public GasState(bool isOpen = false)
        {
            this.isOpen = isOpen;
        }
    }

    /// <summary>
    /// 환기 시스템 상태 (0: Off, 1: 미풍, 2: 약풍, 3: 강풍)
    /// </summary>
    public enum VentilationSpeed
    {
        Off = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    [Serializable]
    public class VentilationState
    {
        public bool isPowered;
        public VentilationSpeed speed;

        public VentilationState()
        {
            isPowered = false;
            speed = VentilationSpeed.Off;
        }
    }

    /// <summary>
    /// 엘리베이터 상태
    /// </summary>
    public enum ElevatorDirection
    {
        Stop,
        Up,
        Down
    }

    [Serializable]
    public class ElevatorState
    {
        public bool isCalled;
        public int currentFloor;
        public ElevatorDirection direction;

        public ElevatorState()
        {
            isCalled = false;
            currentFloor = 1;
            direction = ElevatorDirection.Stop;
        }
    }
}
