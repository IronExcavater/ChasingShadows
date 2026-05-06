using System.Collections.Generic;
using System.Linq;
using ChasingShadows.Characters;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace ChasingShadows.Editor
{
    public static class CinematicTimelineSetup
    {
        private const string BasicRootName = "Joe_Cinematic_Basic_Setup";
        private const string ChaseRootName = "Joe_Chase_Setup";
        private const string TimelineFolder = "Assets/ChasingShadows/Timelines";
        private const string BasicTimelinePath = TimelineFolder + "/Joe_Cinematic_Basic.playable";
        private const string ChaseTimelinePath = TimelineFolder + "/Joe_Chase_Timeline.playable";
        private const string LegacyGenericTimelinePath = TimelineFolder + "/Joe_Cinematic_Timeline.playable";

        private const string JoePrefabPath = "Assets/ChasingShadows/Prefabs/Joe.prefab";
        private const string ShadowPrefabPath = "Assets/ChasingShadows/Prefabs/Shadow.prefab";

        private const string BasePrefabFolder = "Assets/ChasingShadows/Prefabs/Cinematic";
        private const string BaseRootPrefabPath = BasePrefabFolder + "/CinematicSequenceRoot.prefab";
        private const string BaseJoeRigPrefabPath = BasePrefabFolder + "/JoeCinematicRig.prefab";
        private const string BaseCameraRigPrefabPath = BasePrefabFolder + "/CinematicCameraRig.prefab";
        private const string BaseMarkerSetPrefabPath = BasePrefabFolder + "/CinematicMarkerSet.prefab";

        private static readonly string[] RequiredBasePrefabs =
        {
            BaseRootPrefabPath,
            BaseJoeRigPrefabPath,
            BaseCameraRigPrefabPath,
            BaseMarkerSetPrefabPath
        };

        private static readonly string[] PrefabAuthoringRootNames =
        {
            "CinematicSequenceRoot",
            "JoeCinematicRig",
            "CinematicMarkerSet",
            "CinematicCameraRig"
        };

        private static readonly string[] DuplicateGeneratedAssets =
        {
            LegacyGenericTimelinePath,
            BasePrefabFolder + "/CinematicObstacleMarkers.prefab",
            BasePrefabFolder + "/CinematicObstacleMarkers.prefab.meta",
            BasePrefabFolder + "/Basic",
            BasePrefabFolder + "/Basic.meta",
            BasePrefabFolder + "/Chase",
            BasePrefabFolder + "/Chase.meta"
        };

        [MenuItem("Chasing Shadows/Cinematics/Rebuild Base Prefabs")]
        public static void RebuildBaseCinematicPrefabKit()
        {
            DeleteExistingSetupRoots(PrefabAuthoringRootNames);
            EnsureFolder(BasePrefabFolder);
            SaveSequenceRootPrefab(BaseRootPrefabPath, "CinematicSequenceRoot");
            SaveJoeRigPrefab(BaseJoeRigPrefabPath, "JoeCinematicRig");
            SaveBaseMarkerSetPrefab();
            SaveBaseCameraRigPrefab();
            DeleteDuplicateGeneratedAssets();
            DeleteExistingSetupRoots(PrefabAuthoringRootNames);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Base cinematic prefabs rebuilt.");
        }

        [MenuItem("Chasing Shadows/Cinematics/Create Basic Timeline Setup")]
        public static void CreateBasicJoeCinematicSetup()
        {
            EnsureFolder(TimelineFolder);
            JoeAnimationSetup.SetupJoeAnimationSet();
            EnsureBaseCinematicPrefabKit();
            EnsureCinemachineBrain();

            var root = RecreateRootFromPrefab(
                BaseRootPrefabPath,
                BasicRootName,
                "Create basic Joe cinematic setup",
                "Joe_Cinematic_Setup");
            var joeRig = InstantiateChildPrefab(BaseJoeRigPrefabPath, FindOrCreateChild(root.transform, "Joe Rig"));
            var markerSet = InstantiateChildPrefab(BaseMarkerSetPrefabPath, FindOrCreateChild(root.transform, "Marker Set"));
            var cameraRig = InstantiateChildPrefab(BaseCameraRigPrefabPath, FindOrCreateChild(root.transform, "Camera Rig"));
            var director = root.GetComponentInChildren<PlayableDirector>(true) ?? CreateDirector(root.transform);

            var references = BuildTimelineReferences(joeRig, markerSet, cameraRig);
            ConfigureJoeAndShadow(references.joe, references.shadow);

            var timeline = CreateBasicTimelineAsset(references, director);
            director.playableAsset = timeline;
            BindTimeline(director, timeline, references);
            FinishSetup(root, references.joe);
        }

        [MenuItem("Chasing Shadows/Cinematics/Author Sample Chase Timeline")]
        public static void AuthorSampleChase()
        {
            EnsureFolder(TimelineFolder);
            JoeAnimationSetup.SetupJoeAnimationSet();
            EnsureBaseCinematicPrefabKit();
            EnsureCinemachineBrain();

            var root = RecreateRootFromPrefab(
                BaseRootPrefabPath,
                ChaseRootName,
                "Author sample chase",
                new[] { "Joe_Timeline_Setup", "Joe_Cinematic_Setup", BasicRootName }
                    .Concat(PrefabAuthoringRootNames)
                    .ToArray());
            var joeRig = InstantiateChildPrefab(BaseJoeRigPrefabPath, FindOrCreateChild(root.transform, "Joe Rig"));
            var markerSet = CreateChaseMarkerSet(FindOrCreateChild(root.transform, "Marker Set"));
            var cameraRig = CreateChaseCameraRig(FindOrCreateChild(root.transform, "Camera Rig"));
            var director = root.GetComponentInChildren<PlayableDirector>(true) ?? CreateDirector(root.transform);

            var references = BuildTimelineReferences(joeRig, markerSet, cameraRig);
            ConfigureJoeAndShadow(references.joe, references.shadow);

            var timeline = CreateChaseTimelineAsset(references, director);
            director.playableAsset = timeline;
            BindTimeline(director, timeline, references);
            FinishSetup(root, references.joe);
            DeleteDuplicateGeneratedAssets();
        }

        private static void SaveSequenceRootPrefab(string path, string rootName)
        {
            var root = new GameObject(rootName);
            CreateChild(root.transform, "Joe Rig");
            CreateChild(root.transform, "Marker Set");
            CreateChild(root.transform, "Camera Rig");
            CreateDirector(root.transform);
            SavePrefabAndDestroy(root, path);
        }

        private static void SaveJoeRigPrefab(string path, string rootName)
        {
            var root = new GameObject(rootName);
            var joePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(JoePrefabPath);
            var shadowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShadowPrefabPath);

            if (joePrefab == null)
            {
                Debug.LogWarning($"Joe prefab not found at {JoePrefabPath}.");
                SavePrefabAndDestroy(root, path);
                return;
            }

            var joeObject = PrefabUtility.InstantiatePrefab(joePrefab, root.transform) as GameObject;
            if (joeObject != null)
            {
                joeObject.name = "Joe";
                joeObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                joeObject.transform.localScale = Vector3.one;

                if (shadowPrefab != null)
                {
                    var shadowObject = PrefabUtility.InstantiatePrefab(shadowPrefab, joeObject.transform) as GameObject;
                    if (shadowObject != null)
                    {
                        shadowObject.name = "Joe_Shadow";
                        shadowObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                        shadowObject.transform.localScale = Vector3.one;
                    }
                }
            }

            var joe = root.GetComponentInChildren<JoeCinematicController>(true);
            var shadow = FindDeepChild(root.transform, "Joe_Shadow")?.gameObject;
            ConfigureJoeAndShadow(joe, shadow);
            SavePrefabAndDestroy(root, path);
        }

        private static void SaveBaseMarkerSetPrefab()
        {
            var root = new GameObject("CinematicMarkerSet");
            var path = CreateChild(root.transform, "Path Markers").transform;
            CreateMarker(path, "Move_Start", new Vector3(0f, 0f, 0f));
            CreateMarker(path, "Move_End", new Vector3(0f, 0f, 5f));

            var ik = CreateChild(root.transform, "IK Targets").transform;
            CreateMarker(ik, "Look_Target", new Vector3(0f, 1.5f, 3f));
            CreateMarker(ik, "LeftHand_Target", new Vector3(-0.35f, 1.1f, 2f));
            CreateMarker(ik, "RightHand_Target", new Vector3(0.35f, 1.1f, 2f));

            var actions = CreateChild(root.transform, "Action Markers").transform;
            CreateMarker(actions, "Action_Point", new Vector3(0f, 0f, 2.5f));
            CreateMarker(actions, "End_Point", new Vector3(0f, 0f, 5f));
            SavePrefabAndDestroy(root, BaseMarkerSetPrefabPath);
        }

        private static void SaveBaseCameraRigPrefab()
        {
            var root = new GameObject("CinematicCameraRig");
            CreateSequenceCamera(root.transform, "CM_Basic_IntroWide", new Vector3(-3.5f, 2f, -3f), new Vector3(12f, 35f, 0f), 42f);
            CreateSequenceCamera(root.transform, "CM_Basic_Follow", new Vector3(-2.2f, 1.6f, 1.4f), new Vector3(8f, 18f, 0f), 38f);
            CreateSequenceCamera(root.transform, "CM_Basic_Close", new Vector3(-1.4f, 1.25f, 4.5f), new Vector3(7f, 28f, 0f), 34f);
            SavePrefabAndDestroy(root, BaseCameraRigPrefabPath);
        }

        private static GameObject CreateChaseMarkerSet(Transform parent)
        {
            ClearChildren(parent);

            var root = CreateChild(parent, "ChaseMarkerSet");
            var path = CreateChild(root.transform, "Path Markers").transform;
            CreateMarker(path, "Move_Start", new Vector3(0f, 0f, 0f));
            CreateMarker(path, "Alley_Run_End", new Vector3(0f, 0f, 4.5f));
            CreateMarker(path, "Look_Back_End", new Vector3(-0.1f, 0f, 5.6f));
            CreateMarker(path, "Arc_Left_End", new Vector3(-1f, 0f, 7f));
            CreateMarker(path, "Arc_Right_End", new Vector3(0.5f, 0f, 8.4f));
            CreateMarker(path, "Jump_End", new Vector3(0.8f, 0f, 10.2f));
            CreateMarker(path, "Turn_End", new Vector3(1.8f, 0f, 11.1f));
            CreateMarker(path, "Wall_Base", new Vector3(1.8f, 0f, 12.3f));
            CreateMarker(path, "Wall_Hang", new Vector3(1.8f, 1.1f, 12.7f));
            CreateMarker(path, "Climb_Top", new Vector3(1.8f, 2.4f, 13.5f));
            CreateMarker(path, "Drop_Land", new Vector3(1.8f, 0f, 15f));
            CreateMarker(path, "Vault_Start", new Vector3(1.8f, 0f, 17f));
            CreateMarker(path, "Vault_End", new Vector3(2.3f, 0f, 18.2f));
            CreateMarker(path, "Stumble_End", new Vector3(2.6f, 0f, 19f));
            CreateMarker(path, "Slip_End", new Vector3(2.4f, 0f, 20f));
            CreateMarker(path, "Trip_Start", new Vector3(2.4f, 0f, 21.4f));
            CreateMarker(path, "Impact_Point", new Vector3(2.4f, 0f, 22.2f));
            CreateMarker(path, "Knockout_Point", new Vector3(2.4f, 0f, 23f));

            var ik = CreateChild(root.transform, "IK Targets").transform;
            CreateMarker(ik, "Look_Target", new Vector3(0f, 1.5f, 8f));
            CreateMarker(ik, "Look_Back_Target", new Vector3(-2.4f, 1.4f, 4.6f));
            CreateMarker(ik, "LeftHand_Target", new Vector3(-0.35f, 1.1f, 2f));
            CreateMarker(ik, "RightHand_Target", new Vector3(0.35f, 1.1f, 2f));
            CreateMarker(ik, "Climb_LeftHand_Target", new Vector3(1.25f, 1.85f, 12.9f));
            CreateMarker(ik, "Climb_RightHand_Target", new Vector3(2.15f, 2.05f, 13.05f));
            CreateMarker(ik, "Knockout_Look_Target", new Vector3(2.4f, 0.35f, 23.8f));

            var actions = CreateChild(root.transform, "Action Markers").transform;
            CreateMarker(actions, "Look_Back_Action", new Vector3(0f, 0f, 5f));
            CreateMarker(actions, "Jump_Action", new Vector3(0.5f, 0f, 9.2f));
            CreateMarker(actions, "Turn_Action", new Vector3(1.4f, 0f, 10.6f));
            CreateMarker(actions, "Stop_At_Wall_Action", new Vector3(1.8f, 0f, 12f));
            CreateMarker(actions, "Climb_Action", new Vector3(1.8f, 1.1f, 12.8f));
            CreateMarker(actions, "Drop_Action", new Vector3(1.8f, 2f, 14f));
            CreateMarker(actions, "Land_Action", new Vector3(1.8f, 0f, 15f));
            CreateMarker(actions, "Vault_Action", new Vector3(2f, 0f, 17.6f));
            CreateMarker(actions, "Stumble_Action", new Vector3(2.5f, 0f, 18.6f));
            CreateMarker(actions, "Slip_Action", new Vector3(2.5f, 0f, 19.5f));
            CreateMarker(actions, "Trip_Action", new Vector3(2.4f, 0f, 21.6f));
            CreateMarker(actions, "Impact_Action", new Vector3(2.4f, 0f, 22.2f));
            CreateMarker(actions, "Knockout_Action", new Vector3(2.4f, 0f, 23f));
            return root;
        }

        private static GameObject CreateChaseCameraRig(Transform parent)
        {
            ClearChildren(parent);

            var root = CreateChild(parent, "ChaseCameraRig");
            CreateSequenceCamera(root.transform, "CM_Chase_IntroWide", new Vector3(-4f, 2.2f, -3f), new Vector3(14f, 38f, 0f), 42f);
            CreateSequenceCamera(root.transform, "CM_Chase_Follow", new Vector3(-2.4f, 1.6f, 1.2f), new Vector3(8f, 18f, 0f), 38f);
            CreateSequenceCamera(root.transform, "CM_Chase_SideProfile", new Vector3(-4f, 1.5f, 8.5f), new Vector3(4f, 78f, 0f), 40f);
            CreateSequenceCamera(root.transform, "CM_Chase_Jump", new Vector3(-2.2f, 1.4f, 6.8f), new Vector3(8f, 45f, 0f), 36f);
            CreateSequenceCamera(root.transform, "CM_Chase_Climb", new Vector3(-2.2f, 2.8f, 11.2f), new Vector3(18f, 58f, 0f), 34f);
            CreateSequenceCamera(root.transform, "CM_Chase_Impact", new Vector3(-2.8f, 1.1f, 18.8f), new Vector3(6f, 70f, 0f), 32f);
            CreateSequenceCamera(root.transform, "CM_Chase_Knockout", new Vector3(0.2f, 0.65f, 21.9f), new Vector3(10f, 180f, 0f), 30f);
            return root;
        }

        private static TimelineAsset CreateBasicTimelineAsset(TimelineReferences references, PlayableDirector director)
        {
            DeleteAssetIfExists(BasicTimelinePath);

            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.editorSettings.frameRate = 24d;
            AssetDatabase.CreateAsset(timeline, BasicTimelinePath);
            director.playableAsset = timeline;

            var cameraTrack = timeline.CreateTrack<CinemachineTrack>(null, "Camera");
            AddCameraShot(director, cameraTrack, "Intro wide", 0d, 1d, references.GetCamera("CM_Basic_IntroWide"), "BasicCamera");
            AddCameraShot(director, cameraTrack, "Follow", 1d, 4d, references.GetCamera("CM_Basic_Follow"), "BasicCamera");
            AddCameraShot(director, cameraTrack, "Close", 5d, 1d, references.GetCamera("CM_Basic_Close"), "BasicCamera");

            var joeAnimation = timeline.CreateTrack<AnimationTrack>(null, "Joe Animation");
            joeAnimation.trackOffset = TrackOffset.Auto;
            AddAnimationClip(joeAnimation, "Ready hold", 0d, 1d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Breathing Idle.fbx",
                "Assets/ChasingShadows/Animations/Joe/Neutral Idle.fbx",
                "Assets/ChasingShadows/Animations/Joe/Idle.fbx"));
            AddAnimationClip(joeAnimation, "Move", 1d, 4d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Running Forwards.fbx",
                "Assets/ChasingShadows/Animations/Joe/Steady Run.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running.fbx"));
            AddAnimationClip(joeAnimation, "End hold", 5d, 1d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Breathing Idle.fbx",
                "Assets/ChasingShadows/Animations/Joe/Idle.fbx"));

            var joeMovement = timeline.CreateTrack<JoeMovementTrack>(null, "Joe Timeline Movement");
            AddMovementClip(director, joeMovement, "Intro hold", 0d, 1d, references.GetMarker("Move_Start"), null, true, "Basic");
            AddMovementClip(director, joeMovement, "Move", 1d, 4d, references.GetMarker("Move_Start"), references.GetMarker("Move_End"), true, "Basic");
            AddMovementClip(director, joeMovement, "End hold", 5d, 1d, references.GetMarker("Move_End"), null, true, "Basic");

            var shadowAnimation = timeline.CreateTrack<AnimationTrack>(null, "Shadow Animation");
            shadowAnimation.trackOffset = TrackOffset.Auto;
            AddAnimationClip(shadowAnimation, "Shadow ready hold", 0d, 1d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Breathing Idle.fbx",
                "Assets/ChasingShadows/Animations/Joe/Idle.fbx"));
            AddAnimationClip(shadowAnimation, "Shadow move", 1d, 4d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Running Forwards.fbx",
                "Assets/ChasingShadows/Animations/Joe/Steady Run.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running.fbx"));
            AddAnimationClip(shadowAnimation, "Shadow end hold", 5d, 1d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Breathing Idle.fbx",
                "Assets/ChasingShadows/Animations/Joe/Idle.fbx"));

            var cueTrack = timeline.CreateTrack<JoeCueTrack>(null, "Joe Cues");
            AddCue(director, cueTrack, "Look", 0d, 6d, string.Empty, references.GetMarker("Look_Target"), null, null, 0.35f, 0f, 0.85f, "Basic");

            var shadowTrack = timeline.CreateTrack<ActivationTrack>(null, "Shadow Active");
            var activeClip = shadowTrack.CreateDefaultClip();
            activeClip.displayName = "Shadow on";
            activeClip.start = 0d;
            activeClip.duration = 6d;

            timeline.durationMode = TimelineAsset.DurationMode.BasedOnClips;
            EditorUtility.SetDirty(timeline);
            return timeline;
        }

        private static TimelineAsset CreateChaseTimelineAsset(TimelineReferences references, PlayableDirector director)
        {
            DeleteAssetIfExists(ChaseTimelinePath);

            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.editorSettings.frameRate = 24d;
            AssetDatabase.CreateAsset(timeline, ChaseTimelinePath);
            director.playableAsset = timeline;

            var cameraTrack = timeline.CreateTrack<CinemachineTrack>(null, "Camera");
            AddCameraShot(director, cameraTrack, "Intro wide", 0d, 0.9d, references.GetCamera("CM_Chase_IntroWide"), "ChaseCamera");
            AddCameraShot(director, cameraTrack, "Follow run", 0.9d, 2.3d, references.GetCamera("CM_Chase_Follow"), "ChaseCamera");
            AddCameraShot(director, cameraTrack, "Side weave", 3.2d, 2d, references.GetCamera("CM_Chase_SideProfile"), "ChaseCamera");
            AddCameraShot(director, cameraTrack, "Jump gap", 5.2d, 1d, references.GetCamera("CM_Chase_Jump"), "ChaseCamera");
            AddCameraShot(director, cameraTrack, "Turn wall", 6.2d, 1.8d, references.GetCamera("CM_Chase_SideProfile"), "ChaseCamera");
            AddCameraShot(director, cameraTrack, "Wall climb", 8d, 4.1d, references.GetCamera("CM_Chase_Climb"), "ChaseCamera");
            AddCameraShot(director, cameraTrack, "Impact chain", 12.1d, 8d, references.GetCamera("CM_Chase_Impact"), "ChaseCamera");
            AddCameraShot(director, cameraTrack, "Knockout", 20.1d, 2.9d, references.GetCamera("CM_Chase_Knockout"), "ChaseCamera");

            var joeAnimation = timeline.CreateTrack<AnimationTrack>(null, "Joe Animation");
            joeAnimation.trackOffset = TrackOffset.Auto;
            AddChaseAnimationBeatClips(joeAnimation, string.Empty);

            var joeMovement = timeline.CreateTrack<JoeMovementTrack>(null, "Joe Timeline Movement");
            AddMovementClip(director, joeMovement, "Intro Hold", 0d, 0.9d, references.GetMarker("Move_Start"), null, true, "Chase");
            AddMovementClip(director, joeMovement, "Alley Run", 0.9d, 1.5d, references.GetMarker("Move_Start"), references.GetMarker("Alley_Run_End"), true, "Chase");
            AddMovementClip(director, joeMovement, "Look Back", 2.4d, 0.8d, references.GetMarker("Alley_Run_End"), references.GetMarker("Look_Back_End"), true, "Chase");
            AddMovementClip(director, joeMovement, "Arc Left", 3.2d, 1d, references.GetMarker("Look_Back_End"), references.GetMarker("Arc_Left_End"), true, "Chase");
            AddMovementClip(director, joeMovement, "Arc Right", 4.2d, 1d, references.GetMarker("Arc_Left_End"), references.GetMarker("Arc_Right_End"), true, "Chase");
            AddMovementClip(director, joeMovement, "Jump Gap", 5.2d, 1d, references.GetMarker("Arc_Right_End"), references.GetMarker("Jump_End"), true, "Chase");
            AddMovementClip(director, joeMovement, "About Face", 6.2d, 0.9d, references.GetMarker("Jump_End"), references.GetMarker("Turn_End"), true, "Chase");
            AddMovementClip(director, joeMovement, "Stop At Wall", 7.1d, 0.9d, references.GetMarker("Turn_End"), references.GetMarker("Wall_Base"), true, "Chase");
            AddMovementClip(director, joeMovement, "Catch Wall", 8d, 1.2d, references.GetMarker("Wall_Base"), references.GetMarker("Wall_Hang"), false, "Chase");
            AddMovementClip(director, joeMovement, "Wall Climb", 9.2d, 2.1d, references.GetMarker("Wall_Hang"), references.GetMarker("Climb_Top"), false, "Chase");
            AddMovementClip(director, joeMovement, "Drop", 11.3d, 0.8d, references.GetMarker("Climb_Top"), references.GetMarker("Drop_Land"), false, "Chase");
            AddMovementClip(director, joeMovement, "Land Hold", 12.1d, 0.7d, references.GetMarker("Drop_Land"), null, true, "Chase");
            AddMovementClip(director, joeMovement, "Final Sprint", 12.8d, 1.3d, references.GetMarker("Drop_Land"), references.GetMarker("Vault_Start"), true, "Chase");
            AddMovementClip(director, joeMovement, "Vault", 14.1d, 0.9d, references.GetMarker("Vault_Start"), references.GetMarker("Vault_End"), true, "Chase");
            AddMovementClip(director, joeMovement, "Vault Stumble", 15d, 0.8d, references.GetMarker("Vault_End"), references.GetMarker("Stumble_End"), true, "Chase");
            AddMovementClip(director, joeMovement, "Edge Slip", 15.8d, 0.9d, references.GetMarker("Stumble_End"), references.GetMarker("Slip_End"), true, "Chase");
            AddMovementClip(director, joeMovement, "Recovery Sprint", 16.7d, 1.5d, references.GetMarker("Slip_End"), references.GetMarker("Trip_Start"), true, "Chase");
            AddMovementClip(director, joeMovement, "Trip Roll", 18.2d, 1.2d, references.GetMarker("Trip_Start"), references.GetMarker("Impact_Point"), true, "Chase");
            AddMovementClip(director, joeMovement, "Impact Settle", 19.4d, 0.7d, references.GetMarker("Impact_Point"), references.GetMarker("Knockout_Point"), true, "Chase");
            AddMovementClip(director, joeMovement, "Knockout Hold", 20.1d, 2.9d, references.GetMarker("Knockout_Point"), null, true, "Chase");

            var shadowAnimation = timeline.CreateTrack<AnimationTrack>(null, "Shadow Animation");
            shadowAnimation.trackOffset = TrackOffset.Auto;
            AddShadowChaseAnimationBeatClips(shadowAnimation);

            var cueTrack = timeline.CreateTrack<JoeCueTrack>(null, "Joe Cues");
            AddCue(director, cueTrack, "Run Start", 0d, 0.9d, "RunStart", references.GetMarker("Look_Target"), null, null, 0.35f, 0f, 0.85f, "Chase");
            AddCue(director, cueTrack, "Look Back", 2.4d, 0.8d, "LookBack", references.GetMarker("Look_Back_Target"), null, null, 0.8f, 0f, 0.85f, "Chase");
            AddCue(director, cueTrack, "Jump", 5.2d, 1d, "Jump", references.GetMarker("Look_Target"), null, null, 0.45f, 0f, 0.85f, "Chase");
            AddCue(director, cueTrack, "About Face", 6.2d, 0.9d, "AboutFace", references.GetMarker("Look_Target"), null, null, 0.45f, 0f, 0.85f, "Chase");
            AddCue(director, cueTrack, "Stop At Wall", 7.1d, 0.9d, "StopAtWall", references.GetMarker("Look_Target"), null, null, 0.45f, 0f, 0.85f, "Chase");
            AddCue(director, cueTrack, "Climb", 8d, 3.3d, "Climb", references.GetMarker("Look_Target"), references.GetMarker("Climb_LeftHand_Target"), references.GetMarker("Climb_RightHand_Target"), 0.55f, 0.75f, 0.55f, "Chase");
            AddCue(director, cueTrack, "Drop", 11.3d, 0.8d, "Drop", references.GetMarker("Look_Target"), null, null, 0.45f, 0f, 0.35f, "Chase");
            AddCue(director, cueTrack, "Land", 12.1d, 0.7d, "Land", references.GetMarker("Look_Target"), null, null, 0.45f, 0f, 0.85f, "Chase");
            AddCue(director, cueTrack, "Vault", 14.1d, 0.9d, "Vault", references.GetMarker("Look_Target"), null, null, 0.45f, 0f, 0.85f, "Chase");
            AddCue(director, cueTrack, "Vault Stumble", 15d, 0.8d, "VaultStumble", references.GetMarker("Look_Target"), null, null, 0.55f, 0f, 0.75f, "Chase");
            AddCue(director, cueTrack, "Edge Slip", 15.8d, 0.9d, "EdgeSlip", references.GetMarker("Look_Target"), null, null, 0.55f, 0f, 0.75f, "Chase");
            AddCue(director, cueTrack, "Stumble", 18.2d, 1.2d, "Stumble", references.GetMarker("Knockout_Look_Target"), null, null, 0.65f, 0f, 0.65f, "Chase");
            AddCue(director, cueTrack, "Fall Impact", 19.4d, 0.7d, "FallImpact", references.GetMarker("Knockout_Look_Target"), null, null, 0.65f, 0f, 0.45f, "Chase");
            AddCue(director, cueTrack, "Knockout", 20.1d, 2.9d, "Knockout", references.GetMarker("Knockout_Look_Target"), null, null, 0.45f, 0f, 0.35f, "Chase");

            var shadowTrack = timeline.CreateTrack<ActivationTrack>(null, "Shadow Active");
            var activeClip = shadowTrack.CreateDefaultClip();
            activeClip.displayName = "Shadow on";
            activeClip.start = 0d;
            activeClip.duration = 23d;

            timeline.durationMode = TimelineAsset.DurationMode.BasedOnClips;
            EditorUtility.SetDirty(timeline);
            return timeline;
        }

        private static void AddChaseAnimationBeatClips(AnimationTrack track, string displayPrefix)
        {
            var prefix = string.IsNullOrWhiteSpace(displayPrefix) ? string.Empty : displayPrefix + " ";

            AddAnimationClip(track, prefix + "Run Start", 0d, 0.9d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Idle To Sprint.fbx",
                "Assets/ChasingShadows/Animations/Joe/Adventure Run.fbx"));
            AddAnimationClip(track, prefix + "Alley Run", 0.9d, 1.5d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Running Forwards.fbx",
                "Assets/ChasingShadows/Animations/Joe/Steady Run.fbx",
                "Assets/ChasingShadows/Animations/Joe/Adventure Run.fbx"));
            AddAnimationClip(track, prefix + "Look Back", 2.4d, 0.8d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Run Look Back.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running Forwards.fbx"));
            AddAnimationClip(track, prefix + "Arc Left", 3.2d, 1d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Run Forward Arc Left.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running Forwards.fbx"));
            AddAnimationClip(track, prefix + "Arc Right", 4.2d, 1d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Run Forward Arc Right.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running Forwards.fbx"));
            AddAnimationClip(track, prefix + "Jump Gap", 5.2d, 1d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Chase Jump.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running Jump Up.fbx",
                "Assets/ChasingShadows/Animations/Joe/Jumping Up.fbx"));
            AddAnimationClip(track, prefix + "About Face", 6.2d, 0.9d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Running About Face.fbx",
                "Assets/ChasingShadows/Animations/Joe/Long Running About Face.fbx",
                "Assets/ChasingShadows/Animations/Joe/Left Sharp Turn.fbx"));
            AddAnimationClip(track, prefix + "Stop At Wall", 7.1d, 0.9d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Run To Stop At Wall.fbx",
                "Assets/ChasingShadows/Animations/Joe/Run To Stop.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running Turn To Idle.fbx"));
            AddAnimationClip(track, prefix + "Catch Wall", 8d, 1.2d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Jump To Climb Wall.fbx",
                "Assets/ChasingShadows/Animations/Joe/Jump To Hang.fbx"));
            AddAnimationClip(track, prefix + "Wall Climb", 9.2d, 2.1d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Hanging Wall Ascend.fbx",
                "Assets/ChasingShadows/Animations/Joe/Sprint Climb Ascend.fbx",
                "Assets/ChasingShadows/Animations/Joe/Run Climb Ascend.fbx",
                "Assets/ChasingShadows/Animations/Joe/Climbing Up Wall.fbx"));
            AddAnimationClip(track, prefix + "Drop", 11.3d, 0.8d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Dismount Jump From Wall.fbx",
                "Assets/ChasingShadows/Animations/Joe/Jumping Down.fbx",
                "Assets/ChasingShadows/Animations/Joe/Anxious Jump Down.fbx"));
            AddAnimationClip(track, prefix + "Land", 12.1d, 0.7d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Hard Landing.fbx",
                "Assets/ChasingShadows/Animations/Joe/Heavy Hard Landing.fbx",
                "Assets/ChasingShadows/Animations/Joe/Falling To Landing.fbx"));
            AddAnimationClip(track, prefix + "Final Sprint", 12.8d, 1.3d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Run Forward Arc Right.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running Forwards.fbx",
                "Assets/ChasingShadows/Animations/Joe/Steady Run.fbx"));
            AddAnimationClip(track, prefix + "Vault", 14.1d, 0.9d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Vault Over Box.fbx"));
            AddAnimationClip(track, prefix + "Vault Stumble", 15d, 0.8d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Vault Stumble.fbx",
                "Assets/ChasingShadows/Animations/Joe/Jogging Stumble.fbx"));
            AddAnimationClip(track, prefix + "Edge Slip", 15.8d, 0.9d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Edge Slip.fbx",
                "Assets/ChasingShadows/Animations/Joe/Jogging Stumble.fbx"));
            AddAnimationClip(track, prefix + "Recovery Sprint", 16.7d, 1.5d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Jogging Stumble.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running Forwards.fbx"));
            AddAnimationClip(track, prefix + "Trip Roll", 18.2d, 1.2d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Running Trip Roll Onto Side.fbx",
                "Assets/ChasingShadows/Animations/Joe/Walking Trip Onto Side.fbx",
                "Assets/ChasingShadows/Animations/Joe/Falling Roll.fbx"));
            AddAnimationClip(track, prefix + "Impact", 19.4d, 0.7d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Falling Flat Impact.fbx",
                "Assets/ChasingShadows/Animations/Joe/Walking Trip Fall Flat.fbx",
                "Assets/ChasingShadows/Animations/Joe/Falling To Landing.fbx"));
            AddAnimationClip(track, prefix + "Knocked Out Hold", 20.1d, 2.9d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Sleep Idle.fbx",
                "Assets/ChasingShadows/Animations/Joe/Sleeping Restless.fbx",
                "Assets/ChasingShadows/Animations/Joe/Sleeping Mild Cough.fbx"));
        }

        // Shadow animation beats: more athletic/parkour alternatives to Joe's clips.
        // Clips play at 1.15x speed and use sharper variants to reinforce the "agile shadow leading Joe" feel.
        private static void AddShadowChaseAnimationBeatClips(AnimationTrack track)
        {
            const double speed = 1.15;

            AddAnimationClipScaled(track, "Shadow Run Start", 0d, 0.9d, speed, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Idle To Sprint.fbx",
                "Assets/ChasingShadows/Animations/Joe/Adventure Run.fbx"));
            AddAnimationClipScaled(track, "Shadow Alley Run", 0.9d, 1.5d, speed, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Run Forward Arc Left.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running Forwards.fbx",
                "Assets/ChasingShadows/Animations/Joe/Steady Run.fbx"));
            AddAnimationClipScaled(track, "Shadow Look Back", 2.4d, 0.8d, speed, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Run Look Back.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running Forwards.fbx"));
            AddAnimationClipScaled(track, "Shadow Arc Left", 3.2d, 1d, speed, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Left Sharp Turn.fbx",
                "Assets/ChasingShadows/Animations/Joe/Run Forward Arc Left.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running Forwards.fbx"));
            AddAnimationClipScaled(track, "Shadow Arc Right", 4.2d, 1d, speed, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Right Sharp Turn.fbx",
                "Assets/ChasingShadows/Animations/Joe/Run Forward Arc Right.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running Forwards.fbx"));
            AddAnimationClipScaled(track, "Shadow Jump Gap", 5.2d, 1d, speed, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Running Jump Up.fbx",
                "Assets/ChasingShadows/Animations/Joe/Chase Jump.fbx",
                "Assets/ChasingShadows/Animations/Joe/Jumping Up.fbx"));
            AddAnimationClipScaled(track, "Shadow About Face", 6.2d, 0.9d, speed, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Long Running About Face.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running About Face.fbx",
                "Assets/ChasingShadows/Animations/Joe/Left Sharp Turn.fbx"));
            AddAnimationClipScaled(track, "Shadow Stop At Wall", 7.1d, 0.9d, speed, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Running Turn To Idle.fbx",
                "Assets/ChasingShadows/Animations/Joe/Run To Stop At Wall.fbx",
                "Assets/ChasingShadows/Animations/Joe/Run To Stop.fbx"));
            AddAnimationClipScaled(track, "Shadow Catch Wall", 8d, 1.2d, speed, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Sprint Climb Ascend.fbx",
                "Assets/ChasingShadows/Animations/Joe/Jump To Climb Wall.fbx",
                "Assets/ChasingShadows/Animations/Joe/Jump To Hang.fbx"));
            AddAnimationClipScaled(track, "Shadow Wall Climb", 9.2d, 2.1d, speed, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Climbing Up Wall.fbx",
                "Assets/ChasingShadows/Animations/Joe/Sprint Climb Ascend.fbx",
                "Assets/ChasingShadows/Animations/Joe/Run Climb Ascend.fbx"));
            AddAnimationClipScaled(track, "Shadow Drop", 11.3d, 0.8d, speed, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Anxious Jump Down.fbx",
                "Assets/ChasingShadows/Animations/Joe/Dismount Jump From Wall.fbx",
                "Assets/ChasingShadows/Animations/Joe/Jumping Down.fbx"));
            AddAnimationClipScaled(track, "Shadow Land", 12.1d, 0.7d, speed, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Falling To Landing.fbx",
                "Assets/ChasingShadows/Animations/Joe/Hard Landing.fbx",
                "Assets/ChasingShadows/Animations/Joe/Heavy Hard Landing.fbx"));
            AddAnimationClipScaled(track, "Shadow Final Sprint", 12.8d, 1.3d, speed, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Run Forward Arc Right.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running Forwards.fbx",
                "Assets/ChasingShadows/Animations/Joe/Steady Run.fbx"));
            AddAnimationClipScaled(track, "Shadow Vault", 14.1d, 0.9d, speed, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Vault Over Box.fbx"));
            AddAnimationClipScaled(track, "Shadow Vault Stumble", 15d, 0.8d, speed, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Vault Stumble.fbx",
                "Assets/ChasingShadows/Animations/Joe/Jogging Stumble.fbx"));
            AddAnimationClipScaled(track, "Shadow Edge Slip", 15.8d, 0.9d, speed, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Edge Slip.fbx",
                "Assets/ChasingShadows/Animations/Joe/Jogging Stumble.fbx"));
            AddAnimationClipScaled(track, "Shadow Recovery Sprint", 16.7d, 1.5d, speed, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Running Forwards.fbx",
                "Assets/ChasingShadows/Animations/Joe/Steady Run.fbx"));
            AddAnimationClipScaled(track, "Shadow Trip Roll", 18.2d, 1.2d, speed, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Falling Roll.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running Trip Roll Onto Side.fbx",
                "Assets/ChasingShadows/Animations/Joe/Walking Trip Onto Side.fbx"));
            AddAnimationClipScaled(track, "Shadow Impact", 19.4d, 0.7d, speed, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Falling Flat Impact.fbx",
                "Assets/ChasingShadows/Animations/Joe/Walking Trip Fall Flat.fbx",
                "Assets/ChasingShadows/Animations/Joe/Falling To Landing.fbx"));
            AddAnimationClipScaled(track, "Shadow Knocked Out Hold", 20.1d, 2.9d, 1d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Sleep Idle.fbx",
                "Assets/ChasingShadows/Animations/Joe/Sleeping Restless.fbx",
                "Assets/ChasingShadows/Animations/Joe/Sleeping Mild Cough.fbx"));
        }

        private static void AddAnimationClipScaled(AnimationTrack track, string name, double start, double duration, double timeScale, AnimationClip animationClip)
        {
            var clip = track.CreateClip<AnimationPlayableAsset>();
            clip.displayName = name;
            clip.start = start;
            clip.duration = duration;
            // Fit animation to window first, then apply the intended speed multiplier on top.
            var fitScale = animationClip != null && animationClip.length > (float)duration + 0.05f
                ? animationClip.length / duration
                : 1.0;
            clip.timeScale = fitScale * timeScale;
            clip.easeInDuration = System.Math.Min(0.12, duration * 0.4);
            clip.easeOutDuration = System.Math.Min(0.12, duration * 0.4);

            if (clip.asset is AnimationPlayableAsset asset)
            {
                asset.clip = animationClip != null ? animationClip : LoadPlaceholderClip();
                EditorUtility.SetDirty(asset);
            }
        }

        private static void BindTimeline(PlayableDirector director, TimelineAsset timeline, TimelineReferences references)
        {
            foreach (var track in timeline.GetOutputTracks())
            {
                switch (track.name)
                {
                    case "Camera":
                        director.SetGenericBinding(track, EnsureCinemachineBrain());
                        break;
                    case "Joe Animation":
                        director.SetGenericBinding(track, references.joeAnimator);
                        break;
                    case "Joe Timeline Movement":
                        director.SetGenericBinding(track, references.joe);
                        break;
                    case "Shadow Animation":
                        director.SetGenericBinding(track, references.shadowAnimator);
                        break;
                    case "Joe Cues":
                        director.SetGenericBinding(track, references.joe);
                        break;
                    case "Shadow Active":
                        director.SetGenericBinding(track, references.shadow);
                        break;
                }
            }
        }

        private static void AddCameraShot(PlayableDirector director, CinemachineTrack track, string name, double start, double duration, CinemachineCamera camera, string referenceNamespace)
        {
            var clip = track.CreateDefaultClip();
            clip.displayName = name;
            clip.start = start;
            clip.duration = duration;

            if (clip.asset is CinemachineShot shot && camera != null)
            {
                var key = new PropertyName($"{referenceNamespace}_{SanitizeKey(name)}");
                shot.VirtualCamera.exposedName = key;
                director.SetReferenceValue(key, camera);
                EditorUtility.SetDirty(shot);
            }
        }

        private static void AddAnimationClip(AnimationTrack track, string name, double start, double duration, AnimationClip animationClip, double easeIn = 0.12, double easeOut = 0.12)
        {
            var clip = track.CreateClip<AnimationPlayableAsset>();
            clip.displayName = name;
            clip.start = start;
            clip.duration = duration;
            // Cap ease durations so they never exceed 40% of clip length
            clip.easeInDuration = System.Math.Min(easeIn, duration * 0.4);
            clip.easeOutDuration = System.Math.Min(easeOut, duration * 0.4);
            // Compress animations longer than the beat window so they complete fully within the slot.
            // Locomotion clips shorter than the window play at 1x and loop naturally via loopTime.
            if (animationClip != null && animationClip.length > (float)duration + 0.05f)
            {
                clip.timeScale = animationClip.length / duration;
            }

            if (clip.asset is AnimationPlayableAsset asset)
            {
                asset.clip = animationClip != null ? animationClip : LoadPlaceholderClip();
                EditorUtility.SetDirty(asset);
            }
        }

        private static void AddMovementClip(PlayableDirector director, JoeMovementTrack track, string name, double start, double duration, Transform from, Transform to, bool projectToGround, string referenceNamespace)
        {
            var clip = track.CreateClip<JoeMovementTimelineClip>();
            clip.displayName = name;
            clip.start = start;
            clip.duration = duration;

            if (clip.asset is JoeMovementTimelineClip movement)
            {
                movement.mode = to == null ? JoeTimelineMotionMode.Hold : JoeTimelineMotionMode.MoveTo;
                movement.projectToGround = projectToGround;
                movement.faceMotion = to != null;
                movement.smoothStep = true;
                SetExposedReference(director, ref movement.start, from, $"{name}_Start", referenceNamespace);
                SetExposedReference(director, ref movement.end, to, $"{name}_End", referenceNamespace);
                EditorUtility.SetDirty(movement);
            }
        }

        private static void AddCue(PlayableDirector director, JoeCueTrack track, string name, double start, double duration, string trigger, Transform look, Transform leftHand, Transform rightHand, float lookWeight, float handWeight, float footWeight, string referenceNamespace)
        {
            var clip = track.CreateClip<JoeCueClip>();
            clip.displayName = name;
            clip.start = start;
            clip.duration = duration;

            if (clip.asset is JoeCueClip cue)
            {
                cue.animatorTrigger = trigger;
                cue.lookWeight = lookWeight;
                cue.handWeight = handWeight;
                cue.footWeight = footWeight;
                cue.setRootMotion = false;
                cue.clearHandsOnExit = true;
                cue.clearRootMotionOnExit = true;
                SetExposedReference(director, ref cue.lookTarget, look, $"{name}_Look", referenceNamespace);
                SetExposedReference(director, ref cue.leftHandTarget, leftHand, $"{name}_LeftHand", referenceNamespace);
                SetExposedReference(director, ref cue.rightHandTarget, rightHand, $"{name}_RightHand", referenceNamespace);
                EditorUtility.SetDirty(cue);
            }
        }

        private static void SetExposedReference<T>(PlayableDirector director, ref ExposedReference<T> reference, T value, string key, string referenceNamespace) where T : Object
        {
            reference.defaultValue = value;
            if (value == null)
            {
                return;
            }

            var exposedName = new PropertyName($"{referenceNamespace}_{SanitizeKey(key)}");
            reference.exposedName = exposedName;
            director.SetReferenceValue(exposedName, value);
        }

        private static TimelineReferences BuildTimelineReferences(GameObject joeRig, GameObject markerSet, GameObject cameraRig)
        {
            var joe = joeRig != null ? joeRig.GetComponentInChildren<JoeCinematicController>(true) : null;
            var shadow = joe != null ? FindDeepChild(joe.transform, "Joe_Shadow")?.gameObject : null;
            var markers = markerSet != null
                ? markerSet.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First())
                : new Dictionary<string, Transform>();
            var cameras = cameraRig != null
                ? cameraRig.GetComponentsInChildren<CinemachineCamera>(true).GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First())
                : new Dictionary<string, CinemachineCamera>();

            return new TimelineReferences
            {
                joe = joe,
                joeAnimator = joe != null ? joe.GetComponent<Animator>() : null,
                shadow = shadow,
                shadowAnimator = shadow != null ? shadow.GetComponent<Animator>() : null,
                markers = markers,
                cameras = cameras
            };
        }

        private static void ConfigureJoeAndShadow(JoeCinematicController joe, GameObject shadow)
        {
            if (joe == null)
            {
                return;
            }

            joe.name = "Joe";
            joe.enableBaseLocomotion = false;
            joe.SetRootMotionEnabled(false);

            var joeAnimator = joe.GetComponent<Animator>();
            if (joeAnimator != null)
            {
                // Null the controller so the AnimationTrack is the sole animation driver in cinematic.
                // Having the controller active while an AnimationTrack is bound causes the blend tree
                // to compete with the timeline clips, producing wrong animations.
                joeAnimator.runtimeAnimatorController = null;
                joeAnimator.enabled = true;
                joeAnimator.applyRootMotion = false;
            }

            if (shadow == null)
            {
                return;
            }

            shadow.name = "Joe_Shadow";
            // Place shadow 2.5m ahead of Joe in local space — it's parented to Joe so this
            // keeps it consistently in front regardless of Joe's world position or rotation.
            shadow.transform.SetLocalPositionAndRotation(new Vector3(0f, 0f, 2.5f), Quaternion.identity);
            shadow.transform.localScale = Vector3.one;

            foreach (var controller in shadow.GetComponentsInChildren<JoeCinematicController>(true))
            {
                Object.DestroyImmediate(controller);
            }

            var shadowAnimator = shadow.GetComponent<Animator>();
            if (shadowAnimator != null && joeAnimator != null)
            {
                shadowAnimator.avatar = joeAnimator.avatar;
                // Shadow is also driven purely by its AnimationTrack — no controller needed.
                shadowAnimator.runtimeAnimatorController = null;
                shadowAnimator.applyRootMotion = false;
                shadowAnimator.enabled = true;
            }

            var shadowRenderers = new HashSet<Renderer>(shadow.GetComponentsInChildren<Renderer>(true));
            foreach (var renderer in joe.GetComponentsInChildren<Renderer>(true))
            {
                if (!shadowRenderers.Contains(renderer))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            foreach (var renderer in shadowRenderers)
            {
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                renderer.receiveShadows = false;
            }
        }

        private static GameObject RecreateRootFromPrefab(string prefabPath, string rootName, string undoLabel, params string[] extraRootNamesToDelete)
        {
            DeleteExistingSetupRoots(new[] { rootName }.Concat(extraRootNamesToDelete));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var root = prefab != null ? PrefabUtility.InstantiatePrefab(prefab) as GameObject : new GameObject(rootName);
            if (root == null)
            {
                root = new GameObject(rootName);
            }

            root.name = rootName;
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            Undo.RegisterCreatedObjectUndo(root, undoLabel);
            return root;
        }

        private static void DeleteExistingSetupRoots(IEnumerable<string> names)
        {
            var lookup = new HashSet<string>(names);
            var rootsToDelete = Object
                .FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(gameObject =>
                    gameObject != null &&
                    gameObject.scene.IsValid() &&
                    gameObject.transform.parent == null &&
                    lookup.Contains(gameObject.name))
                .ToList();

            foreach (var gameObject in rootsToDelete)
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private static GameObject InstantiateChildPrefab(string path, Transform parent)
        {
            ClearChildren(parent);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"Cinematic prefab not found at {path}.");
                return null;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
            {
                return null;
            }

            instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static void EnsureBaseCinematicPrefabKit()
        {
            if (RequiredBasePrefabs.All(path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null))
            {
                return;
            }

            RebuildBaseCinematicPrefabKit();
        }

        private static CinemachineBrain EnsureCinemachineBrain()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 1.7f, -5f), Quaternion.Euler(10f, 0f, 0f));
                camera = cameraObject.AddComponent<Camera>();
            }

            var brain = camera.GetComponent<CinemachineBrain>();
            if (brain == null)
            {
                brain = camera.gameObject.AddComponent<CinemachineBrain>();
            }

            return brain;
        }

        private static PlayableDirector CreateDirector(Transform parent)
        {
            var directorObject = CreateChild(parent, "TimelineDirector");
            var director = directorObject.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.Hold;
            return director;
        }

        private static AnimationClip LoadPlaceholderClip()
        {
            return JoeAnimationSetup.PlaceholderClip();
        }

        private static AnimationClip LoadAnimationClip(params string[] paths)
        {
            return JoeAnimationSetup.FindClip(paths) ?? LoadPlaceholderClip();
        }

        private static CinemachineCamera CreateSequenceCamera(Transform parent, string name, Vector3 position, Vector3 euler, float fieldOfView)
        {
            var cameraObject = CreateChild(parent, name);
            cameraObject.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(euler));
            var camera = cameraObject.AddComponent<CinemachineCamera>();
            camera.Lens.FieldOfView = fieldOfView;
            return camera;
        }

        private static Transform CreateMarker(Transform parent, string name, Vector3 localPosition)
        {
            var marker = CreateChild(parent, name);
            marker.transform.localPosition = localPosition;
            return marker.transform;
        }

        private static Transform FindOrCreateChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            return existing != null ? existing : CreateChild(parent, name).transform;
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            foreach (var child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void ClearChildren(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static void FinishSetup(GameObject root, JoeCinematicController joe)
        {
            joe?.Stop();
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            AssetDatabase.SaveAssets();
        }

        private static void SavePrefabAndDestroy(GameObject root, string path)
        {
            var previewScene = EditorSceneManager.NewPreviewScene();
            try
            {
                SceneManager.MoveGameObjectToScene(root, previewScene);
                DeleteAssetIfExists(path);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }

                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static void DeleteDuplicateGeneratedAssets()
        {
            foreach (var path in DuplicateGeneratedAssets)
            {
                DeleteAssetIfExists(path);
            }
        }

        private static void DeleteAssetIfExists(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static string SanitizeKey(string value)
        {
            return new string(value.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
        }

        private sealed class TimelineReferences
        {
            public JoeCinematicController joe;
            public Animator joeAnimator;
            public GameObject shadow;
            public Animator shadowAnimator;
            public Dictionary<string, Transform> markers = new Dictionary<string, Transform>();
            public Dictionary<string, CinemachineCamera> cameras = new Dictionary<string, CinemachineCamera>();

            public Transform GetMarker(string name)
            {
                markers.TryGetValue(name, out var marker);
                return marker;
            }

            public CinemachineCamera GetCamera(string name)
            {
                cameras.TryGetValue(name, out var camera);
                return camera;
            }
        }
    }
}
