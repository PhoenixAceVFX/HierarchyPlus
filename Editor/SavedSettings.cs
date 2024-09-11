using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using static Editor.GUI.StylesContainer;
using static Editor.GUI.ContentContainer;

namespace Editor
{
	[Serializable]

	internal class SavedSettings
	{
		internal static SavedSettings Settings => Data;

		private const string PrefsKey = "HierarchyPlusSettingsJSON";

		#region Main

		private static bool _saveDisabled;
		private static bool _pendingSave;
		private static bool _savePaused;
		private static SavedSettings _data;

		internal static bool SavePaused
		{
			get => _savePaused;
			set
			{
				var wasPaused = _savePaused;
				_savePaused = value;

				if (wasPaused && !_savePaused && _pendingSave) Save();
			}
		}

		internal static Action OnClear;

		internal static SavedSettings Data
		{
			get
			{
				if (_data == null) Load();
				return _data;
			}
		}


		#region Methods

		internal static void Save()
		{
			_pendingSave = false;
			if (SavePaused) _pendingSave = true;
			else if (!_saveDisabled)
			{
				var dataBuilder = new StringBuilder($"MAIN[{JsonUtility.ToJson(Data)}]\u200B\u200B\u200B");

				var rawData = dataBuilder.ToString();
				var compressedData = CompressString(rawData);
				EditorPrefs.SetString(PrefsKey, compressedData);
			}
		}

		private static void Load()
		{
			try
			{
				var fullData = EditorPrefs.GetString(PrefsKey, string.Empty);
				if (!string.IsNullOrWhiteSpace(fullData))
					fullData = DecompressString(fullData);

				var dataDictionary = new Dictionary<string, string>();

				if (!string.IsNullOrEmpty(fullData))
				{
					var matches = Regex.Matches(fullData, @"(\w+)\[(.*?)\]\u200B\u200B\u200B");
					for (var i = 0; i < matches.Count; i++)
					{
						var m = matches[i];
						dataDictionary.Add(m.Groups[1].Value, m.Groups[2].Value);
					}
				}

				if (dataDictionary.TryGetValue("MAIN", out var mainJson))
				{
					_data = JsonUtility.FromJson<SavedSettings>(mainJson);
				}

				_data ??= new SavedSettings();
			}
			catch (Exception ex)
			{
				HierarchyPlus.CustomLog($"There was an error loading settings. Settings have been reset.\n\n{ex}", HierarchyPlus.CustomLogType.Warning);
				_data = new SavedSettings();
			}
		}

		internal static void AskClear()
		{
			if (EditorUtility.DisplayDialog("Clearing Settings", "Are you sure you want to clear the settings?", "Clear", "Cancel")) Clear();
		}

		internal static void Clear()
		{
			_data = new SavedSettings();
			OnClear?.Invoke();
			Save();
		}

		private static string CompressString(string text)
		{
			var buffer = Encoding.UTF8.GetBytes(text);
			var memoryStream = new MemoryStream();
			using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
			{
				gZipStream.Write(buffer, 0, buffer.Length);
			}

			memoryStream.Position = 0;

			var compressedData = new byte[memoryStream.Length];
			memoryStream.Read(compressedData, 0, compressedData.Length);

			var gZipBuffer = new byte[compressedData.Length + 4];
			Buffer.BlockCopy(compressedData, 0, gZipBuffer, 4, compressedData.Length);
			Buffer.BlockCopy(BitConverter.GetBytes(buffer.Length), 0, gZipBuffer, 0, 4);
			return Convert.ToBase64String(gZipBuffer);
		}

