using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IronByte.Tools.MaterialConversion.Editor
{
    public sealed class MaterialConversionWindow : EditorWindow
    {
        private const int MaxHistoryEntries = 20;

        private enum QueueFilter
        {
            All,
            Ready,
            Review,
            Completed,
            Skipped
        }

        private enum QueueExecutionState
        {
            None,
            Completed,
            Skipped
        }

        private static readonly List<MaterialConversionHistoryEntry> UndoHistory = new List<MaterialConversionHistoryEntry>();
        private static readonly List<MaterialConversionHistoryEntry> RedoHistory = new List<MaterialConversionHistoryEntry>();

        private readonly List<Material> queuedMaterials = new List<Material>();
        private readonly List<MaterialConversionResult> previewResults = new List<MaterialConversionResult>();
        private readonly HashSet<string> expandedRows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> selectedRows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, QueueExecutionState> executionStates = new Dictionary<string, QueueExecutionState>(StringComparer.OrdinalIgnoreCase);

        private Vector2 queueScroll;
        private MaterialConversionTarget target = MaterialConversionTarget.URPLit;
        private MaterialConversionMode mode = MaterialConversionMode.Copy;
        private string copySuffix = "_Converted";
        private bool previewDirty = true;
        private bool removeConvertedFromQueue;
        private bool remapReferencesAfterCopy;
        private bool allowGeneratedHelperTextures = true;
        private QueueFilter activeFilter = QueueFilter.All;
        private int lastSelectedIndex = -1;

        private GUIStyle cardStyle;
        private GUIStyle rowStyle;
        private GUIStyle selectedRowStyle;
        private GUIStyle compactWrapStyle;
        private GUIStyle detailWrapStyle;
        private GUIStyle detailHeadingStyle;
        private GUIStyle chipStyle;
        private GUIStyle filterButtonStyle;
        private GUIStyle filterButtonLeftStyle;
        private GUIStyle filterButtonMidStyle;
        private GUIStyle filterButtonRightStyle;
        private GUIStyle filterStripStyle;
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle emptyStateStyle;
        private GUIStyle emptyTitleStyle;
        private GUIStyle iconButtonStyle;
        private GUIStyle toolbarButtonStyle;
        private GUIStyle metaLabelStyle;
        private GUIStyle metaValueStyle;
        private GUIStyle parityValueStyle;

        [MenuItem(MaterialConversionToolInfo.MenuPath)]
        public static void ShowWindow()
        {
            GetWindow<MaterialConversionWindow>(MaterialConversionToolInfo.WindowTitle);
        }

        public static void OpenWithMaterials(IEnumerable<Material> materials)
        {
            MaterialConversionWindow window = GetWindow<MaterialConversionWindow>(MaterialConversionToolInfo.WindowTitle);
            window.AddMaterials(materials);
            window.Focus();
        }

        private void OnEnable()
        {
            minSize = new Vector2(760f, 430f);
            RebuildExecutionStatesFromUndoHistory();
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (previewDirty)
            {
                RebuildPreview();
            }

            HandleQueueKeyboard();

            EditorGUILayout.Space(8f);
            DrawHeaderCard();
            EditorGUILayout.Space(8f);
            DrawQueueArea();
        }

        private void EnsureStyles()
        {
            if (cardStyle != null &&
                rowStyle != null &&
                selectedRowStyle != null &&
                compactWrapStyle != null &&
                detailWrapStyle != null &&
                detailHeadingStyle != null &&
                chipStyle != null &&
                filterButtonStyle != null &&
                filterButtonLeftStyle != null &&
                filterButtonMidStyle != null &&
                filterButtonRightStyle != null &&
                filterStripStyle != null &&
                titleStyle != null &&
                subtitleStyle != null &&
                emptyStateStyle != null &&
                emptyTitleStyle != null &&
                iconButtonStyle != null &&
                toolbarButtonStyle != null &&
                metaLabelStyle != null &&
                metaValueStyle != null &&
                parityValueStyle != null)
            {
                return;
            }

            cardStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(12, 12, 10, 10)
            };

            rowStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 0, 0, 6)
            };

            selectedRowStyle = new GUIStyle(rowStyle)
            {
                normal =
                {
                    background = MakeTexture(new Color(0.19f, 0.28f, 0.39f, 1f))
                }
            };

            compactWrapStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true
            };

            detailWrapStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                fontSize = 11
            };

            detailHeadingStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                fontSize = 11
            };

            subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true
            };

            emptyStateStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            emptyTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13
            };

            chipStyle = new GUIStyle(EditorStyles.miniButtonMid)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fixedHeight = 17f,
                padding = new RectOffset(7, 7, 1, 1),
                margin = new RectOffset(0, 4, 0, 0)
            };

            filterButtonStyle = new GUIStyle(EditorStyles.miniButtonMid)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fixedHeight = 22f,
                padding = new RectOffset(10, 10, 2, 2),
                margin = new RectOffset(0, 0, 0, 0)
            };

            filterButtonLeftStyle = new GUIStyle(EditorStyles.miniButtonLeft)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fixedHeight = filterButtonStyle.fixedHeight,
                padding = filterButtonStyle.padding,
                margin = new RectOffset(0, 0, 0, 0)
            };

            filterButtonMidStyle = new GUIStyle(EditorStyles.miniButtonMid)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fixedHeight = filterButtonStyle.fixedHeight,
                padding = filterButtonStyle.padding,
                margin = new RectOffset(0, 0, 0, 0)
            };

            filterButtonRightStyle = new GUIStyle(EditorStyles.miniButtonRight)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fixedHeight = filterButtonStyle.fixedHeight,
                padding = filterButtonStyle.padding,
                margin = new RectOffset(0, 0, 0, 0)
            };

            filterStripStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(6, 6, 5, 5),
                margin = new RectOffset(0, 0, 0, 0)
            };

            titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13
            };

            iconButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fixedWidth = 22f,
                fixedHeight = 20f,
                padding = new RectOffset(0, 0, 0, 0)
            };

            toolbarButtonStyle = new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fixedHeight = 20f,
                padding = new RectOffset(10, 10, 0, 0)
            };

            metaLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                fontSize = 8,
                clipping = TextClipping.Clip
            };

            metaValueStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 10,
                clipping = TextClipping.Clip
            };

            parityValueStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 10,
                clipping = TextClipping.Clip,
                alignment = TextAnchor.MiddleLeft
            };
        }

        private void DrawHeaderCard()
        {
            int averageStrength = GetAverageStrength();
            bool stackedLayout = position.width < 860f;

            using (new EditorGUILayout.VerticalScope(cardStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        GUILayout.Label(MaterialConversionToolInfo.HeaderTitle, titleStyle);
                        GUILayout.Label("Convert queued materials between stock built-in, URP, and HDRP shaders. Queue filters, preview, and review live below.", subtitleStyle);
                    }

                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(UndoHistory.Count == 0))
                    {
                        if (GUILayout.Button("Undo", GUILayout.Width(58f)))
                        {
                            UndoLastTransaction();
                        }
                    }

                    using (new EditorGUI.DisabledScope(RedoHistory.Count == 0))
                    {
                        if (GUILayout.Button("Redo", GUILayout.Width(58f)))
                        {
                            RedoLastTransaction();
                        }
                    }
                }

                GUILayout.Space(8f);

                EditorGUI.BeginChangeCheck();

                if (stackedLayout)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawFieldLabel("Target", 42f);
                        target = (MaterialConversionTarget)EditorGUILayout.EnumPopup(target);
                    }

                    GUILayout.Space(4f);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawFieldLabel("Mode", 38f);
                        mode = (MaterialConversionMode)GUILayout.Toolbar((int)mode, new[] { "Copy", "Replace" }, GUILayout.Width(140f));

                        if (mode == MaterialConversionMode.Copy)
                        {
                            GUILayout.Space(10f);
                            DrawFieldLabel("Suffix", 40f);
                            copySuffix = EditorGUILayout.TextField(copySuffix);
                        }
                    }

                    GUILayout.Space(4f);

                    using (new EditorGUI.DisabledScope(!previewResults.Any(result => result.Success)))
                    {
                        string convertLabel = GetConvertButtonLabel();
                        if (GUILayout.Button(convertLabel, GUILayout.Height(24f)))
                        {
                            ExecuteConversion();
                        }
                    }
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawFieldLabel("Target", 42f);
                        target = (MaterialConversionTarget)EditorGUILayout.EnumPopup(target, GUILayout.MinWidth(240f), GUILayout.MaxWidth(280f));

                        GUILayout.Space(10f);
                        DrawFieldLabel("Mode", 38f);
                        mode = (MaterialConversionMode)GUILayout.Toolbar((int)mode, new[] { "Copy", "Replace" }, GUILayout.Width(140f));

                        if (mode == MaterialConversionMode.Copy)
                        {
                            GUILayout.Space(10f);
                            DrawFieldLabel("Suffix", 40f);
                            copySuffix = EditorGUILayout.TextField(copySuffix, GUILayout.Width(120f));
                        }

                        GUILayout.FlexibleSpace();

                        using (new EditorGUI.DisabledScope(!previewResults.Any(result => result.Success)))
                        {
                            string convertLabel = GetConvertButtonLabel();
                            if (GUILayout.Button(convertLabel, GUILayout.Width(140f), GUILayout.Height(24f)))
                            {
                                ExecuteConversion();
                            }
                        }
                    }
                }

                if (EditorGUI.EndChangeCheck())
                {
                    previewDirty = true;
                }

                GUILayout.Space(8f);
                GUILayout.Label("Estimated parity", EditorStyles.miniBoldLabel);
                Rect meterRect = GUILayoutUtility.GetRect(220f, 18f, GUILayout.ExpandWidth(true));
                string meterLabel = previewResults.Any(result => result.Success)
                    ? $"{averageStrength}/100 {MaterialConversionPresentation.GetStrengthLabel(averageStrength)}"
                    : "No valid preview";
                EditorGUI.ProgressBar(meterRect, averageStrength / 100f, meterLabel);

                GUILayout.Space(3f);
                GUILayout.Label("Parity is estimated from predicted data loss and any helper textures needed to preserve packed channels.", compactWrapStyle);
            }
        }

        private void DrawQueueArea()
        {
            float listHeight = Mathf.Max(270f, position.height - 190f);
            IReadOnlyList<int> visibleIndices = GetVisibleIndices();

            using (new EditorGUILayout.VerticalScope(cardStyle))
            {
                DrawQueueFilterBar();
                GUILayout.Space(4f);
                DrawQueueToolbar();
                GUILayout.Space(6f);

                queueScroll = EditorGUILayout.BeginScrollView(queueScroll, GUILayout.MinHeight(listHeight), GUILayout.MaxHeight(listHeight));
                if (queuedMaterials.Count == 0)
                {
                    DrawEmptyQueueState();
                }
                else if (visibleIndices.Count == 0)
                {
                    using (new EditorGUILayout.VerticalScope(rowStyle))
                    {
                        GUILayout.Space(12f);
                        GUILayout.Label("No queued materials match the current filter.", emptyStateStyle);
                        GUILayout.Space(12f);
                    }
                }
                else
                {
                    foreach (int index in visibleIndices)
                    {
                        DrawQueueRow(index, previewResults[index]);
                    }
                }

                EditorGUILayout.EndScrollView();
                HandleQueueDragAndDrop(GUILayoutUtility.GetLastRect());
            }
        }

        private void DrawQueueFilterBar()
        {
            using (new EditorGUILayout.VerticalScope(filterStripStyle))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawFilterButton(QueueFilter.All, "All", "Show every queued material.", GetFilterCount(QueueFilter.All), new Color(0.34f, 0.34f, 0.34f), 0, 5);
                    DrawFilterButton(QueueFilter.Ready, "Ready", "Show clean previews with no major review items.", GetFilterCount(QueueFilter.Ready), new Color(0.18f, 0.55f, 0.28f), 1, 5);
                    DrawFilterButton(QueueFilter.Review, "Review", "Show previews that need manual review.", GetFilterCount(QueueFilter.Review), new Color(0.74f, 0.52f, 0.17f), 2, 5);
                    DrawFilterButton(QueueFilter.Completed, "Completed", "Show materials converted successfully in this session state.", GetFilterCount(QueueFilter.Completed), new Color(0.24f, 0.47f, 0.74f), 3, 5);
                    DrawFilterButton(QueueFilter.Skipped, "Skipped", "Show materials already handled or currently unsupported.", GetFilterCount(QueueFilter.Skipped), new Color(0.42f, 0.42f, 0.42f), 4, 5);
                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawQueueToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Add Selection", "Add the current material selection to the queue."), GUILayout.Width(92f)))
                {
                    AddMaterials(Selection.objects.OfType<Material>());
                }

                using (new EditorGUI.DisabledScope(selectedRows.Count == 0))
                {
                    if (GUILayout.Button(new GUIContent("Remove", "Remove the selected queued materials."), GUILayout.Width(64f)))
                    {
                        RemoveSelectedRows();
                    }
                }

                if (GUILayout.Button(new GUIContent("Clear", "Clear the full queue."), GUILayout.Width(46f)))
                {
                    ClearQueue();
                }

                GUILayout.FlexibleSpace();

                Rect menuRect = GUILayoutUtility.GetRect(new GUIContent("Options"), toolbarButtonStyle, GUILayout.Width(68f));
                if (GUI.Button(menuRect, new GUIContent("Options", "Advanced options"), toolbarButtonStyle))
                {
                    ShowAdvancedMenu(menuRect);
                }
            }
        }

        private void DrawEmptyQueueState()
        {
            using (new EditorGUILayout.VerticalScope(rowStyle))
            {
                GUILayout.Space(18f);
                GUILayout.Label("Drop material assets here", emptyTitleStyle, GUILayout.ExpandWidth(true));
                GUILayout.Space(4f);
                GUILayout.Label("Or use Add to queue the current material selection.", emptyStateStyle);
                GUILayout.Space(18f);
            }
        }

        private void DrawFilterButton(QueueFilter filter, string label, string tooltip, int count, Color activeColor, int index, int total)
        {
            bool isActive = activeFilter == filter;
            Color previousBackgroundColor = GUI.backgroundColor;
            Color previousContentColor = GUI.contentColor;
            Color inactiveColor = new Color(0.20f, 0.21f, 0.23f);
            GUI.backgroundColor = isActive ? Color.Lerp(activeColor, Color.white, 0.06f) : inactiveColor;
            GUI.contentColor = isActive ? Color.white : Color.Lerp(activeColor, new Color(0.88f, 0.9f, 0.92f), 0.45f);
            GUIStyle style = GetFilterButtonStyle(index, total);
            bool pressed = GUILayout.Toggle(isActive, new GUIContent($"{label} {count}", tooltip), style, GUILayout.ExpandWidth(false));
            Rect tabRect = GUILayoutUtility.GetLastRect();
            GUI.backgroundColor = previousBackgroundColor;
            GUI.contentColor = previousContentColor;

            if (Event.current.type == EventType.Repaint)
            {
                Color marker = isActive ? activeColor : Color.Lerp(activeColor, new Color(0.34f, 0.36f, 0.39f), 0.78f);
                EditorGUI.DrawRect(new Rect(tabRect.x + 5f, tabRect.yMax - 2f, tabRect.width - 10f, isActive ? 2f : 1f), marker);
            }

            if (pressed && !isActive)
            {
                activeFilter = filter;
                Repaint();
            }
        }

        private GUIStyle GetFilterButtonStyle(int index, int total)
        {
            if (index <= 0)
            {
                return filterButtonLeftStyle;
            }

            if (index >= total - 1)
            {
                return filterButtonRightStyle;
            }

            return filterButtonMidStyle;
        }

        private void ShowAdvancedMenu(Rect buttonRect)
        {
            bool canRemapReferences = MaterialReferenceRemapper.CanRemapProjectReferences(out string remapReason);

            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Allow helper textures"), allowGeneratedHelperTextures, () =>
            {
                allowGeneratedHelperTextures = !allowGeneratedHelperTextures;
                previewDirty = true;
                Repaint();
            });

            menu.AddItem(new GUIContent("Remove done"), removeConvertedFromQueue, () =>
            {
                removeConvertedFromQueue = !removeConvertedFromQueue;
                Repaint();
            });

            if (mode == MaterialConversionMode.Copy)
            {
                if (canRemapReferences)
                {
                    menu.AddItem(new GUIContent("Copy and remap usages"), remapReferencesAfterCopy, () =>
                    {
                        remapReferencesAfterCopy = !remapReferencesAfterCopy;
                        Repaint();
                    });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Copy and remap usages"));
                    if (!string.IsNullOrWhiteSpace(remapReason))
                    {
                        menu.AddDisabledItem(new GUIContent(remapReason));
                    }
                }
            }

            menu.DropDown(buttonRect);
        }

        private void DrawQueueRow(int index, MaterialConversionResult result)
        {
            string key = GetRowKey(result.SourceMaterial, index);
            bool expanded = expandedRows.Contains(key);
            bool selected = selectedRows.Contains(key);
            int helperCount = result.GeneratedAssets.Length > 0 ? result.GeneratedAssets.Length : result.ExpectedGeneratedAssetPaths.Length;
            bool compactMetaLayout = position.width < 980f;

            Rect rowRect = EditorGUILayout.BeginVertical(selected ? selectedRowStyle : rowStyle);
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect foldoutRect = GUILayoutUtility.GetRect(14f, EditorGUIUtility.singleLineHeight, GUILayout.Width(14f));
                bool newExpanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, true);
                if (newExpanded != expanded)
                {
                    if (newExpanded)
                    {
                        expandedRows.Add(key);
                    }
                    else
                    {
                        expandedRows.Remove(key);
                    }
                }

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(result.SourceMaterial, typeof(Material), false);
                }

                if (GUILayout.Button(new GUIContent("x", "Remove this material from the queue."), iconButtonStyle))
                {
                    queuedMaterials.RemoveAt(index);
                    expandedRows.Remove(key);
                    selectedRows.Remove(key);
                    previewDirty = true;
                    GUIUtility.ExitGUI();
                }
            }

            GUILayout.Space(3f);
            DrawRowMeta(result, helperCount, compactMetaLayout);

            if (expanded)
            {
                GUILayout.Space(6f);
                GUILayout.Label(result.Success ? result.StrengthSummary : result.Summary, detailWrapStyle);

                string[] helperFiles = result.GeneratedAssets.Length > 0
                    ? result.GeneratedAssets.Select(System.IO.Path.GetFileName).ToArray()
                    : result.ExpectedGeneratedAssetPaths.Select(System.IO.Path.GetFileName).ToArray();

                if (result.Losses.Length > 0)
                {
                    DrawDetailBlock("Will lose", result.Losses);
                }

                if (helperFiles.Length > 0)
                {
                    DrawDetailBlock("Helper assets", helperFiles);
                }

                if (result.Notes.Length > 0)
                {
                    DrawDetailBlock("Notes", result.Notes);
                }

                if (result.Success && result.Losses.Length == 0 && helperFiles.Length == 0 && result.Notes.Length == 0)
                {
                    GUILayout.Space(4f);
                    GUILayout.Label("No major losses or helper assets are expected.", detailWrapStyle);
                }
            }

            EditorGUILayout.EndVertical();
            HandleRowMouse(rowRect, index, key);
        }

        private void DrawRowMeta(MaterialConversionResult result, int helperCount, bool compactMetaLayout)
        {
            if (compactMetaLayout)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawShaderMetaBlock(GetSourceTagLabel(result), 194f);
                    GUILayout.FlexibleSpace();
                    DrawPrimaryStatusArea(result);
                }

                if (result.Success)
                {
                    GUILayout.Space(2f);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawParityMetaBlock(result.StrengthScore, result.StrengthLabel, 154f);
                        GUILayout.FlexibleSpace();
                        DrawSecondaryStatusArea(result, helperCount);
                    }
                }

                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawShaderMetaBlock(GetSourceTagLabel(result), 206f);
                if (result.Success)
                {
                    GUILayout.Space(6f);
                    DrawParityMetaBlock(result.StrengthScore, result.StrengthLabel, 154f);
                }

                GUILayout.FlexibleSpace();
                DrawStatusCluster(result, helperCount);
            }
        }

        private void DrawPrimaryStatusArea(MaterialConversionResult result)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Height(28f)))
            {
                GUILayout.FlexibleSpace();
                DrawPrimaryStatusChip(result);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawSecondaryStatusArea(MaterialConversionResult result, int helperCount)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Height(28f)))
            {
                GUILayout.FlexibleSpace();
                DrawSecondaryStatusChips(result, helperCount);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawStatusCluster(MaterialConversionResult result, int helperCount)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Height(28f)))
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawPrimaryStatusChip(result);
                    if (result.Success)
                    {
                        DrawSecondaryStatusChips(result, helperCount);
                    }
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawPrimaryStatusChip(MaterialConversionResult result)
        {
            if (result.Success)
            {
                DrawChip(MaterialConversionPresentation.GetConfidenceDisplayName(result.Confidence), GetConfidenceColor(result.Confidence));
                return;
            }

            DrawChip(result.Skipped ? "Skipped" : "Unsupported", result.Skipped ? new Color(0.42f, 0.42f, 0.42f) : new Color(0.55f, 0.22f, 0.22f));
        }

        private void DrawSecondaryStatusChips(MaterialConversionResult result, int helperCount)
        {
            if (helperCount > 0)
            {
                DrawChip($"{helperCount} helper{(helperCount == 1 ? string.Empty : "s")}", new Color(0.24f, 0.47f, 0.74f));
            }

            if (result.Notes.Length > 0)
            {
                DrawChip($"{result.Notes.Length} note{(result.Notes.Length == 1 ? string.Empty : "s")}", new Color(0.50f, 0.50f, 0.50f));
            }
        }

        private void ExecuteConversion()
        {
            Dictionary<string, AssetFileSnapshot> beforeSnapshots = CaptureBeforeSnapshots();
            List<MaterialConversionResult> results = new List<MaterialConversionResult>();
            List<Material> processedMaterials = queuedMaterials.Where(material => material != null).ToList();

            foreach (Material material in processedMaterials)
            {
                MaterialConversionRequest request = new MaterialConversionRequest(material, target, mode, copySuffix)
                {
                    AllowGeneratedHelperTextures = allowGeneratedHelperTextures
                };

                results.Add(MaterialConversionService.Convert(request));
            }

            MaterialReferenceRemapResult remapResult = new MaterialReferenceRemapResult();
            if (mode == MaterialConversionMode.Copy && remapReferencesAfterCopy)
            {
                Dictionary<Material, Material> materialMap = results
                    .Where(result => result.Success && result.SourceMaterial != null && result.ResultMaterial != null && result.ResultPath != result.SourcePath)
                    .ToDictionary(result => result.SourceMaterial, result => result.ResultMaterial);

                remapResult = MaterialReferenceRemapper.RemapProjectReferences(materialMap);
                if (remapResult.Notes.Length > 0 && results.Count > 0)
                {
                    results[0].Notes = results[0].Notes.Concat(remapResult.Notes).Distinct().ToArray();
                    results[0].RemappedReferenceCount = remapResult.UpdatedAssetCount;
                }
            }

            MaterialConversionHistoryEntry historyEntry = MaterialConversionHistoryUtility.CreateEntry(
                MaterialConversionHistoryUtility.BuildEntryLabel(mode, target, results.Count(result => result.Success)),
                results,
                remapResult,
                beforeSnapshots);
            PushUndoHistory(historyEntry);

            if (removeConvertedFromQueue)
            {
                for (int i = processedMaterials.Count - 1; i >= 0; i--)
                {
                    MaterialConversionResult result = results[i];
                    if (result.Success || result.Skipped)
                    {
                        queuedMaterials.Remove(processedMaterials[i]);
                    }
                }

                expandedRows.Clear();
                selectedRows.Clear();
                lastSelectedIndex = -1;
            }

            previewDirty = true;
            RebuildPreview();
            EditorUtility.DisplayDialog(MaterialConversionToolInfo.DialogTitle, MaterialConversionPresentation.BuildBatchSummary(results, remapResult.UpdatedAssetCount), "OK");
        }

        private void HandleQueueDragAndDrop(Rect rect)
        {
            Event current = Event.current;
            if (!rect.Contains(current.mousePosition))
            {
                return;
            }

            if (current.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                current.Use();
                return;
            }

            if (current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                AddMaterials(DragAndDrop.objectReferences.OfType<Material>());
                current.Use();
            }
        }

        private void DrawShaderMetaBlock(string shaderLabel, float width)
        {
            DrawMetaBlock("Shader", shaderLabel, new Color(0.29f, 0.43f, 0.58f), width, false, 0f);
        }

        private void DrawParityMetaBlock(int strengthScore, string strengthLabel, float width)
        {
            string parityText = $"{strengthScore}/100 {strengthLabel}";
            Color accent = MaterialConversionPresentation.GetStrengthColor(strengthScore);
            DrawMetaBlock("Parity", parityText, accent, width, true, strengthScore / 100f);
        }

        private void DrawMetaBlock(string label, string value, Color accent, float width, bool showProgressBar, float progress01)
        {
            Rect rect = GUILayoutUtility.GetRect(width, 28f, GUILayout.Width(width), GUILayout.ExpandWidth(false));
            Rect blockRect = new Rect(rect.x, rect.y, rect.width, rect.height);
            Rect fillRect = new Rect(blockRect.x + 1f, blockRect.y + 1f, blockRect.width - 2f, blockRect.height - 2f);

            EditorGUI.DrawRect(fillRect, new Color(0.17f, 0.18f, 0.20f, 1f));
            DrawRectOutline(blockRect, Color.Lerp(accent, new Color(0.34f, 0.36f, 0.39f), 0.62f));
            EditorGUI.DrawRect(new Rect(blockRect.x + 1f, blockRect.y + 1f, 3f, blockRect.height - 2f), accent);

            Color previousContentColor = GUI.contentColor;

            GUI.contentColor = new Color(0.70f, 0.74f, 0.79f);
            GUI.Label(new Rect(blockRect.x + 10f, blockRect.y + 3f, 34f, 10f), label, metaLabelStyle);

            GUI.contentColor = Color.white;
            GUIStyle valueStyle = showProgressBar ? parityValueStyle : metaValueStyle;
            GUI.Label(new Rect(blockRect.x + 46f, blockRect.y + 2f, blockRect.width - 54f, 14f), value, valueStyle);

            if (showProgressBar)
            {
                Rect trackRect = new Rect(blockRect.x + 46f, blockRect.yMax - 6f, blockRect.width - 54f, 2f);
                EditorGUI.DrawRect(trackRect, new Color(0.23f, 0.24f, 0.27f, 1f));
                EditorGUI.DrawRect(new Rect(trackRect.x, trackRect.y, trackRect.width * Mathf.Clamp01(progress01), trackRect.height), accent);
            }

            GUI.contentColor = previousContentColor;
        }

        private void DrawChip(string label, Color color)
        {
            Color previousColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Label(label, chipStyle, GUILayout.ExpandWidth(false));
            GUI.backgroundColor = previousColor;
        }

        private static void DrawRectOutline(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        private void DrawDetailBlock(string label, IEnumerable<string> items)
        {
            string body = MaterialConversionPresentation.BuildMultilineList(items);
            if (string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            GUILayout.Space(6f);
            GUILayout.Label(label, detailHeadingStyle);
            GUILayout.Label(body, detailWrapStyle);
        }

        private static void DrawFieldLabel(string label, float width)
        {
            GUILayout.Label(label, EditorStyles.miniBoldLabel, GUILayout.Width(width));
        }

        private void ClearQueue()
        {
            queuedMaterials.Clear();
            previewResults.Clear();
            expandedRows.Clear();
            selectedRows.Clear();
            lastSelectedIndex = -1;
            previewDirty = true;
        }

        private Dictionary<string, AssetFileSnapshot> CaptureBeforeSnapshots()
        {
            HashSet<string> trackedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (MaterialConversionResult result in previewResults.Where(result => result.Success))
            {
                if (!string.IsNullOrWhiteSpace(result.SourcePath))
                {
                    trackedPaths.Add(result.SourcePath);
                }

                if (mode == MaterialConversionMode.Copy && !string.IsNullOrWhiteSpace(result.ResultPath) && result.ResultPath != result.SourcePath)
                {
                    trackedPaths.Add(result.ResultPath);
                }

                foreach (string helperPath in result.ExpectedGeneratedAssetPaths)
                {
                    trackedPaths.Add(helperPath);
                }
            }

            return MaterialConversionHistoryUtility.CaptureSnapshots(trackedPaths);
        }

        private void PushUndoHistory(MaterialConversionHistoryEntry entry)
        {
            if (entry == null || !entry.HasChanges)
            {
                return;
            }

            UndoHistory.Add(entry);
            if (UndoHistory.Count > MaxHistoryEntries)
            {
                UndoHistory.RemoveAt(0);
            }

            RedoHistory.Clear();
            RebuildExecutionStatesFromUndoHistory();
        }

        private void UndoLastTransaction()
        {
            if (UndoHistory.Count == 0)
            {
                return;
            }

            MaterialConversionHistoryEntry entry = UndoHistory[UndoHistory.Count - 1];
            UndoHistory.RemoveAt(UndoHistory.Count - 1);
            entry.Undo();
            RedoHistory.Add(entry);
            if (RedoHistory.Count > MaxHistoryEntries)
            {
                RedoHistory.RemoveAt(0);
            }

            RebuildExecutionStatesFromUndoHistory();
            previewDirty = true;
            Repaint();
        }

        private void RedoLastTransaction()
        {
            if (RedoHistory.Count == 0)
            {
                return;
            }

            MaterialConversionHistoryEntry entry = RedoHistory[RedoHistory.Count - 1];
            RedoHistory.RemoveAt(RedoHistory.Count - 1);
            entry.Redo();
            UndoHistory.Add(entry);
            if (UndoHistory.Count > MaxHistoryEntries)
            {
                UndoHistory.RemoveAt(0);
            }

            RebuildExecutionStatesFromUndoHistory();
            previewDirty = true;
            Repaint();
        }

        private IReadOnlyList<int> GetVisibleIndices()
        {
            List<int> indices = new List<int>();
            for (int i = 0; i < previewResults.Count; i++)
            {
                if (activeFilter == QueueFilter.All || GetQueueState(previewResults[i], i) == activeFilter)
                {
                    indices.Add(i);
                }
            }

            return indices;
        }

        private int GetFilterCount(QueueFilter filter)
        {
            if (filter == QueueFilter.All)
            {
                return previewResults.Count;
            }

            int count = 0;
            for (int i = 0; i < previewResults.Count; i++)
            {
                if (GetQueueState(previewResults[i], i) == filter)
                {
                    count++;
                }
            }

            return count;
        }

        private QueueFilter GetQueueState(MaterialConversionResult result, int index)
        {
            string key = GetRowKey(result.SourceMaterial, index);
            if (executionStates.TryGetValue(key, out QueueExecutionState executionState))
            {
                return executionState == QueueExecutionState.Completed ? QueueFilter.Completed : QueueFilter.Skipped;
            }

            if (result.Skipped || !result.Success)
            {
                return QueueFilter.Skipped;
            }

            return IsRisky(result) ? QueueFilter.Review : QueueFilter.Ready;
        }

        private static bool IsRisky(MaterialConversionResult result)
        {
            return result.Success && (result.Losses.Length > 0 || result.ExpectedGeneratedAssetPaths.Length > 0 || result.Notes.Length > 0);
        }

        private int GetAverageStrength()
        {
            List<MaterialConversionResult> successfulResults = previewResults.Where(result => result.Success).ToList();
            return successfulResults.Count == 0
                ? 0
                : Mathf.RoundToInt((float)successfulResults.Average(result => result.StrengthScore));
        }

        private static Color GetConfidenceColor(MaterialConversionConfidence confidence)
        {
            return confidence switch
            {
                MaterialConversionConfidence.Official => new Color(0.18f, 0.55f, 0.28f),
                MaterialConversionConfidence.Mapped => new Color(0.21f, 0.46f, 0.67f),
                MaterialConversionConfidence.Heuristic => new Color(0.74f, 0.52f, 0.17f),
                _ => new Color(0.55f, 0.22f, 0.22f)
            };
        }

        private string GetConvertButtonLabel()
        {
            return mode == MaterialConversionMode.Copy ? "Convert Copies" : "Convert In Place";
        }

        private static string GetSourceTagLabel(MaterialConversionResult result)
        {
            if (result.SourceMaterial != null && result.SourceMaterial.shader != null && !string.IsNullOrWhiteSpace(result.SourceMaterial.shader.name))
            {
                return GetCompactShaderLabel(result.SourceMaterial.shader.name);
            }

            return MaterialConversionPresentation.GetSourceDisplayName(result.SourceFamily);
        }

        private static string GetCompactShaderLabel(string shaderName)
        {
            if (string.IsNullOrWhiteSpace(shaderName))
            {
                return "Unknown";
            }

            return shaderName switch
            {
                MaterialConversionService.StandardShaderName => "Built-in/Standard",
                MaterialConversionService.StandardSpecularShaderName => "Built-in/Standard Spec",
                MaterialConversionService.BuiltInUnlitTextureShaderName => "Built-in/Unlit",
                _ when shaderName.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal) => "URP/" + shaderName.Substring("Universal Render Pipeline/".Length),
                _ => shaderName
            };
        }

        private static string GetResultKey(MaterialConversionResult result)
        {
            if (result.SourceMaterial != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(result.SourceMaterial);
                if (!string.IsNullOrWhiteSpace(assetPath))
                {
                    return assetPath;
                }
            }

            return result.SourcePath;
        }

        private void HandleQueueKeyboard()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown)
            {
                return;
            }

            bool commandOrControl = current.command || current.control;
            if (commandOrControl && current.keyCode == KeyCode.A)
            {
                selectedRows.Clear();
                IReadOnlyList<int> visibleIndices = GetVisibleIndices();
                foreach (int index in visibleIndices)
                {
                    selectedRows.Add(GetRowKey(previewResults[index].SourceMaterial, index));
                }

                lastSelectedIndex = visibleIndices.Count > 0 ? visibleIndices[visibleIndices.Count - 1] : -1;
                current.Use();
                Repaint();
                return;
            }

            if ((current.keyCode == KeyCode.Delete || current.keyCode == KeyCode.Backspace) && selectedRows.Count > 0)
            {
                RemoveSelectedRows();
                current.Use();
            }
        }

        private void HandleRowMouse(Rect rowRect, int index, string key)
        {
            Event current = Event.current;
            if (current.type != EventType.MouseDown || current.button != 0 || !rowRect.Contains(current.mousePosition))
            {
                return;
            }

            bool additive = current.control || current.command;
            if (current.shift && lastSelectedIndex >= 0)
            {
                selectedRows.Clear();
                int start = Mathf.Min(lastSelectedIndex, index);
                int end = Mathf.Max(lastSelectedIndex, index);
                for (int i = start; i <= end; i++)
                {
                    selectedRows.Add(GetRowKey(previewResults[i].SourceMaterial, i));
                }
            }
            else if (additive)
            {
                if (!selectedRows.Add(key))
                {
                    selectedRows.Remove(key);
                }
            }
            else
            {
                selectedRows.Clear();
                selectedRows.Add(key);
            }

            lastSelectedIndex = index;
            Repaint();
        }

        private void RemoveSelectedRows()
        {
            if (selectedRows.Count == 0)
            {
                return;
            }

            for (int i = queuedMaterials.Count - 1; i >= 0; i--)
            {
                if (selectedRows.Contains(GetRowKey(queuedMaterials[i], i)))
                {
                    queuedMaterials.RemoveAt(i);
                }
            }

            expandedRows.RemoveWhere(selectedRows.Contains);
            selectedRows.Clear();
            lastSelectedIndex = -1;
            previewDirty = true;
            Repaint();
        }

        private void RebuildExecutionStatesFromUndoHistory()
        {
            executionStates.Clear();
            foreach (MaterialConversionHistoryEntry entry in UndoHistory)
            {
                foreach (MaterialConversionResult result in entry.Results)
                {
                    string key = GetResultKey(result);
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    if (result.Success)
                    {
                        executionStates[key] = QueueExecutionState.Completed;
                    }
                    else if (result.Skipped)
                    {
                        executionStates[key] = QueueExecutionState.Skipped;
                    }
                    else
                    {
                        executionStates.Remove(key);
                    }
                }
            }
        }

        private static string GetRowKey(Material material, int index)
        {
            string assetPath = material != null ? AssetDatabase.GetAssetPath(material) : string.Empty;
            return string.IsNullOrWhiteSpace(assetPath) ? $"row-{index}" : assetPath;
        }

        private void AddMaterials(IEnumerable<Material> materials)
        {
            bool changed = false;
            foreach (Material material in materials)
            {
                if (material == null || queuedMaterials.Contains(material))
                {
                    continue;
                }

                queuedMaterials.Add(material);
                changed = true;
            }

            if (changed)
            {
                previewDirty = true;
                Repaint();
            }
        }

        private void RebuildPreview()
        {
            previewResults.Clear();
            foreach (Material material in queuedMaterials.Where(material => material != null))
            {
                MaterialConversionRequest request = new MaterialConversionRequest(material, target, mode, copySuffix)
                {
                    AllowGeneratedHelperTextures = allowGeneratedHelperTextures
                };
                previewResults.Add(MaterialConversionService.Analyze(request));
            }

            PruneRowState();
            previewDirty = false;
        }

        private void PruneRowState()
        {
            HashSet<string> validKeys = new HashSet<string>(
                previewResults.Select((result, index) => GetRowKey(result.SourceMaterial, index)),
                StringComparer.OrdinalIgnoreCase);

            expandedRows.RemoveWhere(key => !validKeys.Contains(key));
            selectedRows.RemoveWhere(key => !validKeys.Contains(key));
            if (selectedRows.Count == 0)
            {
                lastSelectedIndex = -1;
            }

            if (activeFilter != QueueFilter.All && GetFilterCount(activeFilter) == 0)
            {
                activeFilter = QueueFilter.All;
            }
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            return texture;
        }
    }
}
