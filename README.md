<p align="center">
    <img src="./logo.png" alt="Logo">
</p>

# Space Hash
Hashing of a bounded 2D/3D space using a regular grid for fast queries of objects within a given radius.

> IMPORTANT! Requires C# 9 (or Unity >= 2021.2).

> IMPORTANT! Use `DEBUG` builds for development and `RELEASE` builds for production: all internal checks/exceptions work only in `DEBUG` builds and are stripped in `RELEASE` builds to maximize performance.

> IMPORTANT! Tested on Unity 2021.3 (does not depend on it) and includes asmdef definitions to compile as separate assemblies and reduce main project recompilation time.


# Social
Official blog: https://leopotam.ru


# Installation


## As a Unity package
Installable as a Unity package via a Git URL in the Package Manager or by editing `Packages/manifest.json` directly:
```
"ru.leopotam.spacehash": "https://gitverse.ru/leopotam/spacehash.git",
```


## As source code
You can clone the repository or download an archive from the releases page.


## Other sources
The official, working version is hosted at [https://gitverse.ru/leopotam/spacehash](https://gitverse.ru/leopotam/spacehash). All other sources (including *nuget*, *npm*, and other repositories) are unofficial clones or third‑party code with unknown content.


# Core types


## SpaceHash2
Represents hashing of a bounded 2D space with a regular grid for fast queries of objects within a given radius.
An instance is created by explicitly specifying the grid step and the space bounds (all objects outside this space will be automatically mapped to border cells, which may degrade performance for queries touching them):
```c#
SpaceHash2<string> spaceHash = new SpaceHash2<string> (
    10f, // Cell size.
    0f, 0f, // Minimum X,Y coordinates of the space.
    100f, 100f); // Maximum X,Y coordinates of the space.
```

> IMPORTANT! The object identifier is defined by the user as the generic type parameter (can be any type). In the examples we use `string`.

Objects are points (no size) and can be added to the hash in any amount:
```c#
// In this case the identifier is a string,
// but it can be a number, an entity id, etc.
spaceHash.Add ("p1", 1f, 1f);
// Add object "p2" at coordinates (15f, 15f).
spaceHash.Add ("p2", 15f, 15f);
// Add object "p3" at coordinates (20f, 20f).
spaceHash.Add ("p3", 20f, 20f);
```

After adding objects, you can query objects within a radius from a given point:
```c#
// Returns all objects within radius 1f from point (20f, 0f),
// sorted by ascending distance. If an object exists at the query
// point itself, it will be ignored (self-excluded).
float x = 20f;
float y = 0f;
float radius = 1f;
bool selfIgnore = true;
List<SpaceHashHit<string>> res = spaceHash.Get (x, y, radius, selfIgnore);
// SpaceHashHit:
// Id  - the object identifier of the chosen generic type (string here).
// DistSqr - squared distance from the query point.
```
> IMPORTANT! It’s recommended to keep the query radius within 1–2 grid-cell sizes. This is not a strict limitation, just a guideline. Choose the grid size for your use case and measure performance and memory consumption.

To reduce allocations, you can reuse the results list by passing it as an optional parameter:
```c#
List<SpaceHashHit<string>> res = spaceHash.Get (20f, 0f, 1f, true);
// Reuse the list without new allocations.
res = spaceHash.Get (20f, 0f, 1f, true, res);
```

If you only need the single closest object, use:
```c#
(SpaceHashHit<string> res, bool ok) = spaceHash.GetOne (20f, 0f, 1f, true);
```
This returns whether the lookup succeeded and the hit info.

If you only need to know whether any object exists within the radius (without finding the closest one), use:
```c#
bool found = spaceHash.Has (20f, 0f, 1f, true);
```

After queries, the hash can be cleared and reused for new inserts and queries:
```c#
spaceHash.Clear ();
```

> IMPORTANT! Filling and clearing the hash are not thread‑safe and must be done from the main thread. Queries are thread‑safe and can be executed from multiple threads.

Search within a ring bounded by inner and outer radii is also supported — methods `HasInRing()`, `GetInRing()`, and `GetOneInRing()` — analogous to the circle queries, with parameters `intRadius` (inner) and `extRadius` (outer).


## SpaceHash3
Represents hashing of a bounded 3D space with a regular grid for fast queries of objects within a given radius.
Functionality is identical to `SpaceHash2` except that ring queries aren’t applicable; coordinate parameters add the third axis (Z).


# License
This package is released under the MIT-ZARYA license, see [details here](./LICENSE.md).
