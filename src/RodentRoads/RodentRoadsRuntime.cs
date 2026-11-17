using System;                       // Exception
using System.Collections.Generic;   // Dictionary (per-rodent state)
using Sims3.Gameplay;               // Inventory
using Sims3.Gameplay.Abstracts;     // MinorPet, GameObject
using Sims3.Gameplay.Actors;        // Sim
using Sims3.Gameplay.CAS;           // Household
using Sims3.Gameplay.Core;          // Lot, RandomUtil
using Sims3.Gameplay.Interfaces;    // IMinorPetTerrarium
using Sims3.SimIFace;               // HiddenFlags
using Sims3.UI;                     // StyledNotification

namespace RodentRoads
{
    /// <summary>
    /// Adventure cycle for rodents, driven by <see cref="RodentRoadsPlugin"/>:
    /// <code>
    ///   [Home] --random wait--> [Away] --random trip--> [Home] ...
    /// </code>
    /// A trip only departs from a cage and undisturbed; an in-progress trip always finishes.
    /// While away the rodent is parked in an inventory (invisible, unclickable, cannot die).
    /// </summary>
    public static class RodentRoadsRuntime
    {
        private enum Phase
        {
            Home,
            Away,
        }

        /// <summary>Per-rodent cycle state.</summary>
        /// <remarks>TODO: statics are not saved; move state onto the object (data/PLAN.md §5).</remarks>
        private sealed class AdventureState
        {
            public Phase Phase = Phase.Home;
            public int TicksRemaining;

            /// <summary>Cage to put the rodent back into.</summary>
            public IMinorPetTerrarium Terrarium;

            /// <summary>Parked in a container inventory, so vanilla dispatch must be skipped.</summary>
            public bool WaitingInInventory;
        }

        /// <summary>Ticks used when a phase length cannot be rolled.</summary>
        private const int kFallbackTicks = 5;

        /// <summary>Assumed tick length, in Sim minutes, when the pet has no tuning.</summary>
        private const int kFallbackTickMinutes = 5;

        private static readonly Dictionary<MinorPet, AdventureState> sStates =
            new Dictionary<MinorPet, AdventureState>();

        /// <summary>
        /// Advances the cycle by one behaviour tick.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the caller must skip the vanilla dispatch (rodent is parked in an
        /// inventory, which would otherwise reach <c>BehaviorInWild</c>).
        /// </returns>
        public static bool OnTick(MinorPet pet)
        {
            try
            {
                return Tick(pet);
            }
            catch (Exception e)
            {
                // Never take the pet's simulation down with us.
                ReportErrorOnce(e);
                return false;
            }
        }

        private static bool Tick(MinorPet pet)
        {
            if (pet.IsDead)
            {
                sStates.Remove(pet);
                return false;
            }

            AdventureState state;
            if (!sStates.TryGetValue(pet, out state))
            {
                // Unknown rodent (new, or reloaded mid-trip): wait at home. If it is in a
                // container it resumes once a cage holds it again.
                state = new AdventureState();
                state.Terrarium = pet.CurrentTerrarium;
                state.WaitingInInventory = IsStoredInContainer(pet);
                state.TicksRemaining = RollTicks(pet, RodentRoadsTuning.GetHomeSimMinutesRange());
                sStates[pet] = state;
                return state.WaitingInInventory;
            }

            if (state.Phase == Phase.Away)
            {
                state.TicksRemaining--;
                if (state.TicksRemaining <= 0)
                {
                    EndAdventure(pet, state);
                }
                return true;
            }

            if (state.WaitingInInventory)
            {
                if (!pet.InTerrarium)
                {
                    return true;
                }

                // Back in a cage: fresh wait.
                state.WaitingInInventory = false;
                state.Terrarium = pet.CurrentTerrarium;
                state.TicksRemaining = RollTicks(pet, RodentRoadsTuning.GetHomeSimMinutesRange());
            }

            // Frozen (not re-rolled) while the rodent is out of the cage or busy.
            if (!IsAtHome(pet))
            {
                return false;
            }

            state.TicksRemaining--;
            if (state.TicksRemaining <= 0)
            {
                BeginAdventure(pet, state);
            }

            return false;
        }

