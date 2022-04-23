using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using Dalamud;

using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;

using FFXIVClientStructs.FFXIV.Client.Game;

using ImGuiNET;
using ImGuiScene;

using Lumina.Excel.GeneratedSheets;

using GearsetHelperPlugin.Models;
using GearsetHelperPlugin.Sheets;

namespace GearsetHelperPlugin.Ui;

internal abstract class BaseWindow : IDisposable {

	internal const ImGuiWindowFlags ButtonWindowFlags = ImGuiWindowFlags.None //ImGuiWindowFlags.NoBackground
		| ImGuiWindowFlags.NoDecoration
		| ImGuiWindowFlags.NoCollapse
		| ImGuiWindowFlags.NoTitleBar
		| ImGuiWindowFlags.NoNav
		| ImGuiWindowFlags.NoNavFocus
		| ImGuiWindowFlags.NoNavInputs
		| ImGuiWindowFlags.NoResize
		| ImGuiWindowFlags.NoScrollbar
		| ImGuiWindowFlags.NoSavedSettings
		| ImGuiWindowFlags.NoFocusOnAppearing
		| ImGuiWindowFlags.AlwaysAutoResize
		| ImGuiWindowFlags.NoDocking;

	protected PluginUI Ui { get; }
	protected abstract string Name { get; }

	private readonly Dictionary<uint, TextureWrap?> ItemIcons = new();
	private readonly Dictionary<uint, TextureWrap?> ItemIconsHQ = new();

	protected EquipmentSet? CachedSet;
	protected Food? SelectedFood;
	protected Food? SelectedMedicine;

	private Task<Exporter.ExportResponse>? ExportTask;
	private Exporter.ExportResponse? ExportResponse;

	private bool visible = false;
	private bool oldVisible = false;

	protected bool Visible {
		get => visible;
		set {
			if (visible != value) {
				visible = value;
				oldVisible = visible;
				OnVisibleChange();
			}
		}
	}

	internal BaseWindow(PluginUI ui) {
		Ui = ui;
	}

	public void Dispose() {
		Dispose(true);

		foreach (var entry in ItemIcons)
			entry.Value?.Dispose();

		foreach (var entry in ItemIconsHQ)
			entry.Value?.Dispose();

		ItemIcons.Clear();
		ItemIconsHQ.Clear();

		CachedSet = null;
		SelectedFood = null;
		SelectedMedicine = null;

		if (ExportTask is not null)
			ExportTask.Dispose();
	}

	public virtual void Dispose(bool disposing) {

	}

	protected virtual void OnVisibleChange() { }

	protected virtual Vector2? GetButtonPosition(float width, float height) { return null; }

	#region Icons

	protected TextureWrap? GetIcon(Item? item, bool hq = false) {
		if (item is not null)
			return GetIcon(item.Icon, hq);
		return null;
	}

	protected TextureWrap? GetIcon(uint id, bool hq = false) {
		if (hq) {
			if (ItemIcons.TryGetValue(id, out var icon))
				return icon;

			icon = Ui.Plugin.DataManager.GetImGuiTextureHqIcon(id);
			ItemIconsHQ[id] = icon;
			return icon;

		} else {
			if (ItemIcons.TryGetValue(id, out var icon))
				return icon;

			icon = Ui.Plugin.DataManager.GetImGuiTextureIcon(id);
			ItemIcons[id] = icon;
			return icon;
		}
	}

	#endregion

	#region Equipment Set Creation

	protected virtual bool HasEquipment() {
		return true;
	}

	protected abstract unsafe InventoryContainer* GetInventoryContainer();

	protected abstract void UpdatePlayerData(EquipmentSet set);

