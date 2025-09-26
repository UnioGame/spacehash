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
    public class SpaceHash2<T> {
        protected readonly List<Item>[] _space;
        protected readonly float _invCellSize;
        protected readonly float _minX, _minY;
        protected readonly int _spaceX, _spaceY;

        protected const int DefaultCellCapacity = 64;
        protected readonly List<List<Item>> _pool;
        protected readonly List<List<Item>> _activeLists;

        public SpaceHash2 (float cellSize, float minX, float minY, float maxX, float maxY) {
#if DEBUG
            if (cellSize <= 0f) { throw new Exception ("некорректный размер клетки"); }
#endif
            _invCellSize = 1f / cellSize;
            _spaceX = (int) Math.Ceiling ((maxX - minX) * _invCellSize);
            _spaceY = (int) Math.Ceiling ((maxY - minY) * _invCellSize);
#if DEBUG
            if (_spaceX <= 0 || _spaceY <= 0) { throw new Exception ("некорректные параметры пространства"); }
#endif
            (_minX, _minY) = (minX, minY);
            _space = new List<Item>[_spaceX * _spaceY];
            _pool = new (_space.Length);
            _activeLists = new (_space.Length);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public void Add (T id, float x, float y) {
            var hash = Hash (
                WorldToSpace (x, _minX, _spaceX),
                WorldToSpace (y, _minY, _spaceY));
            var list = _space[hash];
            if (list == null) {
                list = GetList ();
                _space[hash] = list;
                _activeLists.Add (list);
            }
            list.Add (new () { Id = id, X = x, Y = y });
        }

        public void Clear () {
            Array.Clear (_space, 0, _space.Length);
            foreach (var list in _activeLists) {
                list.Clear ();
                RecycleList (list);
            }
            _activeLists.Clear ();
        }

        public bool Has (float xPos, float yPos, float radius, bool selfIgnore) {
            var minCellX = WorldToSpace (xPos - radius, _minX, _spaceX);
            var minCellY = WorldToSpace (yPos - radius, _minY, _spaceY);
            var maxCellX = WorldToSpace (xPos + radius, _minX, _spaceX);
            var maxCellY = WorldToSpace (yPos + radius, _minY, _spaceY);
            float xDiff, yDiff, distSqr;
            var rSqr = radius * radius;
            for (var y = minCellY; y <= maxCellY; y++) {
                for (var x = minCellX; x <= maxCellX; x++) {
                    var list = _space[Hash (x, y)];
                    if (list != null) {
                        foreach (var item in list) {
                            xDiff = xPos - item.X;
                            yDiff = yPos - item.Y;
                            distSqr = xDiff * xDiff + yDiff * yDiff;
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
            return false;
        }

        public bool HasInRing (float xPos, float yPos, float intRadius, float extRadius, bool selfIgnore) {
            var minCellX = WorldToSpace (xPos - extRadius, _minX, _spaceX);
            var minCellY = WorldToSpace (yPos - extRadius, _minY, _spaceY);
            var maxCellX = WorldToSpace (xPos + extRadius, _minX, _spaceX);
            var maxCellY = WorldToSpace (yPos + extRadius, _minY, _spaceY);
            float xDiff, yDiff, distSqr;
            var intRadSqr = intRadius * intRadius;
            var extRadSqr = extRadius * extRadius;
            for (var y = minCellY; y <= maxCellY; y++) {
                for (var x = minCellX; x <= maxCellX; x++) {
                    var list = _space[Hash (x, y)];
                    if (list != null) {
                        foreach (var item in list) {
                            xDiff = xPos - item.X;
                            yDiff = yPos - item.Y;
                            distSqr = xDiff * xDiff + yDiff * yDiff;
                            if (distSqr <= extRadSqr && distSqr >= intRadSqr) {
                                if (distSqr < 1e-4f && selfIgnore) {
                                    continue;
                                }
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        public (SpaceHashHit<T> hit, bool ok) GetOne (float xPos, float yPos, float radius, bool selfIgnore) {
            var minCellX = WorldToSpace (xPos - radius, _minX, _spaceX);
            var minCellY = WorldToSpace (yPos - radius, _minY, _spaceY);
            var maxCellX = WorldToSpace (xPos + radius, _minX, _spaceX);
            var maxCellY = WorldToSpace (yPos + radius, _minY, _spaceY);
            float xDiff, yDiff, distSqr;
            SpaceHashHit<T> hit;
            hit.Id = default;
            hit.DistSqr = radius * radius;
            var found = false;
            for (var y = minCellY; y <= maxCellY; y++) {
                for (var x = minCellX; x <= maxCellX; x++) {
                    var list = _space[Hash (x, y)];
                    if (list != null) {
                        foreach (var item in list) {
                            xDiff = xPos - item.X;
                            yDiff = yPos - item.Y;
                            distSqr = xDiff * xDiff + yDiff * yDiff;
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
            return (hit, found);
        }

        public (SpaceHashHit<T> hit, bool ok) GetOneInRing (float xPos, float yPos, float intRadius, float extRadius, bool selfIgnore) {
            var minCellX = WorldToSpace (xPos - extRadius, _minX, _spaceX);
            var minCellY = WorldToSpace (yPos - extRadius, _minY, _spaceY);
            var maxCellX = WorldToSpace (xPos + extRadius, _minX, _spaceX);
            var maxCellY = WorldToSpace (yPos + extRadius, _minY, _spaceY);
            float xDiff, yDiff, distSqr;
            SpaceHashHit<T> hit;
            hit.Id = default;
            var intRadSqr = intRadius * intRadius;
            hit.DistSqr = extRadius * extRadius;
            var found = false;
            for (var y = minCellY; y <= maxCellY; y++) {
                for (var x = minCellX; x <= maxCellX; x++) {
                    var list = _space[Hash (x, y)];
                    if (list != null) {
                        foreach (var item in list) {
                            xDiff = xPos - item.X;
                            yDiff = yPos - item.Y;
                            distSqr = xDiff * xDiff + yDiff * yDiff;
                            if (distSqr <= hit.DistSqr && distSqr >= intRadSqr) {
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
            return (hit, found);
        }

        public List<SpaceHashHit<T>> Get (float xPos, float yPos, float radius, bool selfIgnore, List<SpaceHashHit<T>> result = default) {
            if (result == null) {
                result = new (DefaultCellCapacity);
            } else {
                result.Clear ();
            }
            var minCellX = WorldToSpace (xPos - radius, _minX, _spaceX);
            var minCellY = WorldToSpace (yPos - radius, _minY, _spaceY);
            var maxCellX = WorldToSpace (xPos + radius, _minX, _spaceX);
            var maxCellY = WorldToSpace (yPos + radius, _minY, _spaceY);
            var rSqr = radius * radius;
            float xDiff, yDiff, distSqr;
            for (var y = minCellY; y <= maxCellY; y++) {
                for (var x = minCellX; x <= maxCellX; x++) {
                    var list = _space[Hash (x, y)];
                    if (list != null) {
                        foreach (var item in list) {
                            xDiff = xPos - item.X;
                            yDiff = yPos - item.Y;
                            distSqr = xDiff * xDiff + yDiff * yDiff;
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
            if (result.Count > 1) {
                result.Sort (OnSort);
            }
            return result;
        }

        public List<SpaceHashHit<T>> GetInRing (float xPos, float yPos, float intRadius, float extRadius, bool selfIgnore, List<SpaceHashHit<T>> result = default) {
            if (result == null) {
                result = new (DefaultCellCapacity);
            } else {
                result.Clear ();
            }
            var minCellX = WorldToSpace (xPos - extRadius, _minX, _spaceX);
            var minCellY = WorldToSpace (yPos - extRadius, _minY, _spaceY);
            var maxCellX = WorldToSpace (xPos + extRadius, _minX, _spaceX);
            var maxCellY = WorldToSpace (yPos + extRadius, _minY, _spaceY);
            var intRadSqr = intRadius * intRadius;
            var extRadSqr = extRadius * extRadius;
            float xDiff, yDiff, distSqr;
            for (var y = minCellY; y <= maxCellY; y++) {
                for (var x = minCellX; x <= maxCellX; x++) {
                    var list = _space[Hash (x, y)];
                    if (list != null) {
                        foreach (var item in list) {
                            xDiff = xPos - item.X;
                            yDiff = yPos - item.Y;
                            distSqr = xDiff * xDiff + yDiff * yDiff;
                            if (distSqr <= extRadSqr && distSqr >= intRadSqr) {
                                if (distSqr < 1e-4f && selfIgnore) {
                                    continue;
                                }
                                result.Add (new () { Id = item.Id, DistSqr = distSqr });
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
        public int PointHash (float x, float y) {
            var cellX = WorldToSpace (x, _minX, _spaceX);
            var cellY = WorldToSpace (y, _minY, _spaceY);
            return Hash (cellX, cellY);
        }

        [MethodImpl (MethodImplOptions.AggressiveInlining)]
        public List<Item> PointSpace (float x, float y) => _space[PointHash (x, y)];

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
        protected int Hash (int x, int y) {
            return y * _spaceX + x;
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
            public float X, Y;
        }
    }

    public struct SpaceHashHit<T> {
        public T Id;
        public float DistSqr;
    }
}
