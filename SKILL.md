---
name: heathen-gameplaytags-unity-foundation
description: Orientation for an agent working with Heathen's GameplayTags Foundation for Unity — a dot-path hierarchical tag system with O(1) hierarchy lookups and Burst-safe snapshot APIs.
---

# GameplayTags Foundation (Unity)

A lightweight, hierarchy-aware tag system. Tags are dot-separated names (`"Effects.Buff.Strength"`)
stored as hashed `ulong` values (xxHash3) — zero runtime string cost after registration. Hierarchy
is inferred from path segments and compiled to interval (`Lft`/`Rgt`) ranges, so ancestor checks
are O(1) range tests instead of transitive-closure lookups. Includes editor tooling (a
`Project Settings > Gameplay Tags` panel for managing tag databases) and Burst-safe snapshot/
interval-map APIs for use inside jobs.

## Tier

**Foundation** — free, open source (Apache 2.0), this whole repo. The paid **Toolkit** tier
(`Heathen.GameplayTagsToolkit`: an Inspector-driven tag collection component, a condition-set
asset, and an event-trigger/display pair) lives at
`Unity/ToolkitSource/Assets/Toolkits/com.heathen.gameplaytagstoolkit/` inside Heathen's private
`SourceRepo` (sponsor/enterprise access only — see that repo's own `SKILL.md` if you have access).
This Foundation is a complete, independent tag system on its own; the Toolkit only adds
higher-level, no-code components on top of it.

## Up

[`github.com/heathen-engineering/SourceRepo/SKILL.md`](https://github.com/heathen-engineering/SourceRepo/blob/main/SKILL.md)
— the ecosystem-level guide (what Heathen Engineering is, how products/tiers are organized across
engines). This repo has no local engine-level `SKILL.md` to link to instead, since it's a
standalone single-product repo, not part of that larger source tree.

## Key namespaces / entry points

Namespace: `Heathen.GameplayTags` (runtime), `Heathen.GameplayTags.Editor` (editor-only: settings
provider, drawers, `ITagSource`/`ITagVocabulary`).

| Type | Purpose |
| :--- | :--- |
| `GameplayTag` (`readonly struct`, wraps `ulong`) | A single tag. `FromName(dotPath)` / `FromId(ulong)`; implicit conversions `string ↔ GameplayTag ↔ ulong`; `IsAncestorOf(other)`, `GetDescendants()`. |
| `GameplayTagRegistry` (static class) | Source of truth for what tags exist. `RegisterDefaults`/`UnregisterDefaults`, `Hash`, `IsAncestor`, `GetDescendants`, `GetAllIds`/`GetAllNames`, `TryGetInterval`, `IntervalGeneration`, `GetNativeIntervalMap(Allocator)` (Burst-safe). |
| `GameplayTagCollection` (managed class, `ISerializationCallbackReceiver`) | Per-entity tag container with stack-count semantics: `AddTag`/`RemoveTag`/`Apply(tag, arithmetic, value)`, `Contains`/`ContainsAll`/`ContainsAny`/`ContainsNone`, `Subscribe`/`Unsubscribe`, `GetSnapshot(Allocator)` (Burst-safe), `Changed` event. |
| `GameplayTagCondition` | A single tag comparison (`Exists`/`NotExists`/`Equal`/`Less`/`Greater`/etc.) plus `LogicOp` (`And`/`Or`/`Xor`) for chaining. `EvaluateAll(conditions, collection)` does AND > OR > XOR precedence reduction. |
| `GameplayTagOperation` | Bundles a tag + `GameplayTagArithmetic` + value + optional `GameplayTagCondition` list so a mutation can be stored as data. `ShouldApply`/`Apply(collection)`. |
| `CompiledTagEntry`, `NativeDescendantsMap`, `NativeIntervalMap` | Supporting types for the interval-encoded hierarchy and Burst job access (`NativeDescendantsMap` is obsolete — prefer interval range tests via `NativeIntervalMap`). |
| `GameplayTagArithmetic` (enum) | `Set`, `Add`, `Sub`, `Mul`, `Div`, `Min`, `Max`. |
| `GameplayTagComparisonOp` / `GameplayTagLogicOp` (enums) | Condition comparisons / chain logic. |
| `GameplayTagsSubsystem` (`[Subsystem(SubsystemScope.Global)]`) | Owns registry-reset timing at framework boot (Game-Framework Subsystem, not a `MonoBehaviour`). |
| `ITagSource` / `ITagVocabulary` (Editor) | Provenance/autocomplete interfaces other tools implement to surface their own authored tags in the Gameplay Tags settings panel. |

Tag databases are `GameplayTagsData` `ScriptableObject` assets, authored/managed from
`Project Settings > Gameplay Tags` (multiple databases merge into the registry at load).

## Dependencies

- `com.unity.collections` `2.6.2`, `com.unity.mathematics` `1.2.5`, `com.unity.nuget.newtonsoft-json`
  `3.2.1` — real UPM packages, listed in `package.json`, resolved automatically.
- `Heathen.GameFramework` (`Unity-Game-Framework` repo) — **not** in `package.json`. It's a hard
  `.asmdef` reference (`Heathen.GameplayTags.asmdef` → `Heathen.GameFramework`), the same
  non-UPM dependency-guarding pattern used elsewhere in Heathen's Unity products: install
  `Unity-Game-Framework` alongside this package, don't expect it in the manifest. It supplies the
  `Subsystem`/`ISubsystemDebug` base for `GameplayTagsSubsystem`.
- Unity's own `xxHash3` (`Unity.Collections.xxHash3.Hash64`, built into `com.unity.collections`) —
  no separate xxHash dependency needed on this engine (O3DE and Godot ports each vendor their own).

## Common tasks

- **Register/author tags**: `Project Settings > Gameplay Tags` — create a `GameplayTagsData`
  asset, type a dot-path (e.g. `Effects.Buff.Strength`) into the New Tag field. Intermediate
  segments are implied automatically.
- **Look up / compare tags in code**: `GameplayTag.FromName(...)`, implicit `string`/`ulong`
  conversions, `IsAncestorOf`.
- **Give an entity a tag set**: a `GameplayTagCollection` field — `AddTag`/`RemoveTag`/`Contains`.
  (Foundation is code-only here; the no-code `GameplayTagCollectionComponent` is Toolkit-tier.)
- **Stack-count values on a tag** (buff stacks, ammo count, etc.): `collection.Apply(tag,
  GameplayTagArithmetic.Add, n)`, read back via `GetValue(tag)`.
- **Data-driven conditional logic**: `GameplayTagCondition` list + `EvaluateAll`, or bundle a
  mutation with its own guard conditions via `GameplayTagOperation`.
- **React to a tag's value changing**: `GameplayTagCollection.Subscribe(tag, callback, exactMatch)`
  or the collection-wide `Changed` event.
- **Use tags/hierarchy inside a Burst job**: `collection.GetSnapshot(Allocator.TempJob)` and/or
  `GameplayTagRegistry.GetNativeIntervalMap(Allocator.TempJob)`; dispose both when the job
  completes.
- **See what tags exist and where they come from**: `Project Settings > Gameplay Tags` panel
  (tree view, inline rename, filter); tools implement `ITagSource` to report provenance there.

## Where full usage docs live

KB: `https://heathen.group/kb/gameplaytags-welcome/` (confirmed live in source, via
`GameplayTagsSubsystemHealth.DocumentationUrl`). No per-component KB article slugs were found
committed in this repo to cite beyond the welcome page — check the live KB for anything more
specific before assuming a URL.

## Version

`com.heathen.gameplaytagsfoundation/package.json` (`version` field, currently `1.0.10`) +
`com.heathen.gameplaytagsfoundation/CHANGELOG.md` (currently a single baseline entry, prior
history not itemized).
