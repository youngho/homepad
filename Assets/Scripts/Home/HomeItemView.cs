using UnityEngine;

namespace Homepad.Home
{
    public class HomeItemView : MonoBehaviour
    {
        public string InstanceId;
        public Transform Visual;
        public Transform CurtainLeaf;

        public PlacedItem Item { get; set; }

        public void Bind(PlacedItem item, Transform visual, Transform curtainLeaf = null)
        {
            Item = item;
            InstanceId = item.InstanceId;
            Visual = visual;
            CurtainLeaf = curtainLeaf;
        }
    }
}
