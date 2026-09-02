using UnityEngine;

namespace Homepad.Home
{
    public class CurtainCloth : MonoBehaviour
    {
        [SerializeField] private float wave = 0.015f;
        [SerializeField] private float speed = 1.6f;

        private MeshFilter filter;
        private Mesh mesh;
        private Vector3[] rest;
        private Vector3[] live;
        private float open;
        private float minAxis = -0.5f;
        private float maxAxis = 0.5f;
        private int horizAxis = 2; // 0 for X, 2 for Z

        public void SetOpen(float value)
        {
            open = Mathf.Clamp01(value);
            ApplyMesh();
        }

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (mesh != null) return;
            if (filter == null) filter = GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;

            mesh = Instantiate(filter.sharedMesh);
            mesh.name = "CurtainClothRuntime";
            filter.sharedMesh = mesh;
            rest = mesh.vertices;
            live = new Vector3[rest.Length];

            if (rest.Length == 0) return;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < rest.Length; i++)
            {
                minX = Mathf.Min(minX, rest[i].x);
                maxX = Mathf.Max(maxX, rest[i].x);
                minZ = Mathf.Min(minZ, rest[i].z);
                maxZ = Mathf.Max(maxZ, rest[i].z);
            }

            if ((maxX - minX) >= (maxZ - minZ))
            {
                horizAxis = 0;
                minAxis = minX;
                maxAxis = maxX;
            }
            else
            {
                horizAxis = 2;
                minAxis = minZ;
                maxAxis = maxZ;
            }
        }

        private void LateUpdate()
        {
            ApplyMesh();
        }

        public void ApplyMesh()
        {
            if (mesh == null || rest == null || rest.Length == 0) return;

            float t = Time.time * speed;
            float gather = open;

            for (int i = 0; i < rest.Length; i++)
            {
                Vector3 p = rest[i];
                float coord = horizAxis == 0 ? p.x : p.z;
                float u = Mathf.InverseLerp(minAxis, maxAxis, coord);
                // When open = 1, gather fabric towards the edge (u -> 0.95f)
                float fold = Mathf.Lerp(u, 0.92f + (u - 0.5f) * 0.12f, gather);
                float newCoord = Mathf.Lerp(minAxis, maxAxis, fold);
                float flutter = (1f - gather * 0.8f) * wave * Mathf.Sin(p.y * 7.5f + t + u * 4f);

                if (horizAxis == 0)
                {
                    live[i] = new Vector3(newCoord, p.y, p.z + flutter);
                }
                else
                {
                    live[i] = new Vector3(p.x + flutter, p.y, newCoord);
                }
            }

            mesh.vertices = live;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
