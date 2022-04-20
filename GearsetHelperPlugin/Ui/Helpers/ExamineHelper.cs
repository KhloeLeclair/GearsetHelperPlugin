using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;

using Dalamud.Logging;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;

using Dalamud.Interface;
using Dalamud.Interface.Colors;

using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;

using ImGuiNET;

using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

using GearsetExportPlugin.Sheets;


namespace GearsetExportPlugin.Ui.Helpers;

internal class ExamineHelper : IDisposable {

	private PluginUI Ui { get; }

	private readonly ExcelSheet<ExtendedItem>? ItemSheet;
	private readonly ExcelSheet<ExtendedItemLevel>? ItemLevelSheet;
	private readonly ExcelSheet<Materia>? MateriaSheet;
	private readonly ExcelSheet<ExtendedBaseParam>? ParamSheet;

	private readonly Dictionary<uint, ImGuiScene.TextureWrap?> ItemIcons = new();

	internal ExamineHelper(PluginUI ui) {
		Ui = ui;

		ItemIcons = new();

		ItemSheet = Ui.Plugin.DataManager.Excel.GetSheet<ExtendedItem>();
		ItemLevelSheet = Ui.Plugin.DataManager.Excel.GetSheet<ExtendedItemLevel>();
		MateriaSheet = Ui.Plugin.DataManager.Excel.GetSheet<Materia>();
		ParamSheet = Ui.Plugin.DataManager.Excel.GetSheet<ExtendedBaseParam>();
	}

	public void Dispose() {
		foreach(var entry in ItemIcons)
			entry.Value?.Dispose();

		ItemIcons.Clear();
		CachedGearset = null;
	}

	private Gearset? CachedGearset;

	private ImGuiScene.TextureWrap? GetIcon(uint id) {
		if (ItemIcons.TryGetValue(id, out var icon))
			return icon;

		icon = Ui.Plugin.DataManager.GetImGuiTextureHqIcon(id);
		ItemIcons[id] = icon;
		return icon;
	}

