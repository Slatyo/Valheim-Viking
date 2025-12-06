<h1 align="center">Valheim-Viking</h1>

<p align="center">
  <a href="https://github.com/Slatyo/Valheim-Viking/releases"><img src="https://img.shields.io/github/v/release/Slatyo/Valheim-Viking?style=flat-square" alt="GitHub release"></a>
  <a href="https://opensource.org/licenses/MIT"><img src="https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square" alt="License: MIT"></a>
</p>

<p align="center">
  Talent tree and ability system for Valheim.<br>
  Server-authoritative player progression with passive talents and active abilities.
</p>

## Features

- **Talent Tree** - Passive skill tree with interconnected nodes
- **Starting Points** - Choose your starting archetype (Warrior, Ranger, Mage)
- **Ability Bar** - 8-slot action bar for active abilities (keys 1-8)
- **Server-Authoritative** - All talent allocations validated server-side
- **Backtrack System** - Undo recent talent allocations
- **Full Reset** - Complete talent reset with optional currency cost

## Dependencies

### Required
- [BepInEx](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/)
- [Jotunn](https://valheim.thunderstore.io/package/ValheimModding/Jotunn/)
- [Vital](https://github.com/Slatyo/Valheim-Vital) - Player data and leveling

### Optional (Enhanced Features)
- **Veneer** - Talent tree UI, character window, ability bar visuals
- **Prime** - Stat modifiers and ability execution
- **Spark** - Visual effects for abilities
- **Munin** - Console commands
- **Tome** - Respec currency integration

## Installation

### Thunderstore (Recommended)
Install via [r2modman](https://valheim.thunderstore.io/package/ebkr/r2modman/) or [Thunderstore Mod Manager](https://www.overwolf.com/app/Thunderstore-Thunderstore_Mod_Manager).

### Manual
1. Install BepInEx and Jotunn
2. Install Vital (required dependency)
3. Place `Viking.dll` in `BepInEx/plugins/`

## Keybinds

| Key | Action |
|-----|--------|
| `K` | Open Talent Tree |
| `1-8` | Activate ability in slot |

## Talent Tree

Viking uses a classless talent system where all players start at one of several entry points and branch out through a shared tree. Talents provide:

- **Passive Bonuses** - Stat increases (health, damage, resistances)
- **Active Abilities** - Skills that can be placed on the ability bar
- **Keystones** - Powerful build-defining nodes

### Node Types

- **Start** - Entry points for new characters
- **Minor** - Small stat bonuses (1 point)
- **Notable** - Significant bonuses (1 point)
- **Keystone** - Major build-defining effects (1 point)
- **Ability** - Grants an active ability

## Commands

When Munin is installed:

```
munin viking status          - Show talent status
munin viking reset           - Reset all talents
munin viking points          - Show available points
munin viking tree            - Show allocated nodes
```

## Architecture

Viking follows server-authoritative design:

1. Client requests talent allocation via RPC
2. Server validates (points available, node reachable, not maxed)
3. Server applies allocation and syncs to client
4. Client UI updates to reflect new state

This prevents cheating and ensures multiplayer consistency.

## Integration

- **Vital** - Stores talent data in extensible player data system
- **Prime** - Talent stat bonuses become Prime modifiers
- **Veneer** - All UI rendered through Veneer components
- **Spark** - Ability VFX triggered through Spark

## License

[MIT](LICENSE)
