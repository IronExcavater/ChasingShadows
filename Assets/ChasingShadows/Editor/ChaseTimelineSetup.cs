using System.IO;
using ChasingShadows.Characters;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Timeline;

namespace ChasingShadows.Editor
{
    public static class ChaseTimelineSetup
    {
        private const string RootName = "Chase_Timeline_Example";
        private const string TimelinePath = "Assets/ChasingShadows/Timelines/Chase_Sequence.playable";

        [MenuItem("Chasing Shadows/Chase/Create Timeline Example")]
        public static void CreateTimelineExample()
        {
            EnsureFolder("Assets/ChasingShadows/Timelines");

            var root = RecreateRoot();
            var joe = FindOrCreateJoe(root.transform);
            var shadow = CreateShadowFromJoe(root.transform, joe);
            CreateTargets(CreateChild(root.transform, "Targets - drag these into cue clips").transform);
            CreateActionMarkers(CreateChild(root.transform, "Action Marks").transform);
            CreateSet(CreateChild(root.transform, "Set").transform);
            CreateLighting(CreateChild(root.transform, "Lighting").transform);
            var cameras = CreateCameras(CreateChild(root.transform, "Cameras").transform);
            var director = CreateDirector(root);
            var timeline = CreateTimelineAsset();

            director.playableAsset = timeline;
            BindTimeline(director, timeline, joe, shadow, cameras);

            if (joe != null)
            {
                joe.SetMotionDriver(JoeCinematicController.MotionDriver.External);
                joe.SetLookTarget(null, 0f);
                joe.SetHandTargets(null, null, 0f);
                joe.SetIkWeights(0f, 0f, joe.footIkWeight);
            }

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
            AssetDatabase.SaveAssets();
        }

        private static GameObject RecreateRoot()
        {
            DestroyIfExists(RootName);
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create chase timeline example");
            return root;
        }

