// ----------------------------------------------------------------------------
// Лицензия MIT-ZARYA
// (c) 2025 Leopotam <leopotam@yandex.ru>
// ----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
#if ENABLE_IL2CPP
using Unity.IL2CPP.CompilerServices;
#endif

namespace Leopotam.SpaceHash {
#if ENABLE_IL2CPP
    [Il2CppSetOption (Option.NullChecks, false)]
    [Il2CppSetOption (Option.ArrayBoundsChecks, false)]
#endif
    public class SpaceHash3<T> {
        protected readonly List<Item>[] _space;
        protected readonly float _invCellSize;
        protected readonly float _minX, _minY, _minZ;
        protected readonly int _spaceX, _spaceY, _spaceZ;

        protected const int DefaultCellCapacity = 64;
        protected readonly List<List<Item>> _pool;
        protected readonly List<List<Item>> _activeLists;

        public SpaceHash3 (float cellSize, float minX, float minY, float minZ, float maxX, float maxY, float maxZ) {
#if DEBUG
            if (cellSize <= 0f) { throw new Exception ("некорректный размер клетки"); }
#endif
            _invCellSize = 1f / cellSize;
            _spaceX = (int) Math.Ceiling ((maxX - minX) * _invCellSize);
            _spaceY = (int) Math.Ceiling ((maxY - minY) * _invCellSize);
            _spaceZ = (int) Math.Ceiling ((maxZ - minZ) * _invCellSize);
#if DEBUG
            if (_spaceX <= 0 || _spaceY <= 0 || _spaceZ <= 0) { throw new Exception ("некорректные параметры пространства"); }
#endif
            (_minX, _minY, _minZ) = (minX, minY, minZ);
            _space = new List<Item>[_spaceX * _spaceY * _spaceZ];
            _pool = new (_space.Length);
            _activeLists = new (_space.Length);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public void Add (T id, float x, float y, float z) {
            var hash = Hash (
                WorldToSpace (x, _minX, _spaceX),
                WorldToSpace (y, _minY, _spaceY),
                WorldToSpace (z, _minZ, _spaceZ));
            var list = _space[hash];
            if (list == null) {
                list = GetList ();
                _space[hash] = list;
                _activeLists.Add (list);
            }
            list.Add (new () { Id = id, X = x, Y = y, Z = z });
        }

        public void Clear () {
            Array.Clear (_space, 0, _space.Length);
            foreach (var list in _activeLists) {
                list.Clear ();
                RecycleList (list);
            }
            _activeLists.Clear ();
        }

        public bool Has (float xPos, float yPos, float zPos, float radius, bool selfIgnore) {
            var minCellX = WorldToSpace (xPos - radius, _minX, _spaceX);
            var minCellY = WorldToSpace (yPos - radius, _minY, _spaceY);
            var minCellZ = WorldToSpace (zPos - radius, _minZ, _spaceZ);
            var maxCellX = WorldToSpace (xPos + radius, _minX, _spaceX);
            var maxCellY = WorldToSpace (yPos + radius, _minY, _spaceY);
            var maxCellZ = WorldToSpace (zPos + radius, _minZ, _spaceZ);
            float xDiff, yDiff, zDiff, distSqr;
            var rSqr = radius * radius;
            for (var z = minCellZ; z <= maxCellZ; z++) {
                for (var y = minCellY; y <= maxCellY; y++) {
                    for (var x = minCellX; x <= maxCellX; x++) {
                        var list = _space[Hash (x, y, z)];
                        if (list != null) {
                            foreach (var item in list) {
                                xDiff = xPos - item.X;
                                yDiff = yPos - item.Y;
                                zDiff = zPos - item.Z;
                                distSqr = xDiff * xDiff + yDiff * yDiff + zDiff * zDiff;
                                if (distSqr <= rSqr) {
                                    if (distSqr < 1e-4f && selfIgnore) {
                                        continue;
                                    }
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }

        public (SpaceHashHit<T> hit, bool ok) GetOne (float xPos, float yPos, float zPos, float radius, bool selfIgnore) {
            var minCellX = WorldToSpace (xPos - radius, _minX, _spaceX);
            var minCellY = WorldToSpace (yPos - radius, _minY, _spaceY);
            var minCellZ = WorldToSpace (zPos - radius, _minZ, _spaceZ);
            var maxCellX = WorldToSpace (xPos + radius, _minX, _spaceX);
            var maxCellY = WorldToSpace (yPos + radius, _minY, _spaceY);
            var maxCellZ = WorldToSpace (zPos + radius, _minZ, _spaceZ);
            float xDiff, yDiff, zDiff, distSqr;
            SpaceHashHit<T> hit;
            hit.Id = default;
            hit.DistSqr = radius * radius;
            var found = false;
            for (var z = minCellZ; z <= maxCellZ; z++) {
                for (var y = minCellY; y <= maxCellY; y++) {
                    for (var x = minCellX; x <= maxCellX; x++) {
                        var list = _space[Hash (x, y, z)];
                        if (list != null) {
                            foreach (var item in list) {
                                xDiff = xPos - item.X;
                                yDiff = yPos - item.Y;
                                zDiff = zPos - item.Z;
                                distSqr = xDiff * xDiff + yDiff * yDiff + zDiff * zDiff;
                                if (distSqr <= hit.DistSqr) {
                                    if (distSqr < 1e-4f && selfIgnore) {
                                        continue;
                                    }
                                    found = true;
                                    hit.DistSqr = distSqr;
                                    hit.Id = item.Id;
                                }
                            }
                        }
                    }
                }
            }
            return (hit, found);
        }

        public List<SpaceHashHit<T>> Get (float xPos, float yPos, float zPos, float radius, bool selfIgnore, List<SpaceHashHit<T>> result = default) {
            if (result == null) {
                result = new (DefaultCellCapacity);
            } else {
                result.Clear ();
            }
            var minCellX = WorldToSpace (xPos - radius, _minX, _spaceX);
            var minCellY = WorldToSpace (yPos - radius, _minY, _spaceY);
            var minCellZ = WorldToSpace (zPos - radius, _minZ, _spaceZ);
            var maxCellX = WorldToSpace (xPos + radius, _minX, _spaceX);
            var maxCellY = WorldToSpace (yPos + radius, _minY, _spaceY);
            var maxCellZ = WorldToSpace (zPos + radius, _minZ, _spaceZ);
            var rSqr = radius * radius;
            float xDiff, yDiff, zDiff, distSqr;
            for (var z = minCellZ; z <= maxCellZ; z++) {
                for (var y = minCellY; y <= maxCellY; y++) {
                    for (var x = minCellX; x <= maxCellX; x++) {
                        var list = _space[Hash (x, y, z)];
                        if (list != null) {
                            foreach (var item in list) {
                                xDiff = xPos - item.X;
                                yDiff = yPos - item.Y;
                                zDiff = zPos - item.Z;
                                distSqr = xDiff * xDiff + yDiff * yDiff + zDiff * zDiff;
                                if (distSqr <= rSqr) {
                                    if (distSqr < 1e-4f && selfIgnore) {
                                        continue;
                                    }
                                    result.Add (new () { Id = item.Id, DistSqr = distSqr });
                                }
                            }
                        }
                    }
                }
            }
            if (result.Count > 1) {
                result.Sort (OnSort);
            }
            return result;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public List<Item>[] Space () => _space;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public int PointHash (float x, float y, float z) {
            var cellX = WorldToSpace (x, _minX, _spaceX);
            var cellY = WorldToSpace (y, _minY, _spaceY);
            var cellZ = WorldToSpace (z, _minZ, _spaceZ);
            return Hash (cellX, cellY, cellZ);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public List<Item> PointSpace (float x, float y, float z) => _space[PointHash (x, y, z)];

        protected static int OnSort (SpaceHashHit<T> x, SpaceHashHit<T> y) => x.DistSqr < y.DistSqr ? -1 : 1;

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        protected List<Item> GetList () {
            var count = _pool.Count;
            if (count > 0) {
                count--;
                var l = _pool[count];
                _pool.RemoveAt (count);
                return l;
            }
            return new (DefaultCellCapacity);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        protected void RecycleList (List<Item> list) {
            _pool.Add (list);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        protected int Hash (int x, int y, int z) {
            return z * _spaceX * _spaceY + y * _spaceX + x;
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        protected int WorldToSpace (float v, float min, int spaceMax) {
            // допустимо ошибочное округление отрицательных чисел
            // без floor(), т.к дальше будет отсечение до нуля.
            var res = (int) ((v - min) * _invCellSize);
            if (res >= spaceMax) {
                res = spaceMax - 1;
            } else {
                if (res < 0) {
                    res = 0;
                }
            }
            return res;
        }

        public struct Item {
            public T Id;
            public float X, Y, Z;
        }
    }
}
