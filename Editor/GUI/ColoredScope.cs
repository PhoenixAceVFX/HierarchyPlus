using System;
using UnityEngine;

namespace Editor.GUI
{
	internal sealed class ColoredScope : IDisposable
	{
		internal enum ColoringType
		{
			Bg = 1 << 0,
			Fg = 1 << 1,
			General = 1 << 2,
			All = Bg | Fg | General
		}

		private readonly Color[] _ogColors = new Color[3];
		private readonly ColoringType _coloringType;
		private bool _changedAnyColor;

		private void MemorizeColor()
		{
			_changedAnyColor = true;
			_ogColors[0] = UnityEngine.GUI.backgroundColor;
			_ogColors[1] = UnityEngine.GUI.contentColor;
			_ogColors[2] = UnityEngine.GUI.color;
		}

		private void SetColors(Color color)
		{
			MemorizeColor();

			if (_coloringType.HasFlag(ColoringType.Bg))
				UnityEngine.GUI.backgroundColor = color;

			if (_coloringType.HasFlag(ColoringType.Fg))
				UnityEngine.GUI.contentColor = color;

			if (_coloringType.HasFlag(ColoringType.General))
				UnityEngine.GUI.color = color;
		}

		internal ColoredScope(ColoringType type, Color color)
		{
			_coloringType = type;
			SetColors(color);
		}

		internal ColoredScope(ColoringType type, bool isActive, Color color)
		{
			_coloringType = type;
			if (isActive) SetColors(color);

		}

		internal ColoredScope(ColoringType type, bool isActive, Color active, Color inactive)
		{
			_coloringType = type;
			SetColors(isActive ? active : inactive);
		}

		public void Dispose()
		{
			if (!_changedAnyColor) return;

			if (_coloringType.HasFlag(ColoringType.Bg))
				UnityEngine.GUI.backgroundColor = _ogColors[0];
			if (_coloringType.HasFlag(ColoringType.Fg))
				UnityEngine.GUI.contentColor = _ogColors[1];
			if (_coloringType.HasFlag(ColoringType.General))
				UnityEngine.GUI.color = _ogColors[2];


		}
	}
}
