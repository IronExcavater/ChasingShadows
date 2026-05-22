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
        private const double AnimationBlendDuration = 0.18d;

        private const string BasePrefabFolder = "Assets/ChasingShadows/Prefabs/Cinematic";
        private const string BaseRootPrefabPath = BasePrefabFolder + "/CinematicSequenceRoot.prefab";
        private const string BaseJoeRigPrefabPath = BasePrefabFolder + "/JoeCinematicRig.prefab";
        private const string BaseCameraPrefabFolder = BasePrefabFolder + "/Cameras";
        private const string LegacyCameraRigPrefabPath = BasePrefabFolder + "/CinematicCameraRig.prefab";
        private const string BaseMarkerSetPrefabPath = BasePrefabFolder + "/CinematicMarkerSet.prefab";

        private static readonly string[] RequiredBasePrefabs =
        {
            BaseRootPrefabPath,
            BaseJoeRigPrefabPath,
            BaseMarkerSetPrefabPath
        };

        private static readonly CameraPrefabSpec[] BasicCameraPrefabs =
        {
            new("CM_Basic_IntroWide", new Vector3(-3.5f, 2f, -3f), new Vector3(12f, 35f, 0f), 42f),
            new("CM_Basic_Follow", new Vector3(-2.2f, 1.6f, 1.4f), new Vector3(8f, 18f, 0f), 38f),
            new("CM_Basic_Close", new Vector3(-1.4f, 1.25f, 4.5f), new Vector3(7f, 28f, 0f), 34f)
        };

        private static readonly CameraPrefabSpec[] ChaseCameraPrefabs =
        {
            new("CM_Chase_IntroWide", new Vector3(-4f, 2.2f, -3f), new Vector3(14f, 38f, 0f), 42f),
            new("CM_Chase_Follow", new Vector3(-2.4f, 1.6f, 1.2f), new Vector3(8f, 18f, 0f), 38f),
            new("CM_Chase_SideProfile", new Vector3(-4f, 1.5f, 8.5f), new Vector3(4f, 78f, 0f), 40f),
            new("CM_Chase_Jump", new Vector3(-2.2f, 1.4f, 6.8f), new Vector3(8f, 45f, 0f), 36f),
            new("CM_Chase_Climb", new Vector3(-2.2f, 2.8f, 11.2f), new Vector3(18f, 58f, 0f), 34f),
            new("CM_Chase_Impact", new Vector3(-2.8f, 1.1f, 18.8f), new Vector3(6f, 70f, 0f), 32f),
            new("CM_Chase_Knockout", new Vector3(0.2f, 0.65f, 21.9f), new Vector3(10f, 180f, 0f), 30f)
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
            LegacyCameraRigPrefabPath,
            LegacyCameraRigPrefabPath + ".meta",
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
            EnsureFolder(BaseCameraPrefabFolder);
            SaveSequenceRootPrefab(BaseRootPrefabPath, "CinematicSequenceRoot");
            SaveJoeRigPrefab(BaseJoeRigPrefabPath, "JoeCinematicRig");
            SaveBaseMarkerSetPrefab();
            SaveCameraPrefabs(BasicCameraPrefabs);
            SaveCameraPrefabs(ChaseCameraPrefabs);
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
            var cameraRig = CreateCameraRigFromPrefabs(FindOrCreateChild(root.transform, "Camera Rig"), BasicCameraPrefabs);
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
            var cameraRig = CreateCameraRigFromPrefabs(FindOrCreateChild(root.transform, "Camera Rig"), ChaseCameraPrefabs);
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

            var joeRoot = CreateChild(root.transform, "Joe Root");
            var shadowRoot = CreateChild(root.transform, "Shadow Root");
            shadowRoot.transform.SetLocalPositionAndRotation(new Vector3(0f, 0f, 2.5f), Quaternion.identity);
            var joeObject = PrefabUtility.InstantiatePrefab(joePrefab, joeRoot.transform) as GameObject;
            if (joeObject != null)
            {
                joeObject.name = "Joe";
                joeObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                joeObject.transform.localScale = Vector3.one;
            }

            if (shadowPrefab != null)
            {
                var shadowObject = PrefabUtility.InstantiatePrefab(shadowPrefab, shadowRoot.transform) as GameObject;
                if (shadowObject != null)
                {
                    shadowObject.name = "Shadow";
                    shadowObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    shadowObject.transform.localScale = Vector3.one;
                }
            }

            var joe = FindDeepChild(root.transform, "Joe")?.GetComponent<JoeCinematicController>();
            var shadowTransform = FindDeepChild(root.transform, "Shadow");
            var shadow = shadowTransform != null ? shadowTransform.GetComponent<JoeCinematicController>() : null;
            if (shadow == null && shadowTransform != null)
            {
                shadow = shadowTransform.gameObject.AddComponent<JoeCinematicController>();
            }

            ConfigureJoeAndShadow(joe, shadow);
            SavePrefabAndDestroy(root, path);
        }

        private static void SaveBaseMarkerSetPrefab()
        {
            var root = new GameObject("CinematicMarkerSet");
            var ik = CreateChild(root.transform, "IK Targets").transform;
            CreateMarker(ik, "Look_Target", new Vector3(0f, 1.5f, 3f));
            CreateMarker(ik, "LeftHand_Target", new Vector3(-0.35f, 1.1f, 2f));
            CreateMarker(ik, "RightHand_Target", new Vector3(0.35f, 1.1f, 2f));
            SavePrefabAndDestroy(root, BaseMarkerSetPrefabPath);
        }

        private static void SaveCameraPrefabs(IEnumerable<CameraPrefabSpec> cameras)
        {
            foreach (var camera in cameras)
            {
                SaveCameraPrefab(camera);
            }
        }

        private static void SaveCameraPrefab(CameraPrefabSpec spec)
        {
            var cameraObject = new GameObject(spec.Name);
            cameraObject.transform.SetPositionAndRotation(spec.Position, Quaternion.Euler(spec.Euler));
            var camera = cameraObject.AddComponent<CinemachineCamera>();
            camera.Lens.FieldOfView = spec.FieldOfView;
            SavePrefabAndDestroy(cameraObject, CameraPrefabPath(spec.Name));
        }

        private static GameObject CreateChaseMarkerSet(Transform parent)
        {
            ClearChildren(parent);

            var root = CreateChild(parent, "ChaseMarkerSet");
            var ik = CreateChild(root.transform, "IK Targets").transform;
            CreateMarker(ik, "Look_Target", new Vector3(0f, 1.5f, 8f));
            CreateMarker(ik, "Look_Back_Target", new Vector3(-2.4f, 1.4f, 4.6f));
            CreateMarker(ik, "Climb_LeftHand_Target", new Vector3(1.25f, 1.85f, 12.9f));
            CreateMarker(ik, "Climb_RightHand_Target", new Vector3(2.15f, 2.05f, 13.05f));
            CreateMarker(ik, "Knockout_Look_Target", new Vector3(2.4f, 0.35f, 23.8f));
            return root;
        }

        private static GameObject CreateCameraRigFromPrefabs(Transform parent, IEnumerable<CameraPrefabSpec> cameras)
        {
            ClearChildren(parent);

            foreach (var camera in cameras)
            {
                InstantiateCameraPrefab(camera, parent);
            }

            return parent.gameObject;
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

            var joeMotion = CreateMotionTrack(timeline, "Joe Motion");
            AddMotionClip(joeMotion, "Joe hold", 0d, 1d, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero);
            AddMotionClip(joeMotion, "Joe move", 1d, 4d, Vector3.zero, new Vector3(0f, 0f, 6f), Vector3.zero, Vector3.zero);
            AddMotionClip(joeMotion, "Joe end hold", 5d, 1d, new Vector3(0f, 0f, 6f), new Vector3(0f, 0f, 6f), Vector3.zero, Vector3.zero);

            var shadowMotion = CreateMotionTrack(timeline, "Shadow Motion");
            AddMotionClip(shadowMotion, "Shadow hold", 0d, 1d, new Vector3(0f, 0f, 2.5f), new Vector3(0f, 0f, 2.5f), Vector3.zero, Vector3.zero);
            AddMotionClip(shadowMotion, "Shadow move", 1d, 4d, new Vector3(0f, 0f, 2.5f), new Vector3(0f, 0f, 8.5f), Vector3.zero, Vector3.zero);
            AddMotionClip(shadowMotion, "Shadow end hold", 5d, 1d, new Vector3(0f, 0f, 8.5f), new Vector3(0f, 0f, 8.5f), Vector3.zero, Vector3.zero);

            var joeAnimation = timeline.CreateTrack<AnimationTrack>(null, "Joe Animation");
            joeAnimation.trackOffset = TrackOffset.ApplyTransformOffsets;
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

            var shadowAnimation = timeline.CreateTrack<AnimationTrack>(null, "Shadow Animation");
            shadowAnimation.trackOffset = TrackOffset.ApplyTransformOffsets;
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
            AddCameraShot(director, cameraTrack, "Intro wide", 0d, 1d, references.GetCamera("CM_Chase_IntroWide"), "ChaseCamera");
            AddCameraShot(director, cameraTrack, "Follow run", 1d, 5.2d, references.GetCamera("CM_Chase_Follow"), "ChaseCamera");
            AddCameraShot(director, cameraTrack, "Jump gap", 4d, 3.2d, references.GetCamera("CM_Chase_Jump"), "ChaseCamera");
            AddCameraShot(director, cameraTrack, "Vault", 7.2d, 7.8d, references.GetCamera("CM_Chase_SideProfile"), "ChaseCamera");
            AddCameraShot(director, cameraTrack, "Wall climb", 15d, 7.4d, references.GetCamera("CM_Chase_Climb"), "ChaseCamera");
            AddCameraShot(director, cameraTrack, "Fall chain", 22.4d, 4.6d, references.GetCamera("CM_Chase_Impact"), "ChaseCamera");
            AddCameraShot(director, cameraTrack, "Knockout", 27d, 3d, references.GetCamera("CM_Chase_Knockout"), "ChaseCamera");

            var joeMotion = CreateMotionTrack(timeline, "Joe Motion");
            AddChaseMotionBeatClips(joeMotion, Vector3.zero);

            var shadowMotion = CreateMotionTrack(timeline, "Shadow Motion");
            AddChaseMotionBeatClips(shadowMotion, new Vector3(0f, 0f, 2.5f));

            var joeAnimation = timeline.CreateTrack<AnimationTrack>(null, "Joe Animation");
            joeAnimation.trackOffset = TrackOffset.ApplyTransformOffsets;
            AddChaseAnimationBeatClips(joeAnimation, string.Empty);

            var shadowAnimation = timeline.CreateTrack<AnimationTrack>(null, "Shadow Animation");
            shadowAnimation.trackOffset = TrackOffset.ApplyTransformOffsets;
            AddChaseAnimationBeatClips(shadowAnimation, "Shadow");

            var cueTrack = timeline.CreateTrack<JoeCueTrack>(null, "Joe Cues");
            AddCue(director, cueTrack, "Run Start", 0d, 1d, "RunStart", references.GetMarker("Look_Target"), null, null, 0.35f, 0f, 0.85f, "Chase");
            AddCue(director, cueTrack, "Jump", 4d, 2.2d, "Jump", references.GetMarker("Look_Target"), null, null, 0.45f, 0f, 0.85f, "Chase");
            AddCue(director, cueTrack, "Land", 6.2d, 1d, "Land", references.GetMarker("Look_Target"), null, null, 0.45f, 0f, 0.85f, "Chase");
            AddCue(director, cueTrack, "Vault", 10d, 3.6d, "Vault", references.GetMarker("Look_Target"), null, null, 0.45f, 0f, 0.85f, "Chase");
            AddCue(director, cueTrack, "Stop At Wall", 15d, 1.2d, "StopAtWall", references.GetMarker("Look_Target"), null, null, 0.45f, 0f, 0.85f, "Chase");
            AddCue(director, cueTrack, "Climb", 16.2d, 4d, "Climb", references.GetMarker("Look_Target"), references.GetMarker("Climb_LeftHand_Target"), references.GetMarker("Climb_RightHand_Target"), 0.55f, 0.75f, 0.55f, "Chase");
            AddCue(director, cueTrack, "Drop", 20.2d, 1.2d, "Drop", references.GetMarker("Look_Target"), null, null, 0.45f, 0f, 0.35f, "Chase");
            AddCue(director, cueTrack, "Land Again", 21.4d, 1d, "Land", references.GetMarker("Look_Target"), null, null, 0.45f, 0f, 0.85f, "Chase");
            AddCue(director, cueTrack, "Trip", 24.4d, 1.6d, "Stumble", references.GetMarker("Knockout_Look_Target"), null, null, 0.65f, 0f, 0.65f, "Chase");
            AddCue(director, cueTrack, "Fall Impact", 26d, 1d, "FallImpact", references.GetMarker("Knockout_Look_Target"), null, null, 0.65f, 0f, 0.45f, "Chase");
            AddCue(director, cueTrack, "Knockout", 27d, 3d, "Knockout", references.GetMarker("Knockout_Look_Target"), null, null, 0.45f, 0f, 0.35f, "Chase");

            timeline.durationMode = TimelineAsset.DurationMode.BasedOnClips;
            EditorUtility.SetDirty(timeline);
            return timeline;
        }

        private static void AddChaseAnimationBeatClips(AnimationTrack track, string displayPrefix)
        {
            var prefix = string.IsNullOrWhiteSpace(displayPrefix) ? string.Empty : displayPrefix + " ";

            AddAnimationClip(track, prefix + "Run Start", 0d, 1d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Idle To Sprint.fbx",
                "Assets/ChasingShadows/Animations/Joe/Adventure Run.fbx"));
            AddAnimationClip(track, prefix + "Run To Gap", 1d, 3d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Running Forwards.fbx",
                "Assets/ChasingShadows/Animations/Joe/Steady Run.fbx",
                "Assets/ChasingShadows/Animations/Joe/Adventure Run.fbx"));
            AddAnimationClip(track, prefix + "Jump Gap", 4d, 2.2d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Chase Jump.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running Jump Up.fbx",
                "Assets/ChasingShadows/Animations/Joe/Jumping Up.fbx"));
            AddAnimationClip(track, prefix + "Land", 6.2d, 1d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Hard Landing.fbx",
                "Assets/ChasingShadows/Animations/Joe/Heavy Hard Landing.fbx",
                "Assets/ChasingShadows/Animations/Joe/Falling To Landing.fbx"));
            AddAnimationClip(track, prefix + "Run To Vault", 7.2d, 2.8d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Running Forwards.fbx",
                "Assets/ChasingShadows/Animations/Joe/Run Forward Arc Right.fbx",
                "Assets/ChasingShadows/Animations/Joe/Steady Run.fbx"));
            AddAnimationClip(track, prefix + "Vault", 10d, 3.6d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Vault Over Box.fbx"));
            AddAnimationClip(track, prefix + "Run To Wall", 13.6d, 1.4d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Running Forwards.fbx",
                "Assets/ChasingShadows/Animations/Joe/Steady Run.fbx"));
            AddAnimationClip(track, prefix + "Stop At Wall", 15d, 1.2d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Run To Stop At Wall.fbx",
                "Assets/ChasingShadows/Animations/Joe/Run To Stop.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running Turn To Idle.fbx"));
            AddAnimationClip(track, prefix + "Catch Wall", 16.2d, 1.6d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Jump To Climb Wall.fbx",
                "Assets/ChasingShadows/Animations/Joe/Jump To Hang.fbx"));
            AddAnimationClip(track, prefix + "Climb Wall", 17.8d, 2.4d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Hanging Wall Ascend.fbx",
                "Assets/ChasingShadows/Animations/Joe/Sprint Climb Ascend.fbx",
                "Assets/ChasingShadows/Animations/Joe/Run Climb Ascend.fbx",
                "Assets/ChasingShadows/Animations/Joe/Climbing Up Wall.fbx"));
            AddAnimationClip(track, prefix + "Drop", 20.2d, 1.2d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Dismount Jump From Wall.fbx",
                "Assets/ChasingShadows/Animations/Joe/Jumping Down.fbx",
                "Assets/ChasingShadows/Animations/Joe/Anxious Jump Down.fbx"));
            AddAnimationClip(track, prefix + "Land Again", 21.4d, 1d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Hard Landing.fbx",
                "Assets/ChasingShadows/Animations/Joe/Heavy Hard Landing.fbx",
                "Assets/ChasingShadows/Animations/Joe/Falling To Landing.fbx"));
            AddAnimationClip(track, prefix + "Final Run", 22.4d, 2d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Running Forwards.fbx",
                "Assets/ChasingShadows/Animations/Joe/Steady Run.fbx"));
            AddAnimationClip(track, prefix + "Trip Roll", 24.4d, 1.6d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Running Trip Roll Onto Side.fbx",
                "Assets/ChasingShadows/Animations/Joe/Walking Trip Onto Side.fbx",
                "Assets/ChasingShadows/Animations/Joe/Falling Roll.fbx"));
            AddAnimationClip(track, prefix + "Impact", 26d, 1d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Falling Flat Impact.fbx",
                "Assets/ChasingShadows/Animations/Joe/Walking Trip Fall Flat.fbx",
                "Assets/ChasingShadows/Animations/Joe/Falling To Landing.fbx"));
            AddAnimationClip(track, prefix + "Knocked Out Hold", 27d, 3d, LoadAnimationClip(
                "Assets/ChasingShadows/Animations/Joe/Sleep Idle.fbx",
                "Assets/ChasingShadows/Animations/Joe/Sleeping Restless.fbx",
                "Assets/ChasingShadows/Animations/Joe/Sleeping Mild Cough.fbx"));
        }

        private static void AddChaseMotionBeatClips(CinematicMotionTrack track, Vector3 offset)
        {
            AddMotionClip(track, "Run Start", 0d, 1d, offset + new Vector3(0f, 0f, 0f), offset + new Vector3(0f, 0f, 1.2f), Vector3.zero, Vector3.zero);
            AddMotionClip(track, "Run To Gap", 1d, 3d, offset + new Vector3(0f, 0f, 1.2f), offset + new Vector3(0f, 0f, 7.4f), Vector3.zero, Vector3.zero);
            AddMotionClip(track, "Jump Gap", 4d, 2.2d, offset + new Vector3(0f, 0f, 7.4f), offset + new Vector3(0f, 0f, 10.2f), Vector3.zero, Vector3.zero);
            AddMotionClip(track, "Land", 6.2d, 1d, offset + new Vector3(0f, 0f, 10.2f), offset + new Vector3(0f, 0f, 10.8f), Vector3.zero, Vector3.zero);
            AddMotionClip(track, "Run To Vault", 7.2d, 2.8d, offset + new Vector3(0f, 0f, 10.8f), offset + new Vector3(1.2f, 0f, 15.8f), Vector3.zero, new Vector3(0f, 12f, 0f));
            AddMotionClip(track, "Vault", 10d, 3.6d, offset + new Vector3(1.2f, 0f, 15.8f), offset + new Vector3(1.6f, 0f, 18.6f), new Vector3(0f, 12f, 0f), new Vector3(0f, 8f, 0f));
            AddMotionClip(track, "Run To Wall", 13.6d, 1.4d, offset + new Vector3(1.6f, 0f, 18.6f), offset + new Vector3(1.6f, 0f, 21.2f), new Vector3(0f, 8f, 0f), Vector3.zero);
            AddMotionClip(track, "Stop At Wall", 15d, 1.2d, offset + new Vector3(1.6f, 0f, 21.2f), offset + new Vector3(1.6f, 0f, 22.1f), Vector3.zero, Vector3.zero);
            AddMotionClip(track, "Catch Wall", 16.2d, 1.6d, offset + new Vector3(1.6f, 0f, 22.1f), offset + new Vector3(1.6f, 1.1f, 22.4f), Vector3.zero, Vector3.zero);
            AddMotionClip(track, "Climb Wall", 17.8d, 2.4d, offset + new Vector3(1.6f, 1.1f, 22.4f), offset + new Vector3(1.6f, 2.6f, 22.7f), Vector3.zero, Vector3.zero);
            AddMotionClip(track, "Drop", 20.2d, 1.2d, offset + new Vector3(1.6f, 2.6f, 22.7f), offset + new Vector3(1.6f, 0f, 24.1f), Vector3.zero, Vector3.zero);
            AddMotionClip(track, "Land Again", 21.4d, 1d, offset + new Vector3(1.6f, 0f, 24.1f), offset + new Vector3(1.6f, 0f, 24.5f), Vector3.zero, Vector3.zero);
            AddMotionClip(track, "Final Run", 22.4d, 2d, offset + new Vector3(1.6f, 0f, 24.5f), offset + new Vector3(1.6f, 0f, 28.6f), Vector3.zero, Vector3.zero);
            AddMotionClip(track, "Trip Roll", 24.4d, 1.6d, offset + new Vector3(1.6f, 0f, 28.6f), offset + new Vector3(1.4f, 0f, 30.2f), Vector3.zero, new Vector3(0f, -18f, 0f));
            AddMotionClip(track, "Impact", 26d, 1d, offset + new Vector3(1.4f, 0f, 30.2f), offset + new Vector3(1.4f, 0f, 30.6f), new Vector3(0f, -18f, 0f), new Vector3(0f, -18f, 0f));
            AddMotionClip(track, "Knocked Out Hold", 27d, 3d, offset + new Vector3(1.4f, 0f, 30.6f), offset + new Vector3(1.4f, 0f, 30.6f), new Vector3(0f, -18f, 0f), new Vector3(0f, -18f, 0f));
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
                    case "Joe Motion":
                        director.SetGenericBinding(track, references.joeRoot);
                        break;
                    case "Shadow Motion":
                        director.SetGenericBinding(track, references.shadowRoot);
                        break;
                    case "Joe Animation":
                        director.SetGenericBinding(track, references.joeAnimator);
                        break;
                    case "Shadow Animation":
                        director.SetGenericBinding(track, references.shadowAnimator);
                        break;
                    case "Joe Cues":
                        director.SetGenericBinding(track, references.joe);
                        break;
                }
            }
        }

        private static CinematicMotionTrack CreateMotionTrack(TimelineAsset timeline, string trackName)
        {
            return timeline.CreateTrack<CinematicMotionTrack>(null, trackName);
        }

        private static void AddMotionClip(CinematicMotionTrack track, string name, double start, double duration, Vector3 firstPosition, Vector3 lastPosition, Vector3 firstEuler, Vector3 lastEuler)
        {
            var clip = track.CreateClip<CinematicMotionClip>();
            clip.displayName = name;
            clip.start = start;
            clip.duration = duration;
            clip.easeInDuration = 0d;
            clip.easeOutDuration = 0d;
            clip.blendInDuration = 0d;
            clip.blendOutDuration = 0d;

            if (clip.asset is CinematicMotionClip motion)
            {
                motion.knots = new[]
                {
                    new CinematicMotionKnot(firstPosition, firstEuler),
                    new CinematicMotionKnot(lastPosition, lastEuler)
                };
                motion.faceAlongSpline = false;
                motion.rotationOffset = Vector3.zero;
                motion.worldUp = Vector3.up;
                motion.applyPosition = true;
                motion.applyRotation = true;
                EditorUtility.SetDirty(motion);
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
            var previousClip = track.GetClips().OrderBy(clip => clip.start).LastOrDefault();
            var blendDuration = 0d;
            if (previousClip != null && start >= previousClip.end - 0.0001d)
            {
                blendDuration = System.Math.Min(AnimationBlendDuration, System.Math.Min(duration * 0.25d, previousClip.duration * 0.25d));
                previousClip.duration += blendDuration;
                previousClip.blendOutDuration = blendDuration;
            }

            var clip = track.CreateClip<AnimationPlayableAsset>();
            clip.displayName = name;
            clip.start = start;
            clip.duration = duration;
            clip.easeInDuration = 0d;
            clip.easeOutDuration = 0d;
            clip.blendInDuration = blendDuration;
            clip.blendOutDuration = 0d;

            if (clip.asset is AnimationPlayableAsset asset)
            {
                asset.clip = animationClip != null ? animationClip : LoadPlaceholderClip();
                asset.removeStartOffset = true;
                asset.applyFootIK = false;
                EditorUtility.SetDirty(asset);
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
                cue.clearRootMotionOnExit = false;
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
            var joeObject = joeRig != null ? FindDeepChild(joeRig.transform, "Joe")?.gameObject : null;
            var shadowObject = joeRig != null ? FindDeepChild(joeRig.transform, "Shadow")?.gameObject : null;
            shadowObject ??= joeRig != null ? FindDeepChild(joeRig.transform, "Joe_Shadow")?.gameObject : null;
            var joe = joeObject != null ? joeObject.GetComponent<JoeCinematicController>() : null;
            var shadow = shadowObject != null ? shadowObject.GetComponent<JoeCinematicController>() : null;
            var joeRoot = joeObject != null && joeObject.transform.parent != null ? joeObject.transform.parent : null;
            var shadowRoot = shadowObject != null && shadowObject.transform.parent != null ? shadowObject.transform.parent : null;
            if (shadow == null && shadowObject != null)
            {
                shadow = shadowObject.AddComponent<JoeCinematicController>();
            }
            var markers = markerSet != null
                ? markerSet.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First())
                : new Dictionary<string, Transform>();
            var cameras = cameraRig != null
                ? cameraRig.GetComponentsInChildren<CinemachineCamera>(true).GroupBy(c => c.name).ToDictionary(g => g.Key, g => g.First())
                : new Dictionary<string, CinemachineCamera>();

            return new TimelineReferences
            {
                joe = joe,
                joeRoot = joeRoot,
                joeAnimator = joe != null ? joe.GetComponent<Animator>() : null,
                shadow = shadow,
                shadowRoot = shadowRoot,
                shadowAnimator = shadowObject != null ? shadowObject.GetComponent<Animator>() : null,
                markers = markers,
                cameras = cameras
            };
        }

        private static void ConfigureJoeAndShadow(JoeCinematicController joe, JoeCinematicController shadow)
        {
            if (joe == null)
            {
                return;
            }

            var joeAnimator = ConfigureTimelineCharacter(joe, "Joe");

            if (shadow == null)
            {
                return;
            }

            var shadowAnimator = ConfigureTimelineCharacter(shadow, "Shadow");
            if (shadowAnimator != null && joeAnimator != null)
            {
                shadowAnimator.avatar = joeAnimator.avatar;
                PrefabUtility.RecordPrefabInstancePropertyModifications(shadowAnimator);
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

        private static Animator ConfigureTimelineCharacter(JoeCinematicController character, string characterName)
        {
            if (character == null)
            {
                return null;
            }

            character.name = characterName;
            character.enableBaseLocomotion = false;
            character.projectTimelineMotionToGround = false;
            character.finalIkEnabled = true;
            character.SetRootMotionEnabled(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(character);

            var animator = character.GetComponent<Animator>();
            if (animator == null)
            {
                return null;
            }

            animator.runtimeAnimatorController = null;
            animator.enabled = true;
            animator.applyRootMotion = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            return animator;
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

        private static GameObject InstantiateCameraPrefab(CameraPrefabSpec spec, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPrefabPath(spec.Name));
            if (prefab == null)
            {
                Debug.LogWarning($"Cinematic camera prefab not found for {spec.Name}.");
                return null;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
            {
                return null;
            }

            instance.name = spec.Name;
            instance.transform.SetLocalPositionAndRotation(spec.Position, Quaternion.Euler(spec.Euler));
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static void EnsureBaseCinematicPrefabKit()
        {
            var requiredCameraPrefabs = BasicCameraPrefabs.Concat(ChaseCameraPrefabs)
                .Select(camera => CameraPrefabPath(camera.Name));

            if (RequiredBasePrefabs.Concat(requiredCameraPrefabs).All(path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null))
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
            joe?.SetLookTarget(null, 0f);
            joe?.SetHandTargets(null, null, 0f);
            Selection.activeGameObject = root;
            if (root != null && root.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(root.scene);
                EditorSceneManager.SaveScene(root.scene);
            }

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

        private static string CameraPrefabPath(string cameraName)
        {
            return $"{BaseCameraPrefabFolder}/{cameraName}.prefab";
        }

        private readonly struct CameraPrefabSpec
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly Vector3 Euler;
            public readonly float FieldOfView;

            public CameraPrefabSpec(string name, Vector3 position, Vector3 euler, float fieldOfView)
            {
                Name = name;
                Position = position;
                Euler = euler;
                FieldOfView = fieldOfView;
            }
        }

        private sealed class TimelineReferences
        {
            public JoeCinematicController joe;
            public Transform joeRoot;
            public Animator joeAnimator;
            public JoeCinematicController shadow;
            public Transform shadowRoot;
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
