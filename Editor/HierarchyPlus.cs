using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Editor.GUI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using static Editor.SavedSettings;
using static Editor.GUI.StylesContainer;

namespace Editor
{
    public class HierarchyPlus : EditorWindow
    {
        #region Constants
        private const string ProductName = "HierarchyPlus";
        private const string PackageIconFolderPath = "CustomIcons";
        private const string MissingScriptIconName = "Missing";
        private const string DefaultIconName = "Default";
        private static readonly int DragToggleHotControlID = "HierarchyPlusDragToggleId".GetHashCode();
        #endregion
        
        #region Variables
        private static readonly Dictionary<Type, GUIContent> IconCache = new();
        private static readonly Dictionary<string, GUIContent> CustomIconCache = new();
        private static readonly Texture2D[] DefaultTextures = new Texture2D[3];
        private static readonly HashSet<Object> DragToggledObjects = new();
        private static bool _dragToggleNewState;

        private static MethodInfo _getGameObjectIconMethod;
        private static GUIContent _gameObjectContent;
        private static GUIContent _missingScriptContent;
        private static GUIContent _defaultContent;
        private static string _iconFolderPath;
        private static Vector2 _scroll;

        private static bool _ranOnceThisFrame;
        private static int _lastMaxIconCount;
        private static int _maxIconCount;

        private static bool
	        _colorsFoldout = true,
	        _mainColorsFoldout,
	        _miscColorsFolddout,
	        _iconsFoldout = true,
	        _labelsFoldout,
	        _layerLabelFoldout,
	        _tagLabelFoldout,
	        _coloredItemsFoldout,
	        _hiddenIconsFoldout,
	        _rowShadingFolout;
        #endregion
        
        #region Window
        [MenuItem("DreadTools/HierarchyPlus", false, 366)]
        private static void OpenSettings()
		{
			GetWindow<HierarchyPlus>($"{ProductName} Settings");
		}

