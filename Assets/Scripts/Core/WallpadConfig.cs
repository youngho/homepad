using System;
using System.Collections.Generic;
using UnityEngine;

namespace Homepad.Core
{
    [CreateAssetMenu(menuName = "Homepad/Wallpad Config", fileName = "WallpadConfig")]
    public class WallpadConfig : ScriptableObject
    {
        [Header("Household")]
        public string householdName = "세종시 첫마을 503동 2801호";
        public int householdFloor = 28;

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
            config.householdName = "세종시 첫마을 503동 2801호";
            config.householdFloor = 28;
            config.lights = new List<LightDefinition>();
            config.heatingRooms = new List<HeatingRoomDefinition>();
            return config;
        }
    }
}
