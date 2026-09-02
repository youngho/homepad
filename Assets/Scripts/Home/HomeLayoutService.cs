using System;
using System.Collections.Generic;
using Homepad.Core;
using UnityEngine;

namespace Homepad.Home
{
    public sealed class HomeLayoutService
    {
        private readonly HomeLayout layout;
        private static readonly Vector2Int[] AttachDirs =
        {
            new Vector2Int(0, HomeLayout.RoomSize),
            new Vector2Int(HomeLayout.RoomSize, 0),
            new Vector2Int(0, -HomeLayout.RoomSize),
            new Vector2Int(-HomeLayout.RoomSize, 0)
        };

        public HomeLayout Layout => layout;

        public HomeLayoutService(HomeLayout layout)
        {
            this.layout = layout;
        }

        public RoomRecord EnsureRoom(RoomHint hint)
        {
            var existing = layout.FindRoom(hint);
            if (existing != null) return existing;
            return CreateRoom(hint);
        }

        public RoomRecord CreateRoom(RoomHint hint)
        {
            return CreateRoom(hint, FindAttachOrigin());
        }

        public RoomRecord CreateRoom(RoomHint hint, Vector2Int origin)
        {
            var room = new RoomRecord
            {
                Id = layout.NextRoomId(),
                Hint = hint,
                Origin = origin,
                Size = new Vector2Int(HomeLayout.RoomSize, HomeLayout.RoomSize),
                Name = HomeItemDef.RoomName(hint)
            };
            layout.Rooms.Add(room);
            PaintRoom(room);
            RebuildWalls();
            return room;
        }

        public PlacedItem Place(HomeItemDef def, Vector2Int cell, int wallDir)
        {
            if (def == null) return null;
            if (layout.IsCatalogBlocked(def)) return null;

            RoomRecord room;
            if (def.Kind == HomeItemKind.ElectricCurtain)
            {
                room = layout.RoomAt(cell);
                if (room == null) room = layout.Rooms.Count > 0 ? layout.Rooms[0] : EnsureRoom(def.RoomHint);
            }
            else
            {
                room = EnsureRoom(def.RoomHint);
            }

            if (!IsCellInRoom(cell, room))
            {
                cell = DefaultCell(def, room);
                wallDir = DefaultWallDir(def, room, cell);
            }

            if (def.Kind == HomeItemKind.ElectricCurtain)
            {
                if (!TryPickExteriorWall(cell, ref wallDir, room))
                {
                    if (!TryFindExteriorWall(room, out cell, out wallDir)) return null;
                }

                PunchWindow(cell, wallDir);
            }
            else if (def.Surface == Surface.Wall)
            {
                wallDir = ClampWallDir(cell, wallDir, room);
            }

            var item = new PlacedItem
            {
                InstanceId = Guid.NewGuid().ToString("N").Substring(0, 8),
                CatalogId = def.CatalogId,
                Kind = def.Kind,
                RoomHint = room.Hint,
                Surface = def.Surface,
                DisplayName = MakeDisplayName(def, room),
                Cell = cell,
                WallDir = wallDir,
                DeviceId = 0,
                CurtainOpen = 0f
            };

            BindDevice(item);
            layout.Items.Add(item);
            return item;
        }

        public PlacedItem Restore(PlacedItem saved)
        {
            if (saved == null) return null;
            if (layout.FindRoom(saved.RoomHint) == null)
            {
                CreateRoom(saved.RoomHint);
            }

            if (saved.Kind == HomeItemKind.ElectricCurtain)
            {
                PunchWindow(saved.Cell, saved.WallDir);
            }

            BindDevice(saved);
            layout.Items.Add(saved);
            return saved;
        }

        public void RestoreRoom(RoomRecord room)
        {
            if (room == null || layout.FindRoom(room.Hint) != null) return;
            if (room.Size.x <= 0) room.Size = new Vector2Int(HomeLayout.RoomSize, HomeLayout.RoomSize);
            layout.Rooms.Add(room);
            PaintRoom(room);
        }

        public void RemoveRoomIfEmpty(RoomHint hint)
        {
            var room = layout.FindRoom(hint);
            if (room == null) return;
            for (int i = 0; i < layout.Items.Count; i++)
            {
                if (layout.Items[i].RoomHint == hint) return;
            }

            for (int x = 0; x < room.Size.x; x++)
            {
                for (int y = 0; y < room.Size.y; y++)
                {
                    layout.Cells.Remove(room.Origin + new Vector2Int(x, y));
                }
            }

            layout.Rooms.Remove(room);
            RebuildWalls();
        }