	internal unsafe void Draw() {
		var examineAddon = (AtkUnitBase*) Ui.Plugin.GameGui.GetAddonByName("CharacterInspect", 1);
		if (examineAddon == null || !examineAddon->IsVisible) {
			CachedGearset = null;
			Ui.Plugin.Exporter.ClearError();
			return;
		}

		if (ItemSheet == null || ItemLevelSheet == null || MateriaSheet == null || ParamSheet == null)
			return;

		UpdateGearset();
		if (CachedGearset == null)
			return;

		var root = examineAddon->RootNode;
		if (root == null)
			return;

		Vector2 pos = ImGuiHelpers.MainViewport.Pos
			+ new Vector2(examineAddon->X, examineAddon->Y)
			+ Vector2.UnitX * (root->Width * examineAddon->Scale)
			+ Vector2.UnitX * (ImGui.GetStyle().FramePadding.X + ImGui.GetStyle().FrameBorderSize)
			+ Vector2.UnitY * (ImGui.GetStyle().FramePadding.Y + ImGui.GetStyle().FrameBorderSize);

		ImGui.SetNextWindowSize(new Vector2(375, 330), ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSizeConstraints(new Vector2(400, 330), new Vector2(400, float.MaxValue));
		ImGui.SetNextWindowPos(pos);

		if (ImGui.Begin("GearsetHelper", ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoMove)) {

			if (Ui.Plugin.Exporter.Exporting) {
				ImGui.Text("Exporting...");
			} else {
				if (ImGui.Button("Export to Ariyala"))
					Ui.Plugin.Exporter.ExportAriyala(CachedGearset);

				if (Ui.Plugin.Exporter.CanExportEtro && ImGui.Button("Export to Etro"))
					Ui.Plugin.Exporter.ExportEtro(CachedGearset);

				if (Ui.Plugin.Exporter.Error != null) {
					ImGui.TextColored(ImGuiColors.DalamudYellow, "Error:");
					ImGui.TextWrapped(Ui.Plugin.Exporter.Error);
				}
			}

			if (ImGui.CollapsingHeader("Stats", ImGuiTreeNodeFlags.DefaultOpen)) {
				/*ImGui.TextColored(ImGuiColors.DalamudGrey, "Item Level");
				ImGui.Indent(20);
				ImGui.Text(CachedGearset.Level.ToString());
				ImGui.Unindent(20);*/

				foreach (var stat in CachedGearset.Stats.Values) {
					if (!CachedGearset.Params.TryGetValue(stat.StatID, out var param))
						continue;

					ImGui.TextColored(ImGuiColors.DalamudGrey, param.Name);
					ImGui.Indent(20);
					if (stat.Base > 0) {
						ImGui.Text(stat.Base.ToString());
						ImGui.SameLine();
					}
					if (stat.Delta > 0) {
						ImGui.TextColored(ImGuiColors.DalamudGrey, "+");
						ImGui.SameLine();
						ImGui.Text(stat.Delta.ToString());
						ImGui.SameLine();
					}
					if (stat.Waste > 0) {
						ImGui.TextColored(ImGuiColors.DalamudGrey, "-");
						ImGui.SameLine();
						ImGui.Text(stat.Waste.ToString());
						ImGui.SameLine();
					}
					if (stat.Base > 0 || stat.Delta > 0 || stat.Waste > 0) {
						ImGui.TextColored(ImGuiColors.DalamudGrey, "=");
						ImGui.SameLine();
					}
					ImGui.Text(stat.Value.ToString());
					ImGui.Unindent(20);
				}
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

						ImGui.TableSetColumnIndex(0);

						if (icon != null)
							ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (icon.Height - ImGui.GetFontSize()) / 2);

						ImGui.Text($" {entry.Value}");

						ImGui.TableSetColumnIndex(1);

						if (icon != null) {
							ImGui.Image(icon.ImGuiHandle, new Vector2(icon.Width, icon.Height));
							ImGui.SameLine();
							ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (icon.Height - ImGui.GetFontSize()) / 2);
						}

						if (mat == null)
							ImGui.TextColored(ImGuiColors.ParsedGrey, "(Empty Slots)");
						else
							ImGui.Text(mat.Name);
					}
					ImGui.EndTable();
				}
			}

			if (Ui.Plugin.Config.DisplayItemsDebug && ImGui.CollapsingHeader("Items")) {
				foreach (var item in CachedGearset.Items) {
					var data = item.GetItem(ItemSheet);
					if (data == null)
						continue;

					var levelData = ItemLevelSheet.GetRow(data.LevelItem.Row);

					var icon = GetIcon(data.Icon);
					if (icon != null) {

						ImGui.Image(icon.ImGuiHandle, new Vector2(icon.Width, icon.Height));
						ImGui.SameLine();
						ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (icon.Height - ImGui.GetFontSize()) / 2);
					}

					ImGui.Text(data.Name);
					ImGui.Indent(20);

					foreach (var stat in item.Stats.Values) {
						if (!CachedGearset.Params.TryGetValue(stat.StatID, out var param))
							continue;

						ImGui.TextColored(ImGuiColors.DalamudGrey, param.Name);
						ImGui.Indent(20);
						if (stat.Base > 0) {
							ImGui.Text(stat.Base.ToString());
							ImGui.SameLine();
						}
						if (stat.Delta > 0) {
							ImGui.TextColored(ImGuiColors.DalamudGrey, "+");
							ImGui.SameLine();
							ImGui.Text(stat.Delta.ToString());
							ImGui.SameLine();
						}
						if (stat.Waste > 0) {
							ImGui.TextColored(ImGuiColors.DalamudGrey, "-");
							ImGui.SameLine();
							ImGui.Text(stat.Waste.ToString());
							ImGui.SameLine();
						}
						if (stat.Base > 0 || stat.Delta > 0 || stat.Waste > 0) {
							ImGui.TextColored(ImGuiColors.DalamudGrey, "=");
							ImGui.SameLine();
						}
						ImGui.Text(stat.Value.ToString());
						ImGui.Unindent(20);
					}

					ImGui.Unindent(20);

					ImGui.Spacing();
					ImGui.Separator();
					ImGui.Spacing();
				}
			}
		}
		ImGui.End();
	}

	private unsafe void UpdateGearset() {
		if (ItemSheet == null || ItemLevelSheet == null || MateriaSheet == null || ParamSheet == null)
			return;

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
		if (ItemSheet == null || ItemLevelSheet == null || MateriaSheet == null || ParamSheet == null)
			return null;

		List<MeldedItem>? items = GetItems();
		if (items == null)
			return null;

		Gearset gearset = new(items);

		uint totalLevel = 0;
		uint levelCount = 0;

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
			if (itemData.EquipSlotCategory.Value?.MainHand != 0)
				gearset.Category = itemData.ClassJobCategory.Value;

			//PluginLog.Log($"Item: {itemData.Name} -- Equip Slot: {itemData.EquipSlotCategory.Row} -- {itemData.EquipSlotCategory.Value?.MainHand}");

			foreach(var pd in itemData.BaseParam) {
				uint row = pd.BaseParam;
				short value = pd.BaseParamValue;

				if (row == 0 || value == 0)
					continue;

				// Get the Parameter
				if (!gearset.Params.TryGetValue(row, out var param)) {
					param = ParamSheet.GetRow(row);
					if (param == null)
						continue;

					gearset.Params[row] = param;
				}

				// Save this stat to the gearset
				if (!gearset.Stats.ContainsKey(row)) {
					gearset.Stats.Add(row, new(row));
					if (BaseStats.Lvl90_Stats.ContainsKey(row))
						gearset.Stats[row].Base += BaseStats.Lvl90_Stats[row];
				}

				gearset.Stats[row].Base += value;

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

				item.Stats[row].Base += value;
			}

			// For HQ items, add the HQ bonus to the stats.
			if (item.HQ)
				foreach(var pd in itemData.BaseParamSpecial) {
					uint row = pd.BaseParamSpecial;
					short value = pd.BaseParamValueSpecial;

					if (row == 0 || value == 0 || ! item.Stats.ContainsKey(row))
						continue;

					item.Stats[row].Base += value;
					gearset.Stats[row].Base += value;
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

				if (!gearset.Stats.ContainsKey(stat.StatID)) {
					gearset.Stats.Add(stat.StatID, new(stat.StatID));
					if (BaseStats.Lvl90_Stats.ContainsKey(stat.StatID))
						gearset.Stats[stat.StatID].Base += BaseStats.Lvl90_Stats[stat.StatID];
				}

				var gs = gearset.Stats[stat.StatID];
				gs.Delta += stat.Delta;
				gs.Waste += stat.Waste;
			}
		}

		var player = GetActor();
		if (player != null) {
			gearset.PlayerName = player.Name.ToString();
			gearset.Level = player.Level;
			gearset.Class = player.ClassJob.Id;
		}

		if (levelCount > 0)
			gearset.ItemLevel = totalLevel / levelCount;

		return gearset;
	}

	private unsafe PlayerCharacter? GetActor() {
		var examineAddon = (AtkUnitBase*) Ui.Plugin.GameGui.GetAddonByName("CharacterInspect", 1);
		if (examineAddon == null || !examineAddon->IsVisible)
			return null;

		var rawPlayers = Ui.Plugin.ObjectTable
			.Where(obj => obj is PlayerCharacter && obj.IsValid())
			.Cast<PlayerCharacter>();

		var players = new Dictionary<string, PlayerCharacter>();

		foreach(var entry in rawPlayers) {
			string name = entry.Name.ToString();
			if (!players.ContainsKey(name))
				players[name] = entry;
		}

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

			if (result != null && players.TryGetValue(result, out PlayerCharacter? player))
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