		private static string DecompressString(string compressedText)
		{
			var gZipBuffer = Convert.FromBase64String(compressedText);
			using var memoryStream = new MemoryStream();
			var dataLength = BitConverter.ToInt32(gZipBuffer, 0);
			memoryStream.Write(gZipBuffer, 4, gZipBuffer.Length - 4);

			var buffer = new byte[dataLength];

			memoryStream.Position = 0;
			using (var gZipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
			{
				gZipStream.Read(buffer, 0, buffer.Length);
			}

			return Encoding.UTF8.GetString(buffer);
		}

		#endregion

		internal class SaveOnChange : IDisposable
		{
			private readonly Action _onChange;
			private readonly bool _wasPaused;
			private readonly EditorGUI.ChangeCheckScope _changeScope;

			public SaveOnChange(Action onChange = null)
			{
				_onChange = onChange;
				_wasPaused = SavePaused;
				SavePaused = true;
				_changeScope = new EditorGUI.ChangeCheckScope();
			}

			public void Dispose()
			{
				var hasChanged = _changeScope.changed;
				_changeScope.Dispose();
				if (hasChanged)
				{
					_onChange?.Invoke();
					Save();
				}

				SavePaused = _wasPaused;
			}

			public static implicit operator bool(SaveOnChange soc) => soc._changeScope.changed;
		}

		internal class SavePauseScope : IDisposable
		{
			private readonly bool _wasPaused;

			public SavePauseScope()
			{
				_wasPaused = SavePaused;
				SavePaused = true;
			}

			public void Dispose()
			{
				SavePaused = _wasPaused;
			}
		}

		#endregion

		#region Classes

		[Serializable]
		internal class SavedBool : SavedValue
		{
			[SerializeField] private bool _value;
			internal readonly Action OnChanged;

			internal bool Value
			{
				get => _value;
				set
				{
					if (_value == value) return;
					_value = value;
					OnChanged?.Invoke();
					Save();
				}
			}

			internal SavedBool(bool defaultValue, Action onChangedCallback = null)
			{
				this.DefaultValue = defaultValue;
				_value = defaultValue;
				OnChanged = onChangedCallback;
			}

			internal void Toggle() => Value = !_value;

			internal void DrawField(string label, GUIStyle style = null, params GUILayoutOption[] options)
				=> DrawField(new GUIContent(label), style, options);


			internal void DrawField(GUIContent label, GUIStyle style = null, params GUILayoutOption[] options)
			{
				Value = style == null ? EditorGUILayout.Toggle(label, Value, options) : EditorGUILayout.Toggle(label, Value, style, options);
			}

			internal void DrawToggle(string activeLabel, string inactiveLabel = null, GUIStyle style = null, Color? activeColor = null, Color? inactiveColor = null, params GUILayoutOption[] options)
				=> DrawToggle(string.IsNullOrEmpty(activeLabel) ? GUIContent.none : new GUIContent(activeLabel), string.IsNullOrEmpty(inactiveLabel) ? GUIContent.none : new GUIContent(inactiveLabel), style, activeColor, inactiveColor, options);

			internal void DrawToggle(GUIContent activeLabel, GUIContent inactiveLabel = null, GUIStyle style = null, Color? activeColor = null, Color? inactiveColor = null, params GUILayoutOption[] options)
			{
				activeColor ??= UnityEngine.GUI.backgroundColor;
				inactiveColor ??= UnityEngine.GUI.backgroundColor;
				var ogColor = UnityEngine.GUI.backgroundColor;
				UnityEngine.GUI.backgroundColor = Value ? (Color) activeColor : (Color) inactiveColor;
				Value = GUILayout.Toggle(Value, Value || inactiveLabel == null ? activeLabel : inactiveLabel, style ?? UnityEngine.GUI.skin.button, options);
				UnityEngine.GUI.backgroundColor = ogColor;
			}

			internal bool DrawFoldout(string label) => DrawFoldout(new GUIContent(label));

			internal bool DrawFoldout(GUIContent label)
			{
				return _value = EditorGUILayout.Foldout(_value, label);
			}

			public static implicit operator bool(SavedBool s) => s._value;
			internal virtual void Reset() => Value = (bool) DefaultValue;

		}

		[Serializable]
		internal class SavedFloat : SavedValue
		{

			[SerializeField] private float _value;
			internal readonly Action OnChanged;

			internal float Value
			{
				get => _value;
				set
				{
					if (Mathf.Approximately(_value, value)) return;
					_value = value;
					OnChanged?.Invoke();
					Save();
				}
			}

			internal SavedFloat(float defaultValue, Action onChangedCallback = null)
			{
				this.DefaultValue = defaultValue;
				_value = defaultValue;
				OnChanged = onChangedCallback;
			}

			internal virtual void Reset() => Value = (float) DefaultValue;

			public static implicit operator int(SavedFloat s) => (int) s._value;
			public static implicit operator float(SavedFloat s) => s._value;
		}

		[Serializable]
		internal class SavedString : SavedValue
		{
			[SerializeField] private string _value;
			internal readonly Action OnChanged;

			internal string Value
			{
				get => _value;
				set
				{
					if (_value == value) return;
					_value = value;
					OnChanged?.Invoke();
					Save();
				}
			}

			internal SavedString(string defaultValue = "", Action onChangedCallback = null)
			{
				this.DefaultValue = defaultValue;
				_value = defaultValue;
				OnChanged = onChangedCallback;
			}

			internal virtual void Reset() => Value = (string) DefaultValue;

			public override string ToString() => Value;

			public void DrawField(string label, GUIStyle style = null, params GUILayoutOption[] options) => DrawField(new GUIContent(label));

			public void DrawField(GUIContent label)
			{
				Value = EditorGUILayout.DelayedTextField(label, Value);
			}

			public static implicit operator string(SavedString s) => s._value;
		}

		[Serializable]
		internal class SavedColor : SavedValue
		{
			internal readonly Action OnChanged;

			[SerializeField] private float r;
			[SerializeField] private float g;
			[SerializeField] private float b;
			[SerializeField] private float a;

			internal Color Color
			{
				get => new(r, g, b, a);
				set
				{
					r = value.r;
					g = value.g;
					b = value.b;
					a = value.a;
					OnChanged?.Invoke();
					Save();
				}
			}

			internal SavedColor(float r, float g, float b, float a = 1, Action onChangedCallback = null)
			{
				var def = new Color(r, g, b, a);
				DefaultValue = def;
				this.r = r;
				this.g = g;
				this.b = b;
				this.a = a;
				OnChanged = onChangedCallback;
			}

			internal SavedColor(Color defaultColor, Action onChangedCallback = null)
			{
				DefaultValue = defaultColor;
				r = defaultColor.r;
				g = defaultColor.g;
				b = defaultColor.b;
				a = defaultColor.a;
				OnChanged = onChangedCallback;
			}

			internal void DrawField(string label, bool drawReset = true, params GUILayoutOption[] options)
			{
				DrawField(new GUIContent(label), drawReset, options);
			}

			internal void DrawField(GUIContent label, bool drawReset = true, params GUILayoutOption[] options)
			{
				using (new GUILayout.HorizontalScope())
				{
					Color = EditorGUILayout.ColorField(label, Color, options);
					if (!drawReset) return;
					if (GUILayout.Button(Content.ResetIcon, Styles.LabelButton, GUILayout.Width(18), GUILayout.Height(18)))
						Reset();
					HierarchyPlus.MakeRectLinkCursor();
				}
			}

			internal virtual void Reset() => Color = (Color) DefaultValue;

			public static implicit operator Color(SavedColor s) => s.Color;
		}


		internal abstract class SavedValue
		{
			internal object DefaultValue;
		}



		#endregion

		#region Saved Data

		[SerializeField] internal SavedString[]
			hiddenIconTypes = {new("MeshFilter")};

		[SerializeField] internal SavedColor
			rowOddColor = new(new Color(0.5f, 0.5f, 1, 0.07f)),
			rowEvenColor = new(new Color(0, 0, 0, 0.07f)),
			colorOne = new(Color.white),
			colorTwo = new(Color.white),
			colorThree = new(Color.white),
			guideLinesColor = new(Color.white),
			iconTintColor = new(Color.white),
			iconFadedTintColor = new(new Color(1, 1, 1, 0.5f)),
			iconBackgroundColor = new(new Color(0.22f, 0.22f, 0.22f));

		[SerializeField] internal SavedBool
			enabled = new(true),
			colorsEnabled = new(true),
			iconsEnabled = new(true),
			enableContextClick = new(true),
			enableDragToggle = new(true),
			colorOneEnabled = new(false),
			colorTwoEnabled = new(false),
			colorThreeEnabled = new(false),
			guideLinesEnabled = new(true),
			rowColoringOddEnabled = new(false),
			rowColoringEvenEnabled = new(true),
			showGameObjectIcon = new(true),
			useCustomGameObjectIcon = new(true),
			showTransformIcon = new(false),
			showNonBehaviourIcons = new(true),
			linkCursorOnHover = new(false),
			alwaysShowIcons = new(false),
			iconBackgroundColorEnabled = new(true),
			iconBackgroundOverlapOnly = new(true),
			labelsEnabled = new(true),
			enableLabelContextClick = new(true),
			tagLabelEnabled = new(true),
			displayUntaggedLabel = new(false),
			layerLabelEnabled = new(true),
			displayLayerIndex = new(false),
			displayDefaultLayerLabel = new(false);

		[SerializeField] internal SavedFloat
			guiXOffset = new(0),
			tagLabelWidth = new(75),
			layerLabelWidth = new(75);

		#endregion

		internal bool GetColorsEnabled() => enabled && colorsEnabled;
		internal bool GetIconsEnabled() => enabled && iconsEnabled;
		internal bool GetLabelsEnabled() => enabled && labelsEnabled;
		internal bool GetRowColoringEnabled() => rowColoringOddEnabled || rowColoringEvenEnabled;
	}
}
