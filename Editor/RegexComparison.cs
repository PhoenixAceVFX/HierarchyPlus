using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Editor
{
	[Serializable]
	internal class RegexComparison
	{
		[SerializeField] internal ComparisonType _comparisonType = ComparisonType.Contains;
		[SerializeField] internal bool caseSensitive;
		[SerializeField] internal string comparisonPattern;

		internal ComparisonType comparisonType
		{
			get => _comparisonType;
			set
			{
				if (_comparisonType == value) return;
				if (value == ComparisonType.Regex)
					comparisonPattern = GetFinalPattern();

				_comparisonType = value;
			}
		}

		internal enum ComparisonType
		{
			Contains = 0,
			StartsWith = 1 << 0,
			EndsWith = 1 << 1,
			EqualsTo = 3,
			Regex = 4,
		}

		internal bool IsMatch(string input) => !string.IsNullOrEmpty(comparisonPattern) && Regex.IsMatch(input, GetFinalPattern());

		internal bool[] IsMatch(IEnumerable<string> inputs)
		{
			var enumerable = inputs as string[] ?? inputs.ToArray();
			var results = new bool[enumerable.Length];
			if (string.IsNullOrEmpty(comparisonPattern)) return results;

			var pattern = GetFinalPattern();
			for (var i = 0; i < enumerable.Length; i++)
				results[i] = Regex.IsMatch(enumerable[i], pattern);

			return results;
		}

		internal string GetFinalPattern()
		{
			if (comparisonType == ComparisonType.Regex) return comparisonPattern;

			var finalPattern = new StringBuilder();
			if (((int) comparisonType & 1) > 0) finalPattern.Append('^');
			if (!caseSensitive) finalPattern.Append("(?i)");
			finalPattern.Append(Regex.Escape(comparisonPattern));
			if (((int) comparisonType & 2) > 0) finalPattern.Append('$');
			return finalPattern.ToString();
		}

		internal static bool IsValidRegex(string pattern)
		{
			if (string.IsNullOrEmpty(pattern)) return false;

			try
			{
				Regex.Match(string.Empty, pattern);
			}
			catch
			{
				return false;
			}

			return true;
		}

		internal void ExitRegexComparison()
		{
			var starts = comparisonPattern.StartsWith("^");
			var ends = comparisonPattern.EndsWith("$");
			_comparisonType = starts switch
			{
				true when ends => ComparisonType.EqualsTo,
				true => ComparisonType.StartsWith,
				_ => ends ? ComparisonType.EndsWith : ComparisonType.Contains
			};
			if (starts) comparisonPattern = comparisonPattern[1..];
			if (ends) comparisonPattern = comparisonPattern[..^1];

			var temp = comparisonPattern;
			comparisonPattern = comparisonPattern.Replace("(?i)", "");
			if (temp == comparisonPattern)
				caseSensitive = true;
		}
	}
}
