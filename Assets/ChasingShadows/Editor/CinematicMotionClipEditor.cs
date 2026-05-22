using System.Collections.Generic;
using ChasingShadows.Characters;
using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

namespace ChasingShadows.Editor
{
    [CustomEditor(typeof(CinematicMotionClip))]
    internal sealed class CinematicMotionClipEditor : UnityEditor.Editor
    {
        private static CinematicMotionKnot[] copiedKnots;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(CinematicMotionClip.knots)), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(CinematicMotionClip.faceAlongSpline)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(CinematicMotionClip.rotationOffset)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(CinematicMotionClip.worldUp)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(CinematicMotionClip.applyPosition)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(CinematicMotionClip.applyRotation)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(CinematicMotionClip.positionCurve)));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(CinematicMotionClip.rotationCurve)));

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy Knots"))
                {
                    CopyKnots();
                }

                using (new EditorGUI.DisabledScope(copiedKnots == null || copiedKnots.Length == 0))
                {
                    if (GUILayout.Button("Paste Knots"))
                    {
                        PasteKnots();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Knot"))
                {
                    AddKnot();
                }

                if (GUILayout.Button("Remove Last"))
                {
                    RemoveLastKnot();
                }

                if (GUILayout.Button("Smooth Tangents"))
                {
                    SmoothTangents();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Clear Tangents"))
                {
                    ClearTangents();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void CopyKnots()
        {
            var clip = (CinematicMotionClip)target;
            copiedKnots = CopyKnots(clip.knots);
        }

        private void PasteKnots()
        {
            if (copiedKnots == null || copiedKnots.Length == 0)
            {
                return;
            }

            Undo.RecordObjects(targets, "Paste Motion Knots");
            foreach (var selectedTarget in targets)
            {
                var clip = (CinematicMotionClip)selectedTarget;
                clip.knots = CopyKnots(copiedKnots);
                EditorUtility.SetDirty(clip);
            }

            serializedObject.Update();
            MarkTimelineDirty();
        }

        private void AddKnot()
        {
            var knots = serializedObject.FindProperty(nameof(CinematicMotionClip.knots));
            var insertIndex = knots.arraySize;
            knots.InsertArrayElementAtIndex(insertIndex);

            var position = Vector3.zero;
            var euler = Vector3.zero;
            if (insertIndex > 0)
            {
                var previous = knots.GetArrayElementAtIndex(insertIndex - 1);
                position = previous.FindPropertyRelative(nameof(CinematicMotionKnot.position)).vector3Value + Vector3.forward * 2f;
                euler = previous.FindPropertyRelative(nameof(CinematicMotionKnot.euler)).vector3Value;
            }

            SetKnot(knots.GetArrayElementAtIndex(insertIndex), position, euler, Vector3.zero, Vector3.zero);
            MarkTimelineDirty();
        }

        private void RemoveLastKnot()
        {
            var knots = serializedObject.FindProperty(nameof(CinematicMotionClip.knots));
            if (knots.arraySize <= 1)
            {
                return;
            }

            knots.DeleteArrayElementAtIndex(knots.arraySize - 1);
            MarkTimelineDirty();
        }

        private void SmoothTangents()
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObjects(targets, "Smooth Motion Tangents");

            foreach (var selectedTarget in targets)
            {
                var clip = (CinematicMotionClip)selectedTarget;
                SmoothTangents(clip.knots);
                EditorUtility.SetDirty(clip);
            }

            serializedObject.Update();
            MarkTimelineDirty();
        }

        private void ClearTangents()
        {
            var knots = serializedObject.FindProperty(nameof(CinematicMotionClip.knots));
            for (var i = 0; i < knots.arraySize; i++)
            {
                var knot = knots.GetArrayElementAtIndex(i);
                knot.FindPropertyRelative(nameof(CinematicMotionKnot.inTangent)).vector3Value = Vector3.zero;
                knot.FindPropertyRelative(nameof(CinematicMotionKnot.outTangent)).vector3Value = Vector3.zero;
            }

            MarkTimelineDirty();
        }

        private static void SetKnot(SerializedProperty knot, Vector3 position, Vector3 euler, Vector3 inTangent, Vector3 outTangent)
        {
            knot.FindPropertyRelative(nameof(CinematicMotionKnot.position)).vector3Value = position;
            knot.FindPropertyRelative(nameof(CinematicMotionKnot.euler)).vector3Value = euler;
            knot.FindPropertyRelative(nameof(CinematicMotionKnot.inTangent)).vector3Value = inTangent;
            knot.FindPropertyRelative(nameof(CinematicMotionKnot.outTangent)).vector3Value = outTangent;
        }

        private static CinematicMotionKnot[] CopyKnots(CinematicMotionKnot[] source)
        {
            if (source == null || source.Length == 0)
            {
                return new CinematicMotionKnot[0];
            }

            var copy = new CinematicMotionKnot[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }

        private static void SmoothTangents(CinematicMotionKnot[] knots)
        {
            if (knots == null || knots.Length < 2)
            {
                return;
            }

            for (var i = 0; i < knots.Length; i++)
            {
                var previous = i > 0 ? knots[i - 1].position : knots[i].position;
                var current = knots[i].position;
                var next = i < knots.Length - 1 ? knots[i + 1].position : knots[i].position;

                var knot = knots[i];
                knot.inTangent = i > 0 ? (previous - next) / 6f : Vector3.zero;
                knot.outTangent = i < knots.Length - 1 ? (next - previous) / 6f : Vector3.zero;

                if (i == 0)
                {
                    knot.outTangent = (next - current) / 3f;
                }
                else if (i == knots.Length - 1)
                {
                    knot.inTangent = (previous - current) / 3f;
                }

                knots[i] = knot;
            }
        }

        private static void MarkTimelineDirty()
        {
            TimelineEditor.Refresh(RefreshReason.ContentsModified | RefreshReason.SceneNeedsUpdate);
            SceneView.RepaintAll();
        }
    }

    [InitializeOnLoad]
    internal static class CinematicMotionClipSceneEditor
    {
        private static readonly Color PathColor = new(0.15f, 0.65f, 1f, 1f);
        private static readonly Color TangentColor = new(1f, 0.78f, 0.25f, 1f);
        private static readonly Color KnotColor = new(0.1f, 0.8f, 1f, 1f);

        static CinematicMotionClipSceneEditor()
        {
            SceneView.duringSceneGui += DrawSelectedMotionClips;
        }

        private static void DrawSelectedMotionClips(SceneView sceneView)
        {
            var clips = GetSelectedMotionClips();
            if (clips.Count == 0)
            {
                return;
            }

            foreach (var clip in clips)
            {
                DrawClip(clip);
            }
        }

        private static List<CinematicMotionClip> GetSelectedMotionClips()
        {
            var clips = new List<CinematicMotionClip>();
            var seen = new HashSet<CinematicMotionClip>();

            if (Selection.activeObject is CinematicMotionClip activeClip)
            {
                clips.Add(activeClip);
                seen.Add(activeClip);
            }

            TimelineClip[] selectedClips = null;
            try
            {
                selectedClips = TimelineEditor.selectedClips;
            }
            catch
            {
                return clips;
            }

            if (selectedClips == null)
            {
                return clips;
            }

            foreach (var timelineClip in selectedClips)
            {
                if (timelineClip?.asset is not CinematicMotionClip motionClip || !seen.Add(motionClip))
                {
                    continue;
                }

                clips.Add(motionClip);
            }

            return clips;
        }

        private static void DrawClip(CinematicMotionClip clip)
        {
            if (clip == null || clip.knots == null || clip.knots.Length == 0)
            {
                return;
            }

            DrawPath(clip.knots);
            DrawHandles(clip);
        }

        private static void DrawPath(IReadOnlyList<CinematicMotionKnot> knots)
        {
            Handles.color = PathColor;
            for (var i = 0; i < knots.Count - 1; i++)
            {
                var current = knots[i];
                var next = knots[i + 1];
                Handles.DrawBezier(
                    current.position,
                    next.position,
                    current.position + current.outTangent,
                    next.position + next.inTangent,
                    PathColor,
                    null,
                    3f);
            }
        }

        private static void DrawHandles(CinematicMotionClip clip)
        {
            for (var i = 0; i < clip.knots.Length; i++)
            {
                DrawKnotHandle(clip, i);
            }
        }

        private static void DrawKnotHandle(CinematicMotionClip clip, int index)
        {
            var knot = clip.knots[index];
            var handleRotation = Quaternion.Euler(knot.euler);
            var size = HandleUtility.GetHandleSize(knot.position);

            Handles.color = KnotColor;
            Handles.Label(knot.position + Vector3.up * size * 0.2f, $"Motion {index}");

            EditorGUI.BeginChangeCheck();
            var position = Handles.PositionHandle(knot.position, handleRotation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(clip, "Move Motion Knot");
                knot.position = position;
                clip.knots[index] = knot;
                MarkChanged(clip);
            }

            EditorGUI.BeginChangeCheck();
            var rotation = Handles.RotationHandle(handleRotation, knot.position);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(clip, "Rotate Motion Knot");
                knot.euler = rotation.eulerAngles;
                clip.knots[index] = knot;
                MarkChanged(clip);
            }

            if (index > 0)
            {
                DrawTangentHandle(clip, index, true);
            }

            if (index < clip.knots.Length - 1)
            {
                DrawTangentHandle(clip, index, false);
            }
        }

        private static void DrawTangentHandle(CinematicMotionClip clip, int index, bool inTangent)
        {
            var knot = clip.knots[index];
            var tangent = inTangent ? knot.inTangent : knot.outTangent;
            var endpoint = knot.position + tangent;
            var size = HandleUtility.GetHandleSize(endpoint) * 0.08f;

            Handles.color = TangentColor;
            Handles.DrawLine(knot.position, endpoint, 2f);

            EditorGUI.BeginChangeCheck();
            var movedEndpoint = Handles.FreeMoveHandle(endpoint, size, Vector3.zero, Handles.SphereHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(clip, inTangent ? "Move In Tangent" : "Move Out Tangent");
                if (inTangent)
                {
                    knot.inTangent = movedEndpoint - knot.position;
                }
                else
                {
                    knot.outTangent = movedEndpoint - knot.position;
                }

                clip.knots[index] = knot;
                MarkChanged(clip);
            }
        }

        private static void MarkChanged(CinematicMotionClip clip)
        {
            EditorUtility.SetDirty(clip);
            TimelineEditor.Refresh(RefreshReason.ContentsModified | RefreshReason.SceneNeedsUpdate);
            SceneView.RepaintAll();
        }
    }
}