        /// <summary>In its cage, alive, unheld and not being played with.</summary>
        private static bool IsAtHome(MinorPet pet)
        {
            if (pet.IsDead || pet.IsUnconscious || pet.Parent is Sim)
            {
                return false;
            }

            if (pet.InInventory || pet.ActorsUsingMe.Count > 0)
            {
                return false;
            }

            return pet.InTerrarium;
        }

        private static void BeginAdventure(MinorPet pet, AdventureState state)
        {
            state.Phase = Phase.Away;
            state.TicksRemaining = RollTicks(pet, RodentRoadsTuning.GetAwaySimMinutesRange());
            state.Terrarium = pet.CurrentTerrarium;
            state.WaitingInInventory = true;

            SendToLimbo(pet, state.Terrarium);

            ShowRodentNotification(pet, "Rodent Roads: test - rodent is on an adventure!");
        }

        private static void EndAdventure(MinorPet pet, AdventureState state)
        {
            state.Phase = Phase.Home;
            state.TicksRemaining = RollTicks(pet, RodentRoadsTuning.GetHomeSimMinutesRange());

            if (CanHoldPet(state.Terrarium))
            {
                ReturnToTerrarium(pet, state.Terrarium);
                state.Terrarium = pet.CurrentTerrarium;
                state.WaitingInInventory = false;

                ShowRodentNotification(pet, "Rodent Roads: test - rodent has returned!");
                return;
            }

            // Cage gone or put away: leave it in the inventory instead of a slot that is not there.
            state.Terrarium = null;
            state.WaitingInInventory = true;

            ShowRodentNotification(
                pet,
                "Rodent Roads: test - rodent came home, but its cage is gone. It is waiting in the household inventory.");
        }

        /// <summary>Cage exists and is placed on a lot.</summary>
        private static bool CanHoldPet(IMinorPetTerrarium terrarium)
        {
            GameObject cage = terrarium as GameObject;
            return cage != null && !cage.HasBeenDestroyed && cage.InWorld;
        }

        /// <summary>
        /// Parks the rodent in the household family inventory: out of world (invisible,
        /// unclickable), <c>InInventory</c> so it is not <c>Escaped</c>, and with no parent so
        /// <c>DeathUpdate</c>/<c>UpdateLifespan</c> cannot kill it.
        /// </summary>
        /// <remarks>
        /// Not the cage's own inventory: <c>Inventory.Dispose</c> destroys its contents, so a
        /// deleted cage would destroy the rodent with it.
        /// </remarks>
        private static void SendToLimbo(MinorPet pet, IMinorPetTerrarium terrarium)
        {
            // Stop the cage state machine, or it keeps animating the rodent in the empty cage.
            pet.StopBehaviorSMC(false);

            if (Park(pet, GetHouseholdInventory(pet, terrarium)))
            {
                return;
            }

            GameObject cage = terrarium as GameObject;
            if (cage != null && Park(pet, cage.Inventory))
            {
                return;
            }

            Inventory.RemoveItemFromWorld(pet);
            pet.SetHiddenFlags(HiddenFlags.Everything);
        }

        /// <summary>Puts the rodent back into its cage and restarts its behaviour.</summary>
        private static void ReturnToTerrarium(MinorPet pet, IMinorPetTerrarium terrarium)
        {
            RemoveFromParking(pet);
            pet.SetHiddenFlags(HiddenFlags.Nothing);
            pet.UnParent();
            pet.ParentToSlot((GameObject)terrarium, terrarium.PetContainmentSlot, 0f, false);
            pet.FadeIn();
            pet.StartBehaviorSMC(false);
        }

