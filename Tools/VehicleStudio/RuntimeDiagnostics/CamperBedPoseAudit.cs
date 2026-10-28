using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Platform;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Rebirth.VehicleStudio.Diagnostics
{
    // Visual-only preview infrastructure. Never invokes sleep/death gameplay APIs,
    // replaces a controller, changes seat roots, or supplies a fabricated lying pose.
    internal sealed class CamperBedPosePreview : MonoBehaviour
    {
        private EntityAlive occupant;
        private EntityVehicle camper;
        private readonly List<PlayableGraph> graphs = new List<PlayableGraph>();
        private readonly List<Tuple<Animator, int, float, bool>> restore = new List<Tuple<Animator, int, float, bool>>();

        internal static Animator[] BodyAnimators(EntityAlive player)
        {
            var avatar = player?.emodel?.avatarController as AvatarMultiBodyController;
            if (avatar == null) throw new InvalidOperationException("Living player MultiBody avatar required.");
            var animators = avatar.BodyAnimators.Select(body => body.Animator)
                .Where(animator => animator != null && animator.isActiveAndEnabled).Distinct().ToArray();
            if (animators.Length == 0) throw new InvalidOperationException("No active player body animators.");
            return animators;
        }

        internal static void Apply(EntityAlive player, EntityVehicle vehicle, string state)
        {
            if (player == null || player.IsDead() || vehicle == null || vehicle.GetAttachMaxCount() <= 4
                || vehicle.GetAttached(4) != player || player.AttachedToEntity != vehicle
                || EntityClass.list[vehicle.entityClass].entityClassName != "rebirthCamper80s")
                throw new InvalidOperationException("A living camper occupant in native slot4 is required.");
            var animators = BodyAnimators(player);
            int stateHash = Animator.StringToHash(state);
            var clips = new List<AnimationClip>();
            foreach (var animator in animators)
            {
                var controller = animator.runtimeAnimatorController;
                var matches = controller == null ? new AnimationClip[0]
                    : controller.animationClips.Where(clip => clip != null && clip.name == state && clip.humanMotion).Distinct().ToArray();
                // A known state hash alone does not prove a compatible lying clip.
                if (!animator.isHuman || !animator.HasState(0, stateHash) || matches.Length != 1)
                    throw new InvalidOperationException("No unambiguous humanoid lying state/clip '" + state
                        + "' on " + animator.name + ". Run camperbedposeaudit; author a scoped compatible lying clip if unavailable.");
                clips.Add(matches[0]);
            }
            var previous = player.gameObject.GetComponent<CamperBedPosePreview>();
            if (previous != null) { previous.Stop(); Destroy(previous); }
            var preview = player.gameObject.AddComponent<CamperBedPosePreview>();
            preview.occupant = player; preview.camper = vehicle;
            try
            {
                for (int i = 0; i < animators.Length; i++)
                {
                    var animator = animators[i];
                    var native = animator.GetCurrentAnimatorStateInfo(0);
                    preview.restore.Add(Tuple.Create(animator, native.fullPathHash, native.normalizedTime, animator.applyRootMotion));
                    animator.applyRootMotion = false;
                    var graph = PlayableGraph.Create("REBIRTHCamperBedVisualPreview");
                    preview.graphs.Add(graph);
                    graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                    var clip = AnimationClipPlayable.Create(graph, clips[i]);
                    clip.SetApplyFootIK(false); clip.SetApplyPlayableIK(false);
                    // Evaluate an actual lying animation frame; no horizontal sitting rotation.
                    clip.SetTime(clips[i].length * .5); clip.SetSpeed(0);
                    var output = AnimationPlayableOutput.Create(graph, "BedBody", animator);
                    output.SetSourcePlayable(clip);
                    graph.Play();
                }
            }
            catch { preview.Stop(); Destroy(preview); throw; }
        }

        private void LateUpdate()
        {
            if (occupant == null || camper == null || occupant.IsDead()
                || occupant.AttachedToEntity != camper || camper.GetAttached(4) != occupant)
            { Stop(); Destroy(this); }
        }
        internal void Stop()
        {
            foreach (var graph in graphs) if (graph.IsValid()) graph.Destroy();
            graphs.Clear();
            foreach (var state in restore)
            {
                if (state.Item1 == null) continue;
                state.Item1.applyRootMotion = state.Item4;
                // A death transition must retain its native animation.
                if (occupant != null && !occupant.IsDead() && state.Item1.runtimeAnimatorController != null
                    && state.Item1.HasState(0, state.Item2)) state.Item1.Play(state.Item2, 0, state.Item3);
            }
            restore.Clear();
        }
        private void OnDisable() { Stop(); }
        private void OnDestroy() { Stop(); }
    }

    public sealed class ConsoleCmdCamperBedPoseAudit : ConsoleCmdAbstract
    {
        public override bool IsExecuteOnClient => true;
        public override DeviceFlag AllowedDeviceTypesClient => DeviceFlag.StandaloneWindows;
        public override string[] getCommands() => new[] { "camperbedposeaudit" };
        public override string getDescription() => "Read-only living-player animator clips and native lying-state availability.";
        public override void Execute(List<string> args, CommandSenderInfo sender)
        {
            try
            {
                if (GameManager.IsDedicatedServer || GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest" || args.Count != 0)
                    throw new InvalidOperationException("camperbedposeaudit: CodexTest living local player, no arguments.");
                var player = GameManager.Instance.World.GetPrimaryPlayer();
                if (player == null || player.IsDead()) throw new InvalidOperationException("Living local player required.");
                var states = new[] { "SleeperIdleBack", "SleeperIdleSideLeft", "SleeperIdleSideRight", "SleeperIdleStomach" };
                var bodies = CamperBedPosePreview.BodyAnimators(player).Select(animator => new {
                    name = animator.name, humanoid = animator.isHuman,
                    controller = animator.runtimeAnimatorController == null ? null : animator.runtimeAnimatorController.name,
                    currentState = animator.GetCurrentAnimatorStateInfo(0).fullPathHash,
                    parameters = animator.parameters.Select(parameter => new { parameter.name, type = parameter.type.ToString() }).ToArray(),
                    lyingStates = states.Select(state => new { state, available = animator.HasState(0, Animator.StringToHash(state)) }).ToArray(),
                    clips = animator.runtimeAnimatorController == null ? new object[0]
                        : animator.runtimeAnimatorController.animationClips.Where(clip => clip != null).Distinct()
                            .Select(clip => (object)new { clip.name, clip.length, clip.humanMotion, clip.isLooping }).ToArray()
                }).ToArray();
                Directory.CreateDirectory(CamperDiagnosticsMod.ExportFolder);
                string path = Path.Combine(CamperDiagnosticsMod.ExportFolder, "bed_pose_audit_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff") + ".json");
                File.WriteAllText(path, JsonConvert.SerializeObject(new {
                    status = "READ_ONLY_LIVING_PLAYER_ANIMATOR_AUDIT_NOT_POSE_ACCEPTANCE",
                    player = player.entityId, avatar = player.emodel.avatarController.GetType().FullName, bodies
                }, Formatting.Indented));
                SdtdConsole.Instance.Output("[CamperBedPoseAudit] " + path + "; no animation or gameplay state changed.");
            }
            catch (Exception error) { SdtdConsole.Instance.Output("*** ERROR [CamperBedPoseAudit] " + error.Message); }
        }
    }

    public sealed class ConsoleCmdCamperBedPosePreview : ConsoleCmdAbstract
    {
        public override bool IsExecuteOnClient => true;
        public override DeviceFlag AllowedDeviceTypesClient => DeviceFlag.StandaloneWindows;
        public override string[] getCommands() => new[] { "camperbedposepreview" };
        public override string getDescription() => "Scoped visual-only native lying clip preview for camper slot4; restores on detach.";
        public override void Execute(List<string> args, CommandSenderInfo sender)
        {
            try
            {
                if (GameManager.IsDedicatedServer || GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest" || args.Count != 1)
                    throw new InvalidOperationException("camperbedposepreview back|left|right|stomach|clear: CodexTest only.");
                var player = GameManager.Instance.World.GetPrimaryPlayer();
                if (player == null) throw new InvalidOperationException("Local player required.");
                if (args[0] == "clear")
                {
                    var preview = player.gameObject.GetComponent<CamperBedPosePreview>();
                    if (preview != null) { preview.Stop(); UnityEngine.Object.Destroy(preview); }
                }
                else
                {
                    var states = new Dictionary<string, string> { { "back", "SleeperIdleBack" }, { "left", "SleeperIdleSideLeft" },
                        { "right", "SleeperIdleSideRight" }, { "stomach", "SleeperIdleStomach" } };
                    if (!states.TryGetValue(args[0], out string state)) throw new InvalidOperationException("Unknown lying preview posture.");
                    CamperBedPosePreview.Apply(player, player.AttachedToEntity as EntityVehicle, state);
                }
                SdtdConsole.Instance.Output("[CamperBedPosePreview] visual-only " + args[0] + "; fit and detach restoration require validation.");
            }
            catch (Exception error) { SdtdConsole.Instance.Output("*** ERROR [CamperBedPosePreview] " + error.Message); }
        }
    }
}
