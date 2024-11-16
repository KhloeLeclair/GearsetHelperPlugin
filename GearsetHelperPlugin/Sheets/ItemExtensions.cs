using System;
using System.Collections.Generic;

using Lumina.Excel.Sheets;

namespace GearsetHelperPlugin.Sheets;

internal static class ItemExtensions {

	internal static IEnumerable<(BaseParam, short)> GetParams(this Item item) {
		int count = Math.Min(item.BaseParam.Count, item.BaseParamValue.Count);
		for (int i = 0; i < count; i++) {
			var param = item.BaseParam[i];
			if (param.IsValid)
				yield return (param.Value, item.BaseParamValue[i]);
		}
	}

	internal static IEnumerable<(BaseParam, short)> GetSpecialParams(this Item item) {
		int count = Math.Min(item.BaseParamSpecial.Count, item.BaseParamValueSpecial.Count);
		for (int i = 0; i < count; i++) {
			var param = item.BaseParamSpecial[i];
			if (param.IsValid)
				yield return (param.Value, item.BaseParamValueSpecial[i]);
		}
	}

}
