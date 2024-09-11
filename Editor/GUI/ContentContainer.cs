using UnityEditor;
using UnityEngine;

namespace Editor.GUI
{
	internal class ContentContainer
	{
		private static ContentContainer _contentContainer;
		internal static ContentContainer Content => _contentContainer ??= new ContentContainer();

		internal readonly GUIContent ResetIcon = new(EditorGUIUtility.IconContent("Refresh")) {tooltip = "Reset"};

		private readonly GUIContent _tempContent = new();
	}
}
