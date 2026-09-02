using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Homepad.Home
{
    public class HomeDioramaBuilder : MonoBehaviour
    {
        public const float WallThickness = 0.12f;
        public const float HighWallHeight = 1.95f;
        public const float LowWallHeight = 0.45f;
        public const float InteriorWallHeight = 1.05f;
        public const float WindowSillHeight = 0.65f;
        public const float WindowLintelHeight = 1.60f;
        public const float DoorLintelHeight = 1.55f;
        public const float FloorPlinthHeight = 0.04f;

        public sealed class MeshData
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> UVs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();

            public void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n)
            {
                int start = Vertices.Count;
                Vertices.Add(p0); Vertices.Add(p1); Vertices.Add(p2); Vertices.Add(p3);
                Normals.Add(n); Normals.Add(n); Normals.Add(n); Normals.Add(n);

                // Compute plan/facade UVs
                float u = Vector3.Distance(p0, p1);
                float v = Vector3.Distance(p0, p3);
                UVs.Add(new Vector2(0f, 0f));
                UVs.Add(new Vector2(u, 0f));
                UVs.Add(new Vector2(u, v));
                UVs.Add(new Vector2(0f, v));

                Triangles.Add(start + 0); Triangles.Add(start + 1); Triangles.Add(start + 2);
                Triangles.Add(start + 0); Triangles.Add(start + 2); Triangles.Add(start + 3);
            }

            public void AddBox(Vector3 center, Vector3 size)
            {
                Vector3 h = size * 0.5f;
                Vector3 c000 = center + new Vector3(-h.x, -h.y, -h.z);
                Vector3 c100 = center + new Vector3(h.x, -h.y, -h.z);
                Vector3 c110 = center + new Vector3(h.x, h.y, -h.z);
                Vector3 c010 = center + new Vector3(-h.x, h.y, -h.z);

                Vector3 c001 = center + new Vector3(-h.x, -h.y, h.z);
                Vector3 c101 = center + new Vector3(h.x, -h.y, h.z);
                Vector3 c111 = center + new Vector3(h.x, h.y, h.z);
                Vector3 c011 = center + new Vector3(-h.x, h.y, h.z);

                // Front (-Z)
                AddQuad(c000, c100, c110, c010, Vector3.back);
                // Back (+Z)
                AddQuad(c101, c001, c011, c111, Vector3.forward);
                // Left (-X)
                AddQuad(c001, c000, c010, c011, Vector3.left);
                // Right (+X)
                AddQuad(c100, c101, c111, c110, Vector3.right);
                // Top (+Y)
                AddQuad(c010, c110, c111, c011, Vector3.up);
                // Bottom (-Y)
                AddQuad(c001, c101, c100, c000, Vector3.down);
            }

            public Mesh ToMesh(string name = "ProceduralMesh")
            {
                if (Vertices.Count == 0) return null;
                var mesh = new Mesh { name = name };
                if (Vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(Vertices);
                mesh.SetNormals(Normals);
                mesh.SetUVs(0, UVs);
                mesh.SetTriangles(Triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        public sealed class BuildOutput
        {
            public readonly Dictionary<RoomHint, MeshData> RoomFloors = new Dictionary<RoomHint, MeshData>();
            public readonly MeshData WallData = new MeshData();
            public readonly MeshData WallCapData = new MeshData();
            public readonly MeshData FrameData = new MeshData();
            public readonly MeshData GlassData = new MeshData();
            public readonly MeshData SkirtingData = new MeshData();
        }

        public static BuildOutput Generate(HomeLayout layout)
        {
            Vector3 forward = Camera.main != null ? Camera.main.transform.forward : new Vector3(1f, -1f, 1f);
            return Generate(layout, forward);
        }

        public static BuildOutput Generate(HomeLayout layout, Vector3 cameraForward)
        {
            var output = new BuildOutput();
            if (layout == null) return output;

            var cutaway = HomeLayout.CutawayView.FromCamera(cameraForward);
            float halfCell = HomeLayout.CellSize * 0.5f;
            float t = WallThickness;

            // 1. Build Room Floors & Plinths
            foreach (var pair in layout.Cells)
            {
                var cell = pair.Value;
                if (!cell.HasFloor) continue;

                var room = layout.FindRoomById(cell.RoomId);
                var hint = room != null ? room.Hint : RoomHint.Living;
                if (!output.RoomFloors.TryGetValue(hint, out var floorMesh))
                {
                    floorMesh = new MeshData();
                    output.RoomFloors[hint] = floorMesh;
                }

                Vector3 center = layout.CellCenter(cell.Pos, 0f);
                // Plinth top quad (facing UP)
                floorMesh.AddQuad(
                    new Vector3(center.x - halfCell, FloorPlinthHeight, center.z + halfCell),
                    new Vector3(center.x + halfCell, FloorPlinthHeight, center.z + halfCell),
                    new Vector3(center.x + halfCell, FloorPlinthHeight, center.z - halfCell),
                    new Vector3(center.x - halfCell, FloorPlinthHeight, center.z - halfCell),
                    Vector3.up);

                // Plinth outer edges (if boundary)
                for (int d = 0; d < 4; d++)
                {
                    Vector2Int neighborPos = cell.Pos + HomeLayout.DirVec[d];
                    var neighbor = layout.GetCell(neighborPos);
                    if (neighbor == null || !neighbor.HasFloor)
                    {
                        AddPlinthSkirt(output.SkirtingData, center, halfCell, d, FloorPlinthHeight);
                    }
                }
            }

            // 2. Build Volumetric Walls
            var processedEdges = new HashSet<string>();

            foreach (var pair in layout.Cells)
            {
                var cell = pair.Value;
                if (!cell.HasFloor) continue;

                for (int d = 0; d < 4; d++)
                {
                    bool hasWall = cell.Walls[d];
                    bool hasWindow = cell.Windows[d];
                    bool hasDoor = cell.Doors[d];

                    if (!hasWall && !hasWindow && !hasDoor) continue;

                    string edgeKey = GetEdgeKey(cell.Pos, d);
                    if (processedEdges.Contains(edgeKey)) continue;
                    processedEdges.Add(edgeKey);

                    Vector3 center = layout.CellCenter(cell.Pos, 0f);
                    float wallH = GetWallHeight(layout, cell, d, cutaway);

                    if (hasDoor)
                    {
                        BuildVolumetricDoorway(output.WallData, output.WallCapData, output.FrameData, center, halfCell, d, t, wallH);
                    }
                    else if (hasWindow)
                    {
                        BuildVolumetricWindow(output.WallData, output.WallCapData, output.FrameData, output.GlassData, center, halfCell, d, t, wallH);
                    }
                    else
                    {
                        BuildVolumetricWall(output.WallData, output.WallCapData, center, halfCell, d, t, wallH);
                    }
                }
            }

            return output;
        }

        private static float GetWallHeight(HomeLayout layout, HomeCell cell, int dir, HomeLayout.CutawayView cutaway)
        {
            if (!layout.Cutaway) return HighWallHeight;

            Vector2Int neighborPos = cell.Pos + HomeLayout.DirVec[dir];
            var neighbor = layout.GetCell(neighborPos);
            bool interior = neighbor != null && neighbor.HasFloor;
            if (interior) return InteriorWallHeight;

            return cutaway.IsFront(dir) ? LowWallHeight : HighWallHeight;
        }

        private static void BuildVolumetricWall(MeshData walls, MeshData caps, Vector3 center, float hCell, int dir, float t, float h)
        {
            GetWallLine(center, hCell, dir, out Vector3 p0, out Vector3 p1, out _, out _);
            float length = Vector3.Distance(p0, p1);
            Vector3 mid = (p0 + p1) * 0.5f;
            AddOrientedBox(walls, mid, dir, length + t, h, t, FloorPlinthHeight);
            AddOrientedBox(caps, mid, dir, length + t + 0.02f, 0.03f, t + 0.02f, FloorPlinthHeight + h);
        }

        private static void BuildVolumetricWindow(MeshData walls, MeshData caps, MeshData frames, MeshData glass, Vector3 center, float hCell, int dir, float t, float h)
        {
            GetWallLine(center, hCell, dir, out Vector3 p0, out Vector3 p1, out _, out Vector3 side);
            float length = Vector3.Distance(p0, p1);
            Vector3 mid = (p0 + p1) * 0.5f;

            float openingTop = h > WindowLintelHeight ? WindowLintelHeight : Mathf.Max(h, LowWallHeight + 0.35f);
            float sillH = Mathf.Min(WindowSillHeight, openingTop * 0.45f);
            if (sillH < 0.08f) sillH = Mathf.Min(0.12f, h * 0.35f);

            if (sillH > 0.05f)
            {
                AddOrientedBox(walls, mid, dir, length + t, sillH, t, FloorPlinthHeight);
            }

            if (h > openingTop + 0.02f)
            {
                float topH = h - openingTop;
                AddOrientedBox(walls, mid, dir, length + t, topH, t, FloorPlinthHeight + openingTop);
                AddOrientedBox(caps, mid, dir, length + t + 0.02f, 0.03f, t + 0.02f, FloorPlinthHeight + h);
            }
            else
            {
                AddOrientedBox(caps, mid, dir, length + t + 0.02f, 0.03f, t + 0.02f, FloorPlinthHeight + Mathf.Max(sillH, h));
            }

            float glassBottom = FloorPlinthHeight + sillH;
            float glassTop = FloorPlinthHeight + openingTop;
            float glassH = Mathf.Max(0.18f, glassTop - glassBottom);
            float openingW = Mathf.Max(0.35f, length * 0.72f);
            AddOrientedBox(glass, mid, dir, openingW, glassH, 0.018f, glassBottom);
            AddHollowFrame(frames, mid, side, dir, length, t * 1.12f, glassBottom, glassBottom + glassH);
        }

        private static void BuildVolumetricDoorway(MeshData walls, MeshData caps, MeshData frames, Vector3 center, float hCell, int dir, float t, float h)
        {
            GetWallLine(center, hCell, dir, out Vector3 p0, out Vector3 p1, out _, out Vector3 side);
            float length = Vector3.Distance(p0, p1);
            Vector3 mid = (p0 + p1) * 0.5f;

            float openingH = Mathf.Min(DoorLintelHeight, Mathf.Max(h, LowWallHeight));
            float postW = 0.08f;
            float openingW = Mathf.Max(0.4f, length * 0.7f);

            Vector3 post1 = mid + side * ((openingW + postW) * 0.5f);
            Vector3 post2 = mid - side * ((openingW + postW) * 0.5f);
            AddOrientedBox(frames, post1, dir, postW, openingH, t * 1.15f, FloorPlinthHeight);
            AddOrientedBox(frames, post2, dir, postW, openingH, t * 1.15f, FloorPlinthHeight);
            AddOrientedBox(frames, mid, dir, openingW, 0.035f, t * 1.2f, 0f);

            if (h > openingH + 0.02f)
            {
                float topH = h - openingH;
                AddOrientedBox(walls, mid, dir, length + t, topH, t, FloorPlinthHeight + openingH);
                AddOrientedBox(frames, mid, dir, openingW + postW * 2f, 0.06f, t * 1.15f, FloorPlinthHeight + openingH - 0.03f);
                AddOrientedBox(caps, mid, dir, length + t + 0.02f, 0.03f, t + 0.02f, FloorPlinthHeight + h);
            }
            else
            {
                AddOrientedBox(frames, mid, dir, openingW + postW * 2f, 0.045f, t * 1.15f, FloorPlinthHeight + openingH);
            }
        }

        private static void AddPlinthSkirt(MeshData skirt, Vector3 center, float hCell, int dir, float height)
        {
            GetWallLine(center, hCell, dir, out Vector3 p0, out Vector3 p1, out Vector3 norm, out Vector3 side);
            float length = Vector3.Distance(p0, p1);
            Vector3 mid = (p0 + p1) * 0.5f;

            Vector3 size = (dir == 0 || dir == 2) ? new Vector3(length, height, 0.03f) : new Vector3(0.03f, height, length);
            Vector3 pos = new Vector3(mid.x, height * 0.5f, mid.z) + norm * 0.015f;
            skirt.AddBox(pos, size);
        }

        private static void AddHollowFrame(MeshData frames, Vector3 mid, Vector3 side, int dir, float length, float thickness, float y0, float y1)
        {
            float h = Mathf.Max(0.12f, y1 - y0);
            float openingW = Mathf.Max(0.3f, length * 0.72f);
            float stile = Mathf.Max(0.045f, (length - openingW) * 0.5f);
            float rail = 0.045f;
            Vector3 left = mid - side * ((openingW + stile) * 0.5f);
            Vector3 right = mid + side * ((openingW + stile) * 0.5f);
            AddOrientedBox(frames, left, dir, stile, h, thickness, y0);
            AddOrientedBox(frames, right, dir, stile, h, thickness, y0);
            AddOrientedBox(frames, mid, dir, openingW, rail, thickness, y0);
            AddOrientedBox(frames, mid, dir, openingW, rail, thickness, y1 - rail);
        }

        private static void AddOrientedBox(MeshData data, Vector3 mid, int dir, float length, float height, float thickness, float yBottom)
        {
            Vector3 size = (dir == 0 || dir == 2)
                ? new Vector3(length, height, thickness)
                : new Vector3(thickness, height, length);
            Vector3 center = new Vector3(mid.x, yBottom + height * 0.5f, mid.z);
            data.AddBox(center, size);
        }

        private static void GetWallLine(Vector3 center, float hCell, int dir, out Vector3 p0, out Vector3 p1, out Vector3 norm, out Vector3 side)
        {
            switch (dir)
            {
                case 0: // North (+Z)
                    p0 = new Vector3(center.x - hCell, 0f, center.z + hCell);
                    p1 = new Vector3(center.x + hCell, 0f, center.z + hCell);
                    norm = Vector3.forward;
                    side = Vector3.right;
                    break;
                case 1: // East (+X)
                    p0 = new Vector3(center.x + hCell, 0f, center.z + hCell);
                    p1 = new Vector3(center.x + hCell, 0f, center.z - hCell);
                    norm = Vector3.right;
                    side = Vector3.back;
                    break;
                case 2: // South (-Z)
                    p0 = new Vector3(center.x + hCell, 0f, center.z - hCell);
                    p1 = new Vector3(center.x - hCell, 0f, center.z - hCell);
                    norm = Vector3.back;
                    side = Vector3.left;
                    break;
                default: // West (-X)
                    p0 = new Vector3(center.x - hCell, 0f, center.z - hCell);
                    p1 = new Vector3(center.x - hCell, 0f, center.z + hCell);
                    norm = Vector3.left;
                    side = Vector3.forward;
                    break;
            }
        }

        private static string GetEdgeKey(Vector2Int pos, int dir)
        {
            Vector2Int other = pos + HomeLayout.DirVec[dir];
            int otherDir = HomeLayout.Opposite(dir);
            if (pos.x < other.x || (pos.x == other.x && pos.y < other.y))
                return $"{pos.x},{pos.y}_{dir}";
            else
                return $"{other.x},{other.y}_{otherDir}";
        }
    }
}
