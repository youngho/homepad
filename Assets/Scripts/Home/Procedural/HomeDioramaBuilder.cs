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
        public const float InteriorWallHeight = 1.95f;
        public const float WindowSillHeight = 0.70f;
        public const float WindowLintelHeight = 1.65f;
        public const float DoorLintelHeight = 1.60f;
        public const float FloorPlinthHeight = 0.04f;
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

                AddQuad(c000, c100, c110, c010, Vector3.back);
                AddQuad(c101, c001, c011, c111, Vector3.forward);
                AddQuad(c001, c000, c010, c011, Vector3.left);
                AddQuad(c100, c101, c111, c110, Vector3.right);
                AddQuad(c010, c110, c111, c011, Vector3.up);
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
            Vector3 forward = Camera.main != null ? Camera.main.transform.forward : new Vector3(1f, -1f, 1f);
            return Generate(layout, forward);
        }

        public static BuildOutput Generate(HomeLayout layout, Vector3 cameraForward)
        {
            var output = new BuildOutput();
            if (layout == null || layout.Rooms.Count == 0) return output;

            var cutaway = HomeLayout.CutawayView.FromCamera(cameraForward);
            float cSize = HomeLayout.CellSize;
            float t = WallThickness;

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

            float wallH = GetWallHeight(layout, room, dir, cutaway);

            bool hasWindow = false;
            bool hasDoor = false;
            Vector2Int winCell = Vector2Int.zero;
            Vector2Int doorCell = Vector2Int.zero;

            int spanCount = (dir == 0 || dir == 2) ? room.Size.x : room.Size.y;
            for (int i = 0; i < spanCount; i++)
            {
                Vector2Int cellPos = GetSideCellPos(room, dir, i);
                var cell = layout.GetCell(cellPos);
                if (cell == null) continue;
                if (cell.Windows[dir]) { hasWindow = true; winCell = cellPos; }
                if (cell.Doors[dir]) { hasDoor = true; doorCell = cellPos; }
            }

            AddCornerPostIfNew(output.EdgeLines, processedCorners, p0, wallH);
            AddCornerPostIfNew(output.EdgeLines, processedCorners, p1, wallH);

            Vector2Int neighborCell = GetSideCellPos(room, dir, 0) + HomeLayout.DirVec[dir];
            var neighbor = layout.GetCell(neighborCell);
            bool isExterior = neighbor == null || !neighbor.HasFloor;
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
                BuildContinuousWallSpan(output.TranslucentWalls, output.EdgeLines, p0, p1, dir, t, wallH);
            }
        }

        private static float GetWallHeight(HomeLayout layout, RoomRecord room, int dir, HomeLayout.CutawayView cutaway)
        {
            if (!layout.Cutaway) return HighWallHeight;

            Vector2Int neighborCell = GetSideCellPos(room, dir, 0) + HomeLayout.DirVec[dir];
            var neighbor = layout.GetCell(neighborCell);
            bool interior = neighbor != null && neighbor.HasFloor;
            if (interior) return InteriorWallHeight;

            return cutaway.IsFront(dir) ? LowWallHeight : HighWallHeight;
        }

        private static void BuildContinuousWallSpan(MeshData transWalls, MeshData edges, Vector3 p0, Vector3 p1, int dir, float t, float h)
        {
            Vector3 mid = (p0 + p1) * 0.5f;
            float length = Vector3.Distance(p0, p1);

            AddOrientedBox(transWalls, mid, dir, length, h, t, FloorPlinthHeight);
            AddOrientedBox(edges, mid, dir, length, LineThickness, LineThickness * 1.4f, FloorPlinthHeight + h - LineThickness * 0.5f);
            AddOrientedBox(edges, mid, dir, length, LineThickness * 0.7f, LineThickness, FloorPlinthHeight);
        }

        private static void BuildDoorwayWallSpan(BuildOutput output, Vector3 p0, Vector3 p1, int dir, float t, float h, Vector2Int doorCell, HomeLayout layout)
        {
            Vector3 doorCenter = layout.CellCenter(doorCell, 0f);
            float doorWidth = HomeLayout.CellSize * 0.7f;
            float totalLen = Vector3.Distance(p0, p1);
            Vector3 dirVec = (p1 - p0).normalized;

            float doorDist = Vector3.Dot(doorCenter - p0, dirVec);
            float dStart = Mathf.Max(0f, doorDist - doorWidth * 0.5f);
            float dEnd = Mathf.Min(totalLen, doorDist + doorWidth * 0.5f);

            if (dStart > 0.05f)
            {
                BuildContinuousWallSpan(output.TranslucentWalls, output.EdgeLines, p0, p0 + dirVec * dStart, dir, t, h);
            }

            if (totalLen - dEnd > 0.05f)
            {
                BuildContinuousWallSpan(output.TranslucentWalls, output.EdgeLines, p0 + dirVec * dEnd, p1, dir, t, h);
            }

            Vector3 dMid = p0 + dirVec * doorDist;
            float doorH = Mathf.Min(DoorLintelHeight, Mathf.Max(h, LowWallHeight));
            float postW = 0.06f;

            AddOrientedBox(output.DoorFrames, p0 + dirVec * dStart, dir, postW, doorH, t * 1.15f, FloorPlinthHeight);
            AddOrientedBox(output.DoorFrames, p0 + dirVec * dEnd, dir, postW, doorH, t * 1.15f, FloorPlinthHeight);
            AddOrientedBox(output.DoorFrames, dMid, dir, doorWidth, 0.035f, t * 1.2f, 0f);
            AddOrientedBox(output.DoorFrames, dMid, dir, doorWidth + postW * 2f, 0.05f, t * 1.15f, FloorPlinthHeight + doorH - 0.025f);

            if (h > doorH + 0.02f)
            {
                float topH = h - doorH;
                AddOrientedBox(output.TranslucentWalls, dMid, dir, doorWidth, topH, t, FloorPlinthHeight + doorH);
                AddOrientedBox(output.EdgeLines, dMid, dir, doorWidth, LineThickness, LineThickness * 1.4f, FloorPlinthHeight + h - LineThickness * 0.5f);
            }
        }

        private static void BuildWindowedWallSpan(BuildOutput output, Vector3 p0, Vector3 p1, int dir, float t, float h, Vector2Int winCell, HomeLayout layout)
        {
            Vector3 winCenter = layout.CellCenter(winCell, 0f);
            float winWidth = HomeLayout.CellSize * 1.15f;
            float totalLen = Vector3.Distance(p0, p1);
            Vector3 dirVec = (p1 - p0).normalized;

            float winDist = Vector3.Dot(winCenter - p0, dirVec);
            float wStart = Mathf.Max(0f, winDist - winWidth * 0.5f);
            float wEnd = Mathf.Min(totalLen, winDist + winWidth * 0.5f);

            if (wStart > 0.05f)
            {
                BuildContinuousWallSpan(output.TranslucentWalls, output.EdgeLines, p0, p0 + dirVec * wStart, dir, t, h);
            }

            if (totalLen - wEnd > 0.05f)
            {
                BuildContinuousWallSpan(output.TranslucentWalls, output.EdgeLines, p0 + dirVec * wEnd, p1, dir, t, h);
            }

            Vector3 wMid = p0 + dirVec * winDist;
            float openingTop = h > WindowLintelHeight ? WindowLintelHeight : Mathf.Max(h, LowWallHeight + 0.28f);
            float sillH = Mathf.Min(WindowSillHeight, openingTop * 0.45f);
            if (sillH < 0.08f) sillH = Mathf.Min(0.12f, h * 0.35f);

            if (sillH > 0.05f)
            {
                AddOrientedBox(output.TranslucentWalls, wMid, dir, winWidth, sillH, t, FloorPlinthHeight);
                AddOrientedBox(output.EdgeLines, wMid, dir, winWidth, LineThickness, LineThickness * 1.3f, FloorPlinthHeight + sillH - LineThickness * 0.5f);
            }

            if (h > openingTop + 0.02f)
            {
                float topH = h - openingTop;
                AddOrientedBox(output.TranslucentWalls, wMid, dir, winWidth, topH, t, FloorPlinthHeight + openingTop);
                AddOrientedBox(output.EdgeLines, wMid, dir, winWidth, LineThickness, LineThickness * 1.4f, FloorPlinthHeight + h - LineThickness * 0.5f);
            }

            float glassBottom = FloorPlinthHeight + sillH;
            float glassTop = FloorPlinthHeight + openingTop;
            float glassH = Mathf.Max(0.18f, glassTop - glassBottom);
            float openingW = Mathf.Max(0.35f, winWidth * 0.78f);
            AddOrientedBox(output.WindowGlass, wMid, dir, openingW, glassH, 0.016f, glassBottom);
            AddHollowFrame(output.WindowFrames, wMid, dirVec, dir, winWidth, t * 1.12f, glassBottom, glassBottom + glassH);
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

        private static void AddHollowFrame(MeshData frames, Vector3 mid, Vector3 side, int dir, float length, float thickness, float y0, float y1)
        {
            float h = Mathf.Max(0.12f, y1 - y0);
            float openingW = Mathf.Max(0.3f, length * 0.78f);
            float stile = Mathf.Max(0.04f, (length - openingW) * 0.5f);
            float rail = 0.04f;
            AddOrientedBox(frames, mid - side * ((openingW + stile) * 0.5f), dir, stile, h, thickness, y0);
            AddOrientedBox(frames, mid + side * ((openingW + stile) * 0.5f), dir, stile, h, thickness, y0);
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