        public bool MoveItem(string instanceId, Vector2Int cell, int wallDir)
        {
            var item = layout.FindItem(instanceId);
            if (item == null) return false;
            var room = layout.FindRoom(item.RoomHint);
            if (room == null) return false;
            if (!IsCellInRoom(cell, room)) return false;

            if (item.Kind == HomeItemKind.ElectricCurtain)
            {
                ClearWindow(item.Cell, item.WallDir);
                if (!TryPickExteriorWall(cell, ref wallDir, room)) return false;
                PunchWindow(cell, wallDir);
            }
            else if (item.Surface == Surface.Wall)
            {
                wallDir = ClampWallDir(cell, wallDir, room);
            }

            item.Cell = cell;
            item.WallDir = wallDir;
            return true;
        }

        public void SetCurtainOpen(string instanceId, float open)
        {
            var item = layout.FindItem(instanceId);
            if (item == null || item.Kind != HomeItemKind.ElectricCurtain) return;
            item.CurtainOpen = Mathf.Clamp01(open);
        }

        public CurtainState GetCurtain(string instanceId)
        {
            var item = layout.FindItem(instanceId);
            if (item == null || item.Kind != HomeItemKind.ElectricCurtain) return null;
            return new CurtainState(item.InstanceId, item.CurtainOpen);
        }

        public bool TrySnapPlacement(HomeItemDef def, Vector3 world, out Vector2Int cell, out int wallDir)
        {
            cell = layout.WorldToCell(world);
            wallDir = 0;
            if (def == null) return false;

            var room = layout.FindRoom(def.RoomHint);
            if (def.Kind == HomeItemKind.ElectricCurtain)
            {
                var hovered = layout.RoomAt(cell);
                if (hovered != null) room = hovered;
            }

            if (room == null) return false;
            if (!IsCellInRoom(cell, room))
            {
                cell = ClampToRoom(cell, room);
            }

            if (def.Kind == HomeItemKind.ElectricCurtain)
            {
                SnapToPerimeter(ref cell, ref wallDir, room);
                return TryPickExteriorWall(cell, ref wallDir, room);
            }

            if (def.Surface == Surface.Wall)
            {
                wallDir = NearestWall(world, cell);
                wallDir = ClampWallDir(cell, wallDir, room);
            }

            return true;
        }

        public Vector2Int DefaultCell(HomeItemDef def, RoomRecord room)
        {
            int cx = room.Origin.x + room.Size.x / 2;
            int cy = room.Origin.y + room.Size.y / 2;
            if (def.Surface == Surface.Wall || def.Kind == HomeItemKind.ElectricCurtain)
            {
                var view = HomeLayout.CutawayView.FromCamera(CameraForward());
                return layout.EdgeCell(room, view.PrimaryBack);
            }

            if (def.Kind == HomeItemKind.Elevator)
            {
                var view = HomeLayout.CutawayView.FromCamera(CameraForward());
                return layout.EdgeCell(room, view.PrimaryFront);
            }

            return new Vector2Int(cx, cy);
        }

        public int DefaultWallDir(HomeItemDef def, RoomRecord room, Vector2Int cell)
        {
            var view = HomeLayout.CutawayView.FromCamera(CameraForward());
            if (def.Kind == HomeItemKind.ElectricCurtain)
            {
                int dir = view.PrimaryBack;
                TryPickExteriorWall(cell, ref dir, room);
                return dir;
            }

            if (def.Surface != Surface.Wall) return 0;
            return ClampWallDir(cell, view.PrimaryBack, room);
        }

        private static Vector3 CameraForward()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.forward : new Vector3(1f, -1f, 1f);
        }

        private void BindDevice(PlacedItem item)
        {
            var manager = WallpadManager.Instance;
            if (manager == null) return;

            switch (item.Kind)
            {
                case HomeItemKind.Light:
                    var light = manager.AddLight(item.DisplayName, HomeItemDef.RoomCode(item.RoomHint));
                    item.DeviceId = light != null ? light.id : 0;
                    break;
                case HomeItemKind.Heating:
                    var heat = manager.AddHeatingRoom(HomeItemDef.RoomName(item.RoomHint), HomeItemDef.RoomCode(item.RoomHint));
                    item.DeviceId = heat != null ? heat.roomId : 0;
                    break;
                default:
                    item.DeviceId = 0;
                    break;
            }
        }

