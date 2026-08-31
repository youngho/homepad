using System;
using System.Collections.Generic;
using UnityEngine;

namespace Homepad.Core
{
    [CreateAssetMenu(menuName = "Homepad/Wallpad Config", fileName = "WallpadConfig")]
    public class WallpadConfig : ScriptableObject
    {
        [Header("Household")]
        public string householdName = "우리집";
        public int householdFloor = 12;

        [Header("Arduino")]
        public string arduinoIp = "192.168.0.100";
        public int arduinoPort = 8080;

        [Header("Devices")]
        public List<LightDefinition> lights = new List<LightDefinition>();
        public List<HeatingRoomDefinition> heatingRooms = new List<HeatingRoomDefinition>();

        [Serializable]
        public class LightDefinition
        {
            public int id = 1;
            public string name = "조명";
            public ushort roomCode = 0x0001;
            public int slot;
        }

        [Serializable]
        public class HeatingRoomDefinition
        {
            public int roomId = 1;
            public string roomName = "거실";
            public ushort roomCode = 0x0001;
            public float currentTemp = 22f;
            public float targetTemp = 24f;
        }

        public static WallpadConfig CreateRuntimeDefault()
        {
            var config = CreateInstance<WallpadConfig>();
            config.householdName = "우리집";
            config.householdFloor = 12;
            config.lights = new List<LightDefinition>
            {
                new LightDefinition { id = 1, name = "거실 조명 1", roomCode = 0x0001, slot = 0 },
                new LightDefinition { id = 2, name = "거실 조명 2", roomCode = 0x0001, slot = 1 },
                new LightDefinition { id = 3, name = "안방 조명", roomCode = 0x0101, slot = 0 },
                new LightDefinition { id = 4, name = "주방 조명", roomCode = 0x0401, slot = 0 },
                new LightDefinition { id = 5, name = "침실 1 조명", roomCode = 0x0201, slot = 0 },
                new LightDefinition { id = 6, name = "침실 2 조명", roomCode = 0x0301, slot = 0 }
            };
            config.heatingRooms = new List<HeatingRoomDefinition>
            {
                new HeatingRoomDefinition { roomId = 1, roomName = "거실", roomCode = 0x0001, currentTemp = 22.5f, targetTemp = 24f },
                new HeatingRoomDefinition { roomId = 2, roomName = "안방", roomCode = 0x0101, currentTemp = 23f, targetTemp = 24.5f },
                new HeatingRoomDefinition { roomId = 3, roomName = "침실 1", roomCode = 0x0201, currentTemp = 21f, targetTemp = 23f },
                new HeatingRoomDefinition { roomId = 4, roomName = "침실 2", roomCode = 0x0301, currentTemp = 21.5f, targetTemp = 23f }
            };
            return config;
        }
    }
}
