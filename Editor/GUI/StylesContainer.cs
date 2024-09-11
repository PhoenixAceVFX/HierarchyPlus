using UnityEditor;
using UnityEngine;

namespace Editor.GUI
{
	internal class StylesContainer
	{
		private static StylesContainer _styles;
		internal static StylesContainer Styles => _styles ??= new StylesContainer();

		internal readonly GUIStyle
			LabelButton = new(UnityEngine.GUI.skin.label) {padding = new RectOffset(), margin = new RectOffset(1, 1, 1, 1)};

		private readonly GUIStyle
			_faintLabel = new(UnityEngine.GUI.skin.label) {fontStyle = FontStyle.Italic, richText = true, fontSize = 11, normal = {textColor = EditorGUIUtility.isProSkin ? Color.gray : new Color(0.357f, 0.357f, 0.357f)}};

		internal readonly GUIStyle BigTitle = "in bigtitle",
			FaintLinkLabel;

		private StylesContainer()
		{
			FaintLinkLabel = new GUIStyle(_faintLabel) {name = "Toggle", hover = {textColor = new Color(0.3f, 0.7f, 1)}};
		}
	}
}