	protected unsafe void UpdateEquipmentSet() {
		if (!HasEquipment()) {
			CachedSet = null;
			return;
		}

		if (CachedSet is not null) {
			InventoryContainer* inventory = GetInventoryContainer();
			if (inventory is null) {
				CachedSet = null;
				return;
			}

			bool match = true;
			int idx = 0;

			for(uint i = 0; i < inventory->Size; i++) {
				var item = inventory->Items[i];
				if (item.ItemID == 0)
					continue;

				if (idx >= CachedSet.Items.Count) {
					match = false;
					break;
				}

				MeldedItem mitem = CachedSet.Items[idx];
				if (mitem.ID != item.ItemID) {
					match = false;
					break;
				}

				if (item.Flags.HasFlag(InventoryItem.ItemFlags.HQ) != mitem.HighQuality) {
					match = false;
					break;
				}

				for (int j = 0; j < 5; j++) {
					var mat = mitem.Melds[j];
					if (mat.ID != item.Materia[j] && mat.Grade != item.MateriaGrade[j]) {
						match = false;
						break;
					}
				}

				if (!match)
					break;

				idx++;
			}

			if (!match)
				CachedSet = null;
		}

		if (CachedSet is null)
			CachedSet = GetEquipmentSet();
	}

	protected virtual EquipmentSet? GetEquipmentSet() {
		List<MeldedItem>? items = GetItems();
		if (items == null)
			return null;

		var sw = new System.Diagnostics.Stopwatch();
		sw.Start();

		EquipmentSet result = new(items);

		result.Level = 90;

		UpdatePlayerData(result);
		result.Food = SelectedFood;
		result.Medicine = SelectedMedicine;

		result.Recalculate();

		sw.Stop();
		Dalamud.Logging.PluginLog.Log($"Processed equipment in {sw.ElapsedMilliseconds}ms");

		return result;
	}

