using UnityEngine;

namespace Homepad.Home
{
    public class CurtainCloth : MonoBehaviour
    {
        [SerializeField] private float wave = 0.045f;
        [SerializeField] private float speed = 1.6f;

        private MeshFilter filter;
        private Mesh mesh;
        private Vector3[] rest;
        private Vector3[] live;
        private float open;

        public void SetOpen(float value)
        {
            open = Mathf.Clamp01(value);
        }

        private void Awake()
        {
            filter = GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            mesh = Instantiate(filter.sharedMesh);
            mesh.name = "CurtainClothRuntime";
            filter.sharedMesh = mesh;
            rest = mesh.vertices;
            live = new Vector3[rest.Length];
        }

        private void LateUpdate()
        {
            if (mesh == null || rest == null) return;
            float t = Time.time * speed;
            float gather = open;
            for (int i = 0; i < rest.Length; i++)
            {
                Vector3 p = rest[i];
                float u = Mathf.InverseLerp(-0.5f, 0.5f, p.x);
                float fold = Mathf.Lerp(u, u < 0.5f ? 0.08f : 0.92f, gather);
                float x = Mathf.Lerp(-0.5f, 0.5f, fold);
                float flutter = (1f - gather * 0.65f) * wave * Mathf.Sin(p.y * 7.5f + t + u * 4f);
                live[i] = new Vector3(x, p.y, p.z + flutter);
            }

            mesh.vertices = live;
            mesh.RecalculateNormals();
        }
    }
}
