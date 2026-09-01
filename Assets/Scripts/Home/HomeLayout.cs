using System;
using System.Collections.Generic;
using UnityEngine;

namespace Homepad.Home
{
    [Serializable]
    public class HomeCell
    {
        public Vector2Int Pos;
        public bool HasFloor;
        public int RoomId = -1;
        public bool[] Walls = new bool[4];
        public bool[] Windows = new bool[4];
        public bool[] Doors = new bool[4];
    }

    [Serializable]
    public class RoomRecord
    {
        public int Id;
        public RoomHint Hint;
        public Vector2Int Origin;
        public Vector2Int Size;
        public string Name;
    }

    [Serializable]
    public class PlacedItem
    {
        public string InstanceId;
        public string CatalogId;
        public HomeItemKind Kind;
        public RoomHint RoomHint;
        public Surface Surface;
        public string DisplayName;
        public Vector2Int Cell;
        public int WallDir;
        public int DeviceId;
        public float CurtainOpen;
    }

    public sealed class HomeLayout
    {
        public const int RoomSize = 4;
        public const float CellSize = 1.6f;
        public const float WallHeight = 2.4f;
        public const float WallThickness = 0.08f;

        public static readonly Vector2Int[] DirVec =
        {
            new Vector2Int(0, 1),
            new Vector2Int(1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0)
        };

        public readonly Dictionary<Vector2Int, HomeCell> Cells = new Dictionary<Vector2Int, HomeCell>();
        public readonly List<RoomRecord> Rooms = new List<RoomRecord>();
        public readonly List<PlacedItem> Items = new List<PlacedItem>();
        public bool Cutaway = true;

        public static int Opposite(int dir) => (dir + 2) % 4;

        public HomeCell GetCell(Vector2Int pos)
        {
            Cells.TryGetValue(pos, out var cell);
            return cell;
        }

        public HomeCell GetOrCreateCell(Vector2Int pos)
        {
            if (Cells.TryGetValue(pos, out var cell)) return cell;
            cell = new HomeCell { Pos = pos };
            Cells[pos] = cell;
            return cell;
        }

        public RoomRecord FindRoom(RoomHint hint)
        {
            for (int i = 0; i < Rooms.Count; i++)
            {
                if (Rooms[i].Hint == hint) return Rooms[i];
            }

            return null;
        }

        public RoomRecord FindRoomById(int id)
        {
            for (int i = 0; i < Rooms.Count; i++)
            {
                if (Rooms[i].Id == id) return Rooms[i];
            }

            return null;
        }

        public RoomRecord RoomAt(Vector2Int cell)
        {
            var c = GetCell(cell);
            if (c == null || !c.HasFloor) return null;
            return FindRoomById(c.RoomId);
        }

        public PlacedItem FindItem(string instanceId)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].InstanceId == instanceId) return Items[i];
            }

            return null;
        }

        public bool HasSingleton(HomeItemKind kind, RoomHint hint)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                if (item.Kind != kind) continue;
                if (kind == HomeItemKind.Heating) return item.RoomHint == hint || HasHeatingInHint(hint);
                if (kind == HomeItemKind.Gas || kind == HomeItemKind.Vent || kind == HomeItemKind.Elevator)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasHeatingInHint(RoomHint hint)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].Kind == HomeItemKind.Heating && Items[i].RoomHint == hint) return true;
            }

            return false;
        }

        public bool IsCatalogBlocked(HomeItemDef def)
        {
            if (def == null || !def.Singleton) return false;
            if (def.Kind == HomeItemKind.Heating) return HasHeatingInHint(def.RoomHint);
            return HasSingleton(def.Kind, def.RoomHint);
        }

        public int NextRoomId()
        {
            int id = 1;
            for (int i = 0; i < Rooms.Count; i++)
            {
                if (Rooms[i].Id >= id) id = Rooms[i].Id + 1;
            }

            return id;
        }

        public Vector3 CellCenter(Vector2Int cell, float y = 0f)
        {
            return new Vector3((cell.x + 0.5f) * CellSize, y, (cell.y + 0.5f) * CellSize);
        }

        public Vector2Int WorldToCell(Vector3 world)
        {
            int x = Mathf.FloorToInt(world.x / CellSize);
            int y = Mathf.FloorToInt(world.z / CellSize);
            return new Vector2Int(x, y);
        }

        public Vector3 WallCenter(Vector2Int cell, int dir, float y)
        {
            Vector3 c = CellCenter(cell, y);
            float h = CellSize * 0.5f - WallThickness * 0.5f;
            Vector2Int v = DirVec[dir];
            return c + new Vector3(v.x * h, 0f, v.y * h);
        }

        public bool TryGetBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (var pair in Cells)
            {
                if (!pair.Value.HasFloor) continue;
                Vector3 p = CellCenter(pair.Key, WallHeight * 0.5f);
                if (!any)
                {
                    bounds = new Bounds(p, new Vector3(CellSize, WallHeight, CellSize));
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(new Bounds(p, new Vector3(CellSize, WallHeight, CellSize)));
                }
            }

            return any;
        }
    }

    [Serializable]
    public class HomeSaveData
    {
        public bool cutaway = true;
        public List<RoomSave> rooms = new List<RoomSave>();
        public List<ItemSave> items = new List<ItemSave>();
    }

    [Serializable]
    public class RoomSave
    {
        public int id;
        public int hint;
        public int ox;
        public int oy;
        public int sx;
        public int sy;
        public string name;
    }

    [Serializable]
    public class ItemSave
    {
        public string instanceId;
        public string catalogId;
        public int kind;
        public int roomHint;
        public int surface;
        public string displayName;
        public int cx;
        public int cy;
        public int wallDir;
        public int deviceId;
        public float curtainOpen;
    }
}
