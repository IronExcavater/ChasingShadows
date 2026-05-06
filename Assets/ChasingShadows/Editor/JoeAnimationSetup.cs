using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ChasingShadows.Editor
{
    public static class JoeAnimationSetup
    {
        public const string ControllerPath = "Assets/ChasingShadows/AnimationControllers/Joe_Cinematic.controller";

        private const string JoeAnimationFolder = "Assets/ChasingShadows/Animations/Joe";
        private const string JoeAvatarModelPath = "Assets/ChasingShadows/Characters/Joe/Joe.fbx";
        private const string ImportedFolder = JoeAnimationFolder + "/Imported";
        private const string PlaceholderPath = JoeAnimationFolder + "/Empty Pose.anim";
        private const string LegacyPlaceholderPath = JoeAnimationFolder + "/Joe_Empty.anim";

        private static readonly string[] FloatParameters =
        {
            "MoveSpeed",
            "MoveForward",
            "MoveRight",
            "TurnSpeed",
            "LeanForward",
            "LeanRight"
        };

        private static readonly string[] TriggerParameters =
        {
            "RootMotionBeat",
            "Startled",
            "Interact",
            "RunStart",
            "RunStop",
            "HardTurn",
            "HardTurnLeft",
            "HardTurnRight",
            "Jump",
            "Vault",
            "Climb",
            "Drop",
            "Stumble",
            "Land",
            "Knockout",
            "CoverEnter",
            "CoverExit",
            "CoverSneakLeft",
            "CoverSneakRight",
            "CrouchSneakLeft",
            "CrouchSneakRight",
            "FallingRoll",
            "LookBack",
            "AboutFace",
            "StopAtWall",
            "VaultStumble",
            "EdgeSlip",
            "FallImpact",
            "StandUp",
            "CoverEnterCrouch"
        };

        private static readonly AssetRename[] AnimationRenames =
        {
            new("Assets/ChasingShadows/Animations/Joe/Imported/Locomotion Pack/idle.fbx", "Assets/ChasingShadows/Animations/Joe/Neutral Idle.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Locomotion Pack/walking.fbx", "Assets/ChasingShadows/Animations/Joe/Steady Walk.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Locomotion Pack/running.fbx", "Assets/ChasingShadows/Animations/Joe/Steady Run.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Locomotion Pack/jump.fbx", "Assets/ChasingShadows/Animations/Joe/Chase Jump.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Locomotion Pack/left strafe walking.fbx", "Assets/ChasingShadows/Animations/Joe/Left Smooth Strafe Walk.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Locomotion Pack/right strafe walking.fbx", "Assets/ChasingShadows/Animations/Joe/Right Smooth Strafe Walk.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Locomotion Pack/left strafe.fbx", "Assets/ChasingShadows/Animations/Joe/Left Fast Strafe.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Locomotion Pack/right strafe.fbx", "Assets/ChasingShadows/Animations/Joe/Right Fast Strafe.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Locomotion Pack/left turn.fbx", "Assets/ChasingShadows/Animations/Joe/Left Turn.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Locomotion Pack/right turn.fbx", "Assets/ChasingShadows/Animations/Joe/Right Turn.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Locomotion Pack/left turn 90.fbx", "Assets/ChasingShadows/Animations/Joe/Left Sharp Turn.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Locomotion Pack/right turn 90.fbx", "Assets/ChasingShadows/Animations/Joe/Right Sharp Turn.fbx"),

            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/cover to stand.fbx", "Assets/ChasingShadows/Animations/Joe/Cover Exit.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/cover to stand (2).fbx", "Assets/ChasingShadows/Animations/Joe/Cover Exit Quick.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/stand to cover.fbx", "Assets/ChasingShadows/Animations/Joe/Cover Enter.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/stand to cover (2).fbx", "Assets/ChasingShadows/Animations/Joe/Cover Enter Quick.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/crouched sneaking left.fbx", "Assets/ChasingShadows/Animations/Joe/Left Crouch Sneak.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/crouched sneaking right.fbx", "Assets/ChasingShadows/Animations/Joe/Right Crouch Sneak.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/falling idle.fbx", "Assets/ChasingShadows/Animations/Joe/Falling Airborne Idle.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/falling to roll.fbx", "Assets/ChasingShadows/Animations/Joe/Falling Roll.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/hard landing.fbx", "Assets/ChasingShadows/Animations/Joe/Heavy Hard Landing.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/idle.fbx", "Assets/ChasingShadows/Animations/Joe/Adventure Idle.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/idle (2).fbx", "Assets/ChasingShadows/Animations/Joe/Alert Idle.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/idle (3).fbx", "Assets/ChasingShadows/Animations/Joe/Nervous Idle.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/idle (4).fbx", "Assets/ChasingShadows/Animations/Joe/Tired Idle.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/idle (5).fbx", "Assets/ChasingShadows/Animations/Joe/Ready Idle.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/jumping up.fbx", "Assets/ChasingShadows/Animations/Joe/Adventure Jump Up.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/left cover sneak.fbx", "Assets/ChasingShadows/Animations/Joe/Left Cover Sneak.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/right cover sneak.fbx", "Assets/ChasingShadows/Animations/Joe/Right Cover Sneak.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/left turn.fbx", "Assets/ChasingShadows/Animations/Joe/Adventure Left Turn.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/right turn.fbx", "Assets/ChasingShadows/Animations/Joe/Adventure Right Turn.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/run to stop.fbx", "Assets/ChasingShadows/Animations/Joe/Adventure Run Stop.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/running.fbx", "Assets/ChasingShadows/Animations/Joe/Adventure Run.fbx"),
            new("Assets/ChasingShadows/Animations/Joe/Imported/Action Adventure Pack/walking.fbx", "Assets/ChasingShadows/Animations/Joe/Adventure Walk.fbx"),
            new(LegacyPlaceholderPath, PlaceholderPath)
        };

        private static readonly string[] ExtraClipNameQualifiers =
        {
            "Alternate",
            "Extended",
            "Short",
            "Loop",
            "Intro",
            "Outro",
            "Variant",
            "Secondary"
        };

        [MenuItem("Chasing Shadows/Animations/Prepare Joe Animation Set")]
        public static void SetupJoeAnimationSet()
        {
            NormalizeJoeAnimationAssets();
            ApplyJoeImportSettings();
            RebuildJoeCinematicController();
        }

        [MenuItem("Chasing Shadows/Animations/Normalize Joe Animation Assets")]
        public static void NormalizeJoeAnimationAssets()
        {
            AssetDatabase.Refresh();

            foreach (var rename in AnimationRenames)
            {
                MoveAnimationAsset(rename.Source, rename.Destination);
            }

            DeleteFolderIfEmpty(ImportedFolder + "/Locomotion Pack");
            DeleteFolderIfEmpty(ImportedFolder + "/Action Adventure Pack");
            DeleteFolderIfEmpty(ImportedFolder);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Chasing Shadows/Animations/Apply Joe Humanoid Import Settings")]
        public static void ApplyJoeImportSettings()
        {
            AssetDatabase.Refresh();

            var joeAvatar = LoadJoeAvatar();
            if (joeAvatar == null)
            {
                Debug.LogError($"Could not find a humanoid JoeAvatar in {JoeAvatarModelPath}.");
                return;
            }

            var modelPaths = AssetDatabase
                .FindAssets("t:Model", new[] { JoeAnimationFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .OrderBy(path => path)
                .ToArray();

            foreach (var path in modelPaths)
            {
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                {
                    continue;
                }

                var changed = false;
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    changed = true;
                }

                if (importer.avatarSetup != ModelImporterAvatarSetup.CopyFromOther)
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    changed = true;
                }

                if (importer.sourceAvatar != joeAvatar)
                {
                    importer.sourceAvatar = joeAvatar;
                    changed = true;
                }

                if (!importer.importAnimation)
                {
                    importer.importAnimation = true;
                    changed = true;
                }

                var clipAnimations = importer.clipAnimations is { Length: > 0 }
                    ? importer.clipAnimations
                    : importer.defaultClipAnimations;
                var shouldLoop = ShouldLoop(path);
                var baseClipName = QualitativeName(Path.GetFileNameWithoutExtension(path));
                for (var i = 0; i < clipAnimations.Length; i++)
                {
                    var desiredClipName = ClipName(baseClipName, i, clipAnimations.Length);
                    if (clipAnimations[i].name != desiredClipName)
                    {
                        clipAnimations[i].name = desiredClipName;
                        changed = true;
                    }

                    if (clipAnimations[i].loopTime != shouldLoop)
                    {
                        clipAnimations[i].loopTime = shouldLoop;
                        changed = true;
                    }

                    if (clipAnimations[i].loopPose != shouldLoop)
                    {
                        clipAnimations[i].loopPose = shouldLoop;
                        changed = true;
                    }

                    // Bake root motion into pose so all animations are truly in-place.
                    // JoeMovementTrack owns all positional/rotational changes during cinematics.
                    if (!clipAnimations[i].lockRootPositionXZ)
                    {
                        clipAnimations[i].lockRootPositionXZ = true;
                        changed = true;
                    }

                    if (!clipAnimations[i].lockRootHeightY)
                    {
                        clipAnimations[i].lockRootHeightY = true;
                        changed = true;
                    }

                    if (!clipAnimations[i].lockRootRotation)
                    {
                        clipAnimations[i].lockRootRotation = true;
                        changed = true;
                    }
                }

                if (changed)
                {
                    importer.clipAnimations = clipAnimations;
                    AssetDatabase.WriteImportSettingsIfDirty(path);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Chasing Shadows/Animations/Rebuild Joe Cinematic Controller")]
        public static void RebuildJoeCinematicController()
        {
            EnsureFolder(Path.GetDirectoryName(ControllerPath)?.Replace('\\', '/'));

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            RemoveControllerSubAssets(controller);
            RebuildParameters(controller);

            var baseMachine = CreateStateMachine(controller, "Base Layer");
            var baseLayer = new AnimatorControllerLayer
            {
                name = "Base Layer",
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                stateMachine = baseMachine
            };

            var locomotion = baseMachine.AddState("Base Movement", new Vector3(220f, 120f, 0f));
            locomotion.iKOnFeet = true;
            locomotion.motion = CreateLocomotionTree(controller);
            baseMachine.defaultState = locomotion;

            AddActionState(controller, baseMachine, locomotion, "RunStart", "RunStart", 1.1f,
                "Assets/ChasingShadows/Animations/Joe/Idle To Sprint.fbx",
                "Assets/ChasingShadows/Animations/Joe/Adventure Run.fbx");
            AddActionState(controller, baseMachine, locomotion, "RunStop", "RunStop", 0.9f,
                "Assets/ChasingShadows/Animations/Joe/Run To Stop.fbx",
                "Assets/ChasingShadows/Animations/Joe/Adventure Run Stop.fbx");
            AddActionState(controller, baseMachine, locomotion, "Hard Turn Left", "HardTurn", 1f,
                "Assets/ChasingShadows/Animations/Joe/Left Sharp Turn.fbx",
                "Assets/ChasingShadows/Animations/Joe/Anxious Left Turn.fbx",
                "Assets/ChasingShadows/Animations/Joe/Adventure Left Turn.fbx");
            AddActionState(controller, baseMachine, locomotion, "Hard Turn Left Sharp", "HardTurnLeft", 1f,
                "Assets/ChasingShadows/Animations/Joe/Left Sharp Turn.fbx",
                "Assets/ChasingShadows/Animations/Joe/Anxious Left Turn.fbx");
            AddActionState(controller, baseMachine, locomotion, "Hard Turn Right Sharp", "HardTurnRight", 1f,
                "Assets/ChasingShadows/Animations/Joe/Right Sharp Turn.fbx",
                "Assets/ChasingShadows/Animations/Joe/Anxious Right Turn.fbx",
                "Assets/ChasingShadows/Animations/Joe/Startled Turning Right.fbx");
            AddActionState(controller, baseMachine, locomotion, "Look Back", "LookBack", 1f,
                "Assets/ChasingShadows/Animations/Joe/Run Look Back.fbx");
            AddActionState(controller, baseMachine, locomotion, "About Face", "AboutFace", 1f,
                "Assets/ChasingShadows/Animations/Joe/Running About Face.fbx",
                "Assets/ChasingShadows/Animations/Joe/Long Running About Face.fbx",
                "Assets/ChasingShadows/Animations/Joe/Walking About Face.fbx",
                "Assets/ChasingShadows/Animations/Joe/Idle Left About Face.fbx",
                "Assets/ChasingShadows/Animations/Joe/Idle Right About Face.fbx");
            AddActionState(controller, baseMachine, locomotion, "Stop At Wall", "StopAtWall", 0.95f,
                "Assets/ChasingShadows/Animations/Joe/Run To Stop At Wall.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running Turn To Idle.fbx",
                "Assets/ChasingShadows/Animations/Joe/Run To Stop.fbx");
            AddActionState(controller, baseMachine, locomotion, "Jump", "Jump", 1f,
                "Assets/ChasingShadows/Animations/Joe/Chase Jump.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running Jump Up.fbx",
                "Assets/ChasingShadows/Animations/Joe/Jumping Up.fbx");
            AddActionState(controller, baseMachine, locomotion, "Vault", "Vault", 1.1f,
                "Assets/ChasingShadows/Animations/Joe/Vault Over Box.fbx");
            AddActionState(controller, baseMachine, locomotion, "Vault Stumble", "VaultStumble", 1f,
                "Assets/ChasingShadows/Animations/Joe/Vault Stumble.fbx",
                "Assets/ChasingShadows/Animations/Joe/Jogging Stumble.fbx");
            AddActionState(controller, baseMachine, locomotion, "Climb", "Climb", 1.3f,
                "Assets/ChasingShadows/Animations/Joe/Jump To Climb Wall.fbx",
                "Assets/ChasingShadows/Animations/Joe/Hanging Wall Ascend.fbx",
                "Assets/ChasingShadows/Animations/Joe/Sprint Climb Ascend.fbx",
                "Assets/ChasingShadows/Animations/Joe/Run Climb Ascend.fbx",
                "Assets/ChasingShadows/Animations/Joe/Climbing Up Wall.fbx");
            AddActionState(controller, baseMachine, locomotion, "Drop", "Drop", 0.8f,
                "Assets/ChasingShadows/Animations/Joe/Dismount Jump From Wall.fbx",
                "Assets/ChasingShadows/Animations/Joe/Jumping Down.fbx",
                "Assets/ChasingShadows/Animations/Joe/Anxious Jump Down.fbx");
            AddActionState(controller, baseMachine, locomotion, "Land", "Land", 0.6f,
                "Assets/ChasingShadows/Animations/Joe/Hard Landing.fbx",
                "Assets/ChasingShadows/Animations/Joe/Heavy Hard Landing.fbx",
                "Assets/ChasingShadows/Animations/Joe/Falling To Landing.fbx");
            AddActionState(controller, baseMachine, locomotion, "Stumble", "Stumble", 1f,
                "Assets/ChasingShadows/Animations/Joe/Running Trip Roll Onto Side.fbx",
                "Assets/ChasingShadows/Animations/Joe/Jogging Stumble.fbx",
                "Assets/ChasingShadows/Animations/Joe/Vault Stumble.fbx",
                "Assets/ChasingShadows/Animations/Joe/Walking Trip Onto Side.fbx",
                "Assets/ChasingShadows/Animations/Joe/Walking Trip Onto Front.fbx",
                "Assets/ChasingShadows/Animations/Joe/Falling Roll.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running Slide.fbx",
                "Assets/ChasingShadows/Animations/Joe/Diving Away.fbx");
            AddActionState(controller, baseMachine, locomotion, "Edge Slip", "EdgeSlip", 1f,
                "Assets/ChasingShadows/Animations/Joe/Edge Slip.fbx");
            AddActionState(controller, baseMachine, locomotion, "Fall Impact", "FallImpact", 1f,
                "Assets/ChasingShadows/Animations/Joe/Falling Flat Impact.fbx",
                "Assets/ChasingShadows/Animations/Joe/Walking Trip Fall Flat.fbx",
                "Assets/ChasingShadows/Animations/Joe/Falling To Landing.fbx");
            AddActionState(controller, baseMachine, locomotion, "Knockout", "Knockout", 1f,
                "Assets/ChasingShadows/Animations/Joe/Sleep Idle.fbx",
                "Assets/ChasingShadows/Animations/Joe/Sleeping Restless.fbx",
                "Assets/ChasingShadows/Animations/Joe/Sleeping Mild Cough.fbx");
            AddActionState(controller, baseMachine, locomotion, "Interact", "Interact", 1f,
                "Assets/ChasingShadows/Animations/Joe/Using Touchscreen Tablet Standing.fbx",
                "Assets/ChasingShadows/Animations/Joe/Texting Standing.fbx",
                "Assets/ChasingShadows/Animations/Joe/Calling Standing.fbx",
                "Assets/ChasingShadows/Animations/Joe/Working On Computer.fbx");
            AddActionState(controller, baseMachine, locomotion, "Startled", "Startled", 1f,
                "Assets/ChasingShadows/Animations/Joe/Startled Turning Right.fbx");
            AddActionState(controller, baseMachine, locomotion, "Cover Enter", "CoverEnter", 1f,
                "Assets/ChasingShadows/Animations/Joe/Cover Enter.fbx",
                "Assets/ChasingShadows/Animations/Joe/Cover Enter Quick.fbx");
            AddActionState(controller, baseMachine, locomotion, "Cover Exit", "CoverExit", 1f,
                "Assets/ChasingShadows/Animations/Joe/Cover Exit.fbx",
                "Assets/ChasingShadows/Animations/Joe/Cover Exit Quick.fbx");
            AddActionState(controller, baseMachine, locomotion, "Cover Sneak Left", "CoverSneakLeft", 1f,
                "Assets/ChasingShadows/Animations/Joe/Left Cover Sneak.fbx");
            AddActionState(controller, baseMachine, locomotion, "Cover Sneak Right", "CoverSneakRight", 1f,
                "Assets/ChasingShadows/Animations/Joe/Right Cover Sneak.fbx");
            AddActionState(controller, baseMachine, locomotion, "Crouch Sneak Left", "CrouchSneakLeft", 1f,
                "Assets/ChasingShadows/Animations/Joe/Left Crouch Sneak.fbx");
            AddActionState(controller, baseMachine, locomotion, "Crouch Sneak Right", "CrouchSneakRight", 1f,
                "Assets/ChasingShadows/Animations/Joe/Right Crouch Sneak.fbx");
            AddActionState(controller, baseMachine, locomotion, "Falling Roll", "FallingRoll", 1f,
                "Assets/ChasingShadows/Animations/Joe/Falling Roll.fbx");
            AddActionState(controller, baseMachine, locomotion, "Stand Up", "StandUp", 1f,
                "Assets/ChasingShadows/Animations/Joe/Fall Flat To Standing Up.fbx",
                "Assets/ChasingShadows/Animations/Joe/Back To Standing Up.fbx",
                "Assets/ChasingShadows/Animations/Joe/Crouch Fall To Standing Up.fbx");
            AddActionState(controller, baseMachine, locomotion, "Cover Enter Crouch", "CoverEnterCrouch", 1f,
                "Assets/ChasingShadows/Animations/Joe/Cover Enter Crouch.fbx");

            var additiveMachine = CreateStateMachine(controller, "AdditiveLookLean");
            var additiveLayer = new AnimatorControllerLayer
            {
                name = "AdditiveLookLean",
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Additive,
                stateMachine = additiveMachine
            };
            var empty = additiveMachine.AddState("Empty", new Vector3(220f, 120f, 0f));
            empty.motion = PlaceholderClip();
            additiveMachine.defaultState = empty;

            controller.layers = new[] { baseLayer, additiveLayer };
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static AnimationClip FindClip(params string[] paths)
        {
            foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(candidate => !candidate.name.StartsWith("__preview", StringComparison.OrdinalIgnoreCase));
                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }

        public static AnimationClip PlaceholderClip()
        {
            EnsureFolder(JoeAnimationFolder);

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(PlaceholderPath);
            if (clip != null)
            {
                return clip;
            }

            clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(LegacyPlaceholderPath);
            if (clip != null)
            {
                return clip;
            }

            clip = new AnimationClip
            {
                name = "Empty Pose",
                frameRate = 60f
            };
            AssetDatabase.CreateAsset(clip, PlaceholderPath);
            AssetDatabase.SaveAssets();
            return clip;
        }

        private static BlendTree CreateLocomotionTree(UnityEngine.Object asset)
        {
            var tree = new BlendTree
            {
                name = "Joe_Base_Movement_2D",
                blendType = BlendTreeType.FreeformCartesian2D,
                blendParameter = "MoveRight",
                blendParameterY = "MoveForward",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, asset);

            AddBlendChild(tree, 0f, 0f,
                "Assets/ChasingShadows/Animations/Joe/Breathing Idle.fbx",
                "Assets/ChasingShadows/Animations/Joe/Neutral Idle.fbx",
                "Assets/ChasingShadows/Animations/Joe/Adventure Idle.fbx",
                "Assets/ChasingShadows/Animations/Joe/Idle.fbx");
            AddBlendChild(tree, 0f, 1.35f,
                "Assets/ChasingShadows/Animations/Joe/Walking Forwards.fbx",
                "Assets/ChasingShadows/Animations/Joe/Steady Walk.fbx",
                "Assets/ChasingShadows/Animations/Joe/Adventure Walk.fbx");
            // Intermediate jog node bridges the large gap between walk (1.35) and full run (5.1)
            AddBlendChild(tree, 0f, 2.8f,
                "Assets/ChasingShadows/Animations/Joe/Steady Run.fbx",
                "Assets/ChasingShadows/Animations/Joe/Adventure Run.fbx",
                "Assets/ChasingShadows/Animations/Joe/Running Forwards.fbx");
            AddBlendChild(tree, 0f, 5.1f,
                "Assets/ChasingShadows/Animations/Joe/Running Forwards.fbx",
                "Assets/ChasingShadows/Animations/Joe/Steady Run.fbx",
                "Assets/ChasingShadows/Animations/Joe/Adventure Run.fbx");
            AddBlendChild(tree, 0f, -1.35f,
                "Assets/ChasingShadows/Animations/Joe/Walking Backwards.fbx");
            AddBlendChild(tree, 0f, -2.5f,
                "Assets/ChasingShadows/Animations/Joe/Running Backwards.fbx");
            AddBlendChild(tree, -1.35f, 0f,
                "Assets/ChasingShadows/Animations/Joe/Left Strafe Walk.fbx",
                "Assets/ChasingShadows/Animations/Joe/Left Smooth Strafe Walk.fbx");
            AddBlendChild(tree, 1.35f, 0f,
                "Assets/ChasingShadows/Animations/Joe/Right Strafe Walk.fbx",
                "Assets/ChasingShadows/Animations/Joe/Right Smooth Strafe Walk.fbx");
            AddBlendChild(tree, -3.8f, 0f,
                "Assets/ChasingShadows/Animations/Joe/Left Fast Strafe.fbx",
                "Assets/ChasingShadows/Animations/Joe/Left Strafe Walk.fbx");
            AddBlendChild(tree, 3.8f, 0f,
                "Assets/ChasingShadows/Animations/Joe/Right Fast Strafe.fbx",
                "Assets/ChasingShadows/Animations/Joe/Right Strafe Walk.fbx");

            return tree;
        }

        private static void AddBlendChild(BlendTree tree, float x, float y, params string[] paths)
        {
            var clip = FindClip(paths);
            if (clip == null)
            {
                return;
            }

            tree.AddChild(clip, new Vector2(x, y));
        }

        private static void AddActionState(
            UnityEngine.Object asset,
            AnimatorStateMachine stateMachine,
            AnimatorState locomotionState,
            string stateName,
            string triggerName,
            float speed,
            params string[] clipPaths)
        {
            var clip = FindClip(clipPaths);
            if (clip == null)
            {
                return;
            }

            var state = stateMachine.AddState(stateName);
            state.motion = clip;
            state.speed = speed;
            state.iKOnFeet = true;

            var enter = stateMachine.AddAnyStateTransition(state);
            enter.hasExitTime = false;
            enter.duration = 0.15f;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, triggerName);

            var exit = state.AddTransition(locomotionState);
            exit.hasExitTime = true;
            exit.exitTime = 0.82f;
            exit.duration = 0.22f;
            exit.hasFixedDuration = true;

            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(asset);
        }

        private static AnimatorStateMachine CreateStateMachine(UnityEngine.Object asset, string name)
        {
            var stateMachine = new AnimatorStateMachine { name = name };
            AssetDatabase.AddObjectToAsset(stateMachine, asset);
            return stateMachine;
        }

        private static void RebuildParameters(AnimatorController controller)
        {
            foreach (var parameter in controller.parameters.ToArray())
            {
                controller.RemoveParameter(parameter);
            }

            foreach (var parameter in FloatParameters)
            {
                controller.AddParameter(parameter, AnimatorControllerParameterType.Float);
            }

            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);

            foreach (var parameter in TriggerParameters)
            {
                controller.AddParameter(parameter, AnimatorControllerParameterType.Trigger);
            }
        }

        private static void RemoveControllerSubAssets(AnimatorController controller)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            {
                if (asset == null || asset == controller || AssetDatabase.IsMainAsset(asset))
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(asset, true);
            }
        }

        private static Avatar LoadJoeAvatar()
        {
            var avatars = AssetDatabase.LoadAllAssetsAtPath(JoeAvatarModelPath)
                .OfType<Avatar>()
                .ToArray();

            return avatars.FirstOrDefault(avatar => avatar.name.Equals("JoeAvatar", StringComparison.OrdinalIgnoreCase))
                ?? avatars.FirstOrDefault(avatar => avatar.isHuman)
                ?? avatars.FirstOrDefault();
        }

        private static bool ShouldLoop(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (name.Contains("sleeping") || name.Contains("sleep") || name.Contains("driving"))
            {
                return true;
            }

            if (name.Contains("idle") &&
                !name.Contains(" to ") &&
                !name.Contains("jump") &&
                !name.Contains("turn") &&
                !name.Contains("climb") &&
                !name.Contains("fall"))
            {
                return true;
            }

            if (name.Contains("falling airborne"))
            {
                return true;
            }

            if (name.Contains("walk") || name.Contains("run") || name.Contains("strafe") || name.Contains("sneak") ||
                name.Contains("wall run"))
            {
                return !name.Contains("jump") &&
                       !name.Contains("stop") &&
                       !name.Contains("turn") &&
                       !name.Contains("climb") &&
                       !name.Contains("cover enter") &&
                       !name.Contains("cover exit") &&
                       !name.Contains("falling roll") &&
                       !name.Contains("trip") &&
                       !name.Contains("stumble") &&
                       !name.Contains("vault") &&
                       !name.Contains("slip") &&
                       !name.Contains("impact") &&
                       !name.Contains("look back") &&
                       !name.Contains("sprint") &&
                       !name.Contains("about face");
            }

            return name.Contains("climbing up ladder") || name.Contains("climbing wall idle");
        }

        private static string ClipName(string baseName, int index, int total)
        {
            if (total <= 1)
            {
                return baseName;
            }

            var qualifier = index < ExtraClipNameQualifiers.Length
                ? ExtraClipNameQualifiers[index]
                : "Extra";
            return $"{baseName} {qualifier}";
        }

        private static string QualitativeName(string name)
        {
            var chars = name.Select(character =>
                char.IsLetter(character) || char.IsWhiteSpace(character)
                    ? character
                    : ' ').ToArray();
            var cleaned = new string(chars);
            while (cleaned.Contains("  ", StringComparison.Ordinal))
            {
                cleaned = cleaned.Replace("  ", " ", StringComparison.Ordinal);
            }

            cleaned = cleaned.Trim();
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return "Joe Animation";
            }

            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(cleaned.ToLowerInvariant());
        }

        private static void MoveAnimationAsset(string source, string destination)
        {
            if (AssetDatabase.LoadMainAssetAtPath(source) == null)
            {
                return;
            }

            EnsureFolder(Path.GetDirectoryName(destination)?.Replace('\\', '/'));

            if (AssetDatabase.LoadMainAssetAtPath(destination) != null)
            {
                Debug.LogWarning($"Could not move {source} to {destination} because the destination already exists.");
                return;
            }

            var error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrWhiteSpace(error))
            {
                Debug.LogError($"Could not move {source} to {destination}: {error}");
            }
        }

        private static void DeleteFolderIfEmpty(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            if (AssetDatabase.FindAssets(string.Empty, new[] { folder }).Length == 0)
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private readonly struct AssetRename
        {
            public readonly string Source;
            public readonly string Destination;

            public AssetRename(string source, string destination)
            {
                Source = source;
                Destination = destination;
            }
        }
    }
}