        private string MakeDisplayName(HomeItemDef def, RoomRecord room)
        {
            if (def.Kind == HomeItemKind.Light)
            {
                int count = 1;
                for (int i = 0; i < layout.Items.Count; i++)
                {
                    if (layout.Items[i].Kind == HomeItemKind.Light && layout.Items[i].RoomHint == room.Hint) count++;
                }

                return count <= 1 ? $"{room.Name} 조명" : $"{room.Name} 조명 {count}";
            }

            return def.DisplayName;
        }

        private Vector2Int FindAttachOrigin()
        {
            if (layout.Rooms.Count == 0) return Vector2Int.zero;

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var pair in layout.Cells)
            {
                if (!pair.Value.HasFloor) continue;
                minX = Mathf.Min(minX, pair.Key.x);
                minY = Mathf.Min(minY, pair.Key.y);
                maxX = Mathf.Max(maxX, pair.Key.x);
                maxY = Mathf.Max(maxY, pair.Key.y);
            }

            int step = HomeLayout.RoomSize;
            var candidates = new Vector2Int[]
            {
                new Vector2Int(0, step),
                new Vector2Int(step, 0),
                new Vector2Int(-step, 0),
                new Vector2Int(step, step),
                new Vector2Int(-step, step),
                new Vector2Int(0, -step),
                new Vector2Int(step, -step),
                new Vector2Int(-step, -step),
                new Vector2Int(step * 2, 0),
                new Vector2Int(0, step * 2)
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (!RectOccupied(candidates[i], step)) return candidates[i];
            }

            for (int radius = 1; radius < 12; radius++)
            {
                for (int i = 0; i < AttachDirs.Length; i++)
                {
                    var origin = new Vector2Int(minX, minY) + AttachDirs[i] * radius;
                    if (!RectOccupied(origin, step)) return origin;
                }
            }

