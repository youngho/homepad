using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Homepad.Home
{
    public class HomeDioramaBuilder : MonoBehaviour
    {
        public const float WallThickness = 0.08f;
        public const float HighWallHeight = 1.95f;
        public const float LowWallHeight = 0.50f;
        public const float FloorPlinthHeight = 0.04f;

        // Line accent dimensions
        public const float LineThickness = 0.018f;
        public const float PostThickness = 0.028f;

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
            public readonly MeshData TranslucentWalls = new MeshData();
            public readonly MeshData EdgeLines = new MeshData();
            public readonly MeshData PlinthData = new MeshData();
        }

        public static BuildOutput Generate(HomeLayout layout, Vector3? cameraForward = null)
        {
            var output = new BuildOutput();
            if (layout == null || layout.Rooms.Count == 0) return output;

            float cSize = HomeLayout.CellSize;
            float t = WallThickness;
            var cutaway = HomeLayout.CutawayView.FromCamera(cameraForward ?? new Vector3(1f, -1f, 1f));

            // 1. Build Room Floors
            foreach (var room in layout.Rooms)
            {
                if (!output.RoomFloors.TryGetValue(room.Hint, out var floorMesh))
                {
                    floorMesh = new MeshData();
                    output.RoomFloors[room.Hint] = floorMesh;
                }

                Vector3 min = new Vector3(room.Origin.x * cSize, FloorPlinthHeight, room.Origin.y * cSize);
                Vector3 max = new Vector3((room.Origin.x + room.Size.x) * cSize, FloorPlinthHeight, (room.Origin.y + room.Size.y) * cSize);

                floorMesh.AddQuad(
                    new Vector3(min.x, FloorPlinthHeight, max.z),
                    new Vector3(max.x, FloorPlinthHeight, max.z),
                    new Vector3(max.x, FloorPlinthHeight, min.z),
                    new Vector3(min.x, FloorPlinthHeight, min.z),
                    Vector3.up);
            }

            // 2. Build Seamless Room Boundary Walls
            var processedEdges = new HashSet<string>();
            var processedCorners = new HashSet<string>();

            foreach (var room in layout.Rooms)
            {
                Vector3 rMin = new Vector3(room.Origin.x * cSize, 0f, room.Origin.y * cSize);
                Vector3 rMax = new Vector3((room.Origin.x + room.Size.x) * cSize, 0f, (room.Origin.y + room.Size.y) * cSize);

                Vector3 cNW = new Vector3(rMin.x, 0f, rMax.z);
                Vector3 cNE = new Vector3(rMax.x, 0f, rMax.z);
                Vector3 cSE = new Vector3(rMax.x, 0f, rMin.z);
                Vector3 cSW = new Vector3(rMin.x, 0f, rMin.z);

                BuildRoomSide(output, layout, room, cutaway, 0, cNW, cNE, processedEdges, processedCorners, t);
                BuildRoomSide(output, layout, room, cutaway, 1, cNE, cSE, processedEdges, processedCorners, t);
                BuildRoomSide(output, layout, room, cutaway, 2, cSE, cSW, processedEdges, processedCorners, t);
                BuildRoomSide(output, layout, room, cutaway, 3, cSW, cNW, processedEdges, processedCorners, t);
            }

            return output;
        }

        private static void BuildRoomSide(
            BuildOutput output,
            HomeLayout layout,
            RoomRecord room,
            HomeLayout.CutawayView cutaway,
            int dir,
            Vector3 p0,
            Vector3 p1,
            HashSet<string> processedEdges,
            HashSet<string> processedCorners,
            float t)
        {
            string edgeKey = GetSpanKey(p0, p1);
            if (processedEdges.Contains(edgeKey)) return;
            processedEdges.Add(edgeKey);

            Vector2Int neighborCell = GetSideCellPos(room, dir, 0) + HomeLayout.DirVec[dir];
            var neighbor = layout.GetCell(neighborCell);
            bool isExterior = neighbor == null || !neighbor.HasFloor;
            bool isFront = isExterior && layout.Cutaway && cutaway.IsFront(dir);

            float wallH = HighWallHeight;

            // Exterior boundary plinth line
            if (isExterior)
            {
                AddPlinthSpan(output.PlinthData, output.EdgeLines, p0, p1, dir, FloorPlinthHeight);
            }

            if (isFront)
            {
                // Front exterior wall: open in cutaway (floor plinth line defines boundary)
            }
            else
            {
                // Seamless Pure Translucent Glass Wall Span
                AddCornerPostIfNew(output.EdgeLines, processedCorners, p0, wallH);
                AddCornerPostIfNew(output.EdgeLines, processedCorners, p1, wallH);
                BuildContinuousWallSpan(output.TranslucentWalls, output.EdgeLines, p0, p1, dir, t, wallH);
            }
        }

        private static void BuildContinuousWallSpan(MeshData transWalls, MeshData edges, Vector3 p0, Vector3 p1, int dir, float t, float h)
        {
            Vector3 mid = (p0 + p1) * 0.5f;
            float length = Vector3.Distance(p0, p1);

            // 1. Sleek Continuous Translucent Frosted Glass Panel
            AddOrientedBox(transWalls, mid, dir, length, h, t, FloorPlinthHeight);

            // 2. Crisp Glowing Top Outline Rail
            AddOrientedBox(edges, mid, dir, length, LineThickness, LineThickness * 1.4f, FloorPlinthHeight + h - LineThickness * 0.5f);

            // 3. Sleek Bottom Baseboard Line
            AddOrientedBox(edges, mid, dir, length, LineThickness * 0.7f, LineThickness, FloorPlinthHeight);
        }

        private static void AddCornerPostIfNew(MeshData edges, HashSet<string> visited, Vector3 cornerPos, float height)
        {
            string key = $"{Mathf.RoundToInt(cornerPos.x * 100)},{Mathf.RoundToInt(cornerPos.z * 100)}";
            if (visited.Contains(key)) return;
            visited.Add(key);

            Vector3 center = new Vector3(cornerPos.x, FloorPlinthHeight + height * 0.5f, cornerPos.z);
            edges.AddBox(center, new Vector3(PostThickness, height, PostThickness));
        }

        private static void AddPlinthSpan(MeshData plinth, MeshData edges, Vector3 p0, Vector3 p1, int dir, float height)
        {
            Vector3 mid = (p0 + p1) * 0.5f;
            float length = Vector3.Distance(p0, p1);
            Vector3 norm = dir switch
            {
                0 => Vector3.forward,
                1 => Vector3.right,
                2 => Vector3.back,
                _ => Vector3.left
            };

            Vector3 size = (dir == 0 || dir == 2) ? new Vector3(length, height, 0.025f) : new Vector3(0.025f, height, length);
            Vector3 pos = new Vector3(mid.x, height * 0.5f, mid.z) + norm * 0.012f;
            plinth.AddBox(pos, size);

            Vector3 lineSize = (dir == 0 || dir == 2) ? new Vector3(length, 0.012f, 0.028f) : new Vector3(0.028f, 0.012f, length);
            Vector3 linePos = new Vector3(mid.x, height, mid.z) + norm * 0.012f;
            edges.AddBox(linePos, lineSize);
        }

        private static void AddOrientedBox(MeshData data, Vector3 mid, int dir, float length, float height, float thickness, float yBottom)
        {
            Vector3 size = (dir == 0 || dir == 2)
                ? new Vector3(length, height, thickness)
                : new Vector3(thickness, height, length);
            Vector3 center = new Vector3(mid.x, yBottom + height * 0.5f, mid.z);
            data.AddBox(center, size);
        }

        private static Vector2Int GetSideCellPos(RoomRecord room, int dir, int index)
        {
            return dir switch
            {
                0 => new Vector2Int(room.Origin.x + index, room.Origin.y + room.Size.y - 1),
                1 => new Vector2Int(room.Origin.x + room.Size.x - 1, room.Origin.y + (room.Size.y - 1 - index)),
                2 => new Vector2Int(room.Origin.x + (room.Size.x - 1 - index), room.Origin.y),
                _ => new Vector2Int(room.Origin.x, room.Origin.y + index)
            };
        }

        private static string GetSpanKey(Vector3 p0, Vector3 p1)
        {
            int x0 = Mathf.RoundToInt(p0.x * 100);
            int z0 = Mathf.RoundToInt(p0.z * 100);
            int x1 = Mathf.RoundToInt(p1.x * 100);
            int z1 = Mathf.RoundToInt(p1.z * 100);

            if (x0 < x1 || (x0 == x1 && z0 < z1))
                return $"{x0},{z0}_{x1},{z1}";
            return $"{x1},{z1}_{x0},{z0}";
        }
    }
}
