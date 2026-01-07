# Rust PetPlugin

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Carbon Rust server plugin that allows players to spawn and control **client-side pets**.

## Features

- Spawn 4 pet types: **wolf**, **tiger**, **panther**, **chicken**
- Pet behaviors: **follow**, **stay**, **lead**
- Permissions-based access
- Optimized visibility (only nearby players see pets)
- Auto-despawn on death/disconnect/vehicle use

## Permissions

```
pet.wolf
pet.tiger
pet.panther
pet.chicken
```

## Commands

```
/pet spawn [wolf|tiger|panther|chicken]
/pet follow
/pet stay
/pet lead
/pet remove
```

## Installation

1. Download `PetPlugin.cs`
2. Place in `oxide/plugins/`
3. Grant permissions: `oxide.grant user <steamid> pet.wolf`
4. Reload: `oxide.reload PetPlugin`

## Configuration

Edit constants in code:

- `VISIBILITY_RADIUS = 50f` (pet visibility distance)
- `FOLLOW_SPEED = 6.25f` (pet movement speed)
- `FOLLOW_DISTANCE = 2f` (follow distance)

## Usage

1. Grant permission: `/pet spawn wolf`
2. Control: `/pet follow|stay|lead|remove`

Pets use **client entities** - only visible to nearby players, no server entity cost.

## Credits

- **kaehmper** (author)
- Carbon mod framework

Version: **1.4.2**