        private static JoeCinematicController FindOrCreateJoe(Transform parent)
        {
            foreach (var controller in Object.FindObjectsByType<JoeCinematicController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!controller.name.Contains("Shadow", System.StringComparison.OrdinalIgnoreCase))
                {
                    controller.transform.SetParent(parent, true);
                    controller.gameObject.name = "Joe";
                    return controller;
                }
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChasingShadows/Prefabs/Joe.prefab") ??
                         AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChasingShadows/Prefabs/Joe_Base.prefab");
            if (prefab == null)
            {
                Debug.LogWarning("Joe prefab not found. Create or drag Joe into the scene, then rerun the setup.");
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

        private static GameObject CreateShadowFromJoe(Transform parent, JoeCinematicController joe)
        {
            if (joe == null)
            {
                return null;
            }

            var shadow = Object.Instantiate(joe.gameObject, parent);
            shadow.name = "Shadow";
            shadow.transform.SetPositionAndRotation(joe.transform.position, joe.transform.rotation);
            shadow.transform.localScale = joe.transform.localScale;

            foreach (var renderer in joe.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            foreach (var renderer in shadow.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                renderer.receiveShadows = false;
            }

            foreach (var controller in shadow.GetComponentsInChildren<JoeCinematicController>(true))
            {
                Object.DestroyImmediate(controller);
            }

            foreach (var agent in shadow.GetComponentsInChildren<NavMeshAgent>(true))
            {
                Object.DestroyImmediate(agent);
            }

            foreach (var collider in shadow.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            return shadow;
        }

        private static void CreateTargets(Transform parent)
        {
            CreateMarker(parent, "Look_Shadow", new Vector3(1.7f, 1.35f, 3.8f));
            CreateMarker(parent, "Look_Woman", new Vector3(0f, 1.35f, 24f));
            CreateMarker(parent, "LeftHand_Vault", new Vector3(1.05f, 0.9f, 10.05f));
            CreateMarker(parent, "RightHand_Vault", new Vector3(1.4f, 0.9f, 10.05f));
            CreateMarker(parent, "LeftHand_Climb", new Vector3(-1.55f, 1.45f, 13.2f));
            CreateMarker(parent, "RightHand_Climb", new Vector3(-0.9f, 1.45f, 13.2f));
        }

        private static void CreateActionMarkers(Transform parent)
        {
            var markers = new (string name, Vector3 position)[]
            {
                ("00_Start", new Vector3(0f, 0f, 0f)),
                ("01_Run_Start", new Vector3(0f, 0f, 1.2f)),
                ("02_Hard_Turn", new Vector3(1.2f, 0f, 3.2f)),
                ("03_Jump_Takeoff", new Vector3(-0.3f, 0f, 5.8f)),
                ("04_Jump_Land", new Vector3(0.8f, 0f, 7.6f)),
                ("05_Vault_Rail", new Vector3(0.75f, 0f, 10.25f)),
                ("06_Climb_Wall", new Vector3(-1.2f, 0f, 13.7f)),
                ("07_Drop_Land", new Vector3(0.4f, 0f, 15.8f)),
                ("08_Stumble", new Vector3(1.6f, 0f, 17.4f)),
                ("09_End", new Vector3(0.1f, 0f, 21.8f)),
            };

            foreach (var marker in markers)
            {
                CreateMarker(parent, marker.name, marker.position);
            }
        }

        private static void CreateSet(Transform parent)
        {
            CreateCube(parent, "Jump_Curb", new Vector3(0.25f, 0.18f, 6.7f), new Vector3(2.2f, 0.35f, 0.35f));
            CreateCube(parent, "Vault_Rail", new Vector3(0.75f, 0.72f, 10.25f), new Vector3(2.6f, 0.16f, 0.16f));
            CreateCube(parent, "Climb_Wall", new Vector3(-1.2f, 0.85f, 13.7f), new Vector3(1.8f, 1.7f, 0.22f));
            CreateCube(parent, "Drop_Platform", new Vector3(-1.2f, 1.55f, 14.8f), new Vector3(2.2f, 0.16f, 1.4f));
            CreateCube(parent, "Foreground_Cut_Post", new Vector3(0.7f, 1.2f, 16.6f), new Vector3(0.22f, 2.4f, 1.3f));
        }

        private static void CreateLighting(Transform parent)
        {
            CreateLight(parent, "Chase_Key_Light", new Vector3(-3.5f, 3.4f, 5.2f), new Vector3(54f, 140f, 0f), 1000f, new Color(1f, 0.83f, 0.52f));
        }

        private static CameraSet CreateCameras(Transform parent)
        {
            EnsureCinemachineBrain();
            return new CameraSet
            {
                alleyRun = CreateCamera(parent, "CM_01_Alley_Run", new Vector3(-3.2f, 1.45f, 1.6f), new Vector3(8f, 54f, 0f), 38f),
                shadowTurn = CreateCamera(parent, "CM_02_Shadow_Turn", new Vector3(1.2f, 1.25f, 3.8f), new Vector3(5f, 178f, 0f), 46f),
                parkWide = CreateCamera(parent, "CM_03_Park_Wide", new Vector3(-5.8f, 2.1f, 10.6f), new Vector3(11f, 68f, 0f), 35f),
                lowJump = CreateCamera(parent, "CM_04_Low_Jump", new Vector3(0.35f, 0.45f, 5.4f), new Vector3(-2f, 18f, 0f), 42f),
                vaultClimb = CreateCamera(parent, "CM_05_Vault_Climb", new Vector3(2.4f, 1.25f, 11.8f), new Vector3(7f, -150f, 0f), 40f),
                stumbleClose = CreateCamera(parent, "CM_06_Stumble_Close", new Vector3(0.2f, 0.9f, 16.2f), new Vector3(4f, -176f, 0f), 50f),
            };
        }

        private static PlayableDirector CreateDirector(GameObject parent)
        {
            var directorObject = CreateChild(parent.transform, "TimelineDirector");
            var director = directorObject.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.extrapolationMode = DirectorWrapMode.Hold;
            return director;
        }

        private static TimelineAsset CreateTimelineAsset()
        {
            DeleteAssetIfExists(TimelinePath);

            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.editorSettings.frameRate = 24d;
            AssetDatabase.CreateAsset(timeline, TimelinePath);

            var cameraTrack = timeline.CreateTrack<CinemachineTrack>(null, "Camera Shots");
            AddCameraShot(cameraTrack, "Alley run", 0.0d, 1.3d);
            AddCameraShot(cameraTrack, "Shadow turn", 1.3d, 0.9d);
            AddCameraShot(cameraTrack, "Jump", 2.2d, 1.0d);
            AddCameraShot(cameraTrack, "Park wide", 3.2d, 1.0d);
            AddCameraShot(cameraTrack, "Vault and climb", 4.2d, 2.0d);
            AddCameraShot(cameraTrack, "Drop and stumble", 6.2d, 1.5d);
            AddCameraShot(cameraTrack, "Final sprint", 7.7d, 1.5d);

            var joeAnimation = timeline.CreateTrack<AnimationTrack>(null, "Joe Animation - replace clips here");
            joeAnimation.trackOffset = TrackOffset.Auto;
            AddAnimationClip(joeAnimation, "Idle / anticipation", "Assets/ChasingShadows/Animations/Joe/Idle.fbx", 0.0d, 0.5d);
            AddAnimationClip(joeAnimation, "Run loop - source better sprint", "Assets/ChasingShadows/Animations/Joe/Running.fbx", 0.5d, 1.7d);
            AddAnimationClip(joeAnimation, "Hard turn - replace", "Assets/ChasingShadows/Animations/Joe/Running.fbx", 2.2d, 0.7d);
            AddAnimationClip(joeAnimation, "Jump - replace", "Assets/ChasingShadows/Animations/Joe/Joe_Empty.anim", 2.9d, 0.9d);
            AddAnimationClip(joeAnimation, "Vault - replace", "Assets/ChasingShadows/Animations/Joe/Joe_Empty.anim", 3.8d, 1.0d);
            AddAnimationClip(joeAnimation, "Climb - replace", "Assets/ChasingShadows/Animations/Joe/Joe_Empty.anim", 4.8d, 1.4d);
            AddAnimationClip(joeAnimation, "Drop and stumble - replace", "Assets/ChasingShadows/Animations/Joe/Joe_Empty.anim", 6.2d, 1.2d);
            AddAnimationClip(joeAnimation, "Final run / stop", "Assets/ChasingShadows/Animations/Joe/Running.fbx", 7.4d, 1.8d);

            var shadowAnimation = timeline.CreateTrack<AnimationTrack>(null, "Shadow Animation - mirror Joe clips");
            shadowAnimation.trackOffset = TrackOffset.Auto;
            AddAnimationClip(shadowAnimation, "Shadow run", "Assets/ChasingShadows/Animations/Joe/Running.fbx", 0.5d, 1.7d);
            AddAnimationClip(shadowAnimation, "Shadow action placeholders", "Assets/ChasingShadows/Animations/Joe/Joe_Empty.anim", 2.2d, 5.2d);
            AddAnimationClip(shadowAnimation, "Shadow final run", "Assets/ChasingShadows/Animations/Joe/Running.fbx", 7.4d, 1.8d);

            var cueTrack = timeline.CreateTrack<JoeCueTrack>(null, "Joe Cues - triggers and IK");
            AddCue(cueTrack, "Run start", 0.5d, 0.2d, "RunStart", 0.2f, 0f, 0.9f);
            AddCue(cueTrack, "Look at shadow / hard turn", 1.3d, 0.8d, "HardTurn", 1f, 0f, 0.9f);
            AddCue(cueTrack, "Jump trigger", 2.9d, 0.4d, "Jump", 0.6f, 0f, 0.2f);
            AddCue(cueTrack, "Vault hands - assign targets", 3.8d, 1.0d, "Vault", 0.6f, 0.9f, 0.25f);
            AddCue(cueTrack, "Climb hands - assign targets", 4.8d, 1.4d, "Climb", 0.6f, 1f, 0.35f);
            AddCue(cueTrack, "Drop / stumble", 6.2d, 1.0d, "Stumble", 0.8f, 0.2f, 0.7f);
            AddCue(cueTrack, "Run stop", 8.8d, 0.4d, "RunStop", 0.2f, 0f, 0.9f);

            var shadowActivation = timeline.CreateTrack<ActivationTrack>(null, "Shadow Active");
            var activeClip = shadowActivation.CreateDefaultClip();
            activeClip.displayName = "Shadow visible during chase";
            activeClip.start = 0d;
            activeClip.duration = 9.2d;

            timeline.durationMode = TimelineAsset.DurationMode.BasedOnClips;
            return timeline;
        }

        private static void BindTimeline(PlayableDirector director, TimelineAsset timeline, JoeCinematicController joe, GameObject shadow, CameraSet cameras)
        {
            foreach (var track in timeline.GetOutputTracks())
            {
                switch (track.name)
                {
                    case "Camera Shots":
                        director.SetGenericBinding(track, Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null);
                        BindCameraReferences(director, track, cameras);
                        break;
                    case "Joe Animation - replace clips here":
                        if (joe != null)
                        {
                            director.SetGenericBinding(track, joe.GetComponent<Animator>());
                        }
                        break;
                    case "Shadow Animation - mirror Joe clips":
                        if (shadow != null)
                        {
                            director.SetGenericBinding(track, shadow.GetComponent<Animator>());
                        }
                        break;
                    case "Joe Cues - triggers and IK":
                        director.SetGenericBinding(track, joe);
                        break;
                    case "Shadow Active":
                        director.SetGenericBinding(track, shadow);
                        break;
                }
            }
        }

        private static void BindCameraReferences(PlayableDirector director, TrackAsset track, CameraSet cameras)
        {
            var ordered = new[]
            {
                cameras.alleyRun,
                cameras.shadowTurn,
                cameras.lowJump,
                cameras.parkWide,
                cameras.vaultClimb,
                cameras.stumbleClose,
                cameras.alleyRun,
            };

            var index = 0;
            foreach (var clip in track.GetClips())
            {
                if (clip.asset is CinemachineShot shot && index < ordered.Length && ordered[index] != null)
                {
                    director.SetReferenceValue(shot.VirtualCamera.exposedName, ordered[index]);
                }

                index++;
            }
        }

        private static void AddCameraShot(CinemachineTrack track, string name, double start, double duration)
        {
            var clip = track.CreateDefaultClip();
            clip.displayName = name;
            clip.start = start;
            clip.duration = duration;
        }

        private static void AddAnimationClip(AnimationTrack track, string name, string path, double start, double duration)
        {
            var clip = track.CreateClip<AnimationPlayableAsset>();
            clip.displayName = name;
            clip.start = start;
            clip.duration = duration;

            if (clip.asset is AnimationPlayableAsset asset)
            {
                asset.clip = LoadAnimationClip(path);
            }
        }

        private static void AddCue(
            JoeCueTrack track,
            string name,
            double start,
            double duration,
            string trigger,
            float lookWeight,
            float handWeight,
            float footWeight)
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
            }
        }

        private static AnimationClip LoadAnimationClip(string path)
        {
            var direct = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (direct != null)
            {
                return direct;
            }

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview", System.StringComparison.OrdinalIgnoreCase))
                {
                    return clip;
                }
            }

            return AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ChasingShadows/Animations/Joe/Joe_Empty.anim");
        }

        private static Transform CreateMarker(Transform parent, string name, Vector3 localPosition)
        {
            var marker = CreateChild(parent, name);
            marker.transform.localPosition = localPosition;
            return marker.transform;
        }

        private static void CreateCube(Transform parent, string name, Vector3 localPosition, Vector3 scale)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = scale;
        }

