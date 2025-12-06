# Viking

Talent tree and ability system for Valheim. Server-authoritative player progression with passive talents and active abilities.

## Features

- **Talent Tree** - Passive skill tree with interconnected nodes
- **Starting Points** - Choose your starting archetype (Warrior, Ranger, Mage)
- **Ability Bar** - 8-slot action bar for active abilities (keys 1-8)
- **Server-Authoritative** - All talent allocations validated server-side
- **Backtrack System** - Undo recent talent allocations
- **Full Reset** - Complete talent reset with optional currency cost

## Dependencies

### Required
- BepInEx
- Jotunn
- Vital

### Optional (Enhanced Features)
- **Veneer** - Talent tree UI, character window, ability bar visuals
- **Prime** - Stat modifiers and ability execution
- **Spark** - Visual effects for abilities
- **Munin** - Console commands
- **Tome** - Respec currency integration

## Keybinds

| Key | Action |
|-----|--------|
| `K` | Open Talent Tree |
| `1-8` | Activate ability in slot |

## Talent Tree

Viking uses a classless talent system where all players start at one of several entry points and branch out through a shared tree.

### Node Types

- **Start** - Entry points for new characters
- **Minor** - Small stat bonuses
- **Notable** - Significant bonuses
- **Keystone** - Major build-defining effects
- **Ability** - Grants an active ability

## Commands

When Munin is installed:

```
munin viking status          - Show talent status
munin viking reset           - Reset all talents
munin viking points          - Show available points
```

## Multiplayer

Viking is fully multiplayer compatible with server-authoritative validation. All clients must have the mod installed.
