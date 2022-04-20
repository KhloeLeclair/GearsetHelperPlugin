using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Logging;

using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Enums;

using Dalamud.Interface;
using Dalamud.Interface.Colors;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

using ImGuiNET;

using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

using GearsetHelperPlugin.Sheets;


namespace GearsetHelperPlugin.Ui;

internal class ExamineWindow : IDisposable {

	private PluginUI Ui { get; }

	private readonly ExcelSheet<ExtendedItem>? ItemSheet;
	private readonly ExcelSheet<ExtendedItemLevel>? ItemLevelSheet;
	private readonly ExcelSheet<Materia>? MateriaSheet;
	private readonly ExcelSheet<ExtendedBaseParam>? ParamSheet;
	private readonly ExcelSheet<Tribe>? TribeSheet;
	private readonly ExcelSheet<ClassJob>? ClassSheet;

	private readonly Dictionary<uint, ImGuiScene.TextureWrap?> ItemIcons = new();

	private uint examineLoadStage = 4;

	internal ExamineWindow(PluginUI ui) {
		Ui = ui;

		ItemIcons = new();

		ItemSheet = Ui.Plugin.DataManager.Excel.GetSheet<ExtendedItem>();
		ItemLevelSheet = Ui.Plugin.DataManager.Excel.GetSheet<ExtendedItemLevel>();
		MateriaSheet = Ui.Plugin.DataManager.Excel.GetSheet<Materia>();
		ParamSheet = Ui.Plugin.DataManager.Excel.GetSheet<ExtendedBaseParam>();
		TribeSheet = Ui.Plugin.DataManager.Excel.GetSheet<Tribe>();
		ClassSheet = Ui.Plugin.DataManager.Excel.GetSheet<ClassJob>();

		Ui.Plugin.Functions.ExamineOnRefresh += ExamineRefreshed;
	}

	public void Dispose() {
		foreach(var entry in ItemIcons)
			entry.Value?.Dispose();

		ItemIcons.Clear();
		CachedGearset = null;

		Ui.Plugin.Functions.ExamineOnRefresh -= ExamineRefreshed;
	}

	private void ExamineRefreshed(ushort menuId, int val, uint loadStage) {
		// Just save the load state so our draw call knows if data is loaded or not.
		if (loadStage == 1 || loadStage > examineLoadStage)
			examineLoadStage = loadStage;
	}

	private Gearset? CachedGearset;

	private ImGuiScene.TextureWrap? GetIcon(uint id) {
		if (ItemIcons.TryGetValue(id, out var icon))
			return icon;

		icon = Ui.Plugin.DataManager.GetImGuiTextureHqIcon(id);
		ItemIcons[id] = icon;
		return icon;
	}

	internal static void DrawStatTable(IEnumerable<ItemStat> stats, Dictionary<uint, ExtendedBaseParam> paramDictionary, bool includeBase = false, bool includeRemaining = false) {
		int cols = 8;
		if (includeBase)
			cols += 2;
		if (includeRemaining)
			cols += 2;

		ImGui.BeginTable("StatTable", cols, ImGuiTableFlags.RowBg);
		ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 3f);

