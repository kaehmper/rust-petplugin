using System;

using UnityEngine;

using System.Collections.Generic;

using System.Linq;

using Oxide.Core;

using Oxide.Plugins;

using Carbon.Base;

using Carbon.Components;

namespace Carbon.Plugins



[Info("PetPlugin", "kaehmper", "1.4.2")]

[Description("Allows players to spawn and control a client side pet")]

public class PetPlugin : CarbonPlugin



#region Permissions

private const string PermissionWolf = "pet.wolf";

private const string PermissionTiger = "pet.tiger";

private const string PermissionPanther = "pet.panther";

private const string PermissionChicken = "pet.chicken";

#endregion

#region Data Classes

private class PetData



public BasePlayer Owner { get; set; }

public Vector3 Position { get; set; }

public Quaternion Rotation { get; set; }

public PetState State { get; set; }

public PetType Type { get; set; }

public ClientEntity Entity { get; set; }

public Timer FollowTimer { get; set; }

public Timer VisibilityTimer { get; set; }

public HashSet VisibleTo { get; set; } = new HashSet();

public PetData(BasePlayer owner, Vector3 position, Quaternion rotation, PetType type)



Owner = owner;

Position = position;

Rotation = rotation;

Type = type;

State = PetState.Follow;



private enum PetState



Follow,

Stay,

Lead



private enum PetType



Wolf,

Tiger,

Panther,

Chicken



#endregion

#region Fields

private int groundMask;

private readonly Dictionary activePets = new Dictionary();

// // Edit floats to tweak plugin

private const float VISIBILITY_RADIUS = 50f; // How far players see the pet

private const float FOLLOW_DISTANCE = 2f; // How far the pet will stay behind

private const float SIDE_OFFSET = 1.5f; // In follow mode the pet will stay slight to the side

private const float FOLLOW_SPEED = 6.25f; // pet speed. 6.25 seems optimal. slightly slower than player

private const float UPDATE_INTERVAL = 0.1f; // how often the plugin updates the position

private const float VIS_UPDATE_INTERVAL = 0.1f; // how often visibility updates are made

private const float LEAD_OFFSET = 2.75f; // how far pet will run in front while in lead mode

private const float LEAD_TOLERANCE = 0.25f; // offset to make smooth lead mode

private const float DESPAWN_DISTANCE = 100f; // determines how far the player can be from pet

private const float DESPAWN_DISTANCE_SQR = DESPAWN_DISTANCE * DESPAWN_DISTANCE;

private const string PREFAB_WOLF = "assets/rust.ai/agents/wolf/wolf2.prefab";

private const string PREFAB_TIGER = "assets/rust.ai/agents/tiger/tiger.prefab";

private const string PREFAB_PANTHER = "assets/rust.ai/agents/panther/panther.prefab";

private const string PREFAB_CHICKEN = "assets/rust.ai/agents/chicken/chicken.tutorial.prefab";

private const string SOFA_PREFAB_PREFIX = "assets/prefabs/deployable/sofa/";

#endregion

#region Hooks

private void OnServerInitialized()



permission.RegisterPermission(PermissionWolf, this);

permission.RegisterPermission(PermissionTiger, this);

permission.RegisterPermission(PermissionPanther, this);

permission.RegisterPermission(PermissionChicken, this);

groundMask = LayerMask.GetMask("Terrain", "World", "Construction");

Puts("PetPlugin loaded successfully!");



private void Unload()



var petsToRemove = activePets.Values.ToList();

foreach (var petData in petsToRemove)



petData.FollowTimer = DestroyTimer(petData.FollowTimer);

petData.VisibilityTimer = DestroyTimer(petData.VisibilityTimer);

if (petData.Entity != null)



petData.Entity.KillAll();

petData.Entity.Dispose();



activePets.Clear();



private void OnPlayerDisconnected(BasePlayer player, string reason)



if (player == null) return;

RemovePet(player.userID);



private void OnPlayerDeath(BasePlayer player, HitInfo info)



if (player == null) return;

RemovePet(player.userID);



private void OnEntityMounted(BaseMountable mountable, BasePlayer player)



if (player == null || mountable == null) return;

if (!activePets.ContainsKey(player.userID)) return;

if (IsSofaDeployable(mountable)) return;

var vehicle = mountable.GetComponentInParent();

if (vehicle == null) return;

RemovePet(player.userID);

player.ChatMessage("Pet removed (you entered a vehicle).");



#endregion

#region Commands

[ChatCommand("pet")]

private void PetCommand(BasePlayer player, string command, string[] args)



if (player == null) return;

if (args.Length == 0)



player.ChatMessage("Usage: /pet [wolf|tiger|panther|chicken]");...