using UnityEngine;

namespace Homepad.Home
{
    public sealed class CurtainState
    {
        public string ItemInstanceId;
        public float OpenAmount;

        public CurtainState(string itemInstanceId, float openAmount = 0f)
        {
            ItemInstanceId = itemInstanceId;
            OpenAmount = Mathf.Clamp01(openAmount);
        }
    }
}
