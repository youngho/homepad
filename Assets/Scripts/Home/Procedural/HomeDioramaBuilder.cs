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
            if (layout == null || layout.Rooms.Count == 0) return output;

            float cSize = HomeLayout.CellSize;
            float t = WallThickness;

            // 1. Build Room Floors (1 unified floor quad per room)
            foreach (var room in layout.Rooms)
            {
                if (!output.RoomFloors.TryGetValue(room.Hint, out var floorMesh))
                {
                    floorMesh = new MeshData();
                    output.RoomFloors[room.Hint] = floorMesh;
                }

                Vector3 min = new Vector3(room.Origin.x * cSize, FloorPlinthHeight, room.Origin.y * cSize);
                Vector3 max = new Vector3((room.Origin.x + room.Size.x) * cSize, FloorPlinthHeight, (room.Origin.y + room.Size.y) * cSize);

                // Room floor top quad
                floorMesh.AddQuad(
                    new Vector3(min.x, FloorPlinthHeight, max.z),
                    new Vector3(max.x, FloorPlinthHeight, max.z),
                    new Vector3(max.x, FloorPlinthHeight, min.z),
                    new Vector3(min.x, FloorPlinthHeight, min.z),
                    Vector3.up);
            }

            // 2. Build Room Boundary Wall Spans
            var processedEdges = new HashSet<string>();
            var processedCorners = new HashSet<string>();

            foreach (var room in layout.Rooms)
            {
                Vector3 rMin = new Vector3(room.Origin.x * cSize, 0f, room.Origin.y * cSize);
                Vector3 rMax = new Vector3((room.Origin.x + room.Size.x) * cSize, 0f, (room.Origin.y + room.Size.y) * cSize);

                // 4 Room Corners
                Vector3 cNW = new Vector3(rMin.x, 0f, rMax.z);
                Vector3 cNE = new Vector3(rMax.x, 0f, rMax.z);
                Vector3 cSE = new Vector3(rMax.x, 0f, rMin.z);
                Vector3 cSW = new Vector3(rMin.x, 0f, rMin.z);

                // 4 Sides: North (0), East (1), South (2), West (3)
                BuildRoomSide(output, layout, room, 0, cNW, cNE, processedEdges, processedCorners, t);
                BuildRoomSide(output, layout, room, 1, cNE, cSE, processedEdges, processedCorners, t);
                BuildRoomSide(output, layout, room, 2, cSE, cSW, processedEdges, processedCorners, t);
                BuildRoomSide(output, layout, room, 3, cSW, cNW, processedEdges, processedCorners, t);
            }

            return output;
        }

        private static void BuildRoomSide(
            BuildOutput output,
            HomeLayout layout,
            RoomRecord room,
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

            float wallH = (dir == 2 || dir == 3) && layout.Cutaway ? LowWallHeight : HighWallHeight;

            // Check if there is a window or door in this wall side
            bool hasWindow = false;
            bool hasDoor = false;
            Vector2Int winCell = Vector2Int.zero;
            Vector2Int doorCell = Vector2Int.zero;

            int spanCount = (dir == 0 || dir == 2) ? room.Size.x : room.Size.y;
            for (int i = 0; i < spanCount; i++)
            {
                Vector2Int cellPos = GetSideCellPos(room, dir, i);
                var cell = layout.GetCell(cellPos);
                if (cell != null)
                {
                    if (cell.Windows[dir]) { hasWindow = true; winCell = cellPos; }
                    if (cell.Doors[dir]) { hasDoor = true; doorCell = cellPos; }
                }
            }

            // Room Corner Posts (Only at the real corners!)
            AddCornerPostIfNew(output.EdgeLines, processedCorners, p0, wallH);
            AddCornerPostIfNew(output.EdgeLines, processedCorners, p1, wallH);

            // Exterior Plinth Skirt if boundary
            Vector2Int neighborCell = GetSideCellPos(room, dir, 0) + HomeLayout.DirVec[dir];
            var neighbor = layout.GetCell(neighborCell);
            bool isExterior = (neighbor == null || !neighbor.HasFloor);
            if (isExterior)
            {
                AddPlinthSpan(output.PlinthData, output.EdgeLines, p0, p1, dir, FloorPlinthHeight);
            }

            if (hasWindow)
            {
                BuildWindowedWallSpan(output, p0, p1, dir, t, wallH, winCell, layout);
            }
            else if (hasDoor)
            {
                BuildDoorwayWallSpan(output, p0, p1, dir, t, wallH, doorCell, layout);
            }
            else
            {
                // Single continuous wall span from p0 to p1 (No 4-way slicing!)
                BuildContinuousWallSpan(output.TranslucentWalls, output.EdgeLines, p0, p1, dir, t, wallH);
            }
        }

        private static void BuildContinuousWallSpan(MeshData transWalls, MeshData edges, Vector3 p0, Vector3 p1, int dir, float t, float h)
        {
            Vector3 mid = (p0 + p1) * 0.5f;
            float length = Vector3.Distance(p0, p1);

            // 1. One Unified Glass Wall Panel
            Vector3 wallSize = (dir == 0 || dir == 2)
                ? new Vector3(length, h, t)
                : new Vector3(t, h, length);
            Vector3 wallCenter = new Vector3(mid.x, FloorPlinthHeight + h * 0.5f, mid.z);
            transWalls.AddBox(wallCenter, wallSize);

            // 2. Single Continuous Top Edge Line
            Vector3 topRailSize = (dir == 0 || dir == 2)
                ? new Vector3(length, LineThickness, LineThickness * 1.5f)
                : new Vector3(LineThickness * 1.5f, LineThickness, length);
            Vector3 topRailCenter = new Vector3(mid.x, FloorPlinthHeight + h, mid.z);
            edges.AddBox(topRailCenter, topRailSize);

            // 3. Single Continuous Baseboard Line
            Vector3 baseRailSize = (dir == 0 || dir == 2)
                ? new Vector3(length, LineThickness * 0.7f, LineThickness * 1.2f)
                : new Vector3(LineThickness * 1.2f, LineThickness * 0.7f, length);
            Vector3 baseRailCenter = new Vector3(mid.x, FloorPlinthHeight + LineThickness * 0.35f, mid.z);
            edges.AddBox(baseRailCenter, baseRailSize);
        }

        private static void BuildDoorwayWallSpan(BuildOutput output, Vector3 p0, Vector3 p1, int dir, float t, float h, Vector2Int doorCell, HomeLayout layout)
        {
            Vector3 doorCenter = layout.CellCenter(doorCell, 0f);
            float doorWidth = HomeLayout.CellSize * 1.0f;
            float totalLen = Vector3.Distance(p0, p1);
            Vector3 dirVec = (p1 - p0).normalized;

            // Project door position on the span
            float doorDist = Vector3.Dot(doorCenter - p0, dirVec);
            float dStart = Mathf.Max(0f, doorDist - doorWidth * 0.5f);
            float dEnd = Mathf.Min(totalLen, doorDist + doorWidth * 0.5f);

            // Left Wall Span
            if (dStart > 0.05f)
            {
                Vector3 w0 = p0;
                Vector3 w1 = p0 + dirVec * dStart;
                BuildContinuousWallSpan(output.TranslucentWalls, output.EdgeLines, w0, w1, dir, t, h);
            }

            // Right Wall Span
            if (totalLen - dEnd > 0.05f)
            {
                Vector3 w0 = p0 + dirVec * dEnd;
                Vector3 w1 = p1;
                BuildContinuousWallSpan(output.TranslucentWalls, output.EdgeLines, w0, w1, dir, t, h);
            }

            // Door Portal Frame & Header
            Vector3 dMid = p0 + dirVec * (doorDist);
            float doorH = Mathf.Min(DoorLintelHeight, h);
            float postW = 0.06f;

            // Door Posts
            Vector3 post1 = p0 + dirVec * dStart;
            post1.y = FloorPlinthHeight + doorH * 0.5f;
            Vector3 post2 = p0 + dirVec * dEnd;
            post2.y = FloorPlinthHeight + doorH * 0.5f;

            Vector3 postSize = (dir == 0 || dir == 2) ? new Vector3(postW, doorH, t * 1.15f) : new Vector3(t * 1.15f, doorH, postW);
            output.DoorFrames.AddBox(post1, postSize);
            output.DoorFrames.AddBox(post2, postSize);

            // Door Lintel Beam
            Vector3 lintelCenter = new Vector3(dMid.x, FloorPlinthHeight + doorH, dMid.z);
            Vector3 lintelSize = (dir == 0 || dir == 2) ? new Vector3(doorWidth, LineThickness * 1.2f, t * 1.15f) : new Vector3(t * 1.15f, LineThickness * 1.2f, doorWidth);
            output.DoorFrames.AddBox(lintelCenter, lintelSize);

            // Top Wall above door
            if (h > DoorLintelHeight)
            {
                float topH = h - DoorLintelHeight;
                Vector3 topCenter = new Vector3(dMid.x, FloorPlinthHeight + DoorLintelHeight + topH * 0.5f, dMid.z);
                Vector3 topSize = (dir == 0 || dir == 2) ? new Vector3(doorWidth, topH, t) : new Vector3(t, topH, doorWidth);
                output.TranslucentWalls.AddBox(topCenter, topSize);

                Vector3 topRailCenter = new Vector3(dMid.x, FloorPlinthHeight + h, dMid.z);
                Vector3 topRailSize = (dir == 0 || dir == 2) ? new Vector3(doorWidth, LineThickness, LineThickness * 1.5f) : new Vector3(LineThickness * 1.5f, LineThickness, doorWidth);
                output.EdgeLines.AddBox(topRailCenter, topRailSize);
            }
        }

        private static void BuildWindowedWallSpan(BuildOutput output, Vector3 p0, Vector3 p1, int dir, float t, float h, Vector2Int winCell, HomeLayout layout)
        {
            Vector3 winCenter = layout.CellCenter(winCell, 0f);
            float winWidth = HomeLayout.CellSize * 1.8f; // Wide modern picture window
            float totalLen = Vector3.Distance(p0, p1);
            Vector3 dirVec = (p1 - p0).normalized;

            float winDist = Vector3.Dot(winCenter - p0, dirVec);
            float wStart = Mathf.Max(0f, winDist - winWidth * 0.5f);
            float wEnd = Mathf.Min(totalLen, winDist + winWidth * 0.5f);

            // Left Wall
            if (wStart > 0.05f)
            {
                BuildContinuousWallSpan(output.TranslucentWalls, output.EdgeLines, p0, p0 + dirVec * wStart, dir, t, h);
            }

            // Right Wall
            if (totalLen - wEnd > 0.05f)
            {
                BuildContinuousWallSpan(output.TranslucentWalls, output.EdgeLines, p0 + dirVec * wEnd, p1, dir, t, h);
            }

            // Center Window Section
            Vector3 wMid = p0 + dirVec * winDist;
            float sillH = Mathf.Min(WindowSillHeight, h * 0.8f);

            // Window Sill Wall
            if (sillH > 0.05f)
            {
                Vector3 sillSize = (dir == 0 || dir == 2) ? new Vector3(winWidth, sillH, t) : new Vector3(t, sillH, winWidth);
                Vector3 sillCenter = new Vector3(wMid.x, FloorPlinthHeight + sillH * 0.5f, wMid.z);
                output.TranslucentWalls.AddBox(sillCenter, sillSize);

                Vector3 sillRailSize = (dir == 0 || dir == 2) ? new Vector3(winWidth, LineThickness, LineThickness * 1.4f) : new Vector3(LineThickness * 1.4f, LineThickness, winWidth);
                Vector3 sillRailCenter = new Vector3(wMid.x, FloorPlinthHeight + sillH, wMid.z);
                output.EdgeLines.AddBox(sillRailCenter, sillRailSize);
            }

            // Window Top Wall
            if (h > WindowLintelHeight)
            {
                float topH = h - WindowLintelHeight;
                Vector3 topSize = (dir == 0 || dir == 2) ? new Vector3(winWidth, topH, t) : new Vector3(t, topH, winWidth);
                Vector3 topCenter = new Vector3(wMid.x, FloorPlinthHeight + WindowLintelHeight + topH * 0.5f, wMid.z);
                output.TranslucentWalls.AddBox(topCenter, topSize);

                Vector3 topRailSize = (dir == 0 || dir == 2) ? new Vector3(winWidth, LineThickness, LineThickness * 1.5f) : new Vector3(LineThickness * 1.5f, LineThickness, winWidth);
                Vector3 topRailCenter = new Vector3(wMid.x, FloorPlinthHeight + h, wMid.z);
                output.EdgeLines.AddBox(topRailCenter, topRailSize);
            }

            // Window Frame & Glass
            float glassBottom = FloorPlinthHeight + sillH;
            float glassTop = FloorPlinthHeight + (h > WindowLintelHeight ? WindowLintelHeight : h + 0.3f);
            float glassH = Mathf.Max(0.2f, glassTop - glassBottom);
            Vector3 glassCenter = new Vector3(wMid.x, glassBottom + glassH * 0.5f, wMid.z);

            Vector3 glassSize = (dir == 0 || dir == 2) ? new Vector3(winWidth * 0.96f, glassH, 0.015f) : new Vector3(0.015f, glassH, winWidth * 0.96f);
            output.WindowGlass.AddBox(glassCenter, glassSize);

            Vector3 frameSize = (dir == 0 || dir == 2) ? new Vector3(winWidth * 0.98f, glassH + 0.03f, t * 1.05f) : new Vector3(t * 1.05f, glassH + 0.03f, winWidth * 0.98f);
            output.WindowFrames.AddBox(glassCenter, frameSize);
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

            Vector3 lineSize = (dir == 0 || dir == 2) ? new Vector3(length, 0.015f, 0.035f) : new Vector3(0.035f, 0.015f, length);
            Vector3 linePos = new Vector3(mid.x, height, mid.z) + norm * 0.012f;
            edges.AddBox(linePos, lineSize);
        }

        private static Vector2Int GetSideCellPos(RoomRecord room, int dir, int index)
        {
            return dir switch
            {
                0 => new Vector2Int(room.Origin.x + index, room.Origin.y + room.Size.y - 1), // North
                1 => new Vector2Int(room.Origin.x + room.Size.x - 1, room.Origin.y + (room.Size.y - 1 - index)), // East
                2 => new Vector2Int(room.Origin.x + (room.Size.x - 1 - index), room.Origin.y), // South
                _ => new Vector2Int(room.Origin.x, room.Origin.y + index) // West
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
            else
                return $"{x1},{z1}_{x0},{z0}";
        }
    }
}
