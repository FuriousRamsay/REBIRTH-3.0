using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Rebirth.Vehicles.Camper
{
    // Explicit integration entry point. No assembly-wide scan and no automatic
    // activation while the shared production bootstrap is reserved elsewhere.
    public static class RebirthCamperModulePatchInstaller
    {
        private static readonly object Sync = new object();
        private static readonly Harmony Harmony = new Harmony("rebirth.camper.production");
        private static readonly HashSet<Type> completed = new HashSet<Type>();
        public static bool IsInstalled { get; private set; }

        public static void Install(Func<EntityPlayerLocal, string> authenticatedCreation,
            Func<EntityPlayer,string> remoteAuthenticatedCreation,
            string modPath)
        {
            if (authenticatedCreation == null) throw new ArgumentNullException(nameof(authenticatedCreation));
            if (string.IsNullOrWhiteSpace(modPath)) throw new ArgumentException("Camper module requires its loaded mod path.", nameof(modPath));
            lock (Sync)
            {
                if (IsInstalled) return;
#if !REBIRTH_CAMPER_DIAGNOSTICS
                // Refuse an unsupported native completion body before exposing
                // any mobile menu or queue processing through this installer.
                Patch(typeof(RebirthCamperStationCompletionCallsite));
                Patch(typeof(RebirthCamperStationOpenQueueUpdateHold));
                Patch(typeof(RebirthCamperStationOpenQueueOutputHold));
                Patch(typeof(RebirthCamperStationOpenQueueCancellationHold));
                Patch(typeof(RebirthCamperStationQueueWriteback));
                Patch(typeof(RebirthCamperStationOpenQueuePresentation));
                Patch(typeof(RebirthCamperStationOpenQueueSync));
                Patch(typeof(RebirthCamperStationGridGesture));
                Patch(typeof(RebirthCamperStationGridDirectInsert));
                Patch(typeof(RebirthCamperStationGridFuelSwitch));
#endif
                // The close/publication and position adapters must be present
                // before any menu can expose detached station state.
                Patch(typeof(RebirthCamperStationPosition));
                Patch(typeof(RebirthCamperStationTerrainVisibility));
                Patch(typeof(RebirthCamperStationBlock));
                Patch(typeof(RebirthCamperStationHeat));
                RebirthCamperStationHeat.IsInstalled = true;
                Patch(typeof(RebirthCamperStationWindowClose));
                Patch(typeof(RebirthCamperStationWindowValidity));
                Patch(typeof(RebirthCamperStationWindowBinding));
                Patch(typeof(RebirthCamperCargoSize));
                Patch(typeof(RebirthCamperNativeCargoLockResponse));
                Patch(typeof(RebirthCamperCargoTemplateCapture));
                Patch(typeof(RebirthCamperCargoSlots));
                Patch(typeof(RebirthCamperPortablePlacementGuard));
                Patch(typeof(RebirthCamperModulePreferredTransfer));
                Patch(typeof(RebirthCamperPassengerRadial));
                Patch(typeof(RebirthCamperInteractionVisibility));
                Patch(typeof(RebirthCamperModuleInteractionGrant));
                if(remoteAuthenticatedCreation!=null)
                {
                    RebirthCamperRemoteOwnerEffects.Install();
                    RebirthCamperRemoteStationActions.Install(RebirthCamperStationRemoteCancellationWatch.Handle);
                    RebirthCamperRemoteTransport.Register(remoteAuthenticatedCreation);
                    Patch(typeof(RebirthCamperRemotePendingSync));
                    Patch(typeof(RebirthCamperRemotePendingLock));
                    Patch(typeof(RebirthCamperRemotePendingCommand));
                    Patch(typeof(RebirthCamperRemoteStationSwap));
                    Patch(typeof(RebirthCamperRemoteStationSwapItem));
                    Patch(typeof(RebirthCamperRemoteStationHalf));
                    Patch(typeof(RebirthCamperRemoteStationOne));
                    Patch(typeof(RebirthCamperRemoteStationPreferred));
                    Patch(typeof(RebirthCamperRemoteStationFuelSwitch));
                    Patch(typeof(RebirthCamperRemoteStationDirectInsert));
                    RebirthCamperRemoteStationClient.Install();
                }
                RebirthCamperTurretAccess.Install();
                RebirthCamperRelayWiring.Install();
                RebirthCamperBodyAttachment.Install();
                Patch(typeof(RebirthCamperTurretTick));
                Patch(typeof(RebirthCamperTurretEntityDamage));
                Patch(typeof(RebirthCamperTurretFriendlyFireGuard));
                Patch(typeof(RebirthCamperTurretBlockDamage));
                Patch(typeof(RebirthCamperTurretAmmoRange));
                Patch(typeof(RebirthCamperTurretManualSpread));
                Patch(typeof(RebirthCamperTurretNativeAmmoContext));
                Patch(typeof(RebirthCamperTurretInventoryFence));
                Patch(typeof(RebirthCamperTurretBackpackGestures));
                Patch(typeof(RebirthCamperTurretActorMutationGuard));
                Patch(typeof(RebirthCamperTurretPlayerFence));
                Patch(typeof(RebirthCamperRadialCommands));
                Patch(typeof(RebirthCamperRadialRefresh));
                Patch(typeof(RebirthCamperRadialStorageOrder));
                Patch(typeof(RebirthCamperRadialColors));
                Patch(typeof(RebirthCamperRadialPermission));
                Patch(typeof(RebirthCamperRadialActivate));
                Patch(typeof(RebirthCamperStationTick));
                Patch(typeof(RebirthCamperStationTemplateSourceCapture));
                Patch(typeof(RebirthCamperStationTemplateSourceClear));
                Patch(typeof(RebirthCamperStationTransitionCursorWindow));
                Patch(typeof(RebirthCamperStationTransitionSoftCursorAllowed));
                Patch(typeof(RebirthCamperStationTransitionSoftCursorVisible));
                Patch(typeof(RebirthCamperStationCursorEnableTrace));
                Patch(typeof(RebirthCamperStationCursorPositionTrace));
                Patch(typeof(RebirthCamperStationCursorVisibleTrace));
                // Energy custody is recovered before any native resource tick.
                // Registration remains off until the complete UI/transport bootstrap
                // has been installed; these scoped hooks cannot expose partial menus.
                RebirthCamperEnergyMaintenance.Install(Harmony);
                RebirthCamperEnergyRuntime.CustodyReady = vehicle =>
                    RebirthCamperEnergyMaintenance.Ready(vehicle) && RebirthCamperEnergyCustody.Ready(vehicle);
                RebirthCamperEnergyConfigurationFile.Load(System.IO.Path.Combine(
                    modPath,
                    "Config", "RebirthCamperEnergy.xml"));
                Patch(typeof(RebirthCamperEnergyDetachedStackRead));
                Patch(typeof(RebirthCamperEnergyDetachedArrayRead));
                Patch(typeof(RebirthCamperEnergyOwnerMutationGuard));
                Patch(typeof(RebirthCamperEnergyOwnerCursorGuard));
                Patch(typeof(RebirthCamperEnergyVehicleTick));
                Patch(typeof(RebirthCamperEnergyDriverGuard));
                RebirthCamperEnergyDeviceAdapter.Install();
                RebirthCamperEnergySourceCoordinator.Install();
                Patch(typeof(RebirthCamperEnergyPreferredGuard));
                Patch(typeof(RebirthCamperEnergyBackpackPreferredGuard));
                RebirthCamperEnergyAccess.Install();
                RebirthCamperEnergyBoundsDisplay.Install();
                Patch(typeof(RebirthCamperEnergyBoundsDisplayTick));
                Patch(typeof(RebirthCamperCampStationAppearanceTick));
                Patch(typeof(RebirthCamperCampStationAppearancePhysics));
                Patch(typeof(RebirthCamperOwnCollisionDriven));
                Patch(typeof(RebirthCamperOwnCollisionParts));
                Patch(typeof(RebirthCamperRearLadderAdmission));
                Patch(typeof(RebirthCamperEnergyBoundsDisplayUnload));
                Patch(typeof(RebirthCamperEnergyImmobilityPhysics));
                Patch(typeof(RebirthCamperEnergyDrivingInputRead));
                Patch(typeof(RebirthCamperCampEngine));
                Patch(typeof(RebirthCamperEnergyDrivingInputPhysics));
                Patch(typeof(RebirthCamperEnergyImmobilityPosition));
                Patch(typeof(RebirthCamperEnergyImmobilityRotation));
                Patch(typeof(RebirthCamperEnergyImmobilityUnload));
                RebirthCamperClaimIntegration.Install();
                RebirthCamperStationNativeActorPayment.Install();
                RebirthCamperStationFuelOriginal.Install();
#if !REBIRTH_CAMPER_DIAGNOSTICS
                Patch(typeof(RebirthCamperStationNativeRewardHold));
                RebirthCamperStationPublicationWatch.Install();
                RebirthCamperStationCompletionWatch.Install();
                RebirthCamperStationCancellationWatch.Install();
                RebirthCamperStationTrainingRecoveryWatch.Install();
                RebirthCamperStationRefundRecoveryWatch.Install();
                RebirthCamperStationQueueRecoveryWatch.Install();
                RebirthCamperStationProcessingRecoveryWatch.Install();
                Patch(typeof(RebirthCamperStationCraftIngredients));
                Patch(typeof(RebirthCamperStationCraftActivation));
                Patch(typeof(RebirthCamperStationCraftCount));
#endif
                RebirthCamperLocalAccess.Register(authenticatedCreation);
                // Publish readiness only after every source, owner, device,
                // gesture and authenticated access hook has been installed.
                RebirthCamperCampDoor.Install();
                RebirthCamperCampEntryController.Install();
                RebirthCamperModeInput.Install();
                RebirthCamperCampSeatReturn.Install();
                RebirthCamperPhysicalInteraction.Install();
                RebirthCamperSink.Install();
                RebirthInventoryGestureTrace.Install();
                RebirthCamperEnergyRuntime.SetRegistered(true);
                IsInstalled = true;
            }
        }

        private static void Patch(Type type)
        {
            RebirthHarmonyBootstrap.PatchClassOnce(Harmony,type);
        }
    }
}

