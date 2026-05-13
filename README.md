# Foundation for Gameplay Tags

![License](https://img.shields.io/badge/License-Apache_2.0-blue?style=flat-square)
![Maintained](https://img.shields.io/badge/Maintained%3F-yes-green?style=flat-square)
![Unity](https://img.shields.io/badge/Unity-6%20%2B-black?style=flat-square&logo=unity&logoColor=white)

A lightweight, hierarchy-aware tag system for Unity. Tags are dot-separated names stored as hashed `ulong` values — zero runtime string cost after registration, Burst-safe snapshot APIs included.

-----

## 🛠 Also Available For

[![O3DE](https://img.shields.io/badge/O3DE-25.10%20%2B-%2300AEEF?style=for-the-badge&logo=data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAyNCAyNCI+PHBhdGggZmlsbD0id2hpdGUiIGQ9Ik0xMiAxTDEgNy40djkuMkwxMiAyM2wxMS02LjRWNy40TDEyIDF6bTkuMSAxNC45TDExLjUgMjEuM2wtOC42LTYuNFY4LjFsOC42LTYuNCA5LjEgNi40djYuOHpNMTEuNSA0LjZMMi45IDkuNnY0LjhsOC42IDUuMSA4LjYtNS4xVjkuNmwtOC42LTUuMHoiLz48L3N2Zz4=)](https://github.com/heathen-engineering/O3DE-Foundation-for-GameplayTags)

-----

## Become a GitHub Sponsor
[![Discord](https://img.shields.io/badge/Discord--1877F2?style=social&logo=discord)](https://discord.gg/6X3xrRc)
[![GitHub followers](https://img.shields.io/github/followers/heathen-engineering?style=social)](https://github.com/heathen-engineering?tab=followers)  
Support Heathen by becoming a [GitHub Sponsor](https://github.com/sponsors/heathen-engineering). Sponsorship directly funds the development and maintenance of free tools like this, as well as our game development [Knowledge Base](https://heathen.group/) and community on [Discord](https://discord.gg/6X3xrRc).

Sponsors also get access to our private SourceRepo, which includes developer tools for O3DE, Unreal, Unity, and Godot.  
Learn more or explore other ways to support @ [heathen.group/kb](https://heathen.group/kb/do-more/)

-----

## What it does

Foundation for Gameplay Tags gives you a structured, hierarchy-aware tag system built on three core types:

| Type | Purpose |
|------|---------|
| **`GameplayTag`** | A single named tag stored as a `ulong` xxHash3 value |
| **`GameplayTagCollection`** | A container of tags with optional numeric stack values and set operations |
| **`GameplayTagRegistry`** | Static registry that tracks parent-child relationships between tags |

Tags follow a dot-separated hierarchy. Registering `"Effects.Buff.Strength"` automatically makes it a descendant of both `"Effects.Buff"` and `"Effects"`. Hierarchy lookups are O(1) hash-set operations at runtime — no string comparisons in hot paths.

The following features are included:

- **Registry** — Tag databases as `ScriptableObject` assets, auto-discovered or explicitly pinned. Editor-time registration via `[InitializeOnLoad]`; runtime registration via `[RuntimeInitializeOnLoadMethod]`.
- **Collections** — Per-entity tag containers with stack-count semantics (`AddTag`, `RemoveTag`, `Apply` arithmetic). Serialize cleanly via `ISerializationCallbackReceiver`.
- **Conditions** — `GameplayTagCondition` with full numeric comparison operators (`Exists`, `Equal`, `Less`, `Greater`, etc.) and AND/OR/XOR logic chains via `EvaluateAll`.
- **Operations** — `GameplayTagOperation` bundles a tag, arithmetic, value, and optional condition list so operations can be stored as data and evaluated at runtime.
- **Burst support** — `GetSnapshot(Allocator)` and `GetNativeDescendantsMap(Allocator)` return caller-owned `NativeHashMap` / `NativeDescendantsMap` structs safe to pass into Burst-compiled jobs.
- **Project Settings panel** — Manage all tag databases from `Project Settings > Gameplay Tags`. Unified tree view with inline rename (hierarchical), delete with confirmation, filter, and drag-drop pinning.

---

## Requirements

- Unity **6000.0** or compatible
- `com.unity.collections` **2.6.2**
- `com.unity.mathematics` **1.2.5**

---

## Installation

### Via Unity Package Manager (UPM)

1. In Unity, go to `Window > Package Manager`.
2. Click **+** > **Add package from git URL**.
3. Enter:
   ```
   https://github.com/heathen-engineering/Unity-GameplayTags-Foundation.git?path=/com.heathen.gameplaytagsfoundation
   ```

Dependencies (`com.unity.collections` and `com.unity.mathematics`) are resolved automatically by UPM.

-----

## Setup & Workflow

### 1. Create a Tag Database

Open **Project Settings > Gameplay Tags**. Click **New** in the database toolbar to create a `GameplayTagsData` asset and pin it to your settings. Multiple databases are supported; all are merged into the registry at load time.

### 2. Define Tags

With a database selected (green tick), type a dot-path in the **New Tag** field and click **Add Tag** (or press Enter). Examples:

```
Effects.Buff.Strength
Effects.Buff.Speed
Effects.Debuff.Slow
Status.Burning
Status.Frozen
```

Intermediate nodes (`Effects`, `Effects.Buff`, etc.) are implied automatically — you only need to register leaf tags.

### 3. Use Tags in Code

```csharp
using Heathen.GameplayTags;

// Look up a tag by name (returns invalid tag if not registered)
GameplayTag buffStrength = GameplayTag.FromName("Effects.Buff.Strength");

// Hierarchy checks
GameplayTag effects = GameplayTag.FromName("Effects");
bool isDescendant = effects.IsAncestorOf(buffStrength); // true

// Work with a collection
var active = new GameplayTagCollection();
active.AddTag(buffStrength);
active.AddTag(GameplayTag.FromName("Status.Burning"));

// Presence check — exactMatch: false includes descendants
bool anyBuff = active.Contains(GameplayTag.FromName("Effects.Buff"), false); // true

// Stack-count arithmetic
active.Apply(buffStrength, GameplayTagArithmetic.Add, 2); // stack = 3
ulong stacks = active.GetValue(buffStrength);            // 3
```

### 4. Conditions and Operations

```csharp
// Evaluate a condition list (AND > OR > XOR precedence)
var conditions = new List<GameplayTagCondition>
{
    new() { Tag = GameplayTag.FromName("Status.Burning"), Comparison = GameplayTagComparisonOp.Exists },
    new() { Tag = GameplayTag.FromName("Effects.Buff"),   Comparison = GameplayTagComparisonOp.Exists,
            LogicOp = GameplayTagLogicOp.Or },
};
bool result = GameplayTagCondition.EvaluateAll(conditions, active);

// An operation bundles tag + arithmetic + value + optional conditions
var op = new GameplayTagOperation
{
    Tag        = GameplayTag.FromName("Effects.Buff.Speed"),
    Arithmetic = GameplayTagArithmetic.Add,
    Value      = 1,
};
op.Apply(active); // no-op if conditions fail; returns bool
```

### 5. Burst Jobs

```csharp
using Unity.Collections;

// Caller-owned snapshot for read-only job access
NativeHashMap<ulong, ulong> snapshot = active.GetSnapshot(Allocator.TempJob);

// Caller-owned CSR descendants map
NativeDescendantsMap descMap = GameplayTagRegistry.GetNativeDescendantsMap(Allocator.TempJob);

// ... schedule jobs, then:
snapshot.Dispose();
descMap.Dispose();
```

-----

## API Reference

### `GameplayTag`

| Member | Description |
|--------|-------------|
| `GameplayTag.FromName(name)` | Look up a registered tag by dot-path name |
| `GameplayTag.FromId(id)` | Wrap a stored `ulong` hash directly |
| `Id` | The underlying `ulong` xxHash3 value |
| `IsValid` | `true` if `Id != 0` |
| `IsAncestorOf(other)` | `true` if this tag is an ancestor of `other` in the registry |
| `GetDescendants()` | All registered descendants as `IEnumerable<ulong>` |

### `GameplayTagRegistry`

| Member | Description |
|--------|-------------|
| `RegisterDefaults(data)` | Merge a `GameplayTagsData` asset into the registry |
| `UnregisterDefaults(data)` | Remove a database and rebuild (caller must re-register remaining assets) |
| `Hash(dotPath)` | Hash a string to `ulong` (xxHash3) without registering |
| `IsRegistered(id)` | Check whether an id has been registered |
| `IsAncestor(ancestorId, candidateId)` | Hierarchy check by `ulong` ids |
| `GetDescendants(ancestorId)` | All registered descendant ids |
| `GetAllIds()` | Every registered id |
| `GetAllNames()` | Every registered dot-path name |
| `GetName(id)` | Reverse-lookup a name from an id |
| `ValidateTag(dotPath)` | Validate format without registering |
| `GetRegisteredIds(allocator)` | Burst-safe `NativeHashMap<ulong, bool>`; caller disposes |
| `GetNativeDescendantsMap(allocator)` | Burst-safe CSR descendants map; caller disposes |

### `GameplayTagCollection`

| Member | Description |
|--------|-------------|
| `AddTag(tag)` | Add a tag (initial stack value 1) |
| `RemoveTag(tag)` | Remove a tag entirely |
| `Apply(tag, arithmetic, value)` | Arithmetic on a tag's stack value; tags reaching 0 are pruned |
| `GetValue(tag)` | Returns the stack count (0 if absent) |
| `Contains(tag, exactMatch)` | Presence check; `false` includes descendants |
| `ContainsAll(other, exactMatch)` | All tags in `other` must be present |
| `ContainsAny(other, exactMatch)` | At least one tag from `other` must be present |
| `ContainsNone(other, exactMatch)` | No tags from `other` may be present |
| `GetMatchingTags(tag, exactMatch)` | Tags matching or descending from `tag` |
| `GetExcludingTags(tag, exactMatch)` | Tags that do not match `tag` |
| `GetShared(other, exactMatch)` | Tags present in both collections |
| `GetExclusive(other, exactMatch)` | Tags present in one collection but not both |
| `Subscribe(tag, callback, exactMatch)` | Receive `(GameplayTag, oldValue, newValue)` on change |
| `Unsubscribe(tag, callback)` | Remove a change subscription |
| `GetSnapshot(allocator)` | Burst-safe `NativeHashMap<ulong, ulong>`; caller disposes |
| `Changed` | `event Action<GameplayTagCollection>` fired on any mutation |

`GameplayTagArithmetic` values: `Set`, `Add`, `Sub`, `Mul`, `Div`, `Min`, `Max`

### `GameplayTagCondition`

| Field | Description |
|-------|-------------|
| `Tag` | The tag to test |
| `Comparison` | `Exists`, `NotExists`, `Equal`, `NotEqual`, `Less`, `LessEqual`, `Greater`, `GreaterEqual` |
| `CompareValue` | Right-hand side for numeric comparisons (ignored for `Exists`/`NotExists`) |
| `ExactMatch` | `false` tests against the max value across tag + descendants |
| `LogicOp` | `And`, `Or`, `Xor` — combines with the previous condition |
| `Evaluate(collection)` | Evaluate this single condition |
| `EvaluateAll(conditions, collection)` *(static)* | AND > OR > XOR three-phase reduction |

### `GameplayTagOperation`

| Field/Member | Description |
|--------------|-------------|
| `Tag` | The tag to modify |
| `Arithmetic` | Operation to apply (`GameplayTagArithmetic`) |
| `Value` | Operand value |
| `Conditions` | Optional list of `GameplayTagCondition`; all must pass for the operation to apply |
| `ShouldApply(collection)` | `true` if all conditions pass |
| `Apply(collection)` | Applies to `collection` if conditions pass; returns `true` if applied |

-----

## Namespaces

| Namespace | Contents |
|-----------|----------|
| `Heathen.GameplayTags` | All runtime types: `GameplayTag`, `GameplayTagCollection`, `GameplayTagRegistry`, conditions, operations, enums |
| `Heathen.GameplayTags.Editor` | Editor-only: `GameplayTagsSettings`, `GameplayTagsSettingsProvider`, and all custom drawers |
