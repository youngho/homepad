using UnityEngine;
using UnityEngine.Rendering;

namespace Homepad.Home
{
    public class ProceduralCurtain3D : MonoBehaviour
    {
        [Header("Curtain Dimensions")]
        [SerializeField] private float width = 2.4f;
        [SerializeField] private float height = 1.65f;
        [SerializeField] private float pleatDepth = 0.065f;
        [SerializeField] private int pleatCount = 10;
        [SerializeField] private int segmentsX = 48;
        [SerializeField] private int segmentsY = 16;

        [Header("Animation")]
        [SerializeField] private float breezeWave = 0.012f;
        [SerializeField] private float breezeSpeed = 1.8f;

        [Header("State")]
        [Range(0f, 1f)] [SerializeField] private float openPercent = 0f;

        private MeshFilter clothFilter;
        private Mesh clothMesh;
        private Vector3[] baseVerts;
        private Vector3[] animVerts;
        private Transform rodTransform;
        private Material clothMaterial;
        private Material metalMaterial;

        public float OpenPercent => openPercent;

        public void SetOpen(float value)
        {
            openPercent = Mathf.Clamp01(value);
            ApplyGathering();
        }

        public void Initialize(Material clothMat, Material metalMat)
        {
            clothMaterial = clothMat;
            metalMaterial = metalMat;
            Build3DStructure();
        }

        private void Awake()
        {
            if (clothMesh == null)
            {
                Build3DStructure();
            }
        }

        public void Build3DStructure()
        {
            // Clear existing children
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            // 1. Create Top Curtain Rod (Metal Rail)
            var rodGo = new GameObject("CurtainRod");
            rodGo.transform.SetParent(transform, false);
            rodGo.transform.localPosition = new Vector3(0f, height * 0.5f + 0.04f, 0f);
            var rodCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rodCylinder.transform.SetParent(rodGo.transform, false);
            rodCylinder.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            rodCylinder.transform.localScale = new Vector3(0.024f, width * 0.52f, 0.024f);
            Destroy(rodCylinder.GetComponent<Collider>());
            var rodRend = rodCylinder.GetComponent<MeshRenderer>();
            if (metalMaterial != null) rodRend.sharedMaterial = metalMaterial;

            // Left & Right Rod End Caps (Finials)
            CreateFinial(rodGo.transform, new Vector3(-width * 0.53f, 0f, 0f));
            CreateFinial(rodGo.transform, new Vector3(width * 0.53f, 0f, 0f));

            // Wall Brackets
            CreateWallBracket(rodGo.transform, new Vector3(-width * 0.45f, 0f, 0.04f));
            CreateWallBracket(rodGo.transform, new Vector3(width * 0.45f, 0f, 0.04f));

            // 2. Generate Rich 3D Pleated Cloth Mesh
            var clothGo = new GameObject("PleatedCloth");
            clothGo.transform.SetParent(transform, false);
            clothGo.transform.localPosition = Vector3.zero;

            clothFilter = clothGo.AddComponent<MeshFilter>();
            var clothRend = clothGo.AddComponent<MeshRenderer>();
            clothRend.shadowCastingMode = ShadowCastingMode.On;
            clothRend.receiveShadows = true;
            if (clothMaterial != null) clothRend.sharedMaterial = clothMaterial;

            clothMesh = GeneratePleatedMesh(width, height, pleatDepth, pleatCount, segmentsX, segmentsY);
            clothFilter.sharedMesh = clothMesh;
            baseVerts = clothMesh.vertices;
            animVerts = new Vector3[baseVerts.Length];

            ApplyGathering();
        }

        private void CreateFinial(Transform parent, Vector3 localPos)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Finial";
            sphere.transform.SetParent(parent, false);
            sphere.transform.localPosition = localPos;
            sphere.transform.localScale = new Vector3(0.055f, 0.055f, 0.055f);
            Destroy(sphere.GetComponent<Collider>());
            if (metalMaterial != null) sphere.GetComponent<MeshRenderer>().sharedMaterial = metalMaterial;
        }

        private void CreateWallBracket(Transform parent, Vector3 localPos)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Bracket";
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = new Vector3(0.02f, 0.04f, 0.08f);
            Destroy(cube.GetComponent<Collider>());
            if (metalMaterial != null) cube.GetComponent<MeshRenderer>().sharedMaterial = metalMaterial;
        }

        private static Mesh GeneratePleatedMesh(float w, float h, float depth, int pleats, int resX, int resY)
        {
            var mesh = new Mesh { name = "ProceduralPleatedCurtain" };
            int vertCount = (resX + 1) * (resY + 1);
            var vertices = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var triangles = new int[resX * resY * 6];

            float halfW = w * 0.5f;
            float halfH = h * 0.5f;

            for (int y = 0; y <= resY; y++)
            {
                float v = (float)y / resY;
                float posY = Mathf.Lerp(-halfH, halfH, v);

                for (int x = 0; x <= resX; x++)
                {
                    float u = (float)x / resX;
                    float posX = Mathf.Lerp(-halfW, halfW, u);

                    // Sinusoidal 3D accordion pleat folds
                    float pleatPhase = u * pleats * Mathf.PI * 2f;
                    float posZ = Mathf.Sin(pleatPhase) * depth * Mathf.Lerp(1.1f, 0.9f, v);

                    int idx = y * (resX + 1) + x;
                    vertices[idx] = new Vector3(posX, posY, posZ);
                    uvs[idx] = new Vector2(u, v);
                }
            }

            int triIdx = 0;
            for (int y = 0; y < resY; y++)
            {
                for (int x = 0; x < resX; x++)
                {
                    int i0 = y * (resX + 1) + x;
                    int i1 = i0 + 1;
                    int i2 = (y + 1) * (resX + 1) + x;
                    int i3 = i2 + 1;

                    // Front facing quad
                    triangles[triIdx++] = i0;
                    triangles[triIdx++] = i2;
                    triangles[triIdx++] = i1;

                    triangles[triIdx++] = i1;
                    triangles[triIdx++] = i2;
                    triangles[triIdx++] = i3;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void LateUpdate()
        {
            ApplyGathering();
        }

        public void ApplyGathering()
        {
            if (clothMesh == null || baseVerts == null || baseVerts.Length == 0) return;

            float t = Time.time * breezeSpeed;
            float gather = openPercent;
            float halfW = width * 0.5f;

            for (int i = 0; i < baseVerts.Length; i++)
            {
                Vector3 p = baseVerts[i];
                float u = Mathf.InverseLerp(-halfW, halfW, p.x);

                // Compress fabric towards the side rail (e.g. right side u -> 0.90)
                // Accordion fold stacking effect
                float foldU = Mathf.Lerp(u, 0.88f + (u - 0.5f) * 0.18f, gather);
                float newX = Mathf.Lerp(-halfW, halfW, foldU);

                // Deepen Z pleats when gathered (accordion squeeze effect)
                float pleatScale = Mathf.Lerp(1.0f, 2.2f, gather);
                float newZ = p.z * pleatScale;

                // Subtle organic cloth breeze flutter
                float flutter = (1f - gather * 0.75f) * breezeWave * Mathf.Sin((p.y + halfW) * 5.0f + t + u * 3.5f);

                animVerts[i] = new Vector3(newX, p.y, newZ + flutter);
            }

            clothMesh.vertices = animVerts;
            clothMesh.RecalculateNormals();
            clothMesh.RecalculateBounds();
        }
    }
}
