# Axiom - Unity Development Rules

## Code Style
- Unity naming: PascalCase public, _camelCase private fields
- Use [SerializeField] for inspector-exposed private fields
- Prefer composition over inheritance
- Include XML summaries for public methods on core systems

## Project Architecture

### Core Systems (reference these for consistency)
- **PlayerStats.cs** — Dual-stat system: Drive (calories→damage), Focus (sleep→AoE)
- **GameManager.cs** — Run state, Depth progression (Citadel→Breach→OccupiedSector)
- **DifficultyScaler.cs** — Enemy scaling based on Depth, Drive mitigation
- **Equipment.cs** — Slot-based equip system with stat aggregation
- **LootGenerator.cs** — Weighted rarity drops, affix rolling

### Patterns in Use
- Enemies use a simple chase-and-attack loop in EnemyAI.cs (no formal state machine)
- Abilities are standalone MonoBehaviours with similar conventions (Input → Damage calc → Effect)
- Items use ScriptableObject definitions (ItemDefinition) + runtime instances (ItemInstance)
- UI follows InventoryUIController event-driven pattern

### Folder Structure
- Scripts: Assets/Scripts/{Player,AI,Abilities,LootSystem,UI,Core,Health,iOSBridge}/
- Prefabs: Assets/Prefabs/
- Data: Assets/GameData/Loot/{Items,Tables,Affixes}/
- Plugins/iOS: Native HealthKit bridges

## Response Requirements

### Always Include
1. **Unity Editor TODO** — List all required editor actions:
   - Inspector assignments (be specific: "Assign X to Y field on Z GameObject")
   - Prefab modifications
   - Scene hierarchy changes
   - Asset creation (ScriptableObjects, Layers, Tags)
   - Project Settings changes

2. **Integration Notes** — How this connects to existing systems

3. **Test Instructions** — Quick steps to verify the change works:
   - What to do in Play Mode
   - Expected behavior
   - How to trigger/observe the new functionality

### When Modifying Existing Systems
- Show only the changed methods/sections, not entire files
- Note any ripple effects on dependent scripts
- Flag if changes affect serialized data (will reset inspector values)