            return new Vector2Int(maxX + 1, minY);
        }

        private bool RectOccupied(Vector2Int origin, int size)
        {
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    var cell = layout.GetCell(origin + new Vector2Int(x, y));
                    if (cell != null && cell.HasFloor) return true;
                }
            }

            return false;
        }

        private void PaintRoom(RoomRecord room)
        {
            for (int x = 0; x < room.Size.x; x++)
            {
                for (int y = 0; y < room.Size.y; y++)
                {
                    var pos = room.Origin + new Vector2Int(x, y);
                    var cell = layout.GetOrCreateCell(pos);
                    cell.HasFloor = true;
                    cell.RoomId = room.Id;
                }
            }
        }

        public void RebuildWalls()
        {
            var windows = new List<(Vector2Int pos, int dir)>();
            foreach (var pair in layout.Cells)
            {
                for (int d = 0; d < 4; d++)
                {
                    if (pair.Value.Windows[d]) windows.Add((pair.Key, d));
                }
            }

            foreach (var pair in layout.Cells)
            {
                var cell = pair.Value;
                if (!cell.HasFloor) continue;
                var room = layout.FindRoomById(cell.RoomId);

                for (int d = 0; d < 4; d++)
                {
                    cell.Walls[d] = false;
                    cell.Doors[d] = false;
                    cell.Windows[d] = false;
                    var nbPos = pair.Key + HomeLayout.DirVec[d];
                    var nb = layout.GetCell(nbPos);
                    if (nb == null || !nb.HasFloor)
                    {
                        cell.Walls[d] = true;
                    }
                    else if (nb.RoomId != cell.RoomId)
                    {
                        // Dividing wall between different rooms
                        // Center 1 or 2 cells become a doorway, ends become dividing walls
                        if (room != null)
                        {
                            int coord = (d == 0 || d == 2)
                                ? (cell.Pos.x - room.Origin.x)
                                : (cell.Pos.y - room.Origin.y);

                            if (coord == 1 || coord == 2)
                            {
                                cell.Doors[d] = true;
                            }
                            else
                            {
                                cell.Walls[d] = true;
                            }
                        }
                        else
                        {
                            cell.Doors[d] = true;
                        }
                    }
                }
            }

            for (int i = 0; i < windows.Count; i++)
            {
                PunchWindow(windows[i].pos, windows[i].dir);
            }
        }

        public void PunchWindow(Vector2Int cell, int dir)
        {
            dir = ((dir % 4) + 4) % 4;
            var c = layout.GetOrCreateCell(cell);
            c.Walls[dir] = false;
            c.Doors[dir] = false;
            c.Windows[dir] = true;
        }

        private void ClearWindow(Vector2Int cell, int dir)
        {
            dir = ((dir % 4) + 4) % 4;
            var c = layout.GetCell(cell);
            if (c == null) return;
            c.Windows[dir] = false;
            RebuildWalls();
        }

        private static bool IsCellInRoom(Vector2Int cell, RoomRecord room)
        {
            return cell.x >= room.Origin.x && cell.x < room.Origin.x + room.Size.x
                && cell.y >= room.Origin.y && cell.y < room.Origin.y + room.Size.y;
        }

        private static Vector2Int ClampToRoom(Vector2Int cell, RoomRecord room)
        {
            int x = Mathf.Clamp(cell.x, room.Origin.x, room.Origin.x + room.Size.x - 1);
            int y = Mathf.Clamp(cell.y, room.Origin.y, room.Origin.y + room.Size.y - 1);
            return new Vector2Int(x, y);
        }

        private bool TryPickExteriorWall(Vector2Int cell, ref int wallDir, RoomRecord room)
        {
            var c = layout.GetCell(cell);
            if (c != null)
            {
                wallDir = ((wallDir % 4) + 4) % 4;
                if (c.Walls[wallDir] || c.Windows[wallDir]) return true;
                for (int d = 0; d < 4; d++)
                {
                    if (c.Walls[d] || c.Windows[d])
                    {
                        wallDir = d;
                        return true;
                    }
                }
            }

            return TryFindExteriorWall(room, out _, out wallDir) && IsCellInRoom(cell, room);
        }

        private bool TryFindExteriorWall(RoomRecord room, out Vector2Int cell, out int dir)
        {
            cell = room.Origin;
            dir = (int)WallDir.South;
            for (int x = 0; x < room.Size.x; x++)
            {
                for (int y = 0; y < room.Size.y; y++)
                {
                    var pos = room.Origin + new Vector2Int(x, y);
                    var c = layout.GetCell(pos);
                    if (c == null) continue;
                    for (int d = 0; d < 4; d++)
                    {
                        if (c.Walls[d] || c.Windows[d])
                        {
                            cell = pos;
                            dir = d;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private int ClampWallDir(Vector2Int cell, int wallDir, RoomRecord room)
        {
            wallDir = ((wallDir % 4) + 4) % 4;
            var c = layout.GetCell(cell);
            if (c != null && (c.Walls[wallDir] || c.Windows[wallDir] || c.Doors[wallDir])) return wallDir;

            if (cell.y == room.Origin.y) return (int)WallDir.South;
            if (cell.y == room.Origin.y + room.Size.y - 1) return (int)WallDir.North;
            if (cell.x == room.Origin.x) return (int)WallDir.West;
            if (cell.x == room.Origin.x + room.Size.x - 1) return (int)WallDir.East;
            return wallDir;
        }

        private static void SnapToPerimeter(ref Vector2Int cell, ref int wallDir, RoomRecord room)
        {
            int west = cell.x - room.Origin.x;
            int east = room.Origin.x + room.Size.x - 1 - cell.x;
            int south = cell.y - room.Origin.y;
            int north = room.Origin.y + room.Size.y - 1 - cell.y;
            int min = Mathf.Min(Mathf.Min(west, east), Mathf.Min(south, north));
            if (min == south)
            {
                cell.y = room.Origin.y;
                wallDir = (int)WallDir.South;
            }
            else if (min == north)
            {
                cell.y = room.Origin.y + room.Size.y - 1;
                wallDir = (int)WallDir.North;
            }
            else if (min == west)
            {
                cell.x = room.Origin.x;
                wallDir = (int)WallDir.West;
            }
            else
            {
                cell.x = room.Origin.x + room.Size.x - 1;
                wallDir = (int)WallDir.East;
            }
        }

        private int NearestWall(Vector3 world, Vector2Int cell)
        {
            Vector3 center = layout.CellCenter(cell);
            Vector3 delta = world - center;
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.z))
            {
                return delta.x >= 0f ? (int)WallDir.East : (int)WallDir.West;
            }

            return delta.z >= 0f ? (int)WallDir.North : (int)WallDir.South;
        }
    }
}
