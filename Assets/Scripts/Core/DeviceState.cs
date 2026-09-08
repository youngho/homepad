using System;

namespace Homepad.Core
{
    [Serializable]
    public class LightState
    {
        public int id;
        public string name;
        public bool isOn;
        public byte roomCode;
        public int slot;

        public LightState(int id, string name, bool isOn = false, byte roomCode = 0, int slot = 0)
        {
            this.id = id;
            this.name = name;
            this.isOn = isOn;
            this.roomCode = roomCode;
            this.slot = slot;
        }
    }

    [Serializable]
    public class HeatingState
    {
        public int roomId;
        public string roomName;
        public bool isPowered;
        public bool isAwayMode;
        public float currentTemp;
        public float targetTemp;
        public byte roomCode;

        public HeatingState(int roomId, string roomName, float currentTemp = 99f, float targetTemp = 24f, byte roomCode = 0)
        {
            this.roomId = roomId;
            this.roomName = roomName;
            this.isPowered = true;
            this.isAwayMode = false;
            this.currentTemp = currentTemp;
            this.targetTemp = targetTemp;
            this.roomCode = roomCode;
        }
    }

    [Serializable]
    public class GasState
    {
        public bool isOpen;

        public GasState(bool isOpen = false)
        {
            this.isOpen = isOpen;
        }
    }

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

    [Serializable]
    public class DoorLockState
    {
        public bool isUnlocking;
        public bool isOpen;

        public DoorLockState()
        {
            isUnlocking = false;
            isOpen = false;
        }
    }
}