		private void OnGUI()
		{
			EditorGUI.BeginChangeCheck();
			_scroll = EditorGUILayout.BeginScrollView(_scroll);
			
			using (new GUILayout.HorizontalScope())
			{
				Settings.enabled.DrawField("HierarchyPlus Enabled");
				GUILayout.FlexibleSpace();
				if (GUILayout.Button(new GUIContent("Refresh Icons", "Use this to update the icons in the hierarchy window."), UnityEngine.GUI.skin.button, GUILayout.ExpandWidth(false))) 
					InitializeAll();
				MakeRectLinkCursor();
			}

			using (new GUILayout.VerticalScope(UnityEngine.GUI.skin.box))
			{
				_colorsFoldout = DrawFoldoutTitle("Colors", _colorsFoldout, Settings.colorsEnabled);

				if (_colorsFoldout)
				{
					using (new EditorGUI.DisabledScope(!Settings.GetColorsEnabled()))
					{
						using (new GUILayout.VerticalScope(EditorStyles.helpBox))
							if (Foldout("Main", ref _mainColorsFoldout))
							{
								using (new IndentScope())
								{
									DrawColorSetting("Active Icon Tint", Settings.iconTintColor);
									DrawColorSetting("Inactive Icon Tint", Settings.iconFadedTintColor);
									DrawColorSetting("Guide Lines", Settings.guideLinesColor, Settings.guideLinesEnabled);
									DrawColorSetting("Icon Background", Settings.iconBackgroundColor, Settings.iconBackgroundColorEnabled);
									using (new GUILayout.HorizontalScope())
									{
										var toggle = Settings.iconBackgroundOverlapOnly;
										var toggleTooltip = toggle ? new GUIContent(string.Empty, "Enabled") : new GUIContent(string.Empty, "Disabled");
										toggle.DrawToggle(toggleTooltip, null, EditorStyles.radioButton, PastelGreenColor, Color.grey, GUILayout.Width(18), GUILayout.Height(18));
										var r = GUILayoutUtility.GetLastRect();
										EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
										GUILayout.Label("Icon Background On Overlap Only", EditorStyles.label);
									}
								}
							}


						using (new GUILayout.VerticalScope(EditorStyles.helpBox))
						{
							Foldout("Row Coloring", ref _rowShadingFolout);
							if (_rowShadingFolout)
							{
								using (new IndentScope())
								{
									DrawColorSetting("Odd Color", Settings.rowOddColor, Settings.rowColoringOddEnabled);
									DrawColorSetting("Even Color", Settings.rowEvenColor, Settings.rowColoringEvenEnabled);
								}
							}
						}

						using (new GUILayout.VerticalScope(EditorStyles.helpBox))
						{
							Foldout("Misc", ref _miscColorsFolddout);
							if (_miscColorsFolddout)
								using (new IndentScope())
								{
									DrawColorSetting("Misc 1", Settings.colorOne, Settings.colorOneEnabled);
									DrawColorSetting("Misc 2", Settings.colorTwo, Settings.colorTwoEnabled);
									DrawColorSetting("Misc 3", Settings.colorThree, Settings.colorThreeEnabled);
								}
						}

					}

				}
			}

			using (new GUILayout.VerticalScope(UnityEngine.GUI.skin.box))
			{
				_iconsFoldout = DrawFoldoutTitle("Components", _iconsFoldout, Settings.iconsEnabled);

				if (_iconsFoldout)
				{
					using (new EditorGUI.DisabledScope(!Settings.GetIconsEnabled()))
					using (new GUILayout.VerticalScope())
					{
						EditorGUIUtility.labelWidth = 200;
						Settings.enableContextClick.DrawField("Enable Context Click");
						Settings.enableDragToggle.DrawField("Enable Drag-Toggle");
						Settings.showGameObjectIcon.DrawField("Show GameObject Icon");
						using (new EditorGUI.DisabledScope(!Settings.showGameObjectIcon))
							Settings.useCustomGameObjectIcon.DrawField("Use Custom GameObject Icon");
						Settings.showTransformIcon.DrawField("Show Transform Icon");
						Settings.showNonBehaviourIcons.DrawField("Show Non-Toggleable Icons");
						Settings.alwaysShowIcons.DrawField("Always Render Icons");
						Settings.linkCursorOnHover.DrawField("Link Cursor On Hover");
						Settings.guiXOffset.Value = EditorGUILayout.FloatField("Icons X Offset", Settings.guiXOffset.Value);
						using (new GUILayout.VerticalScope())
						{
							Foldout(new GUIContent("Hidden Types", "Hover over an icon to see its type name.\nWrite the type name here to hide the icon from the hierarchy view."), ref _hiddenIconsFoldout);
							if (_hiddenIconsFoldout)
							{
								using (new IndentScope())
								{
									for (var i = 0; i < Settings.hiddenIconTypes.Length; i++)
									{
										using (new EditorGUILayout.HorizontalScope())
										{
											Settings.hiddenIconTypes[i].DrawField(GUIContent.none);
											if (GUILayout.Button("X", EditorStyles.boldLabel, GUILayout.ExpandWidth(false)))
											{
												var arr = Settings.hiddenIconTypes;
												ArrayUtility.RemoveAt(ref arr, i);
												Settings.hiddenIconTypes = arr;
												Save();
												i--;
											}

											MakeRectLinkCursor();
										}
									}

									if (GUILayout.Button("+", EditorStyles.toolbarButton))
									{
										var arr = Settings.hiddenIconTypes;
										ArrayUtility.Add(ref arr, new SavedString());
										Settings.hiddenIconTypes = arr;
										Save();
									}

									MakeRectLinkCursor();
								}
							}
						}

						EditorGUIUtility.labelWidth = 0;
					}
				}
			}

			using (new GUILayout.VerticalScope(UnityEngine.GUI.skin.box))
			{
				_labelsFoldout = DrawFoldoutTitle("Labels", _labelsFoldout, Settings.labelsEnabled);

				if (_labelsFoldout)
				{
					using (new EditorGUI.DisabledScope(!Settings.GetLabelsEnabled()))
					using (new GUILayout.VerticalScope())
					{
						Settings.enableLabelContextClick.DrawField("Enable Context Click");
						using (new GUILayout.VerticalScope(EditorStyles.helpBox))
						{
							_layerLabelFoldout = Foldout("Layer Label", ref _layerLabelFoldout);
							if (_layerLabelFoldout)
							{
								using (new IndentScope())
								{
									Settings.layerLabelEnabled.DrawField("Show Layer Label");
									Settings.displayDefaultLayerLabel.DrawField("Show Default Label");
									Settings.displayLayerIndex.DrawField("Show Layer Index");
									Settings.layerLabelWidth.Value = EditorGUILayout.FloatField("Layer Label Width", Settings.layerLabelWidth.Value);
								}
							}
						}
						
						using (new GUILayout.VerticalScope(EditorStyles.helpBox))
						{
							_tagLabelFoldout = Foldout("Tag Label", ref _tagLabelFoldout);
							if (_tagLabelFoldout)
							{
								using (new IndentScope())
								{
									Settings.tagLabelEnabled.DrawField("Show Tag Label");
									Settings.displayUntaggedLabel.DrawField("Show Untagged Label");
									Settings.tagLabelWidth.Value = EditorGUILayout.FloatField("Tag Label Width", Settings.tagLabelWidth.Value);
								}
							}
						}
					}
				}
			}
			
			EditorGUILayout.EndScrollView();
			using (new GUILayout.HorizontalScope())
			{
				GUILayout.FlexibleSpace();
				w_Credit();
			}

			if (EditorGUI.EndChangeCheck())
				EditorApplication.RepaintHierarchyWindow();
		}
    