	protected unsafe List<MeldedItem>? GetItems() {
		InventoryContainer* inventory = GetInventoryContainer();
		if (inventory == null)
			return null;

		List<MeldedItem> result = new();

		for (uint i = 0; i < inventory->Size; i++) {
			var item = inventory->Items[i];
			if (item.ItemID == 0)
				continue;

			result.Add(new MeldedItem(
				ID: item.ItemID,
				HighQuality: item.Flags.HasFlag(InventoryItem.ItemFlags.HQ),
				Melds: new MeldedMateria[] {
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

	#endregion

	#region Drawing

	protected bool DrawFoodCombo(string label, Food? selected, List<Food> choices, out Food? choice) {
		bool result = false;
		choice = null;

		if (ImGui.BeginCombo(label, selected?.ItemRow()?.Name ?? Localization.Localize("gui.food.none", "(None)"), ImGuiComboFlags.None)) {
			bool none_selected = selected is null;
			if (ImGui.Selectable(Localization.Localize("gui.food.none", "(None)"), none_selected)) {
				choice = null;
				result = true;
			}
			if (none_selected)
				ImGui.SetItemDefaultFocus();

			Vector2 size = new(ImGui.GetWindowContentRegionWidth(), 40 * ImGui.GetIO().FontGlobalScale);

			foreach (Food food in choices) {
				ExtendedItem? item = food.ItemRow();
				if (item is null)
					continue;

				bool current = selected == food;
				ImGui.PushID($"food#{food.ItemID}");
				var oldPos = ImGui.GetCursorPos();

				if (ImGui.Selectable("", current, ImGuiSelectableFlags.None, size)) {
					result = true;
					choice = food;
				}
				ImGui.PopID();
				if (current)
					ImGui.SetItemDefaultFocus();

				var newPos = ImGui.GetCursorPos();
				ImGui.SetCursorPos(oldPos);

				var image = GetIcon(item.Icon, true);
				if (image != null) {
					ImGui.SetCursorPosX(oldPos.X);
					ImGui.SetCursorPosY(oldPos.Y + (size.Y - image.Height) / 2);
					ImGui.Image(image.ImGuiHandle, new Vector2(image.Width, image.Height));
				}

				ImGui.SetCursorPosX(oldPos.X + (image?.Width ?? 0) + 2 * ImGui.GetStyle().FramePadding.X);
				ImGui.SetCursorPosY(oldPos.Y);

				ImGui.Text(item.Name);
				ImGui.SameLine();
				ImGui.TextColored(ImGuiColors.DalamudGrey, $"i{item.LevelItem.Row}");

				ImGui.SetCursorPosX(oldPos.X + (image?.Width ?? 0) + 2 * ImGui.GetStyle().FramePadding.X);
				ImGui.SetCursorPosY(oldPos.Y + size.Y / 2);

				ImGui.TextColored(ImGuiColors.DalamudGrey, food.StatLine ?? string.Empty);

				ImGui.SetCursorPos(newPos);
			}
			ImGui.EndCombo();
		}

		return result;
	}

	protected void DrawWindow(bool attach, int side, short x, short y, ushort width) {
		UpdateEquipmentSet();

		if (ExportTask != null) {
			if (ExportTask.IsCompleted) {
				ExportResponse = ExportTask.Result;
				ExportTask = null;
			}
		}

		if (CachedSet == null)
			return;

		if (!Data.CheckSheets())
			return;

		// Determine the current UI scale based on the font size.
		float scale = ImGui.GetFontSize() / 17;

		float menuWidth;
		if (visible)
			menuWidth = 430 * scale;
		else
			menuWidth = (ImGui.CalcTextSize(Ui.Plugin.Name).X + ImGui.GetFrameHeight());

		// Position
		bool left = side == 0;
		Vector2 pos = ImGuiHelpers.MainViewport.Pos
			+ new Vector2(x, y)
			+ Vector2.UnitY * (ImGui.GetStyle().FramePadding.Y + ImGui.GetStyle().FrameBorderSize);

		if (left) {
			pos = pos
				- Vector2.UnitX * menuWidth
				- Vector2.UnitX * (ImGui.GetStyle().FramePadding.X + ImGui.GetStyle().FrameBorderSize);

		} else {
			pos = pos

				+ Vector2.UnitX * (width)
				+ Vector2.UnitX * (ImGui.GetStyle().FramePadding.X + ImGui.GetStyle().FrameBorderSize);
		}

		if (!visible) {
			var size = ImGui.CalcTextSize(Ui.Plugin.Name);
			pos = GetButtonPosition(size.X, size.Y) ?? pos;
			ImGui.SetNextWindowPos(pos);
			if (ImGui.Begin($"##{Ui.Plugin.Name}#{Name}", ButtonWindowFlags)) {

				if (ImGui.Button(Ui.Plugin.Name))
					Visible = true;

			}
			ImGui.End();
			return;
		}

		ImGuiWindowFlags flags;
		if (attach) {
			flags = ImGuiWindowFlags.NoMove;
			ImGui.SetNextWindowPos(pos);
		} else
			flags = ImGuiWindowFlags.None;

		ImGui.SetNextWindowSize(new Vector2(menuWidth, 200 * scale), ImGuiCond.FirstUseEver);
		ImGui.SetNextWindowSizeConstraints(new Vector2(menuWidth, 200 * scale), new Vector2(left ? menuWidth : float.MaxValue, float.MaxValue));

		if (ImGui.Begin($"{Ui.Plugin.Name} - {Name}", ref visible, flags | ImGuiWindowFlags.NoDocking | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoCollapse)) {
			if (ExportTask != null)
				ImGui.Text(Localization.Localize("gui.exporting", "Exporting..."));

			else if (ExportResponse != null) {
				if (!string.IsNullOrEmpty(ExportResponse.Error)) {
					ImGui.TextColored(ImGuiColors.DalamudYellow, Localization.Localize("gui.error", "Error:"));
					ImGui.TextWrapped(ExportResponse.Error);
				} else {
					ImGui.TextColored(ImGuiColors.ParsedGreen, Localization.Localize("gui.export-success", "Export Successful!"));
					string url = ExportResponse.Url ?? string.Empty;
					ImGui.InputText(Localization.Localize("gui.url", "URL"), ref url, (uint) url.Length, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.ReadOnly);

					if (ImGui.Button(Localization.Localize("gui.open-browser", "Open in Browser")))
						Exporter.TryOpenURL(ExportResponse.Url!);

					ImGui.SameLine();
				}

				if (ImGui.Button(Localization.Localize("gui.done", "Done")))
					ExportResponse = null;

			} else {
				ImGui.Text(Localization.Localize("gui.export-to", "Export To:"));
				ImGui.SameLine();

				if (ImGui.Button("Ariyala"))
					ExportTask = Ui.Plugin.Exporter.ExportAriyala(CachedSet);

				if (Ui.Plugin.Exporter.CanExportEtro) {
					ImGui.SameLine();
					if (ImGui.Button("Etro"))
						ExportTask = Ui.Plugin.Exporter.ExportEtro(CachedSet);
				}

				ImGui.SameLine();
				if (ImGui.Button("Teamcraft (List)"))
					ExportTask = Ui.Plugin.Exporter.ExportTeamcraft(CachedSet);
			}

			if (SelectedFood is not null || CachedSet.RelevantFood.Count > 0) {
				if (DrawFoodCombo(
					Localization.Localize("gui.food", "Food"),
					SelectedFood,
					CachedSet.RelevantFood,
					out Food? choice
				)) {
					SelectedFood = choice;
					CachedSet.UpdateFood(choice);
				}

				ImGui.SameLine();
				ImGuiComponents.HelpMarker(Localization.Localize("gui.about-food", "Note: All food is assumed to be high-quality for the purpose of checking stats."));
			}

			if (SelectedMedicine is not null || CachedSet.RelevantMedicine.Count > 0) {
				if (DrawFoodCombo(
					Localization.Localize("gui.medicine", "Medicine"),
					SelectedMedicine,
					CachedSet.RelevantMedicine,
					out Food? choice
				)) {
					SelectedMedicine = choice;
					CachedSet.UpdateMedicine(choice);
				}
			}

			if (ImGui.CollapsingHeader(Localization.Localize("gui.attributes", "Attributes"), ImGuiTreeNodeFlags.DefaultOpen)) {
				DrawStatTable(CachedSet.Attributes.Values, CachedSet.Params, true, includeTiers: true, includeFood: (CachedSet.Food is not null || CachedSet.Medicine is not null));
			}

			if (ImGui.CollapsingHeader(Localization.Localize("gui.calculated", "Calculated"), ImGuiTreeNodeFlags.DefaultOpen)) {
				DrawCalculatedTable(CachedSet.Calculated);
			}

			if (CachedSet.MateriaCount.Count > 0 || CachedSet.EmptyMeldSlots > 0) {
				if (ImGui.CollapsingHeader(Localization.Localize("gui.melded", "Melded Materia"), ImGuiTreeNodeFlags.DefaultOpen)) {
					ImGui.BeginTable("MateriaTable", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable);

					ImGui.TableSetupColumn(" #", ImGuiTableColumnFlags.WidthFixed, 32f);
					ImGui.TableSetupColumn(Localization.Localize("gui.item", "Item"), ImGuiTableColumnFlags.WidthStretch);
					ImGui.TableHeadersRow();

					var sort = ImGui.TableGetSortSpecs();

					var entries = CachedSet.MateriaCount
						.Where(raw => raw.Key != 0)
						.Select(entry => {
							return (
								Data.ItemSheet.GetRow(entry.Key),
								entry.Value
							);
						})
						.Where(row => row.Item1 != null);

					if (CachedSet.EmptyMeldSlots > 0) {
						var list = entries.ToList();
						list.Add((null, CachedSet.EmptyMeldSlots));
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
						ExtendedItem? item = entry.Item1;
						ImGui.TableNextRow();

						TextureWrap? icon = GetIcon(item);
						int height = icon == null ? 0 : Math.Min(icon.Height, (int) (32 * scale));

						ImGui.TableSetColumnIndex(0);

						if (icon != null)
							ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (height - ImGui.GetFontSize()) / 2);

						ImGui.Text($" {entry.Value}");

						ImGui.TableSetColumnIndex(1);

						if (icon != null) {
							ImGui.Image(icon.ImGuiHandle, new Vector2(height, height));
							if (ImGui.IsItemClicked())
								Ui.Plugin.ChatGui.LinkItem(item, false);
							ImGui.SameLine();
							ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (height - ImGui.GetFontSize()) / 2);
						}

						if (item == null)
							ImGui.TextColored(ImGuiColors.ParsedGrey, Localization.Localize("gui.empty-slots", "(Empty Slots)"));
						else {
							ImGui.Text(item.Name);
							if (ImGui.IsItemClicked())
								Ui.Plugin.ChatGui.LinkItem(item, false);
						}
					}
					ImGui.EndTable();
				}
			}

			if (ImGui.CollapsingHeader(Localization.Localize("gui.items", "Items"))) {
				bool first = true;

				for (int i = 0; i < CachedSet.Items.Count; i++) {
					MeldedItem rawItem = CachedSet.Items[i];
					Dictionary<uint, StatData> stats = CachedSet.ItemAttributes[i];
					ExtendedItem? item = rawItem.Row();
					if (item is null)
						continue;

					if (first)
						first = false;
					else {
						ImGui.Spacing();
						ImGui.Separator();
						ImGui.Spacing();
					}

					TextureWrap? icon = GetIcon(item, rawItem.HighQuality);
					if (icon != null) {
						int height = Math.Min(icon.Height, (int) (32 * scale));

						ImGui.Image(icon.ImGuiHandle, new Vector2(height, height));
						if (ImGui.IsItemClicked())
							Ui.Plugin.ChatGui.LinkItem(item, rawItem.HighQuality);

						ImGui.SameLine();
						ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (height - ImGui.GetFontSize()) / 2);
					}

					ImGui.Text(item.Name);
					if (ImGui.IsItemClicked())
						Ui.Plugin.ChatGui.LinkItem(item, rawItem.HighQuality);

					if (stats is not null && stats.Count > 0)
						DrawStatTable(stats.Values, CachedSet.Params, false, true);
				}
			}
		}
		ImGui.End();

		if (visible != oldVisible)
			OnVisibleChange();
	}

	internal static void DrawCalculatedTable(IEnumerable<CalculatedStat> calculated) {
		ImGui.BeginTable("CalcTable", 2, ImGuiTableFlags.RowBg);

		ImGui.TableSetupColumn(Localization.Localize("gui.name", "Name"), ImGuiTableColumnFlags.WidthStretch, 1f);
		ImGui.TableSetupColumn(Localization.Localize("gui.value", "Value"), ImGuiTableColumnFlags.None, 1f);

		ImGui.TableHeadersRow();

		foreach (var entry in calculated) {
			ImGui.TableNextRow();

			ImGui.TableNextColumn();
			ImGui.Text(Localization.Localize(entry.Key, entry.Label));

			ImGui.TableNextColumn();
			ImGui.Text(entry.Value.Replace("%", "%%"));
		}

		ImGui.EndTable();
	}

	internal static void DrawStatTable(IEnumerable<StatData> stats, Dictionary<uint, ExtendedBaseParam> paramDictionary, bool includeBase = false, bool includeRemaining = false, bool includeTiers = false, bool includeFood = false) {
		int cols = 8;
		if (includeBase)
			cols += 2;
		if (includeRemaining)
			cols += 2;
		if (includeFood)
			cols += 2;
		if (includeTiers)
			cols += 3;

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

		if (includeFood) {
			ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 10f);
			ImGui.TableSetupColumn("Food", ImGuiTableColumnFlags.None, 1f);
		}

		ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 10f);
		ImGui.TableSetupColumn("Total", ImGuiTableColumnFlags.None, 1f);

		if (includeTiers) {
			ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 10f);
			ImGui.TableSetupColumn("Prev", ImGuiTableColumnFlags.None, 1f);
			ImGui.TableSetupColumn("Next", ImGuiTableColumnFlags.None, 1f);
		}