        private static void CreateLight(Transform parent, string name, Vector3 localPosition, Vector3 euler, float intensity, Color color)
        {
            var lightObject = CreateChild(parent, name);
            lightObject.transform.localPosition = localPosition;
            lightObject.transform.localRotation = Quaternion.Euler(euler);

            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 58f;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.Soft;
        }

        private static CinemachineCamera CreateCamera(Transform parent, string name, Vector3 localPosition, Vector3 euler, float fieldOfView)
        {
            var cameraObject = CreateChild(parent, name);
            cameraObject.transform.localPosition = localPosition;
            cameraObject.transform.localRotation = Quaternion.Euler(euler);

            var camera = cameraObject.AddComponent<CinemachineCamera>();
            camera.Lens.FieldOfView = fieldOfView;
            camera.Priority = 0;
            return camera;
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
                cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 2f, -6f), Quaternion.Euler(10f, 0f, 0f));
                camera = cameraObject.AddComponent<Camera>();
            }

            if (camera.GetComponent<CinemachineBrain>() == null)
            {
                camera.gameObject.AddComponent<CinemachineBrain>();
            }
        }

        private static void DestroyIfExists(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
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

        private struct CameraSet
        {
            public CinemachineCamera alleyRun;
            public CinemachineCamera shadowTurn;
            public CinemachineCamera parkWide;
            public CinemachineCamera lowJump;
            public CinemachineCamera vaultClimb;
            public CinemachineCamera stumbleClose;
        }
    }
}