		private static void w_Credit()
        {
	        using (new ColoredScope(ColoredScope.ColoringType.Bg, Color.clear))
	        {
		        if (GUILayout.Button(new GUIContent("Made By @Dreadrith ♡", "https://dreadrith.com/links"), Styles.FaintLinkLabel))
			        Application.OpenURL("https://dreadrith.com/links");
		        w_UnderlineLastRectOnHover();
	        }
	        using (new ColoredScope(ColoredScope.ColoringType.Bg, Color.clear))
	        {
		        if (GUILayout.Button(new GUIContent("Refactored by RunaXR", "https://github.com/PhoenixAceVFX"), Styles.FaintLinkLabel))
			        Application.OpenURL("https://github.com/PhoenixAceVFX");
		        w_UnderlineLastRectOnHover();
	        }
        }

        private static void w_UnderlineLastRectOnHover(Color? color = null)
        {
	        color ??= new Color(0.3f, 0.7f, 1);
	        if (Event.current.type != EventType.Repaint) return;
	        var rect = GUILayoutUtility.GetLastRect();
	        var mp = Event.current.mousePosition;
	        if (rect.Contains(mp)) EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color.Value);
	        EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);

        }
        #endregion

        #region Hierarchy
        private static ColoredScope _colorScope;
        private static ColoredScope _colorScope2;
        private static ColoredScope _colorScope3;

        private static void OnHierarchyItemGUI(int id, Rect rect)
        {
	        if (!_ranOnceThisFrame)
	        {
		        _ranOnceThisFrame = true;
		        _lastMaxIconCount = _maxIconCount;
		        _maxIconCount = 0;
	        }

	        DisposeOfColorScopes();
	        if (Settings.GetColorsEnabled())
	        {
		        _colorScope = new ColoredScope(ColoredScope.ColoringType.General, Settings.colorOneEnabled, Settings.colorOne);
		        _colorScope2 = new ColoredScope(ColoredScope.ColoringType.Fg, Settings.colorTwoEnabled, Settings.colorTwo);
		        _colorScope3 = new ColoredScope(ColoredScope.ColoringType.Bg, Settings.colorThreeEnabled, Settings.colorThree);
	        }

	        var willDrawColors = Settings.GetColorsEnabled() && (Settings.guideLinesEnabled || Settings.GetRowColoringEnabled());
	        var willDrawIcons = Settings.GetIconsEnabled();

	        if (!willDrawColors && !willDrawIcons) return;

	        var obj = EditorUtility.InstanceIDToObject(id);
	        if (obj is not GameObject go) return;


	        if (willDrawColors)
	        {
		        var t = go.transform;
		        var p = t.parent;
		        bool hasParent = p;
		        var isLastChild = hasParent && t.GetSiblingIndex() == p.childCount - 1;
		        var hasChildren = t.childCount > 0;

		        var middleLines = new List<bool>();
		        var depth = 0;

		        if (Settings.guideLinesEnabled)
		        {
			        while (p)
			        {
				        middleLines.Insert(0, t.GetSiblingIndex() != p.childCount - 1);
				        depth++;
				        t = p;
				        p = p.parent;
			        }
		        }
		        else
			        while (p)
			        {
				        depth++;
				        t = p;
				        p = p.parent;
			        }

		        var marginWidth = hasChildren ? 14 : 2;
		        var lineWidth = 14 * depth + 34;
		        var lineRect = new Rect(rect.x - lineWidth, rect.y, lineWidth - marginWidth, rect.height);

		        if (Settings.GetRowColoringEnabled() && Event.current.type == EventType.Repaint)
		        {
			        var backgroundRect = new Rect(lineRect);
			        backgroundRect.width += rect.width + marginWidth + 12;
			        backgroundRect.x += 5;
			        //backgroundRect.y += 16;



			        if (backgroundRect.y % 32 > 15)
			        {
				        if (Settings.rowColoringOddEnabled)
					        EditorGUI.DrawRect(backgroundRect, Settings.rowOddColor);
			        }
			        else if (Settings.rowColoringEvenEnabled)
				        EditorGUI.DrawRect(backgroundRect, Settings.rowEvenColor);
			        //GUI.depth = guiDepth;
		        }

		        if (hasParent && Settings.guideLinesEnabled)
		        {
			        var extraWidth = hasChildren ? 0 : 12;

			        void Line(Vector3 start, Vector3 end) => Handles.DrawAAPolyLine(1, 2, start, end);

			        Handles.color = Settings.guideLinesColor;

			        UnityEngine.GUI.BeginClip(lineRect);
			        float basef = lineWidth - marginWidth;
			        var startingPoint = new Vector3(basef, rect.height / 2);
			        var middlePoint = new Vector3(basef - extraWidth - 8, rect.height / 2);
			        Line(startingPoint, middlePoint);

			        if (isLastChild)
			        {
				        var connectionPoint = new Vector3(basef - extraWidth - 8, 0);
				        Line(middlePoint, connectionPoint);
			        }

			        for (var i = 0; i < middleLines.Count; i++)
			        {
				        if (!middleLines[i]) continue;
				        var x = lineRect.x + 14 * i;
				        var topConnection = new Vector3(x, 0);
				        var bottomConnection = new Vector3(x, rect.height);
				        Line(topConnection, bottomConnection);
			        }

			        UnityEngine.GUI.EndClip();
		        }
	        }

	        var nameAdjust = UnityEngine.GUI.skin.label.CalcSize(new GUIContent(go.name)).x + 18;
	        var baseRect = new Rect(rect) {width = rect.width - 32 + Settings.guiXOffset - nameAdjust};
	        baseRect.x += nameAdjust;

	        if (Settings.GetIconsEnabled())
	        {
		        var availableIconArea = new Rect(baseRect);
		        var currentIconCount = 0;
		        var iconRect = availableIconArea;
		        iconRect.x = availableIconArea.xMax - 18;
		        iconRect.width = 18;

		        bool CanDrawIcon(out bool drawBackground)
		        {
			        currentIconCount++;
			        var dotsOnly = availableIconArea.width < 36;
			        var overlapping = availableIconArea.width < 18;
			        var drawIcon = Settings.alwaysShowIcons || (!dotsOnly && !overlapping);
			        drawBackground = drawIcon && Settings.colorsEnabled && Settings.iconBackgroundColorEnabled && (overlapping || !Settings.iconBackgroundOverlapOnly);

			        if (!drawIcon && dotsOnly)
			        {
				        UnityEngine.GUI.Label(iconRect, "...", EditorStyles.centeredGreyMiniLabel);
				        availableIconArea.width -= 18;
				        return false;
			        }

			        availableIconArea.width -= 18;
			        return drawIcon;

		        }

		        if (Settings.showGameObjectIcon && CanDrawIcon(out bool withBg))
			        iconRect = DrawIconToggle(iconRect, go, withBg);

		        var isFirstComponent = true;
		        foreach (var c in go.GetComponents<Component>())
		        {
			        if (c != null)
			        {
				        switch (isFirstComponent)
				        {
					        case false when !Settings.showNonBehaviourIcons && c is not Behaviour && c is not Renderer && c is not Behaviour && c is not Renderer && c is not Collider && c is not Collider:
						        continue;
					        case true:
					        {
						        isFirstComponent = false;
						        if (!Settings.showTransformIcon)
							        continue;
						        break;
					        }
				        }


				        if (!isFirstComponent && Settings.hiddenIconTypes.Any(ss => ss.Value == c.GetType().Name)) continue;
			        }

			        if (!CanDrawIcon(out var withBg2)) continue;
			        var nextRect = iconRect;
			        iconRect = DrawIconToggle(iconRect, c, withBg2);
			        var e = Event.current;
			        if (!Settings.enableContextClick || e.type != EventType.MouseDown || e.button != 1 ||
			            !nextRect.Contains(e.mousePosition)) continue;
			        var method = typeof(EditorUtility).GetMethod("DisplayObjectContextMenu", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[] {typeof(Rect), typeof(Object[]), typeof(int)}, null);
			        method.Invoke(null, new object[] {nextRect, new Object[] {c == null ? c as MonoBehaviour : c}, 0});
			        e.Use();
		        }

		        if (currentIconCount > _maxIconCount) _maxIconCount = currentIconCount;
	        }

	        if (!Settings.GetLabelsEnabled()) return;
	        {
		        GUIStyle als = "assetlabel";
		        var availableLabelsArea = baseRect;
		        availableLabelsArea.width -= 18 * (_lastMaxIconCount + 1);

		        if (Settings.layerLabelEnabled && (Settings.displayDefaultLayerLabel || go.layer != 0))
		        {
			        var maxWidth = availableLabelsArea.width;
			        float layerLabelWidth = Settings.layerLabelWidth;
			        var layerRect = UseRectEnd(ref availableLabelsArea, layerLabelWidth);
			        layerRect.width = Mathf.Clamp(layerRect.width, 0, Mathf.Max(0, maxWidth));
			        layerRect.x += layerLabelWidth - layerRect.width;
			        if (layerRect.width > 10)
			        {
				        var layerName = LayerMask.LayerToName(go.layer);
				        var label = Settings.displayLayerIndex ? $"{go.layer}: {layerName}" : layerName;
				        using (new ColoredScope(ColoredScope.ColoringType.Bg, new Color(0, 0, 0, 0.4f)))
				        using (new ColoredScope(ColoredScope.ColoringType.Fg, new Color(0.7f, 0.7f, 0.7f)))
					        UnityEngine.GUI.Label(layerRect, label, als);

				        if (Settings.enableLabelContextClick && RightClicked(layerRect))
				        {
					        var layerNames = Enumerable.Range(0, 31).Select(LayerMask.LayerToName).ToArray();

					        var layerMenu = new GenericMenu();
					        for (var i = 0; i < layerNames.Length; i++)
					        {
						        var n = layerNames[i];
						        if (string.IsNullOrEmpty(n)) continue;
						        var index = i;
						        layerMenu.AddItem(new GUIContent($"{i}: {n}"), go.layer == index, () =>
						        {
							        Undo.RecordObject(go, "Change Layer");
							        go.layer = index;
							        EditorUtility.SetDirty(go);
						        });
					        }

					        layerMenu.ShowAsContext();
				        }
			        }
		        }
		        else UseRectEnd(ref availableLabelsArea, Settings.layerLabelWidth);

		        if (!Settings.tagLabelEnabled || (!Settings.displayUntaggedLabel && go.CompareTag("Untagged"))) return;
		        {
			        UseRectEnd(ref availableLabelsArea, 18);
			        var maxWidth = availableLabelsArea.width;
			        float labelWidth = Settings.tagLabelWidth;
			        var tagRect = UseRectEnd(ref availableLabelsArea, labelWidth);
			        tagRect.width = Mathf.Clamp(tagRect.width, 0, Mathf.Max(0, maxWidth));
			        if (!(tagRect.width > 10)) return;
			        tagRect.x += labelWidth - tagRect.width;
			        var tagName = go.tag;
			        using (new ColoredScope(ColoredScope.ColoringType.Bg, new Color(0, 0, 0, 0.4f)))
			        using (new ColoredScope(ColoredScope.ColoringType.Fg, new Color(0.7f, 0.7f, 0.7f)))
				        UnityEngine.GUI.Label(tagRect, tagName, "assetlabel");

			        if (!Settings.enableLabelContextClick || !RightClicked(tagRect)) return;
			        var tagNames = UnityEditorInternal.InternalEditorUtility.tags;

			        var tagMenu = new GenericMenu();
			        foreach (var n in tagNames)
			        {
				        if (string.IsNullOrEmpty(n)) continue;
				        tagMenu.AddItem(new GUIContent(n), go.CompareTag(n), () =>
				        {
					        Undo.RecordObject(go, "Change Tag");
					        go.tag = n;
					        EditorUtility.SetDirty(go);
				        });
			        }

			        tagMenu.ShowAsContext();
		        }
	        }
        }

        private static void DisposeOfColorScopes()
        {
	        _colorScope3?.Dispose();
	        _colorScope2?.Dispose();
	        _colorScope?.Dispose();
        }
        #endregion

        #region Drawing Helpers

        private static Rect UseRectEnd(ref Rect rect, float width)
        {
	        var returnRect = rect;
	        rect.width -= width;
	        returnRect.x = rect.xMax;
	        returnRect.width = width;
	        return returnRect;
        }
        
        internal static void MakeRectLinkCursor(Rect rect = default)
        {
	        if (Event.current.type != EventType.Repaint) return;
	        if (rect == default) rect = GUILayoutUtility.GetLastRect();
	        EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
        }

        private static bool Foldout(GUIContent label, ref bool b)
        {
	        return b = EditorGUILayout.Foldout(b, label, true);
        }

        private static bool Foldout(string label, ref bool b) => Foldout(new GUIContent(label), ref b);

        private static bool DrawFoldoutTitle(string label, bool foldout, SavedBool enabled)
        {
	        using (new GUILayout.HorizontalScope())
	        {
		        var r = EditorGUILayout.GetControlRect(false, 24, Styles.BigTitle, GUILayout.ExpandWidth(true));
		        UnityEngine.GUI.Label(r, label, EditorStyles.whiteLargeLabel);

		        if (enabled != null)
		        {
			        enabled.DrawToggle("Enabled", "Disabled", null, PastelGreenColor, PastelRedColor, GUILayout.ExpandWidth(false));
			        MakeRectLinkCursor();
		        }

		        if (LeftClicked(r)) foldout = !foldout;
		        MakeRectLinkCursor(r);
	        }

	        return foldout;
        }
        private static void DrawColorSetting(string label, SavedColor color, SavedBool toggle = null)
        {
	        using (new GUILayout.HorizontalScope())
	        {
		        if (toggle != null)
		        {
			        var toggleTooltip = toggle ? new GUIContent("","Enabled") : new GUIContent("","Disabled");
			        toggle.DrawToggle(toggleTooltip, null, EditorStyles.radioButton, PastelGreenColor, Color.grey, GUILayout.Width(18), GUILayout.Height(18));
			        var r = GUILayoutUtility.GetLastRect();
			        EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
		        } else using (new EditorGUI.DisabledScope(true))
			        GUILayout.Toggle(true, " ", EditorStyles.radioButton, GUILayout.Width(18), GUILayout.Height(18));
		        
		        EditorGUILayout.PrefixLabel(label);
		        color.DrawField(GUIContent.none);
	        }
        }

        private static Rect DrawIconToggle(Rect rect, GameObject go, bool withBackground)
        {
	        var newState = !go.activeSelf;
	        var leftClicked = LeftClicked(rect);
	        switch (leftClicked)
	        {
		        case false when !MouseDraggedOver(rect, go):
			        return DrawIcon(rect, go, newState, withBackground);
		        case true:
			        StartDragToggling(newState);
			        break;
	        }

	        Undo.RecordObject(go, "[H+] Toggle GameObject");
	        go.SetActive(_dragToggleNewState);
	        EditorUtility.SetDirty(go);
	        DragToggledObjects.Add(go);

	        return DrawIcon(rect, go, newState, withBackground);
        }

        private static Rect DrawIconToggle(Rect rect, Component c, bool withBackgroun)
        {
	        var newState = !IsComponentEnabled(c);
	        if (!IsComponentToggleable(c)) return DrawIcon(rect, c, newState, withBackgroun);
	        
	        var leftClicked = LeftClicked(rect);
	        switch (leftClicked)
	        {
		        case false when !MouseDraggedOver(rect, c):
			        return DrawIcon(rect, c, newState, withBackgroun);
		        case true:
			        StartDragToggling(newState);
			        break;
	        }

	        Undo.RecordObject(c, "[H+] Toggle Component");
	        SetComponentEnabled(c, _dragToggleNewState);
	        EditorUtility.SetDirty(c);
	        DragToggledObjects.Add(c);

	        return DrawIcon(rect, c, newState, withBackgroun);
        }
        private static Rect DrawIcon(Rect rect, Component c, bool faded, bool withBackground) => DrawIcon(GetIcon(c), rect, faded, withBackground);

        private static Rect DrawIcon(Rect rect, GameObject go, bool faded, bool withBackground)
        {
	        var goContent = _gameObjectContent;
	        if (!Settings.useCustomGameObjectIcon || _getGameObjectIconMethod == null)
		        return DrawIcon(goContent, rect, faded, withBackground);
	        var icon = _getGameObjectIconMethod.Invoke(null, new object[] { go }) as Texture2D;
	        if (icon != null) goContent = new GUIContent(goContent){image = icon};
	        return DrawIcon(goContent, rect, faded, withBackground);
        }
        
        private static Rect DrawIcon(GUIContent content, Rect rect, bool faded, bool withBackground)
        {
	        using (new ColoredScope(ColoredScope.ColoringType.All, faded, Settings.iconFadedTintColor, Settings.iconTintColor))
	        {
		        if (withBackground) EditorGUI.DrawRect(rect, Settings.iconBackgroundColor);
		        UnityEngine.GUI.Label(rect, content);
		        if (Settings.linkCursorOnHover)
			        MakeRectLinkCursor(rect);
	        }
            rect.x -= 18;
            return rect;
        }

        private static GUIContent GetIcon(Component c)
        {
            if (c == null) return _missingScriptContent;
            var type = c.GetType();
            if (CustomIconCache.TryGetValue(type.Name, out var contentIcon)) return contentIcon;
            if (IconCache.TryGetValue(type, out contentIcon)) return contentIcon;
            
            
            var icon = AssetPreview.GetMiniThumbnail(c);
            if (!icon || DefaultTextures.Any(t => icon == t))
            {
                _defaultContent.tooltip = type.Name;
                return _defaultContent;
            }

	        contentIcon = new GUIContent(icon, type.Name);
            IconCache.Add(type, contentIcon);
            return contentIcon;
        }

        private static Texture2D _temporaryTexture;

        #endregion
        
        #region Functional Helpers
        private static bool IsComponentToggleable(Component c) => c is Behaviour or Renderer or Collider;

        private static bool IsComponentEnabled(Component c)
        {
	        if (!IsComponentToggleable(c)) return true;
	        dynamic d = c;
	        return d.enabled;
        }

        private static void SetComponentEnabled(Component c, bool enabled)
        {
	        if (!IsComponentToggleable(c)) return;
	        dynamic d = c;
	        d.enabled = enabled;
        }

        private static bool LeftClicked(Rect rect)
        {
            var e = Event.current;
            var clicked = e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition);
            if (clicked) e.Use();
            return clicked;
        }
        private static bool RightClicked(Rect rect)
        {
            var e = Event.current;
            var clicked = e.type == EventType.MouseDown && e.button == 1 && rect.Contains(e.mousePosition);
            if (clicked) e.Use();
            return clicked;
        }

        private static bool MouseDraggedOver(Rect rect, Object o)
        {
	        var e = Event.current;
	        return GUIUtility.hotControl == DragToggleHotControlID && e.type != EventType.Layout && rect.Contains(e.mousePosition) && !DragToggledObjects.Contains(o);
        }

        private static void StartDragToggling(bool toggleToState)
        {
	        DragToggledObjects.Clear();
	        _dragToggleNewState = toggleToState;
	        if (Settings.enableDragToggle) 
		        GUIUtility.hotControl = DragToggleHotControlID;
        }
        #endregion

        #region Initialization


        [InitializeOnLoadMethod]
        private static void InitializeGUI()
        {
            InitializeAll();
            EditorApplication.hierarchyWindowItemOnGUI -= OnHierarchyItemGUI;
            EditorApplication.hierarchyWindowItemOnGUI = OnHierarchyItemGUI + EditorApplication.hierarchyWindowItemOnGUI;
            EditorApplication.update -= OnCustomUpdate;
            EditorApplication.update += OnCustomUpdate;
        }
        
        private static void OnCustomUpdate() { _ranOnceThisFrame = false; }

        private static void InitializeAll()
        {
	        IconCache.Clear();
            InitializeIconFolderPath();
            InitializeCustomIcons();
            InitializeSpecialIcons();
        }

        private static void InitializeIconFolderPath()
        {
            _iconFolderPath = string.Empty;
            var assembly = Assembly.GetExecutingAssembly();
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(assembly);
            if (packageInfo != null)
            {
                var packagePath = packageInfo.assetPath;
                _iconFolderPath = $"{packagePath}/{PackageIconFolderPath}";

                if (AssetDatabase.IsValidFolder(_iconFolderPath)) return;
                CustomLog($"Custom Icon folder couldn't be found in {_iconFolderPath}. Custom Icons are disabled.");
                _iconFolderPath = string.Empty;
            } else CustomLog("Couldn't get package info for HierarchyPlus. Custom Icons are disabled. Is the script in Packages?", CustomLogType.Warning);
        }
        
        private static void InitializeSpecialIcons()
        {
	        _getGameObjectIconMethod = typeof(EditorGUIUtility).GetMethod("GetIconForObject", BindingFlags.NonPublic | BindingFlags.Static );
	        if (_getGameObjectIconMethod == null)
		        _getGameObjectIconMethod = typeof(EditorGUIUtility).GetMethod("GetIconForObject", BindingFlags.Public | BindingFlags.Static );
	        
	        DefaultTextures[0] = EditorGUIUtility.IconContent("cs Script Icon")?.image as Texture2D;
	        DefaultTextures[1] = EditorGUIUtility.IconContent("d_cs Script Icon")?.image as Texture2D;
	        DefaultTextures[2] = EditorGUIUtility.IconContent("dll Script Icon")?.image as Texture2D;

	        if (!CustomIconCache.TryGetValue("GameObject", out _gameObjectContent))
                _gameObjectContent = new GUIContent(AssetPreview.GetMiniTypeThumbnail(typeof(GameObject)));
	        _gameObjectContent.tooltip = "GameObject";
            
            if (!CustomIconCache.TryGetValue(DefaultIconName, out _defaultContent))
                _defaultContent = new GUIContent(AssetPreview.GetMiniTypeThumbnail(typeof(MonoScript)));
            
            const string missingTooltip = "Missing Script";
            _missingScriptContent = CustomIconCache.TryGetValue(MissingScriptIconName, out var value) ? new GUIContent(value){tooltip = missingTooltip} : new GUIContent(_defaultContent){tooltip = missingTooltip};
        }
        private static void InitializeCustomIcons()
        {            
            CustomIconCache.Clear();
            if (string.IsNullOrWhiteSpace(_iconFolderPath)) return;
            var paths = Directory.GetFiles(_iconFolderPath, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".meta"));
            foreach (var p in paths)
            {
	            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                if (icon != null) CustomIconCache.Add(icon.name, new GUIContent(icon, icon.name));
            }
            
        }
        #endregion

        #region Logging

        private static readonly Color PastelGreenColor = new(0.56f, 0.94f, 0.47f);
        private static readonly Color PastelRedColor = new(1, 0.25f, 0.25f);
        private static readonly Color PastelYellowColor = new(0.99f, 0.95f, 0, 6f);

        internal static bool CustomLog(string message, CustomLogType type = CustomLogType.Regular, bool condition = true)
        {
	        if (!condition) return false;
	        var finalColor = type switch
	        {
		        CustomLogType.Regular => PastelGreenColor,
		        CustomLogType.Warning => PastelYellowColor,
		        _ => PastelRedColor
	        };
            var fullMessage = $"<color=#{ColorUtility.ToHtmlStringRGB(finalColor)}>[{ProductName}]</color> {message.Replace("\\n", "\n")}";
            switch (type)
            {
	            case CustomLogType.Regular:
		            Debug.Log(fullMessage); break;
	            case CustomLogType.Warning:
		            Debug.LogWarning(fullMessage); break;
	            case CustomLogType.Error:
		            Debug.LogError(fullMessage); break;
	            default:
		            throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
            return true;
        }
        internal enum CustomLogType
        {
            Regular,
            Warning,
            Error
        }
        #endregion
    }
}
