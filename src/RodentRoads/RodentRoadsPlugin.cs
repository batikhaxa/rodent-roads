using MonoPatcherLib;
using Sims3.Gameplay.Abstracts;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Objects.MinorPets;

namespace RodentRoads
{
    /// <summary>
    /// This patches <see cref="MinorPet.SimulateBehavior"/>.
    /// </summary>
    /// <remarks>
    /// Requires the MonoPatcher script mod to be installed in the game.
    /// </remarks>
    [Plugin]
    public class RodentRoadsPlugin
    {
        public RodentRoadsPlugin()
        {
            MonoPatcher.PatchAll();
        }

        /// <summary>
        /// Replaces the private <c>MinorPet.SimulateBehavior</c> dispatcher, which the pet's own
        /// <c>Simulate()</c> loop calls every <c>MinorPetTuning.BehaviorAlarmCallbackFrequency</c>
        /// (default 5) Sim minutes.
        /// </summary>
        /// <remarks>
        /// A replacement swaps the whole body, so the original dispatch below is reproduced using only
        /// public members. Keep it in sync with the decompiled source (data/decompiled/MinorPet.cs).
        /// </remarks>
        [ReplaceMethod(typeof(MinorPet), "SimulateBehavior")]
        private void SimulateBehavior_Replacement()
        {
            MinorPet pet = (MinorPet)(this as object);
            if (pet == null)
            {
                return;
            }

            // New hook.
            if (pet is MinorPetRodent)
            {
                RodentRoadsRuntime.OnTick(pet);
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
