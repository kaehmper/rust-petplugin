using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Plugins;
using Carbon.Base;
using Carbon.Components;

namespace Carbon.Plugins
{
    [Info("PetPlugin", "kaehmper", "1.4.2")]
    [Description("Allows players to spawn and control a client side pet")]
    public class PetPlugin : CarbonPlugin
    {
        #region Permissions
        private const string PermissionWolf = "pet.wolf";
        private const string PermissionTiger = "pet.tiger";
        private const string PermissionPanther = "pet.panther";
        private const string PermissionChicken = "pet.chicken";
        #endregion

        #region Data Classes
        private class PetData
        {
            public BasePlayer Owner { get; set; }
            public Vector3 Position { get; set; }
            public Quaternion Rotation { get; set; }
            public PetState State { get; set; }
            public PetType Type { get; set; }
            public ClientEntity Entity { get; set; }
            public Timer FollowTimer { get; set; }
            public Timer VisibilityTimer { get; set; }
            public HashSet<ulong> VisibleTo { get; set; } = new HashSet<ulong>();

            public PetData(BasePlayer owner, Vector3 position, Quaternion rotation, PetType type)
            {
                Owner = owner;
                Position = position;
                Rotation = rotation;
                Type = type;
                State = PetState.Follow;
            }
        }

        private enum PetState
        {
            Follow,
            Stay,
            Lead
        }

        private enum PetType
        {
            Wolf,
            Tiger,
            Panther,
            Chicken
        }
        #endregion

        #region Fields
        private int groundMask;
        private readonly Dictionary<ulong, PetData> activePets = new Dictionary<ulong, PetData>();

        // Edit floats to tweak plugin
        private const float VISIBILITY_RADIUS = 50f;    // How far players see the pet
        private const float FOLLOW_DISTANCE = 2f;       // How far the pet will stay behind
        private const float SIDE_OFFSET = 1.5f;         // In follow mode the pet will stay slight to the side
        private const float FOLLOW_SPEED = 6.25f;       // pet speed. 6.25 seems optimal. slightly slower than player
        private const float UPDATE_INTERVAL = 0.1f;     // how often the plugin updates the position
        private const float VIS_UPDATE_INTERVAL = 0.1f; // how often visibility updates are made
        private const float LEAD_OFFSET = 2.75f;        // how far pet will run in front while in lead mode
        private const float LEAD_TOLERANCE = 0.25f;     // offset to make smooth lead mode
        private const float DESPAWN_DISTANCE = 100f;    // determines how far the player can be from pet

        private const float DESPAWN_DISTANCE_SQR = DESPAWN_DISTANCE * DESPAWN_DISTANCE;
        private const string PREFAB_WOLF = "assets/rust.ai/agents/wolf/wolf2.prefab";
        private const string PREFAB_TIGER = "assets/rust.ai/agents/tiger/tiger.prefab";
        private const string PREFAB_PANTHER = "assets/rust.ai/agents/panther/panther.prefab";
        private const string PREFAB_CHICKEN = "assets/rust.ai/agents/chicken/chicken.tutorial.prefab";
        private const string SOFA_PREFAB_PREFIX = "assets/prefabs/deployable/sofa/";
        #endregion

        #region Hooks
        private void OnServerInitialized()
        {
            permission.RegisterPermission(PermissionWolf, this);
            permission.RegisterPermission(PermissionTiger, this);
            permission.RegisterPermission(PermissionPanther, this);
            permission.RegisterPermission(PermissionChicken, this);

            groundMask = LayerMask.GetMask("Terrain", "World", "Construction");

            Puts("PetPlugin loaded successfully!");
        }

        private void Unload()
        {
            var petsToRemove = activePets.Values.ToList();
            foreach (var petData in petsToRemove)
            {
                petData.FollowTimer = DestroyTimer(petData.FollowTimer);
                petData.VisibilityTimer = DestroyTimer(petData.VisibilityTimer);

                if (petData.Entity != null)
                {
                    petData.Entity.KillAll();
                    petData.Entity.Dispose();
                }
            }

            activePets.Clear();
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;
            RemovePet(player.userID);
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null) return;
            RemovePet(player.userID);
        }

