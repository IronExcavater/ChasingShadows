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
    public static class ChaseTimelineSetup
    {
        private const string RootName = "Joe_Timeline_Setup";
        private const string TimelineFolder = "Assets/ChasingShadows/Timelines";
        private const string TimelinePath = TimelineFolder + "/Joe_Chase_Timeline.playable";
        private const string JoePrefabPath = "Assets/ChasingShadows/Prefabs/Joe.prefab";
        private const string ShadowPrefabPath = "Assets/ChasingShadows/Prefabs/Shadow.prefab";

        [MenuItem("Chasing Shadows/Create Joe Timeline Setup")]
        public static void CreateJoeTimelineSetup()
        {
            EnsureFolder(TimelineFolder);

            var root = RecreateRoot();
            var joe = FindOrCreateJoe(root.transform);
            var shadow = joe != null ? CreateShadow(joe) : null;
            var targets = CreateTargets(root.transform);

            var camera = CreateCamera(root.transform);
            var director = CreateDirector(root.transform);
            var timeline = CreateTimelineAsset(targets);

            director.playableAsset = timeline;
            BindTimeline(director, timeline, joe, shadow, camera);

            if (joe != null)
            {
                joe.Stop();
            }

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
            AssetDatabase.SaveAssets();
        }

        private static GameObject RecreateRoot()
        {
            var existing = GameObject.Find(RootName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Joe timeline setup");
            return root;
        }

        private static JoeCinematicController FindOrCreateJoe(Transform parent)
        {
            foreach (var controller in Object.FindObjectsByType<JoeCinematicController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!controller.name.Contains("Shadow", System.StringComparison.OrdinalIgnoreCase))
                {
                    controller.transform.SetParent(parent, true);
                    controller.name = "Joe";
                    return controller;
                }
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(JoePrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"Joe prefab not found at {JoePrefabPath}. Drag Joe into the scene and run the setup again.");
                return null;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
            {
                return null;
            }

            instance.name = "Joe";
            return instance.GetComponent<JoeCinematicController>() ?? instance.AddComponent<JoeCinematicController>();
        }

        private static GameObject CreateShadow(JoeCinematicController joe)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShadowPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"Shadow prefab not found at {ShadowPrefabPath}. Add the prefab, then run the setup again.");
                return null;
            }

            var joeRenderers = joe.GetComponentsInChildren<Renderer>(true);
            var shadow = PrefabUtility.InstantiatePrefab(prefab, joe.transform) as GameObject;
            if (shadow == null)
            {
                return null;
            }

            shadow.name = "Joe_Shadow";
            shadow.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            shadow.transform.localScale = Vector3.one;

            var shadowAnimator = shadow.GetComponent<Animator>();
            if (shadowAnimator != null)
            {
                var joeAnimator = joe.GetComponent<Animator>();
                if (joeAnimator != null)
                {
                    shadowAnimator.avatar = joeAnimator.avatar;
                    shadowAnimator.runtimeAnimatorController = joeAnimator.runtimeAnimatorController;
                    shadowAnimator.enabled = true;
                }

                shadowAnimator.applyRootMotion = false;
            }

            foreach (var renderer in joeRenderers)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            foreach (var renderer in shadow.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                renderer.receiveShadows = false;
            }

            return shadow;
        }

        private static TimelineTargets CreateTargets(Transform parent)
        {
            var targets = CreateChild(parent, "IK Targets");
            return new TimelineTargets
            {
                look = CreateMarker(targets.transform, "Look_Target", new Vector3(0f, 1.5f, 3f)),
                leftHand = CreateMarker(targets.transform, "LeftHand_Target", new Vector3(-0.35f, 1.1f, 2f)),
                rightHand = CreateMarker(targets.transform, "RightHand_Target", new Vector3(0.35f, 1.1f, 2f)),
                moveStart = CreateMarker(targets.transform, "Move_Start", Vector3.zero),
                moveEnd = CreateMarker(targets.transform, "Move_End", new Vector3(0f, 0f, 4f))
            };
        }

        private static CinemachineCamera CreateCamera(Transform parent)
        {
            EnsureCinemachineBrain();

            var cameraObject = CreateChild(parent, "CM_Chase");
            cameraObject.transform.SetLocalPositionAndRotation(new Vector3(-3.5f, 1.6f, -2.8f), Quaternion.Euler(8f, 38f, 0f));

            var camera = cameraObject.AddComponent<CinemachineCamera>();
            camera.Lens.FieldOfView = 40f;
            return camera;
        }

        private static PlayableDirector CreateDirector(Transform parent)
        {
            var directorObject = CreateChild(parent, "TimelineDirector");
            var director = directorObject.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.Hold;
            return director;
        }

        private static TimelineAsset CreateTimelineAsset(TimelineTargets targets)
        {
            DeleteAssetIfExists(TimelinePath);

            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.editorSettings.frameRate = 24d;
            AssetDatabase.CreateAsset(timeline, TimelinePath);

            var cameraTrack = timeline.CreateTrack<CinemachineTrack>(null, "Camera");
            AddCameraShot(cameraTrack, "Shot 01", 0d, 8d);

            var joeAnimation = timeline.CreateTrack<AnimationTrack>(null, "Joe Animation");
            joeAnimation.trackOffset = TrackOffset.Auto;
            AddAnimationClip(joeAnimation, "Replace with Joe chase clips", 0d, 8d);

            var joeMovement = timeline.CreateTrack<JoeMovementTrack>(null, "Joe Timeline Movement");
            AddMovementClip(joeMovement, "A-to-B or spline movement", 0d, 8d, targets);

            var shadowAnimation = timeline.CreateTrack<AnimationTrack>(null, "Shadow Animation");
            shadowAnimation.trackOffset = TrackOffset.Auto;
            AddAnimationClip(shadowAnimation, "Replace with shadow clips", 0d, 8d);

            var cueTrack = timeline.CreateTrack<JoeCueTrack>(null, "Joe Cues");
            AddCue(cueTrack, "IK / root motion cue example", 1d, 1d, targets);

            var shadowTrack = timeline.CreateTrack<ActivationTrack>(null, "Shadow Active");
            var activeClip = shadowTrack.CreateDefaultClip();
            activeClip.displayName = "Shadow on";
            activeClip.start = 0d;
            activeClip.duration = 8d;

            timeline.durationMode = TimelineAsset.DurationMode.BasedOnClips;
            return timeline;
        }

        private static void BindTimeline(PlayableDirector director, TimelineAsset timeline, JoeCinematicController joe, GameObject shadow, CinemachineCamera camera)
        {
            foreach (var track in timeline.GetOutputTracks())
            {
                switch (track.name)
                {
                    case "Camera":
                        director.SetGenericBinding(track, Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null);
                        BindCameraReference(director, track, camera);
                        break;
                    case "Joe Animation":
                        director.SetGenericBinding(track, joe != null ? joe.GetComponent<Animator>() : null);
                        break;
                    case "Joe Timeline Movement":
                        director.SetGenericBinding(track, joe);
                        break;
                    case "Shadow Animation":
                        director.SetGenericBinding(track, shadow != null ? shadow.GetComponent<Animator>() : null);
                        break;
                    case "Joe Cues":
                        director.SetGenericBinding(track, joe);
                        break;
                    case "Shadow Active":
                        director.SetGenericBinding(track, shadow);
                        break;
                }
            }
        }

        private static void BindCameraReference(PlayableDirector director, TrackAsset track, CinemachineCamera camera)
        {
            foreach (var clip in track.GetClips())
            {
                if (clip.asset is CinemachineShot shot)
                {
                    director.SetReferenceValue(shot.VirtualCamera.exposedName, camera);
                    return;
                }
            }
        }

        private static void AddCameraShot(CinemachineTrack track, string name, double start, double duration)
        {
            var clip = track.CreateDefaultClip();
            clip.displayName = name;
            clip.start = start;
            clip.duration = duration;
        }

        private static void AddAnimationClip(AnimationTrack track, string name, double start, double duration)
        {
            var clip = track.CreateClip<AnimationPlayableAsset>();
            clip.displayName = name;
            clip.start = start;
            clip.duration = duration;

            if (clip.asset is AnimationPlayableAsset asset)
            {
                asset.clip = LoadPlaceholderClip();
            }
        }

        private static void AddMovementClip(JoeMovementTrack track, string name, double start, double duration, TimelineTargets targets)
        {
            var clip = track.CreateClip<JoeMovementTimelineClip>();
            clip.displayName = name;
            clip.start = start;
            clip.duration = duration;

            if (clip.asset is JoeMovementTimelineClip movement)
            {
                movement.mode = JoeTimelineMotionMode.MoveTo;
                movement.start.defaultValue = targets.moveStart;
                movement.end.defaultValue = targets.moveEnd;
                movement.projectToGround = true;
                movement.faceMotion = true;
            }
        }

        private static void AddCue(JoeCueTrack track, string name, double start, double duration, TimelineTargets targets)
        {
            var clip = track.CreateClip<JoeCueClip>();
            clip.displayName = name;
            clip.start = start;
            clip.duration = duration;

            if (clip.asset is JoeCueClip cue)
            {
                cue.lookTarget.defaultValue = targets.look;
                cue.leftHandTarget.defaultValue = targets.leftHand;
                cue.rightHandTarget.defaultValue = targets.rightHand;
                cue.lookWeight = 0.5f;
                cue.handWeight = 0f;
                cue.footWeight = 0.85f;
            }
        }

        private static AnimationClip LoadPlaceholderClip()
        {
            return AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ChasingShadows/Animations/Joe/Joe_Empty.anim");
        }

        private static Transform CreateMarker(Transform parent, string name, Vector3 localPosition)
        {
            var marker = CreateChild(parent, name);
            marker.transform.localPosition = localPosition;
            return marker.transform;
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(child, $"Create {name}");
            return child;
        }

        private static void EnsureCinemachineBrain()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 1.7f, -5f), Quaternion.Euler(10f, 0f, 0f));
                camera = cameraObject.AddComponent<Camera>();
            }

            if (camera.GetComponent<CinemachineBrain>() == null)
            {
                camera.gameObject.AddComponent<CinemachineBrain>();
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

        private struct TimelineTargets
        {
            public Transform look;
            public Transform leftHand;
            public Transform rightHand;
            public Transform moveStart;
            public Transform moveEnd;
        }
    }
}