		if (includeBase) {
			ImGui.TableSetupColumn("Base", ImGuiTableColumnFlags.None, 1f);
			ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 10f);
		}

		ImGui.TableSetupColumn("Gear", ImGuiTableColumnFlags.None, 1f);
		ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 10f);
		ImGui.TableSetupColumn("Meld", ImGuiTableColumnFlags.None, 1f);
		ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 10f);
		ImGui.TableSetupColumn("Over", ImGuiTableColumnFlags.None, 1f);
		ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 10f);
		ImGui.TableSetupColumn("Total", ImGuiTableColumnFlags.None, 1f);

		if (includeRemaining) {
			ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 10f);
			ImGui.TableSetupColumn("Cap", ImGuiTableColumnFlags.None, 1f);
		}

		ImGui.TableHeadersRow();

		var data = stats
			.Where(stat => paramDictionary.ContainsKey(stat.StatID))
			.Select<ItemStat, (ItemStat, ExtendedBaseParam)>(stat => (stat, paramDictionary[stat.StatID]))
			.OrderBy(entry => entry.Item2.OrderPriority);

		foreach (var entry in data) {
			var stat = entry.Item1;
			var param = entry.Item2;

			ImGui.TableNextRow();

			ImGui.TableNextColumn();
			ImGui.Text(param.Name);

			if (includeBase) {
				ImGui.TableNextColumn();
				if (stat.Base > 0)
					ImGui.TextColored(ImGuiColors.DalamudGrey, stat.Base.ToString());
				else
					ImGui.TextColored(ImGuiColors.ParsedGrey, stat.Base.ToString());

				ImGui.TableNextColumn();
				ImGui.TextColored(ImGuiColors.DalamudGrey3, "+");
			}

			ImGui.TableNextColumn();
			ImGui.Text(stat.Gear.ToString());

			ImGui.TableNextColumn();
			ImGui.TextColored(ImGuiColors.DalamudGrey3, "+");

			ImGui.TableNextColumn();
			if (stat.Delta > 0)
				ImGui.TextColored(ImGuiColors.ParsedGreen, stat.Delta.ToString());
			else
				ImGui.TextColored(ImGuiColors.DalamudGrey, stat.Delta.ToString());

			ImGui.TableNextColumn();
			ImGui.TextColored(ImGuiColors.DalamudGrey3, "-");

			ImGui.TableNextColumn();
			if (stat.Waste > 0)
				ImGui.TextColored(ImGuiColors.DalamudRed, stat.Waste.ToString());
			else
				ImGui.TextColored(ImGuiColors.ParsedGrey, stat.Waste.ToString());

			ImGui.TableNextColumn();
			ImGui.TextColored(ImGuiColors.DalamudGrey3, "=");

			ImGui.TableNextColumn();
			ImGui.Text(stat.Value.ToString());

			if (includeRemaining) {
				ImGui.TableNextColumn();
				ImGui.TableNextColumn();
				int remaining = stat.Remaining;
				if (remaining <= 0)
					ImGui.TextColored(ImGuiColors.ParsedGreen, stat.Remaining.ToString());
				else
					ImGui.TextColored(ImGuiColors.DalamudYellow, stat.Remaining.ToString());
			}
		}

		ImGui.EndTable();
	}

	internal unsafe void Draw() {
		if (ItemSheet == null || ItemLevelSheet == null || MateriaSheet == null || ParamSheet == null || TribeSheet == null || ClassSheet == null)
			return;

		var examineAddon = (AtkUnitBase*) Ui.Plugin.GameGui.GetAddonByName("CharacterInspect", 1);
		if (examineAddon == null || !examineAddon->IsVisible) {
			CachedGearset = null;
			Ui.Plugin.Exporter.ClearError();
			return;
		}

		UpdateGearset();

		if (CachedGearset == null) {
			Ui.Plugin.Exporter.ClearError();
			return;
		}

		float scale = ImGui.GetFontSize() / 17;

		ImGuiWindowFlags flags;
		bool left = false;
		if (Ui.Plugin.Config.AttachToExamine) {
			left = Ui.Plugin.Config.AttachSide == 0;
			var root = examineAddon->RootNode;
			if (root == null)
				return;

			Vector2 pos = ImGuiHelpers.MainViewport.Pos
				+ new Vector2(examineAddon->X, examineAddon->Y)
				+ Vector2.UnitY * (ImGui.GetStyle().FramePadding.Y + ImGui.GetStyle().FrameBorderSize);

			if (left) {
				pos = pos
					- Vector2.UnitX * 370 * scale
					- Vector2.UnitX * (ImGui.GetStyle().FramePadding.X + ImGui.GetStyle().FrameBorderSize);

			} else {
				pos = pos
					
					+ Vector2.UnitX * (root->Width * examineAddon->Scale)
					+ Vector2.UnitX * (ImGui.GetStyle().FramePadding.X + ImGui.GetStyle().FrameBorderSize);
			}

			ImGui.SetNextWindowPos(pos);
			flags = ImGuiWindowFlags.NoMove;

		} else
			flags = ImGuiWindowFlags.None;

		ImGui.SetNextWindowSize(new Vector2(370 * scale, 200 * scale), ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSizeConstraints(new Vector2(370 * scale, 200 * scale), new Vector2(left ? 370 * scale : float.MaxValue, float.MaxValue));

		if (ImGui.Begin("Gearset Helper", flags | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoFocusOnAppearing)) {

			if (Ui.Plugin.Exporter.Exporting) {
				ImGui.Text("Exporting...");
			} else {
				ImGui.Text("Export To:");
				ImGui.SameLine();

				if (ImGui.Button("Ariyala"))
					Ui.Plugin.Exporter.ExportAriyala(CachedGearset);

				if (Ui.Plugin.Exporter.CanExportEtro) {
					ImGui.SameLine();
					if (ImGui.Button("Etro"))
						Ui.Plugin.Exporter.ExportEtro(CachedGearset);
				}

				if (Ui.Plugin.Exporter.Error != null) {
					ImGui.TextColored(ImGuiColors.DalamudYellow, "Error:");
					ImGui.TextWrapped(Ui.Plugin.Exporter.Error);
				}
			}

			if (ImGui.CollapsingHeader("Stats", ImGuiTreeNodeFlags.DefaultOpen)) {
				DrawStatTable(CachedGearset.Stats.Values, CachedGearset.Params, true);
			}

			if (CachedGearset.Materia.Count > 0 || CachedGearset.Unmelded > 0) {
				if (ImGui.CollapsingHeader("Melded Materia", ImGuiTreeNodeFlags.DefaultOpen)) {
					ImGui.BeginTable("MateriaTable", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable);

					ImGui.TableSetupColumn(" #", ImGuiTableColumnFlags.WidthFixed, 32f);
					ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
					ImGui.TableHeadersRow();

					var sort = ImGui.TableGetSortSpecs();

					var entries = CachedGearset.Materia
						.Where(raw => raw.Key != 0)
						.Select(entry => {
							return (
								ItemSheet.GetRow(entry.Key),
								entry.Value
							);
						})
						.Where(row => row.Item1 != null);

					if (CachedGearset.Unmelded > 0) {
						var list = entries.ToList();
						list.Add((null, CachedGearset.Unmelded));
						entries = list;
					}

					if (sort.Specs.SortDirection != ImGuiSortDirection.None) {
						bool descending = sort.Specs.SortDirection == ImGuiSortDirection.Descending;
						switch (sort.Specs.ColumnIndex) {
							case 0:
								entries = entries.OrderBy(val => val.Value);
								break;
							case 1:
								entries = entries.OrderBy(val => val.Item1?.Name.RawString);
								break;
						}

						if (sort.Specs.SortDirection == ImGuiSortDirection.Descending)
							entries = entries.Reverse();
					}

					foreach (var entry in entries) {
						var mat = entry.Item1;
						ImGui.TableNextRow();

						var icon = mat == null ? null : GetIcon(mat.Icon);
						int height = icon == null ? 0 : Math.Min(icon.Height, (int)(32 * scale));

						ImGui.TableSetColumnIndex(0);

						if (icon != null)
							ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (height - ImGui.GetFontSize()) / 2);

						ImGui.Text($" {entry.Value}");

						ImGui.TableSetColumnIndex(1);

						if (icon != null) {
							ImGui.Image(icon.ImGuiHandle, new Vector2(height, height));
							ImGui.SameLine();
							ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (height - ImGui.GetFontSize()) / 2);
						}

						if (mat == null)
							ImGui.TextColored(ImGuiColors.ParsedGrey, "(Empty Slots)");
						else
							ImGui.Text(mat.Name);
					}
					ImGui.EndTable();
				}
			}

			if (Ui.Plugin.Config.ShowItems && ImGui.CollapsingHeader("Items")) {
				bool first = true;

				foreach (var item in CachedGearset.Items) {
					var data = item.GetItem(ItemSheet);
					if (data == null)
						continue;

					if (first)
						first = false;
					else {
						ImGui.Spacing();
						ImGui.Separator();
						ImGui.Spacing();
					}

					var levelData = ItemLevelSheet.GetRow(data.LevelItem.Row);

					var icon = GetIcon(data.Icon);
					if (icon != null) {
						int height = Math.Min(icon.Height, (int) (32 * scale));

						ImGui.Image(icon.ImGuiHandle, new Vector2(height, height));
						ImGui.SameLine();
						ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (height - ImGui.GetFontSize()) / 2);
					}

					ImGui.Text(data.Name);

					DrawStatTable(item.Stats.Values, CachedGearset.Params, false, true);
				}
			}
		}
		ImGui.End();
	}

	private unsafe void UpdateGearset() {
		if (ItemSheet == null || ItemLevelSheet == null || MateriaSheet == null || ParamSheet == null)
			return;

		if (examineLoadStage < 4) {
			CachedGearset = null;
			return;
		}

		if (CachedGearset == null) {
			CachedGearset = GetGearset();
			return;
		}

		var inventory = InventoryManager.Instance()->GetInventoryContainer(InventoryType.Examine);
		if (inventory == null) {
			CachedGearset = null;
			return;
		}

		bool match = true;

		int idx = 0;

		for (uint i = 0; i < inventory->Size; i++) {
			var item = inventory->Items[i];
			if (item.ItemID == 0)
				continue;

			if (idx >= CachedGearset.Items.Count) {
				match = false;
				break;
			}

			var eitem = CachedGearset.Items[idx];
			if (eitem == null || eitem.ItemID != item.ItemID) { 
				match = false;
				break;
			}

			if (item.Flags.HasFlag(InventoryItem.ItemFlags.HQ) != eitem.HQ) {
				match = false;
				break;
			}

			for(int j = 0; j < 5; j++) {
				var mat = eitem.Melds[j];
				if (mat.ID != item.Materia[j] && mat.Grade != item.MateriaGrade[j]) {
					match = false;
					break;
				}
			}

			if (!match)
				break;

			idx++;
		}

		if (!match) {
			CachedGearset = GetGearset();
			return;
		}
	}

	private Gearset? GetGearset() {
		if (ItemSheet == null || ItemLevelSheet == null || MateriaSheet == null || ParamSheet == null || ClassSheet == null || TribeSheet == null)
			return null;

		List<MeldedItem>? items = GetItems();
		if (items == null)
			return null;

		Gearset gearset = new(items);

		uint totalLevel = 0;
		uint levelCount = 0;

		ExtendedClassJobCategory? jobCategory = null;

		foreach (MeldedItem item in items) {
			var itemData = item.GetItem(ItemSheet);
			if (itemData == null)
				continue;

			var levelData = ItemLevelSheet.GetRow(itemData.LevelItem.Row);
			if (levelData == null)
				continue;

			if (itemData.ItemUICategory.Row != 62) {
				item.Level = itemData.LevelItem.Row;
				totalLevel += item.Level;
				levelCount++;
			}

			// Main Hand
			var cjc = itemData.ExtendedClassJobCategory;
			var esc = itemData.EquipSlotCategory.Value;
			if (cjc != null && esc != null) {
				if (esc.MainHand != 0)
					jobCategory = cjc.Value;
				else if (esc.OffHand != 0 && jobCategory == null)
					jobCategory = cjc.Value;

				if (esc.SoulCrystal != 0)
					gearset.HasCrystal = true;
			}

			//PluginLog.Log($"Item: {itemData.Name} -- Equip Slot: {itemData.EquipSlotCategory.Row} -- {itemData.EquipSlotCategory.Value?.MainHand}");

			foreach(var pd in itemData.BaseParam) {
				uint row = pd.BaseParam;
				short value = pd.BaseParamValue;

				if (row == 0 || value == 0)
					continue;

				// Specifically disallow "Main Attribute" and "Secondary Attribute"
				// from showing up.
				if (row == 55 || row == 56)
					continue;

				// Get the Parameter
				if (!gearset.Params.TryGetValue(row, out var param)) {
					param = ParamSheet.GetRow(row);
					if (param == null)
						continue;

					gearset.Params[row] = param;
				}

				// Save this stat to the gearset
				if (!gearset.Stats.ContainsKey(row))
					gearset.Stats.Add(row, new(row));

				gearset.Stats[row].Gear += value;

				// Save this stat to the item
				if (!item.Stats.ContainsKey(row)) {
					item.Stats.Add(row, new(row));
					item.Stats[row].Limit = (int) Math.Round(
						(levelData.BaseParam[row] *
						param.EquipSlotCategoryPct[itemData.EquipSlotCategory.Row])
						/ 1000f,
						MidpointRounding.AwayFromZero
					);
				}

				item.Stats[row].Gear += value;
			}

			// For HQ items, add the HQ bonus to the stats.
			if (item.HQ)
				foreach(var pd in itemData.BaseParamSpecial) {
					uint row = pd.BaseParamSpecial;
					short value = pd.BaseParamValueSpecial;

					if (row == 0 || value == 0 || ! item.Stats.ContainsKey(row))
						continue;

					item.Stats[row].Gear += value;
					gearset.Stats[row].Gear += value;
				}

			// Now, check each of the melded materia.
			int melds = 0;

			foreach (var md in item.Melds) {
				if (md.ID == 0 || md.Grade < 0)
					continue;

				var materia = md.GetMateria(MateriaSheet);
				if (materia == null || md.Grade >= materia.Value.Length)
					continue;

				uint row = materia.BaseParam.Row;
				short value = materia.Value[md.Grade];
				if (row == 0 || value == 0)
					continue;

				melds++;

				// Add this materia to the gearset materia
				// sum tracker.
				uint mitem = materia.Item[md.Grade].Row;
				if (!gearset.Materia.ContainsKey(mitem))
					gearset.Materia.Add(mitem, 1);
				else
					gearset.Materia[mitem]++;

				// Ensure we have the parameter.
				if (!gearset.Params.TryGetValue(row, out var param)) {
					param = ParamSheet.GetRow(row);
					if (param == null)
						continue;

					gearset.Params[row] = param;
				}

				// Save this stat to the item.
				if (!item.Stats.ContainsKey(row)) {
					item.Stats.Add(row, new(row));
					item.Stats[row].Limit = (int) Math.Round(
						(levelData.BaseParam[row] *
						param.EquipSlotCategoryPct[itemData.EquipSlotCategory.Row])
						/ 1000f,
						MidpointRounding.AwayFromZero
					);
				}

				item.Stats[row].Delta += value;
			}

			if (melds < itemData.MateriaSlotCount)
				gearset.Unmelded += itemData.MateriaSlotCount - melds;

			// Finally, update all waste values.
			foreach (var stat in item.Stats.Values) {
				stat.UpdateWaste();

				if (!gearset.Stats.ContainsKey(stat.StatID))
					gearset.Stats.Add(stat.StatID, new(stat.StatID));

				var gs = gearset.Stats[stat.StatID];
				gs.Delta += stat.Delta;
				gs.Waste += stat.Waste;
			}
		}

		var player = GetActor();
		gearset.Level = 90;
		if (player != null) {
			gearset.PlayerName = player.Name.ToString();
			gearset.Level = player.Level;
			gearset.Tribe = player.Customize[(int) CustomizeIndex.Tribe];
		}

		// Determine the class from the equipped gear.
		ClassJob? job = null;
		if (jobCategory != null) {
			// First, search for a job specifically. No classes.
			foreach(var row in ClassSheet) {
				if (row.JobIndex == 0)
					continue;

				if (jobCategory.Classes[row.RowId]) {
					gearset.Class = row.RowId;
					gearset.ActualClass = row.RowId;
					job = row;
					break;
				}
			}

			// Widen the search to ANY class if we need to.
			if (job == null) {
				foreach (var row in ClassSheet) {
					if (jobCategory.Classes[row.RowId]) {
						gearset.Class = row.RowId;
						gearset.ActualClass = row.RowId;
						job = row;
						break;
					}
				}
			}

			// If we don't have a soul crystal, and this job has
			// a parent job, then switch it up for stat calculation
			// but leave the class set on gearset for exporting
			// correctly.
			if (job != null && ! gearset.HasCrystal) {
				var parent = job.ClassJobParent.Value;
				if (parent != null) {
					job = parent;
					gearset.ActualClass = parent.RowId;
				}
			}
		}

		// Apply base stats.
		foreach (var entry in gearset.Stats)
			entry.Value.Base = Data.GetBaseStatAtLevel((Stat) entry.Key, gearset.Level);

		// If we have a job, modify the base stats.
		if (job != null) {
			ModifyStat(gearset, Stat.STR, job.ModifierStrength / 100f);
			ModifyStat(gearset, Stat.DEX, job.ModifierDexterity / 100f);
			ModifyStat(gearset, Stat.VIT, job.ModifierVitality / 100f);
			ModifyStat(gearset, Stat.INT, job.ModifierIntelligence / 100f);
			ModifyStat(gearset, Stat.MND, job.ModifierMind / 100f);
			ModifyStat(gearset, Stat.PIE, job.ModifierPiety / 100f);
		}

		// If we have a tribe, modify the base stats.
		if (gearset.Tribe.HasValue) {
			var tribeData = TribeSheet.GetRow(gearset.Tribe.Value);
			if (tribeData != null) {
				ModifyStat(gearset, Stat.STR, extra: tribeData.STR);
				ModifyStat(gearset, Stat.DEX, extra: tribeData.DEX);
				ModifyStat(gearset, Stat.VIT, extra: tribeData.VIT);
				ModifyStat(gearset, Stat.INT, extra: tribeData.INT);
				ModifyStat(gearset, Stat.MND, extra: tribeData.MND);
				ModifyStat(gearset, Stat.PIE, extra: tribeData.PIE);
			}
		}

		return gearset;
	}

	private static void ModifyStat(Gearset gearset, Stat stat, float multiplier = 1f, int extra = 0) {
		if (!gearset.Stats.TryGetValue((uint)stat, out var value) || value.Base <= 0)
			return;

		if (multiplier != 1)
			value.Base = (int) Math.Floor(value.Base * multiplier);

		value.Base += extra;
	}

	private unsafe PlayerCharacter? GetActor() {
		var examineAddon = (AtkUnitBase*) Ui.Plugin.GameGui.GetAddonByName("CharacterInspect", 1);
		if (examineAddon == null || !examineAddon->IsVisible)
			return null;

		Lazy<Dictionary<string, PlayerCharacter>> players = new(() => {
			var rawPlayers = Ui.Plugin.ObjectTable
			.Where(obj => obj is PlayerCharacter && obj.IsValid())
			.Cast<PlayerCharacter>();

			var result = new Dictionary<string, PlayerCharacter>();

			foreach (var entry in rawPlayers) {
				string name = entry.Name.TextValue;
				if (!result.ContainsKey(name))
					result[name] = entry;
			}

			return result;
		});

		var nodeList = examineAddon->UldManager.NodeList;
		ushort count = examineAddon->UldManager.NodeListCount;

		for (ushort i = 0; i < count; i++) {
			var obj = nodeList[i];
			if (obj == null)
				continue;

			if (!obj->IsVisible || obj->Type != NodeType.Text)
				continue;

			var txt = obj->GetAsAtkTextNode();
			if (txt == null)
				continue;

			string? result = txt->NodeText.ToString();

			if (!string.IsNullOrEmpty(result) && players.Value.TryGetValue(result, out PlayerCharacter? player))
				return player;
		}

		return null;
	}

	private unsafe List<MeldedItem>? GetItems() {
		var inventory = InventoryManager.Instance()->GetInventoryContainer(InventoryType.Examine);
		if (inventory == null)
			return null;

		var result = new List<MeldedItem>();

		for(uint i = 0; i < inventory->Size; i++) {
			var item = inventory->Items[i];
			if (item.ItemID == 0)
				continue;

			result.Add(new MeldedItem(
				itemID: item.ItemID,
				hq: item.Flags.HasFlag(InventoryItem.ItemFlags.HQ),
				melds: new MeldedMateria[] {
					new MeldedMateria(item.Materia[0], item.MateriaGrade[0]),
					new MeldedMateria(item.Materia[1], item.MateriaGrade[1]),
					new MeldedMateria(item.Materia[2], item.MateriaGrade[2]),
					new MeldedMateria(item.Materia[3], item.MateriaGrade[3]),
					new MeldedMateria(item.Materia[4], item.MateriaGrade[4]),
				}
			));
		}

		return result;
	}
}
