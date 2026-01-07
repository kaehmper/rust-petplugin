# PetPlugin

Carbon plugin for Rust that lets players spawn and control **client-side** pets (wolf, tiger, panther, chicken).

## Features

- Client-side pets (no server AI entity)
- Follow / Stay / Lead behavior modes
- Per-type permissions
- Visibility handling (pets only visible to nearby players)

## Permissions

Grant the permissions you want players/admins to have:

- `pet.wolf`
- `pet.tiger`
- `pet.panther`
- `pet.chicken`

## Commands

All commands are via chat:

- `/pet spawn <wolf|tiger|panther|chicken>`
- `/pet follow`
- `/pet stay`
- `/pet lead`
- `/pet remove`

## Installation (Carbon)

1. Put `PetPlugin.cs` into your Carbon plugins folder (typical: `carbon/plugins/`).
2. Reload/restart the server so the plugin loads.
3. Grant permissions to players/admins (via your permission system) so they can spawn pets.

## Usage

1. Make sure you have the permission for the pet type (example: `pet.wolf`).
2. Spawn a pet:
   - `/pet spawn wolf`
3. Control the pet:
   - `/pet follow` (pet stays near you)
   - `/pet lead` (pet runs in front of you)
   - `/pet stay` (pet stops moving)
4. Remove the pet:
   - `/pet remove`

Notes:
- Pets are automatically removed when the owner disconnects, dies, or enters a vehicle (except sofa deployables). 
- If the owner moves too far away, the pet despawns automatically.

## Tweaking values

All tuning is done directly in `PetPlugin.cs` by editing the constants under `#region Fields`:

- `VISIBILITY_RADIUS` (default `50f`): How far other players can see the pet.
- `FOLLOW_DISTANCE` (default `2f`): How far behind the player the pet tries to stay.
- `SIDE_OFFSET` (default `1.5f`): Side offset in follow mode.
- `FOLLOW_SPEED` (default `6.25f`): Pet movement speed.
- `UPDATE_INTERVAL` (default `0.1f`): How often the pet position updates.
- `VIS_UPDATE_INTERVAL` (default `0.1f`): How often visibility updates run.
- `LEAD_OFFSET` (default `2.75f`): How far in front the pet runs in lead mode.
- `LEAD_TOLERANCE` (default `0.25f`): Smoothing for lead mode positioning.
- `DESPAWN_DISTANCE` (default `100f`): Max distance before the pet is removed.

After changing values, save the file and reload the plugin.
