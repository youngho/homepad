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
        public const float WindowSillHeight = 0.70f;
        public const float WindowLintelHeight = 1.65f;
        public const float DoorLintelHeight = 1.60f;
        public const float FloorPlinthHeight = 0.04f;

        // Line accent dimensions
        public const float LineThickness = 0.032f;
        public const float PostThickness = 0.045f;

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
            public readonly MeshData DoorFrames = new MeshData();
            public readonly MeshData WindowFrames = new MeshData();
            public readonly MeshData WindowGlass = new MeshData();
            public readonly MeshData PlinthData = new MeshData();
        }

        public static BuildOutput Generate(HomeLayout layout)
        {
            var output = new BuildOutput();
            if (layout == null) return output;

            float halfCell = HomeLayout.CellSize * 0.5f;
            float t = WallThickness;

            // 1. Build Room Floors & Floating Plinth
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

                // Floor Top Quad
                floorMesh.AddQuad(
                    new Vector3(center.x - halfCell, FloorPlinthHeight, center.z + halfCell),
                    new Vector3(center.x + halfCell, FloorPlinthHeight, center.z + halfCell),
                    new Vector3(center.x + halfCell, FloorPlinthHeight, center.z - halfCell),
                    new Vector3(center.x - halfCell, FloorPlinthHeight, center.z - halfCell),
                    Vector3.up);

                // Boundary Skirt Base
                for (int d = 0; d < 4; d++)
                {
                    Vector2Int neighborPos = cell.Pos + HomeLayout.DirVec[d];
                    var neighbor = layout.GetCell(neighborPos);
                    if (neighbor == null || !neighbor.HasFloor)
                    {
                        AddPlinthSkirt(output.PlinthData, output.EdgeLines, center, halfCell, d, FloorPlinthHeight);
                    }
                }
            }

            // 2. Build Translucent Walls & Glowing Edge Lines
            var processedEdges = new HashSet<string>();
            var processedCorners = new HashSet<string>();

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
                    float wallH = GetWallHeight(layout, cell, d);

                    // Add Corner Posts on vertices if not processed
                    GetWallLine(center, halfCell, d, out Vector3 p0, out Vector3 p1, out Vector3 norm, out Vector3 side);
                    AddCornerPostIfNew(output.EdgeLines, processedCorners, p0, wallH);
                    AddCornerPostIfNew(output.EdgeLines, processedCorners, p1, wallH);

                    if (hasDoor)
                    {
                        BuildTranslucentDoorway(output.TranslucentWalls, output.EdgeLines, output.DoorFrames, center, halfCell, d, t, wallH);
                    }
                    else if (hasWindow)
                    {
                        BuildTranslucentWindow(output.TranslucentWalls, output.EdgeLines, output.WindowFrames, output.WindowGlass, center, halfCell, d, t, wallH);
                    }
                    else
                    {
                        BuildTranslucentWall(output.TranslucentWalls, output.EdgeLines, center, halfCell, d, t, wallH);
                    }
                }
            }

            return output;
        }

        private static float GetWallHeight(HomeLayout layout, HomeCell cell, int dir)
        {
            if (!layout.Cutaway) return HighWallHeight;

            // In isometric cutaway: South (dir 2) and West (dir 3) are open / low walls
            // North (dir 0) and East (dir 1) are backdrop high walls
            if (dir == 2 || dir == 3)
            {
                return LowWallHeight;
            }

            Vector2Int neighborPos = cell.Pos + HomeLayout.DirVec[dir];
            var neighbor = layout.GetCell(neighborPos);
            if (neighbor != null && neighbor.HasFloor)
            {
                return (dir == 0 || dir == 1) ? HighWallHeight : LowWallHeight;
            }

            return HighWallHeight;
        }

        private static void BuildTranslucentWall(MeshData transWalls, MeshData edges, Vector3 center, float hCell, int dir, float t, float h)
        {
            GetWallLine(center, hCell, dir, out Vector3 p0, out Vector3 p1, out Vector3 norm, out Vector3 side);
            float length = Vector3.Distance(p0, p1);
            Vector3 mid = (p0 + p1) * 0.5f;

            // 1. Translucent Frosted Glass Wall Body
            Vector3 wallSize = (dir == 0 || dir == 2)
                ? new Vector3(length, h, t)
                : new Vector3(t, h, length);

            Vector3 boxCenter = new Vector3(mid.x, FloorPlinthHeight + h * 0.5f, mid.z);
            transWalls.AddBox(boxCenter, wallSize);

            // 2. Glowing Architectural Top Edge Line
            Vector3 topRailSize = (dir == 0 || dir == 2)
                ? new Vector3(length, LineThickness, LineThickness * 1.5f)
                : new Vector3(LineThickness * 1.5f, LineThickness, length);
            Vector3 topRailCenter = new Vector3(mid.x, FloorPlinthHeight + h, mid.z);
            edges.AddBox(topRailCenter, topRailSize);

            // 3. Baseboard Line Accent
            Vector3 baseRailSize = (dir == 0 || dir == 2)
                ? new Vector3(length, LineThickness * 0.7f, LineThickness * 1.2f)
                : new Vector3(LineThickness * 1.2f, LineThickness * 0.7f, length);
            Vector3 baseRailCenter = new Vector3(mid.x, FloorPlinthHeight + LineThickness * 0.35f, mid.z);
            edges.AddBox(baseRailCenter, baseRailSize);
        }

        private static void BuildTranslucentWindow(MeshData transWalls, MeshData edges, MeshData frames, MeshData glass, Vector3 center, float hCell, int dir, float t, float h)
        {
            GetWallLine(center, hCell, dir, out Vector3 p0, out Vector3 p1, out Vector3 norm, out Vector3 side);
            float length = Vector3.Distance(p0, p1);
            Vector3 mid = (p0 + p1) * 0.5f;

            float sillH = Mathf.Min(WindowSillHeight, h * 0.8f);

            // Translucent Sill Wall
            if (sillH > 0.05f)
            {
                Vector3 sillSize = (dir == 0 || dir == 2) ? new Vector3(length, sillH, t) : new Vector3(t, sillH, length);
                Vector3 sillCenter = new Vector3(mid.x, FloorPlinthHeight + sillH * 0.5f, mid.z);
                transWalls.AddBox(sillCenter, sillSize);

                // Sill Top Edge Line
                Vector3 sillRailSize = (dir == 0 || dir == 2) ? new Vector3(length, LineThickness, LineThickness * 1.4f) : new Vector3(LineThickness * 1.4f, LineThickness, length);
                Vector3 sillRailCenter = new Vector3(mid.x, FloorPlinthHeight + sillH, mid.z);
                edges.AddBox(sillRailCenter, sillRailSize);
            }

            // Top Lintel Wall if high wall
            if (h > WindowLintelHeight)
            {
                float topH = h - WindowLintelHeight;
                Vector3 topSize = (dir == 0 || dir == 2) ? new Vector3(length, topH, t) : new Vector3(t, topH, length);
                Vector3 topCenter = new Vector3(mid.x, FloorPlinthHeight + WindowLintelHeight + topH * 0.5f, mid.z);
                transWalls.AddBox(topCenter, topSize);

                // Top Edge Line
                Vector3 topRailSize = (dir == 0 || dir == 2) ? new Vector3(length, LineThickness, LineThickness * 1.5f) : new Vector3(LineThickness * 1.5f, LineThickness, length);
                Vector3 topRailCenter = new Vector3(mid.x, FloorPlinthHeight + h, mid.z);
                edges.AddBox(topRailCenter, topRailSize);
            }

            // Sleek Window Frame & Glass
            float glassBottom = FloorPlinthHeight + sillH;
            float glassTop = FloorPlinthHeight + (h > WindowLintelHeight ? WindowLintelHeight : h + 0.3f);
            float glassH = Mathf.Max(0.2f, glassTop - glassBottom);
            Vector3 glassCenter = new Vector3(mid.x, glassBottom + glassH * 0.5f, mid.z);

            Vector3 glassSize = (dir == 0 || dir == 2) ? new Vector3(length * 0.94f, glassH, 0.015f) : new Vector3(0.015f, glassH, length * 0.94f);
            glass.AddBox(glassCenter, glassSize);

            // Architectural Slim Window Frame Outline
            Vector3 frameSize = (dir == 0 || dir == 2) ? new Vector3(length * 0.96f, glassH + 0.03f, t * 1.05f) : new Vector3(t * 1.05f, glassH + 0.03f, length * 0.96f);
            frames.AddBox(glassCenter, frameSize);
        }

        private static void BuildTranslucentDoorway(MeshData transWalls, MeshData edges, MeshData frames, Vector3 center, float hCell, int dir, float t, float h)
        {
            GetWallLine(center, hCell, dir, out Vector3 p0, out Vector3 p1, out Vector3 norm, out Vector3 side);
            float length = Vector3.Distance(p0, p1);
            Vector3 mid = (p0 + p1) * 0.5f;

            float doorH = Mathf.Min(DoorLintelHeight, h);
            float postW = 0.06f;

            // Futuristic Slim Door Portal Posts
            Vector3 post1Center = mid + side * (length * 0.46f);
            post1Center.y = FloorPlinthHeight + doorH * 0.5f;
            Vector3 post2Center = mid - side * (length * 0.46f);
            post2Center.y = FloorPlinthHeight + doorH * 0.5f;

            Vector3 postSize = (dir == 0 || dir == 2) ? new Vector3(postW, doorH, t * 1.15f) : new Vector3(t * 1.15f, doorH, postW);
            frames.AddBox(post1Center, postSize);
            frames.AddBox(post2Center, postSize);

            // Lintel Header Beam
            Vector3 lintelCenter = mid;
            lintelCenter.y = FloorPlinthHeight + doorH;
            Vector3 lintelSize = (dir == 0 || dir == 2) ? new Vector3(length, LineThickness * 1.2f, t * 1.15f) : new Vector3(t * 1.15f, LineThickness * 1.2f, length);
            frames.AddBox(lintelCenter, lintelSize);

            // Top Lintel Wall if high wall
            if (h > DoorLintelHeight)
            {
                float topH = h - DoorLintelHeight;
                Vector3 topSize = (dir == 0 || dir == 2) ? new Vector3(length, topH, t) : new Vector3(t, topH, length);
                Vector3 topCenter = new Vector3(mid.x, FloorPlinthHeight + DoorLintelHeight + topH * 0.5f, mid.z);
                transWalls.AddBox(topCenter, topSize);

                // Top Edge Line
                Vector3 topRailSize = (dir == 0 || dir == 2) ? new Vector3(length, LineThickness, LineThickness * 1.5f) : new Vector3(LineThickness * 1.5f, LineThickness, length);
                Vector3 topRailCenter = new Vector3(mid.x, FloorPlinthHeight + h, mid.z);
                edges.AddBox(topRailCenter, topRailSize);
            }
        }

        private static void AddCornerPostIfNew(MeshData edges, HashSet<string> visited, Vector3 cornerPos, float height)
        {
            string key = $"{Mathf.RoundToInt(cornerPos.x * 100)},{Mathf.RoundToInt(cornerPos.z * 100)}";
            if (visited.Contains(key)) return;
            visited.Add(key);

            Vector3 center = new Vector3(cornerPos.x, FloorPlinthHeight + height * 0.5f, cornerPos.z);
            Vector3 size = new Vector3(PostThickness, height, PostThickness);
            edges.AddBox(center, size);
        }

        private static void AddPlinthSkirt(MeshData plinth, MeshData edges, Vector3 center, float hCell, int dir, float height)
        {
            GetWallLine(center, hCell, dir, out Vector3 p0, out Vector3 p1, out Vector3 norm, out Vector3 side);
            float length = Vector3.Distance(p0, p1);
            Vector3 mid = (p0 + p1) * 0.5f;

            Vector3 size = (dir == 0 || dir == 2) ? new Vector3(length, height, 0.025f) : new Vector3(0.025f, height, length);
            Vector3 pos = new Vector3(mid.x, height * 0.5f, mid.z) + norm * 0.012f;
            plinth.AddBox(pos, size);

            // Plinth Edge Line
            Vector3 lineSize = (dir == 0 || dir == 2) ? new Vector3(length, 0.015f, 0.035f) : new Vector3(0.035f, 0.015f, length);
            Vector3 linePos = new Vector3(mid.x, height, mid.z) + norm * 0.012f;
            edges.AddBox(linePos, lineSize);
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