		if (includeRemaining) {
			ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 10f);
			ImGui.TableSetupColumn("Cap", ImGuiTableColumnFlags.None, 1f);
		}

		ImGui.TableHeadersRow();

		var data = stats
			.Where(stat => paramDictionary.ContainsKey(stat.ID))
			.Select<StatData, (StatData, ExtendedBaseParam)>(stat => (stat, paramDictionary[stat.ID]))
			.OrderBy(entry => entry.Item2.OrderPriority);

		foreach (var entry in data) {
			var stat = entry.Item1;
			var param = entry.Item2;

			ImGui.TableNextRow();

			ImGui.TableNextColumn();
			ImGui.Text(param.Name);

			if (ImGui.IsItemHovered()) {
				//if (string.IsNullOrEmpty(param.Description))
					ImGui.SetTooltip(param.Name);
				/*else
					ImGui.SetTooltip($"{param.Name}\n\n{param.Description}");*/
			}

			if (includeBase) {
				ImGui.TableNextColumn();
				if (stat.Base > 0)
					ImGui.TextColored(ImGuiColors.DalamudGrey, stat.Base.ToString());
				else
					ImGui.TextColored(ImGuiColors.ParsedGrey, stat.Base.ToString());

				if (ImGui.IsItemHovered())
					ImGui.SetTooltip(Localization.Localize("attr.base", "The base attribute points, before any gear is taken into account."));

				ImGui.TableNextColumn();
				ImGui.TextColored(ImGuiColors.DalamudGrey3, "+");
			}

			ImGui.TableNextColumn();
			ImGui.Text(stat.Gear.ToString());

			if (ImGui.IsItemHovered())
				ImGui.SetTooltip(Localization.Localize("attr.gear", "The attribute points added by gear, not counting materia."));

			ImGui.TableNextColumn();
			ImGui.TextColored(ImGuiColors.DalamudGrey3, "+");

			ImGui.TableNextColumn();
			if (stat.Delta > 0)
				ImGui.TextColored(ImGuiColors.ParsedGreen, stat.Delta.ToString());
			else
				ImGui.TextColored(ImGuiColors.DalamudGrey, stat.Delta.ToString());

			if (ImGui.IsItemHovered())
				ImGui.SetTooltip(Localization.Localize("attr.delta", "The attribute points added by melded materia."));

			ImGui.TableNextColumn();
			ImGui.TextColored(ImGuiColors.DalamudGrey3, "-");

			ImGui.TableNextColumn();
			if (stat.Waste > 0)
				ImGui.TextColored(ImGuiColors.DalamudRed, stat.Waste.ToString());
			else
				ImGui.TextColored(ImGuiColors.ParsedGrey, stat.Waste.ToString());

			if (ImGui.IsItemHovered())
				ImGui.SetTooltip(Localization.Localize("attr.waste", "The wasted attribute points from materia melded past an item's limits."));

			if (includeFood) {
				ImGui.TableNextColumn();
				ImGui.TextColored(ImGuiColors.DalamudGrey3, "+");

				ImGui.TableNextColumn();
				if (stat.Food > 0)
					ImGui.TextColored(ImGuiColors.ParsedGreen, stat.Food.ToString());
				else
					ImGui.TextColored(ImGuiColors.DalamudGrey, stat.Food.ToString());

				if (ImGui.IsItemHovered())
					ImGui.SetTooltip(Localization.Localize("attr.food", "The attribute points gained from food."));
			}

			ImGui.TableNextColumn();
			ImGui.TextColored(ImGuiColors.DalamudGrey3, "=");

			ImGui.TableNextColumn();
			ImGui.Text(stat.Value.ToString());

			if (ImGui.IsItemHovered())
				ImGui.SetTooltip(Localization.Localize("attr.value", "The final attribute points when you sum everything up."));

			if (includeTiers) {
				bool hasTier = stat.PreviousTier != 0 || stat.NextTier != 0;

				ImGui.TableNextColumn();
				ImGui.TableNextColumn();
				if (hasTier) {
					if (stat.PreviousTier == 0)
						ImGui.TextColored(ImGuiColors.ParsedGreen, stat.PreviousTier.ToString());
					else
						ImGui.TextColored(ImGuiColors.DalamudGrey, stat.PreviousTier.ToString());

					if (ImGui.IsItemHovered())
						ImGui.SetTooltip(Localization.Localize("attr.prev-tier", "The number of attribute points since the previous tier."));

				}

				ImGui.TableNextColumn();
				if (hasTier) {
					ImGui.TextColored(ImGuiColors.DalamudGrey, stat.NextTier.ToString());

					if (ImGui.IsItemHovered())
						ImGui.SetTooltip(Localization.Localize("attr.next-tier", "The number of attribute points to the next tier."));
				}
			}

			if (includeRemaining) {
				ImGui.TableNextColumn();
				ImGui.TableNextColumn();
				int remaining = stat.Remaining;
				if (remaining <= 0)
					ImGui.TextColored(ImGuiColors.ParsedGreen, stat.Remaining.ToString());
				else
					ImGui.TextColored(ImGuiColors.DalamudYellow, stat.Remaining.ToString());

				if (ImGui.IsItemHovered())
					ImGui.SetTooltip(Localization.Localize("attr.remaining", "The number of attribute points remaining until the item's limits are reached."));
			}
		}

		ImGui.EndTable();
	}

	#endregion

}