        /// <summary>
        /// Adds the rodent to an inventory.
        /// </summary>
        /// <remarks>
        /// <c>AddInternal</c> skips <c>CanAddToInventory</c>, whose "cage has no pet" check races
        /// with <c>UnParent</c> and rejected the move.
        /// </remarks>
        private static bool Park(MinorPet pet, Inventory inventory)
        {
            if (inventory == null)
            {
                return false;
            }

            inventory.AddInternal(pet, 0u, false);
            return pet.InInventory;
        }

        /// <summary>
        /// Takes the rodent out of its inventory.
        /// </summary>
        /// <remarks>
        /// <c>addToScene: false</c>: <c>RemoveByForce</c> would flash the rodent into the world at
        /// its stale position first.
        /// </remarks>
        private static void RemoveFromParking(MinorPet pet)
        {
            if (pet.ItemComp == null)
            {
                return;
            }

            Inventory parent = pet.ItemComp.InventoryParent;
            if (parent == null)
            {
                return;
            }

            try
            {
                parent.RemoveInternal(pet, false, false, false);
            }
            catch (Exception)
            {
                // Inventory was disposed with its owner.
            }
        }

        private static Inventory GetHouseholdInventory(MinorPet pet, IMinorPetTerrarium terrarium)
        {
            GameObject cage = terrarium as GameObject;
            Lot lot = cage != null ? cage.LotCurrent : pet.LotCurrent;
            if (lot == null)
            {
                lot = pet.LotCurrent;
            }

            Household household = lot != null && lot.Household != null
                ? lot.Household
                : Household.ActiveHousehold;
            if (household == null || household.SharedFamilyInventory == null)
            {
                return null;
            }

            return ((GameObject)household.SharedFamilyInventory).Inventory;
        }

        /// <summary>In a container inventory, as opposed to a Sim's inventory.</summary>
        private static bool IsStoredInContainer(MinorPet pet)
        {
            if (!pet.InInventory || pet.ItemComp == null || pet.ItemComp.InventoryParent == null)
            {
                return false;
            }

            return !(pet.ItemComp.InventoryParent.Owner is Sim);
        }

        private static string sLastError;

        /// <summary>Reports a caught exception once per distinct message.</summary>
        private static void ReportErrorOnce(Exception e)
        {
            string message = e.GetType().Name + ": " + e.Message;
            if (message == sLastError)
            {
                return;
            }

            sLastError = message;
            StyledNotification.Show(
                new StyledNotification.Format(
                    "Rodent Roads error - " + message,
                    StyledNotification.NotificationStyle.kDebugAlert));
        }

        /// <summary>Speech-bubble notification with the rodent's thumbnail as the icon.</summary>
        private static void ShowRodentNotification(MinorPet pet, string text)
        {
            StyledNotification.Show(
                new StyledNotification.Format(
                    text,
                    pet.ObjectId,
                    StyledNotification.NotificationStyle.kSimTalking));
        }

        /// <summary>
        /// Rolls a phase length from <paramref name="simMinutesRange"/> and converts it to ticks,
        /// rounding up. Never less than 1.
        /// </summary>
        private static int RollTicks(MinorPet pet, int[] simMinutesRange)
        {
            if (simMinutesRange == null || simMinutesRange.Length < 2)
            {
                return kFallbackTicks;
            }

            int minMinutes = simMinutesRange[0];
            int maxMinutes = simMinutesRange[1];
            if (maxMinutes < minMinutes)
            {
                int swap = minMinutes;
                minMinutes = maxMinutes;
                maxMinutes = swap;
            }

            int simMinutes = RandomUtil.GetInt(minMinutes, maxMinutes);

            int tickMinutes = kFallbackTickMinutes;
            if (pet.MinorPetTuning != null && pet.MinorPetTuning.BehaviorAlarmCallbackFrequency > 0)
            {
                tickMinutes = pet.MinorPetTuning.BehaviorAlarmCallbackFrequency;
            }

            int ticks = (simMinutes + tickMinutes - 1) / tickMinutes;
            return ticks < 1 ? 1 : ticks;
        }
    }
}
