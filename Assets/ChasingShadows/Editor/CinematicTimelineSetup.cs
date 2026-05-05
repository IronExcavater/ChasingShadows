using System.Collections.Generic;
using System.Linq;
using ChasingShadows.Characters;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
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
        private const string BadGenericTimelinePath = TimelineFolder + "/Joe_Cinematic_Timeline.playable";
        private const string JoePrefabPath = "Assets/ChasingShadows/Prefabs/Joe.prefab";
        private const string ShadowPrefabPath = "Assets/ChasingShadows/Prefabs/Shadow.prefab";

        private const string BasicPrefabFolder = "Assets/ChasingShadows/Prefabs/Cinematic/Basic";
        private const string ChasePrefabFolder = "Assets/ChasingShadows/Prefabs/Cinematic/Chase";
        private const string BadGenericPrefabFolder = "Assets/ChasingShadows/Prefabs/Cinematic";

        private const string BasicRootPrefabPath = BasicPrefabFolder + "/CinematicSequenceRoot.prefab";
        private const string BasicJoeRigPrefabPath = BasicPrefabFolder + "/JoeCinematicRig.prefab";
        private const string BasicCameraRigPrefabPath = BasicPrefabFolder + "/CinematicCameraRig.prefab";
        private const string BasicMarkerSetPrefabPath = BasicPrefabFolder + "/CinematicMarkerSet.prefab";

        private const string ChaseRootPrefabPath = ChasePrefabFolder + "/ChaseSequenceRoot.prefab";
        private const string ChaseJoeRigPrefabPath = ChasePrefabFolder + "/ChaseJoeRig.prefab";
        private const string ChaseCameraRigPrefabPath = ChasePrefabFolder + "/ChaseCameraRig.prefab";
        private const string ChaseMarkerSetPrefabPath = ChasePrefabFolder + "/ChaseMarkerSet.prefab";
        private const string ChaseObstacleMarkersPrefabPath = ChasePrefabFolder + "/ChaseObstacleMarkers.prefab";

        private static readonly string[] RequiredBasicPrefabs =
        {
            BasicRootPrefabPath,
            BasicJoeRigPrefabPath,
            BasicCameraRigPrefabPath,
            BasicMarkerSetPrefabPath
        };

        private static readonly string[] RequiredChasePrefabs =
        {
            ChaseRootPrefabPath,
            ChaseJoeRigPrefabPath,
            ChaseCameraRigPrefabPath,
            ChaseMarkerSetPrefabPath,
            ChaseObstacleMarkersPrefabPath
        };

        private static readonly string[] BadGenericAssets =
        {
            BadGenericTimelinePath,
            BadGenericPrefabFolder + "/CinematicSequenceRoot.prefab",
            BadGenericPrefabFolder + "/JoeCinematicRig.prefab",
            BadGenericPrefabFolder + "/CinematicCameraRig.prefab",
            BadGenericPrefabFolder + "/CinematicMarkerSet.prefab",
            BadGenericPrefabFolder + "/CinematicObstacleMarkers.prefab"
        };

        [MenuItem("Chasing Shadows/Cinematic/Rebuild Basic Prefab Kit")]
        public static void RebuildBasicCinematicPrefabKit()
        {
            EnsureFolder(BasicPrefabFolder);
            SaveSequenceRootPrefab(BasicRootPrefabPath, "CinematicSequenceRoot", false);
            SaveJoeRigPrefab(BasicJoeRigPrefabPath, "JoeCinematicRig");
            SaveBasicMarkerSetPrefab();
            SaveBasicCameraRigPrefab();
            DeleteBadGenericAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Basic cinematic prefab kit rebuilt.");
        }

        [MenuItem("Chasing Shadows/Cinematic/Create Basic Joe Setup")]
        public static void CreateBasicJoeCinematicSetup()
        {
            EnsureFolder(TimelineFolder);
            EnsureBasicCinematicPrefabKit();
            EnsureCinemachineBrain();

            var root = RecreateRootFromPrefab(
                BasicRootPrefabPath,
                BasicRootName,
                "Create basic Joe cinematic setup",
                "Joe_Cinematic_Setup");
            var joeRig = InstantiateChildPrefab(BasicJoeRigPrefabPath, FindOrCreateChild(root.transform, "Joe Rig"));
            var markerSet = InstantiateChildPrefab(BasicMarkerSetPrefabPath, FindOrCreateChild(root.transform, "Marker Set"));
            var cameraRig = InstantiateChildPrefab(BasicCameraRigPrefabPath, FindOrCreateChild(root.transform, "Camera Rig"));
            var director = root.GetComponentInChildren<PlayableDirector>(true) ?? CreateDirector(root.transform);

            var references = BuildTimelineReferences(joeRig, markerSet, cameraRig);
            ConfigureJoeAndShadow(references.joe, references.shadow);

            var timeline = CreateBasicTimelineAsset(references, director);
            director.playableAsset = timeline;
            BindTimeline(director, timeline, references);
            FinishSetup(root, references.joe);
        }

        [MenuItem("Chasing Shadows/Sequences/Chase/Rebuild Prefab Kit")]
        public static void RebuildChaseSequencePrefabKit()
        {
            EnsureFolder(ChasePrefabFolder);
            SaveSequenceRootPrefab(ChaseRootPrefabPath, "ChaseSequenceRoot", true);
            SaveJoeRigPrefab(ChaseJoeRigPrefabPath, "ChaseJoeRig");
            SaveChaseMarkerSetPrefab();
            SaveChaseObstacleMarkersPrefab();
            SaveChaseCameraRigPrefab();
            DeleteBadGenericAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Chase sequence prefab kit rebuilt.");
        }

        [MenuItem("Chasing Shadows/Sequences/Chase/Create Chase Setup")]
        public static void CreateChaseSequenceSetup()
        {
            EnsureFolder(TimelineFolder);
            EnsureChaseSequencePrefabKit();
            EnsureCinemachineBrain();

            var root = RecreateRootFromPrefab(
                ChaseRootPrefabPath,
                ChaseRootName,
                "Create chase sequence setup",
                "Joe_Timeline_Setup",
                "Joe_Cinematic_Setup");
            var joeRig = InstantiateChildPrefab(ChaseJoeRigPrefabPath, FindOrCreateChild(root.transform, "Joe Rig"));
            var markerSet = InstantiateChildPrefab(ChaseMarkerSetPrefabPath, FindOrCreateChild(root.transform, "Marker Set"));
            InstantiateChildPrefab(ChaseObstacleMarkersPrefabPath, FindOrCreateChild(root.transform, "Obstacle Markers"));
            var cameraRig = InstantiateChildPrefab(ChaseCameraRigPrefabPath, FindOrCreateChild(root.transform, "Camera Rig"));
            var director = root.GetComponentInChildren<PlayableDirector>(true) ?? CreateDirector(root.transform);

            var references = BuildTimelineReferences(joeRig, markerSet, cameraRig);
            ConfigureJoeAndShadow(references.joe, references.shadow);

            var timeline = CreateChaseTimelineAsset(references, director);
            director.playableAsset = timeline;
            BindTimeline(director, timeline, references);
            FinishSetup(root, references.joe);
        }

        private static void SaveSequenceRootPrefab(string path, string rootName, bool includeObstacleGroup)
        {
            var root = new GameObject(rootName);
            CreateChild(root.transform, "Joe Rig");
            CreateChild(root.transform, "Marker Set");
            if (includeObstacleGroup)
            {
                CreateChild(root.transform, "Obstacle Markers");
            }

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

        private static void SaveBasicMarkerSetPrefab()
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
            SavePrefabAndDestroy(root, BasicMarkerSetPrefabPath);
        }

        private static void SaveChaseMarkerSetPrefab()
        {
            var root = new GameObject("ChaseMarkerSet");
            var path = CreateChild(root.transform, "Path Markers").transform;
            CreateMarker(path, "Move_Start", new Vector3(0f, 0f, 0f));
            CreateMarker(path, "Alley_Run_End", new Vector3(0f, 0f, 5f));
            CreateMarker(path, "Jump_End", new Vector3(0.4f, 0f, 8f));
            CreateMarker(path, "Turn_End", new Vector3(1.6f, 0f, 10.5f));
            CreateMarker(path, "Climb_Top", new Vector3(1.6f, 2.4f, 12.5f));
            CreateMarker(path, "Drop_Land", new Vector3(1.6f, 0f, 14f));
            CreateMarker(path, "FinalSprint_End", new Vector3(1.6f, 0f, 18f));
            CreateMarker(path, "Trip_Point", new Vector3(1.6f, 0f, 20f));
            CreateMarker(path, "Knockout_Point", new Vector3(1.6f, 0f, 20.75f));

            var ik = CreateChild(root.transform, "IK Targets").transform;
            CreateMarker(ik, "Look_Target", new Vector3(0f, 1.5f, 3f));
            CreateMarker(ik, "LeftHand_Target", new Vector3(-0.35f, 1.1f, 2f));
            CreateMarker(ik, "RightHand_Target", new Vector3(0.35f, 1.1f, 2f));
            CreateMarker(ik, "Climb_LeftHand_Target", new Vector3(1.15f, 1.65f, 12f));
            CreateMarker(ik, "Climb_RightHand_Target", new Vector3(2.05f, 1.85f, 12f));
            CreateMarker(ik, "Knockout_Look_Target", new Vector3(1.6f, 0.35f, 21.5f));

            var actions = CreateChild(root.transform, "Action Markers").transform;
            CreateMarker(actions, "Jump_Action", new Vector3(0.2f, 0f, 6.5f));
            CreateMarker(actions, "Climb_Action", new Vector3(1.6f, 0f, 12f));
            CreateMarker(actions, "Drop_Action", new Vector3(1.6f, 2f, 13f));
            CreateMarker(actions, "Trip_Action", new Vector3(1.6f, 0f, 19.5f));
            CreateMarker(actions, "Knockout_Action", new Vector3(1.6f, 0f, 20.75f));
            SavePrefabAndDestroy(root, ChaseMarkerSetPrefabPath);
        }

        private static void SaveChaseObstacleMarkersPrefab()
        {
            var root = new GameObject("ChaseObstacleMarkers");
            CreateObstacle(root.transform, "Wall_Climb_Placeholder", new Vector3(1.6f, 1.2f, 12.35f), new Vector3(3f, 2.4f, 0.25f));
            CreateObstacle(root.transform, "Gap_Start_Placeholder", new Vector3(0.2f, -0.05f, 6.3f), new Vector3(1f, 0.1f, 0.2f));
            CreateObstacle(root.transform, "Gap_End_Placeholder", new Vector3(0.4f, -0.05f, 8.1f), new Vector3(1f, 0.1f, 0.2f));
            CreateObstacle(root.transform, "Trip_Obstacle_Placeholder", new Vector3(1.6f, 0.15f, 19.8f), new Vector3(1.2f, 0.3f, 0.25f));
            CreateObstacle(root.transform, "Knockout_Ground_Placeholder", new Vector3(1.6f, -0.02f, 20.75f), new Vector3(2f, 0.05f, 2f));
            SavePrefabAndDestroy(root, ChaseObstacleMarkersPrefabPath);
        }

        private static void SaveBasicCameraRigPrefab()
        {
            var root = new GameObject("CinematicCameraRig");
            CreateSequenceCamera(root.transform, "CM_Basic_IntroWide", new Vector3(-3.5f, 2f, -3f), new Vector3(12f, 35f, 0f), 42f);
            CreateSequenceCamera(root.transform, "CM_Basic_Follow", new Vector3(-2.2f, 1.6f, 1.4f), new Vector3(8f, 18f, 0f), 38f);
            CreateSequenceCamera(root.transform, "CM_Basic_Close", new Vector3(-1.4f, 1.25f, 4.5f), new Vector3(7f, 28f, 0f), 34f);
            SavePrefabAndDestroy(root, BasicCameraRigPrefabPath);
        }

        private static void SaveChaseCameraRigPrefab()
        {
            var root = new GameObject("ChaseCameraRig");
            CreateSequenceCamera(root.transform, "CM_Chase_IntroWide", new Vector3(-4f, 2.2f, -3f), new Vector3(14f, 38f, 0f), 42f);
            CreateSequenceCamera(root.transform, "CM_Chase_Follow", new Vector3(-2.4f, 1.6f, 1.2f), new Vector3(8f, 18f, 0f), 38f);
            CreateSequenceCamera(root.transform, "CM_Chase_SideProfile", new Vector3(-4f, 1.5f, 8.5f), new Vector3(4f, 78f, 0f), 40f);
            CreateSequenceCamera(root.transform, "CM_Chase_Jump", new Vector3(-2.2f, 1.4f, 6.8f), new Vector3(8f, 45f, 0f), 36f);
            CreateSequenceCamera(root.transform, "CM_Chase_Climb", new Vector3(-2.2f, 2.8f, 11.2f), new Vector3(18f, 58f, 0f), 34f);
            CreateSequenceCamera(root.transform, "CM_Chase_Impact", new Vector3(-2.8f, 1.1f, 18.8f), new Vector3(6f, 70f, 0f), 32f);
            CreateSequenceCamera(root.transform, "CM_Chase_Knockout", new Vector3(0.2f, 0.65f, 21.9f), new Vector3(10f, 180f, 0f), 30f);
            SavePrefabAndDestroy(root, ChaseCameraRigPrefabPath);
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
            AddAnimationClip(joeAnimation, "Ready hold", 0d, 1d, LoadPlaceholderClip());
            AddAnimationClip(joeAnimation, "Move", 1d, 4d, LoadAnimationClip("Assets/ChasingShadows/Animations/Joe/Running.fbx"));
            AddAnimationClip(joeAnimation, "End hold", 5d, 1d, LoadPlaceholderClip());

            var joeMovement = timeline.CreateTrack<JoeMovementTrack>(null, "Joe Timeline Movement");
            AddMovementClip(director, joeMovement, "Intro hold", 0d, 1d, references.GetMarker("Move_Start"), null, true, "Basic");
            AddMovementClip(director, joeMovement, "Move", 1d, 4d, references.GetMarker("Move_Start"), references.GetMarker("Move_End"), true, "Basic");
            AddMovementClip(director, joeMovement, "End hold", 5d, 1d, references.GetMarker("Move_End"), null, true, "Basic");

            var shadowAnimation = timeline.CreateTrack<AnimationTrack>(null, "Shadow Animation");
            shadowAnimation.trackOffset = TrackOffset.Auto;
            AddAnimationClip(shadowAnimation, "Shadow ready hold", 0d, 1d, LoadPlaceholderClip());
            AddAnimationClip(shadowAnimation, "Shadow move", 1d, 4d, LoadAnimationClip("Assets/ChasingShadows/Animations/Joe/Running.fbx"));
            AddAnimationClip(shadowAnimation, "Shadow end hold", 5d, 1d, LoadPlaceholderClip());

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
            AddCameraShot(director, cameraTrack, "Intro wide", 0d, 1.2d, references.GetCamera("CM_Chase_IntroWide"), "ChaseCamera");
            AddCameraShot(director, cameraTrack, "Follow run", 1.2d, 3d, references.GetCamera("CM_Chase_Follow"), "ChaseCamera");
            AddCameraShot(director, cameraTrack, "Jump gap", 4.2d, 1.4d, references.GetCamera("CM_Chase_Jump"), "ChaseCamera");
            AddCameraShot(director, cameraTrack, "Side hard turn", 5.6d, 1.6d, references.GetCamera("CM_Chase_SideProfile"), "ChaseCamera");
            AddCameraShot(director, cameraTrack, "Wall climb", 7.2d, 3.6d, references.GetCamera("CM_Chase_Climb"), "ChaseCamera");
            AddCameraShot(director, cameraTrack, "Impact run", 10.8d, 5.4d, references.GetCamera("CM_Chase_Impact"), "ChaseCamera");
            AddCameraShot(director, cameraTrack, "Knockout", 16.2d, 1.8d, references.GetCamera("CM_Chase_Knockout"), "ChaseCamera");

            var joeAnimation = timeline.CreateTrack<AnimationTrack>(null, "Joe Animation");
            joeAnimation.trackOffset = TrackOffset.Auto;
            AddAnimationClip(joeAnimation, "Run start placeholder", 0d, 1.2d, LoadAnimationClip("Assets/ChasingShadows/Animations/Joe/Running.fbx"));
            AddAnimationClip(joeAnimation, "Alley run", 1.2d, 3d, LoadAnimationClip("Assets/ChasingShadows/Animations/Joe/Running.fbx"));
            AddAnimationClip(joeAnimation, "Jump placeholder", 4.2d, 1.4d, LoadPlaceholderClip());
            AddAnimationClip(joeAnimation, "Hard turn placeholder", 5.6d, 1.6d, LoadPlaceholderClip());
            AddAnimationClip(joeAnimation, "Climb placeholder", 7.2d, 3.6d, LoadPlaceholderClip());
            AddAnimationClip(joeAnimation, "Drop land placeholder", 10.8d, 1.2d, LoadPlaceholderClip());
            AddAnimationClip(joeAnimation, "Final sprint", 12d, 3d, LoadAnimationClip("Assets/ChasingShadows/Animations/Joe/Running.fbx"));
            AddAnimationClip(joeAnimation, "Trip placeholder", 15d, 1.2d, LoadPlaceholderClip());
            AddAnimationClip(joeAnimation, "Knocked out hold", 16.2d, 1.8d, LoadAnimationClip("Assets/ChasingShadows/Animations/Joe/Ch33_nonPBR@Laying Sleeping.fbx"));

            var joeMovement = timeline.CreateTrack<JoeMovementTrack>(null, "Joe Timeline Movement");
            AddMovementClip(director, joeMovement, "Intro hold", 0d, 1.2d, references.GetMarker("Move_Start"), null, true, "Chase");
            AddMovementClip(director, joeMovement, "Alley run", 1.2d, 3d, references.GetMarker("Move_Start"), references.GetMarker("Alley_Run_End"), true, "Chase");
            AddMovementClip(director, joeMovement, "Jump gap", 4.2d, 1.4d, references.GetMarker("Alley_Run_End"), references.GetMarker("Jump_End"), true, "Chase");
            AddMovementClip(director, joeMovement, "Hard turn", 5.6d, 1.6d, references.GetMarker("Jump_End"), references.GetMarker("Turn_End"), true, "Chase");
            AddMovementClip(director, joeMovement, "Wall climb", 7.2d, 3.6d, references.GetMarker("Turn_End"), references.GetMarker("Climb_Top"), false, "Chase");
            AddMovementClip(director, joeMovement, "Drop land", 10.8d, 1.2d, references.GetMarker("Climb_Top"), references.GetMarker("Drop_Land"), false, "Chase");
            AddMovementClip(director, joeMovement, "Final sprint", 12d, 3d, references.GetMarker("Drop_Land"), references.GetMarker("FinalSprint_End"), true, "Chase");
            AddMovementClip(director, joeMovement, "Trip", 15d, 1.2d, references.GetMarker("FinalSprint_End"), references.GetMarker("Trip_Point"), true, "Chase");
            AddMovementClip(director, joeMovement, "Knockout hold", 16.2d, 1.8d, references.GetMarker("Trip_Point"), null, true, "Chase");

            var shadowAnimation = timeline.CreateTrack<AnimationTrack>(null, "Shadow Animation");
            shadowAnimation.trackOffset = TrackOffset.Auto;
            AddAnimationClip(shadowAnimation, "Shadow run start placeholder", 0d, 1.2d, LoadAnimationClip("Assets/ChasingShadows/Animations/Joe/Running.fbx"));
            AddAnimationClip(shadowAnimation, "Shadow alley run", 1.2d, 3d, LoadAnimationClip("Assets/ChasingShadows/Animations/Joe/Running.fbx"));
            AddAnimationClip(shadowAnimation, "Shadow jump placeholder", 4.2d, 1.4d, LoadPlaceholderClip());
            AddAnimationClip(shadowAnimation, "Shadow hard turn placeholder", 5.6d, 1.6d, LoadPlaceholderClip());
            AddAnimationClip(shadowAnimation, "Shadow climb placeholder", 7.2d, 3.6d, LoadPlaceholderClip());
            AddAnimationClip(shadowAnimation, "Shadow drop placeholder", 10.8d, 1.2d, LoadPlaceholderClip());
            AddAnimationClip(shadowAnimation, "Shadow final sprint", 12d, 3d, LoadAnimationClip("Assets/ChasingShadows/Animations/Joe/Running.fbx"));
            AddAnimationClip(shadowAnimation, "Shadow trip placeholder", 15d, 1.2d, LoadPlaceholderClip());
            AddAnimationClip(shadowAnimation, "Shadow knocked out hold", 16.2d, 1.8d, LoadAnimationClip("Assets/ChasingShadows/Animations/Joe/Ch33_nonPBR@Laying Sleeping.fbx"));

            var cueTrack = timeline.CreateTrack<JoeCueTrack>(null, "Joe Cues");
            AddCue(director, cueTrack, "RunStart", 0d, 1.2d, "RunStart", references.GetMarker("Look_Target"), null, null, 0.35f, 0f, 0.85f, "Chase");
            AddCue(director, cueTrack, "Jump", 4.2d, 1.4d, "Jump", references.GetMarker("Look_Target"), null, null, 0.45f, 0f, 0.85f, "Chase");
            AddCue(director, cueTrack, "HardTurn", 5.6d, 1.6d, "HardTurn", references.GetMarker("Look_Target"), null, null, 0.45f, 0f, 0.85f, "Chase");
            AddCue(director, cueTrack, "Climb", 7.2d, 3.6d, "Climb", references.GetMarker("Look_Target"), references.GetMarker("Climb_LeftHand_Target"), references.GetMarker("Climb_RightHand_Target"), 0.55f, 0.75f, 0.55f, "Chase");
            AddCue(director, cueTrack, "Drop", 10.8d, 0.6d, "Drop", references.GetMarker("Look_Target"), null, null, 0.45f, 0f, 0.35f, "Chase");
            AddCue(director, cueTrack, "Land", 11.4d, 0.6d, "Land", references.GetMarker("Look_Target"), null, null, 0.45f, 0f, 0.85f, "Chase");
            AddCue(director, cueTrack, "Stumble", 15d, 1.2d, "Stumble", references.GetMarker("Knockout_Look_Target"), null, null, 0.65f, 0f, 0.65f, "Chase");

            var shadowTrack = timeline.CreateTrack<ActivationTrack>(null, "Shadow Active");
            var activeClip = shadowTrack.CreateDefaultClip();
            activeClip.displayName = "Shadow on";
            activeClip.start = 0d;
            activeClip.duration = 18d;

            timeline.durationMode = TimelineAsset.DurationMode.BasedOnClips;
            EditorUtility.SetDirty(timeline);
            return timeline;
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

        private static void AddAnimationClip(AnimationTrack track, string name, double start, double duration, AnimationClip animationClip)
        {
            var clip = track.CreateClip<AnimationPlayableAsset>();
            clip.displayName = name;
            clip.start = start;
            clip.duration = duration;

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
                joeAnimator.enabled = true;
                joeAnimator.applyRootMotion = false;
            }

            if (shadow == null)
            {
                return;
            }

            shadow.name = "Joe_Shadow";
            shadow.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            shadow.transform.localScale = Vector3.one;

            foreach (var controller in shadow.GetComponentsInChildren<JoeCinematicController>(true))
            {
                Object.DestroyImmediate(controller);
            }

            var shadowAnimator = shadow.GetComponent<Animator>();
            if (shadowAnimator != null && joeAnimator != null)
            {
                shadowAnimator.avatar = joeAnimator.avatar;
                shadowAnimator.runtimeAnimatorController = joeAnimator.runtimeAnimatorController;
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
            foreach (var gameObject in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!gameObject.scene.IsValid() || gameObject.transform.parent != null || !lookup.Contains(gameObject.name))
                {
                    continue;
                }

                Object.DestroyImmediate(gameObject);
            }
        }

        private static GameObject InstantiateChildPrefab(string path, Transform parent)
        {
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

        private static void EnsureBasicCinematicPrefabKit()
        {
            if (RequiredBasicPrefabs.All(path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null))
            {
                return;
            }

            RebuildBasicCinematicPrefabKit();
        }

        private static void EnsureChaseSequencePrefabKit()
        {
            if (RequiredChasePrefabs.All(path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null))
            {
                return;
            }

            RebuildChaseSequencePrefabKit();
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
            return AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ChasingShadows/Animations/Joe/Joe_Empty.anim");
        }

        private static AnimationClip LoadAnimationClip(string path)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview", System.StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return clips.FirstOrDefault() ?? LoadPlaceholderClip();
        }

        private static CinemachineCamera CreateSequenceCamera(Transform parent, string name, Vector3 position, Vector3 euler, float fieldOfView)
        {
            var cameraObject = CreateChild(parent, name);
            cameraObject.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(euler));
            var camera = cameraObject.AddComponent<CinemachineCamera>();
            camera.Lens.FieldOfView = fieldOfView;
            return camera;
        }

        private static GameObject CreateObstacle(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = name;
            obstacle.transform.SetParent(parent, false);
            obstacle.transform.localPosition = position;
            obstacle.transform.localScale = scale;
            return obstacle;
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
            DeleteAssetIfExists(path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void DeleteBadGenericAssets()
        {
            foreach (var path in BadGenericAssets)
            {
                DeleteAssetIfExists(path);
            }
        }

        private static void DeleteAssetIfExists(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
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