        private void OnEntityMounted(BaseMountable mountable, BasePlayer player)
        {
            if (player == null || mountable == null) return;

            if (!activePets.ContainsKey(player.userID)) return;

            if (IsSofaDeployable(mountable)) return;

            var vehicle = mountable.GetComponentInParent<BaseVehicle>();
            if (vehicle == null) return;

            RemovePet(player.userID);
            player.ChatMessage("Pet removed (you entered a vehicle).");
        }
        #endregion

        #region Commands
        [ChatCommand("pet")]
        private void PetCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            if (args.Length == 0)
            {
                player.ChatMessage("Usage: /pet <spawn|follow|stay|lead|remove> [wolf|tiger|panther|chicken]");
                player.ChatMessage("Available pets: wolf, tiger, panther, chicken");
                return;
            }

            switch (args[0].ToLower())
            {
                case "spawn":
                    if (args.Length < 2)
                    {
                        player.ChatMessage("Usage: /pet spawn <wolf|tiger|panther|chicken>");
                        ShowAvailablePets(player);
                        return;
                    }
                    CreatePet(player, args[1]);
                    break;

                case "follow":
                    SetPetFollow(player);
                    break;

                case "stay":
                    SetPetStay(player);
                    break;

                case "lead":
                    SetPetLead(player);
                    break;

                case "remove":
                    RemovePetCommand(player);
                    break;

                default:
                    player.ChatMessage("Unknown command. Use: spawn, follow, stay, lead, or remove");
                    break;
            }
        }

        private void ShowAvailablePets(BasePlayer player)
        {
            var availablePets = new List<string>();

            if (permission.UserHasPermission(player.UserIDString, PermissionWolf))
                availablePets.Add("wolf");
            if (permission.UserHasPermission(player.UserIDString, PermissionTiger))
                availablePets.Add("tiger");
            if (permission.UserHasPermission(player.UserIDString, PermissionPanther))
                availablePets.Add("panther");
            if (permission.UserHasPermission(player.UserIDString, PermissionChicken))
                availablePets.Add("chicken");

            if (availablePets.Count > 0)
                player.ChatMessage("You can spawn: " + string.Join(", ", availablePets));
            else
                player.ChatMessage("You don't have permission for any pet types!");
        }

        private void SetPetFollow(BasePlayer player)
        {
            if (!activePets.TryGetValue(player.userID, out var petData))
            {
                player.ChatMessage("You don't have a pet! Use /pet spawn <type>");
                return;
            }

            if (petData.State == PetState.Follow)
            {
                player.ChatMessage("Your pet is already following you!");
                return;
            }

            petData.State = PetState.Follow;
            petData.Rotation = player.eyes.rotation;

            StartFollowTimer(petData);
            player.ChatMessage("Your pet will now follow you!");
        }

        private void SetPetLead(BasePlayer player)
        {
            if (!activePets.TryGetValue(player.userID, out var petData))
            {
                player.ChatMessage("You don't have a pet! Use /pet spawn <type>");
                return;
            }

            if (petData.State == PetState.Lead)
            {
                player.ChatMessage("Your pet is already leading!");
                return;
            }

            petData.State = PetState.Lead;
            petData.Rotation = player.eyes.rotation;

            StartFollowTimer(petData);
            player.ChatMessage("Your pet will now run in front of you!");
        }

        private void SetPetStay(BasePlayer player)
        {
            if (!activePets.TryGetValue(player.userID, out var petData))
            {
                player.ChatMessage("You don't have a pet! Use /pet spawn <type>");
                return;
            }

            if (petData.State == PetState.Stay)
            {
                player.ChatMessage("Your pet is already staying!");
                return;
            }

            petData.State = PetState.Stay;
            petData.FollowTimer = DestroyTimer(petData.FollowTimer);

            player.ChatMessage("Your pet will stay at its current position!");
        }

        private void RemovePetCommand(BasePlayer player)
        {
            if (!activePets.ContainsKey(player.userID))
            {
                player.ChatMessage("You don't have a pet!");
                return;
            }

            RemovePet(player.userID);
            player.ChatMessage("Pet removed!");
        }

        #endregion

        #region Client Entity Management

        private void CreatePet(BasePlayer player, string petTypeName)
        {
            if (activePets.ContainsKey(player.userID))
            {
                player.ChatMessage("You already have a pet! Remove it first with /pet remove");
                return;
            }

            PetType petType;
            string prefab;
            string requiredPermission;

            switch (petTypeName.ToLower())
            {
                case "wolf":
                    petType = PetType.Wolf;
                    prefab = PREFAB_WOLF;
                    requiredPermission = PermissionWolf;
                    break;
                case "tiger":
                    petType = PetType.Tiger;
                    prefab = PREFAB_TIGER;
                    requiredPermission = PermissionTiger;
                    break;
                case "panther":
                    petType = PetType.Panther;
                    prefab = PREFAB_PANTHER;
                    requiredPermission = PermissionPanther;
                    break;
                case "chicken":
                    petType = PetType.Chicken;
                    prefab = PREFAB_CHICKEN;
                    requiredPermission = PermissionChicken;
                    break;
                default:
                    player.ChatMessage($"Unknown pet type: {petTypeName}");
                    player.ChatMessage("Available types: wolf, tiger, panther, chicken");
                    return;
            }

            if (!permission.UserHasPermission(player.UserIDString, requiredPermission))
            {
                player.ChatMessage($"You don't have permission to spawn a {petTypeName}!");
                ShowAvailablePets(player);
                return;
            }

            var position = player.transform.position + player.transform.right * SIDE_OFFSET;
            var rotation = player.eyes.rotation;
            var petData = new PetData(player, position, rotation, petType);

            petData.Entity = ClientEntity.Create(prefab, position, rotation);

            activePets[player.userID] = petData;

            StartVisibilityTimer(petData);
            StartFollowTimer(petData);

            player.ChatMessage($"{petTypeName.ToUpper()} spawned! Use /pet follow, /pet lead or /pet stay to control it.");
        }

        private void StartVisibilityTimer(PetData petData)
        {
            petData.VisibilityTimer = DestroyTimer(petData.VisibilityTimer);

            petData.VisibilityTimer = timer.Every(VIS_UPDATE_INTERVAL, () =>
            {
                if (petData?.Entity == null || petData.Owner == null)
                    return;

                if (!petData.Owner.IsConnected)
                    return;

                UpdatePetVisibility(petData);
            });
        }

        private void UpdatePetVisibility(PetData petData)
        {
            if (petData?.Entity == null || petData.Owner == null) return;

            var ownerPos = petData.Owner.transform.position;
            var diffOwner = ownerPos - petData.Position;
            if (diffOwner.sqrMagnitude > DESPAWN_DISTANCE_SQR)
            {
                var ownerId = petData.Owner.userID;
                petData.Owner.ChatMessage("Pet removed (too far away).");
                RemovePet(ownerId);
                return;
            }

            var radiusSqr = VISIBILITY_RADIUS * VISIBILITY_RADIUS;

            petData.VisibleTo.RemoveWhere(uid =>
            {
                var p = BasePlayer.FindByID(uid);
                return p == null || !p.IsConnected;
            });

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected) continue;

                var diff = player.transform.position - petData.Position;
                var distanceSqr = diff.sqrMagnitude;

                if (distanceSqr <= radiusSqr)
                {
                    if (!petData.VisibleTo.Contains(player.userID))
                    {
                        petData.Entity.SpawnFor(player.Connection);
                        petData.VisibleTo.Add(player.userID);
                    }
                    else
                    {
                        petData.Entity.Position = petData.Position;
                        petData.Entity.Rotation = petData.Rotation.eulerAngles;
                        petData.Entity.SendNetworkUpdate_Position();
                    }
                }
                else if (petData.VisibleTo.Contains(player.userID))
                {
                    petData.Entity.KillFor(player.Connection);
                    petData.VisibleTo.Remove(player.userID);
                }
            }
        }

        private void RemovePet(ulong playerId)
        {
            if (!activePets.TryGetValue(playerId, out var petData)) return;

            petData.FollowTimer = DestroyTimer(petData.FollowTimer);
            petData.VisibilityTimer = DestroyTimer(petData.VisibilityTimer);

            if (petData.Entity != null)
            {
                petData.Entity.KillAll();
                petData.Entity.Dispose();
            }

            activePets.Remove(playerId);
        }

        private Timer DestroyTimer(Timer t)
        {
            if (t != null && !t.Destroyed) t.Destroy();
            return null;
        }

        #endregion

        #region Pet Behavior

        private void StartFollowTimer(PetData petData)
        {
            petData.FollowTimer = DestroyTimer(petData.FollowTimer);

            petData.FollowTimer = timer.Every(UPDATE_INTERVAL, () =>
            {
                if (petData?.Owner == null || petData.Entity == null) return;
                if (!petData.Owner.IsConnected) return;
                if (petData.State == PetState.Stay) return;

                UpdatePetPosition(petData);
            });
        }

        private bool IsSofaDeployable(BaseNetworkable ent)
        {
            if (ent == null) return false;

            var prefab = ent.PrefabName;
            if (!string.IsNullOrEmpty(prefab) &&
                prefab.StartsWith(SOFA_PREFAB_PREFIX, StringComparison.OrdinalIgnoreCase))
                return true;

            var baseEntity = ent as BaseEntity;
            var shortName = baseEntity?.ShortPrefabName;
            if (!string.IsNullOrEmpty(shortName) &&
                shortName.StartsWith("sofa/", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private Vector3 GetFlatLookForward(BasePlayer p)
        {
            var f = p.eyes.rotation * Vector3.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 0.0001f) f = p.transform.forward; // Fallback
            return f.normalized;
        }

        private void UpdatePetPosition(PetData petData)
        {
            var owner = petData.Owner;
            if (owner == null || petData.Entity == null) return;

            Vector3 targetPosition;
            float tolerance;

            if (petData.State == PetState.Lead)
            {
                var leadForward = GetFlatLookForward(owner);
                targetPosition = owner.transform.position + leadForward * LEAD_OFFSET;
                tolerance = LEAD_TOLERANCE;
            }
            else
            {
                targetPosition = owner.transform.position + owner.transform.right * SIDE_OFFSET;
                tolerance = FOLLOW_DISTANCE;
            }

            var toTarget = targetPosition - petData.Position;
            var dist = toTarget.magnitude;

            if (dist > tolerance)
            {
                var direction = toTarget / dist;

                float moveDistance = (petData.State == PetState.Lead)
                    ? Mathf.Min(FOLLOW_SPEED * UPDATE_INTERVAL, dist)
                    : Mathf.Min(FOLLOW_SPEED * UPDATE_INTERVAL, dist - FOLLOW_DISTANCE);

                petData.Position += direction * moveDistance;

                var flatDir = direction;
                flatDir.y = 0f;

                if (flatDir.sqrMagnitude > 0.0001f)
                    petData.Rotation = Quaternion.LookRotation(flatDir.normalized, Vector3.up);

                RaycastHit hit;
                if (Physics.Raycast(petData.Position + Vector3.up * 2f, Vector3.down, out hit, 2f, groundMask))
                    petData.Position = new Vector3(petData.Position.x, hit.point.y + 0.1f, petData.Position.z);
            }
        }

        #endregion
    }
}
