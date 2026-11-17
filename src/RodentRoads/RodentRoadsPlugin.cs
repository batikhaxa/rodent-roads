using MonoPatcherLib;                       // [Plugin], MonoPatcher, [ReplaceMethod]
using Sims3.Gameplay.Abstracts;             // MinorPet
using Sims3.Gameplay.Actors;                // Sim
using Sims3.Gameplay.Objects.MinorPets;     // MinorPetRodent


namespace RodentRoads
{
    /// <summary>Patches <see cref="MinorPet.SimulateBehavior"/>; requires MonoPatcher.</summary>
    [Plugin]
    public class RodentRoadsPlugin
    {
        public RodentRoadsPlugin()
        {
            MonoPatcher.PatchAll();
        }

        /// <summary>
        /// Replacement for <c>MinorPet.SimulateBehavior</c>, called every
        /// <c>MinorPetTuning.BehaviorAlarmCallbackFrequency</c> Sim minutes.
        /// </summary>
        /// <remarks>
        /// MonoPatcher swaps the whole body, so the original dispatch is reproduced below
        /// and must match data/decompiled/MinorPet.cs.
        /// </remarks>
        [ReplaceMethod(typeof(MinorPet), "SimulateBehavior")]
        private void SimulateBehavior_Replacement()
        {
            MinorPet pet = (MinorPet)(this as object);
            if (pet == null)
            {
                return;
            }

            // Above the guards on purpose: an in-progress trip must keep ticking while the
            // rodent is out of its cage. Dead pets are handled inside the runtime.
            if (pet is MinorPetRodent)
            {
                if (RodentRoadsRuntime.OnTick(pet))
                {
                    // Parked in an inventory: the dispatch below does not apply to it.
                    return;
                }
            }

            // -------------- Original clauses --------------
            if (pet.IsUnconscious || pet.IsDead || pet.Parent is Sim)
            {
                return;
            }
            if (!pet.InWorld && !pet.InInventory)
            {
                return;
            }
            if (pet.ActorsUsingMe.Count > 0)
            {
                return;
            }

            if (pet.Escaped)
            {
                pet.BehaviorEscaped();
            }
            else if (pet.InInventory && HasSimInventoryOwner(pet))
            {
                pet.BehaviorInInventory();
            }
            else if (pet.InTerrarium)
            {
                pet.BehaviorInTerrarium();
            }
            else
            {
                pet.BehaviorInWild();
            }
        }

        private static bool HasSimInventoryOwner(MinorPet pet)
        {
            return pet.ItemComp != null
                && pet.ItemComp.InventoryParent != null
                && pet.ItemComp.InventoryParent.Owner is Sim;
        }
    }
}
