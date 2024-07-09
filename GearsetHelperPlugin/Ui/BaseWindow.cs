using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

using Dalamud;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;

using FFXIVClientStructs.FFXIV.Client.Game;

using GearsetHelperPlugin.Models;
using GearsetHelperPlugin.Sheets;

using ImGuiNET;

using Lumina.Excel.GeneratedSheets;

using DStatus = Dalamud.Game.ClientState.Statuses.Status;

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

	protected EquipmentSet? CachedSet;
	protected Food? SelectedFood;
	protected Food? SelectedMedicine;

	private Tuple<string, List<Food>>? FoodFiltered = null;
	private string FoodFilter = string.Empty;
	private bool FoodFocused = false;

	private Tuple<string, List<Food>>? MedicineFiltered = null;
	private string MedicineFilter = string.Empty;
	private bool MedicineFocused = false;

	protected byte SelectedLevelSync = 100;
	protected uint SelectedIlvlSync = 795;

	protected uint SelectedGroupBonus = 0;

	private float WidestFood = 0;

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

		CachedSet = null;
		SelectedFood = null;
		SelectedMedicine = null;

		ExportTask?.Dispose();
	}

	public virtual void Dispose(bool disposing) {

	}

	protected virtual void OnVisibleChange() { }

	protected virtual Vector2? GetButtonPosition(float width, float height) { return null; }

	#region Calculate Party Bonus

	protected void RecalculatePartyBonus(IPlayerCharacter player) {
		if (!Ui.Plugin.Config.CharacterAutoPartyBonus)
			return;

		HashSet<string> roles = [];

		bool encountered = false;
		int members = 0;

		foreach (var member in Ui.Plugin.PartyList) {
			//Ui.Plugin.Logger.Debug($"Party Member: {member.Name} {member}");
			if (member.ClassJob.GameData is null)
				continue;

			if (member.GameObject != null && player.EntityId == member.GameObject.EntityId)
				encountered = true;

			members++;

			var job = member.ClassJob.GameData;
			if (job.IsTank())
				roles.Add("tank");
			else if (job.IsHealer())
				roles.Add("healer");
			else if (job.IsMelee())
				roles.Add("melee");
			else if (job.IsPhysicalRanged())
				roles.Add("physranged");
			else if (job.IsMagicalRanged())
				roles.Add("magranged");
			/*else
				Ui.Plugin.Logger.Debug($"What job is {member.Name}? {member.ClassJob.Id}");*/
		}

		//string entries = string.Join(", ", roles);
		//Ui.Plugin.Logger.Debug($"Enc: {encountered}, count: {members}, Roles: {entries}");

		uint bonus;

		if (!encountered || members < 2)
			bonus = 0;
		else
			bonus = (uint) roles.Count;

		if (SelectedGroupBonus == bonus)
			return;

		SelectedGroupBonus = bonus;
		if (CachedSet is not null && CachedSet.UpdateGroupBonus(SelectedGroupBonus))
			CachedSet.Recalculate();
	}

	#endregion

	#region Icons

	protected ISharedImmediateTexture? GetIcon(Item? item, bool hq = false) {
		if (item is not null)
			return GetIcon(item.Icon, hq);
		return null;
	}

	protected ISharedImmediateTexture GetIcon(uint id, bool hq = false) {
		return Ui.Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(id, hq));
	}

	#endregion

	#region Equipment Set Creation

	protected virtual bool HasEquipment() {
		return true;
	}

	protected abstract unsafe InventoryContainer* GetInventoryContainer();

	protected virtual void UpdatePlayerData(EquipmentSet set) {
		var player = GetActor();
		if (player is null)
			return;

		set.UpdatePlayer(
			name: player.Name.ToString(),
			race: player.Customize[(int) CustomizeIndex.Race],
			gender: player.Customize[(int) CustomizeIndex.Gender],
			tribe: player.Customize[(int) CustomizeIndex.Tribe],
			level: player.Level
		);

		// Check our food, and maybe select it.
		if (SelectedFood is null && Ui.Plugin.Config.CharacterAutoFood)
			UpdateFoodData(set, player, false);
	}

	protected virtual void UpdateFoodData(EquipmentSet? set = null, IPlayerCharacter? player = null, bool update = true) {
		set ??= CachedSet;
		if (set is null)
			return;

		player ??= GetActor();
		if (player is null)
			return;

		var statuses = GetStatuses(player);
		if (statuses is not null)
			foreach (var status in statuses) {
				if (status.StatusId == 48) {
					bool hq = false;
					uint foodId = status.Param;
					if (foodId >= 10000) {
						hq = true;
						foodId -= 10000;
					}

					set.UpdateFood(foodId, hq, update);
					SelectedFood = set.Food;
					break;
				}
			}
	}

	private static DStatus[]? GetStatuses(IPlayerCharacter? player) {
		if (player is null)
			return null;

		var list = player.StatusList;
		if (list is null)
			return null;

		int count = 0;
		for (int i = 0; i < list.Length; i++) {
			var status = list[i];
			if (status is not null && status.StatusId != 0)
				count++;
		}

		DStatus[] result = new DStatus[count];
		int j = 0;
		for (int i = 0; i < list.Length; i++) {
			var status = list[i];
			if (status is not null && status.StatusId != 0)
				result[j++] = status;
		}

		return result;
	}

	protected abstract IPlayerCharacter? GetActor();

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

			for (uint i = 0; i < inventory->Size; i++) {
				var item = inventory->Items[i];
				uint id = item.ItemId;
				if (id == 0)
					continue;

				if (idx >= CachedSet.Items.Count) {
					match = false;
					break;
				}

				MeldedItem mitem = CachedSet.Items[idx];
				if (mitem.ID != id) {
					match = false;
					break;
				}

				if (item.Flags.HasFlag(InventoryItem.ItemFlags.HighQuality) != mitem.HighQuality) {
					match = false;
					break;
				}

				for (int j = 0; j < 5; j++) {
					var mat = mitem.Melds[j];
					if (mat.ID != item.Materia[j] && mat.Grade != item.MateriaGrades[j]) {
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

		result.Level = 100;

		UpdatePlayerData(result);

		var player = GetActor();
		if (player is not null)
			RecalculatePartyBonus(player);

		result.UpdateSync(SelectedLevelSync, SelectedIlvlSync);
		result.UpdateGroupBonus(SelectedGroupBonus);

		result.Food = SelectedFood;
		result.Medicine = SelectedMedicine;

		result.Recalculate();

		sw.Stop();

		Ui.Plugin.Logger.Debug($"Processed equipment in {sw.ElapsedMilliseconds}ms");

		return result;
	}

	protected unsafe List<MeldedItem>? GetItems() {
		InventoryContainer* inventory = GetInventoryContainer();
		if (inventory == null)
			return null;

		List<MeldedItem> result = [];

		for (uint i = 0; i < inventory->Size; i++) {
			var item = inventory->Items[i];
			uint id = item.ItemId;
			if (id == 0)
				continue;

			result.Add(new MeldedItem(
				ID: id,
				HighQuality: item.Flags.HasFlag(InventoryItem.ItemFlags.HighQuality),
				Melds: new MeldedMateria[] {
					new(item.Materia[0], item.MateriaGrades[0]),
					new(item.Materia[1], item.MateriaGrades[1]),
					new(item.Materia[2], item.MateriaGrades[2]),
					new(item.Materia[3], item.MateriaGrades[3]),
					new(item.Materia[4], item.MateriaGrades[4]),
				}
			));
		}

		return result;
	}

	#endregion

	#region Drawing

	private Tuple<string, List<Food>>? FilterFood(List<Food> choices, string filter, Tuple<string, List<Food>>? oldFiltered) {
		if (oldFiltered is not null && oldFiltered.Item1 == filter)
			return oldFiltered;

		if (string.IsNullOrWhiteSpace(filter))
			return null;

		return new(filter, choices.Where(food => {
			ExtendedItem? item = food.ItemRow();
			if (item is null)
				return false;

			return item.Name.ToString().IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) != -1;

		}).ToList());
	}

	protected bool DrawFoodCombo(string label, Food? selected, List<Food> choices, out Food? choice, ref Tuple<string, List<Food>>? filtered, ref string filter, ref bool focused) {
		bool result = false;
		choice = null;

		ExtendedItem? selitem = selected?.ItemRow();

		string curLabel;
		string noneLabel = Localization.Localize("gui.food.none", "(None)");
		if (selitem is not null)
			curLabel = selected!.HQ ? $"{selitem.Name} {(char) SeIconChar.HighQuality}" : selitem.Name;
		else
			curLabel = noneLabel;

		if (ImGui.BeginCombo(label, curLabel, ImGuiComboFlags.None | ImGuiComboFlags.HeightLargest)) {
			ImGui.SetNextItemWidth(-1);
			ImGui.InputTextWithHint($"##{label}#FoodFilter", "Filter", ref filter, 60);
			if (!focused)
				ImGui.SetKeyboardFocusHere();

			float lineHeight = 20 * ImGui.GetIO().FontGlobalScale;
			WidestFood = Math.Max(WidestFood, ImGui.GetWindowContentRegionMin().X);

			ImGui.BeginChild($"###{label}#FoodDisplay", new Vector2(WidestFood, lineHeight * 10), true);
			if (!focused) {
				ImGui.SetScrollY(0);
				focused = true;
			}

			float scroll = ImGui.GetScrollY();
			float padX = ImGui.GetStyle().FramePadding.X;

			Vector2 size = new(ImGui.GetWindowContentRegionMax().X, 2 * lineHeight);

			// Filter stuff
			filtered = FilterFood(choices, filter, filtered);
			List<Food> visible = filtered?.Item2 ?? choices;

			// Now skip off-screen entries.
			int start = (int) Math.Floor(scroll / size.Y);

			if (start > visible.Count)
				start = visible.Count - 4;
			if (start < 0)
				start = 0;

			int end = start + 5;

			if (end > visible.Count)
				end = visible.Count;

			if (start > 0)
				ImGui.Dummy(new Vector2(size.X, size.Y * start));

			// None Element
			if (start == 0) {
				bool none_selected = selected is null;
				ImGui.PushID($"food#none");
				var oldPos = ImGui.GetCursorPos();
				if (ImGui.Selectable("", none_selected, ImGuiSelectableFlags.None, size)) {
					choice = null;
					result = true;
					ImGui.CloseCurrentPopup();
				}
				ImGui.PopID();
				if (none_selected)
					ImGui.SetItemDefaultFocus();

				var newPos = ImGui.GetCursorPos();
				ImGui.SetCursorPosX(oldPos.X + (size.Y - lineHeight) / 2);
				ImGui.SetCursorPosY(oldPos.Y + (size.Y - lineHeight) / 2);

				ImGui.Text(noneLabel);
				ImGui.SetCursorPos(newPos);
			}

			if (start < 1)
				start = 1;

			for (int i = start - 1; i < end; i++) {
				Food food = visible[i];
				ExtendedItem? item = food.ItemRow();
				if (item is null)
					continue;

				bool current = selected == food;
				ImGui.PushID($"food#{food.ItemID}-{food.HQ}");
				var oldPos = ImGui.GetCursorPos();

				if (ImGui.Selectable("", current, ImGuiSelectableFlags.None, size)) {
					result = true;
					choice = food;
					ImGui.CloseCurrentPopup();
				}
				ImGui.PopID();
				if (current)
					ImGui.SetItemDefaultFocus();

				var newPos = ImGui.GetCursorPos();
				ImGui.SetCursorPos(oldPos);

				var image = GetIcon(item.Icon, food.HQ).GetWrapOrEmpty();
				int width = 0;
				if (image != null) {
					width = image.Width;
					int height = image.Height;

					if (height > size.Y) {
						float scale = (float) size.Y / height;
						width = (int) (width * scale);
						height = (int) (height * scale);
					}

					ImGui.SetCursorPosX(oldPos.X);
					ImGui.SetCursorPosY(oldPos.Y + (size.Y - height) / 2);
					ImGui.Image(image.ImGuiHandle, new Vector2(width, height));
				}

				ImGui.SetCursorPosX(oldPos.X + width + 2 * padX);
				ImGui.SetCursorPosY(oldPos.Y);
				ImGui.Text(item.Name);
				if (food.HQ) {
					ImGui.SameLine();
					ImGui.Text($"{(char) SeIconChar.HighQuality}");
				}
				ImGui.SameLine();
				ImGui.TextColored(ImGuiColors.DalamudGrey, $"i{item.LevelItem.Row}");

				ImGui.SetCursorPosX(oldPos.X + (image?.Width ?? 0) + 2 * padX);
				ImGui.SetCursorPosY(oldPos.Y + size.Y / 2);

				string? stat = food.StatLine;
				if (stat is not null) {
					ImGui.TextColored(ImGuiColors.DalamudGrey, food.StatLine);
					ImGui.SameLine();
					WidestFood = Math.Max(WidestFood, (image?.Width ?? 0) + 2 * padX + ImGui.CalcTextSize(food.StatLine).X);
				}

				ImGui.SetCursorPos(newPos);
			}

			if (end < choices.Count)
				ImGui.Dummy(new Vector2(size.X, size.Y * (visible.Count - end)));

			ImGui.EndChild();
			//if (!isFocused)
			//	ImGui.CloseCurrentPopup();

			ImGui.EndCombo();
		} else if (focused) {
			focused = false;
			filter = string.Empty;
		}

		return result;
	}

	protected void DrawWindow(bool attach, int side, short x, short y, ushort width) {
		if (!Data.IsFoodLoaded)
			return;

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
					if (ExportResponse.ShowSuccess)
						ImGui.TextColored(ImGuiColors.ParsedGreen, Localization.Localize("gui.export-success", "Export Successful!"));
					if (!string.IsNullOrWhiteSpace(ExportResponse.Instructions))
						ImGui.TextWrapped(ExportResponse.Instructions);

					string url = ExportResponse.Url ?? string.Empty;
					if (!string.IsNullOrWhiteSpace(url)) {
						ImGui.InputText(Localization.Localize("gui.url", "URL"), ref url, (uint) url.Length, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.ReadOnly);

						ImGui.SameLine();
						ImGui.PushID($"copy-to-clipboard#url");
						if (ImGuiComponents.IconButton(FontAwesomeIcon.Clipboard)) {
							ImGui.SetClipboardText(url);

							string msg = Localization.Localize("gui.copied-to-clipboard", "Copied to Clipboard");
							Ui.Plugin.NotificationManager.AddNotification(new() {
								MinimizedText = msg,
								Content = msg,
								Type = Dalamud.Interface.ImGuiNotification.NotificationType.Success
							});
						}
						if (ImGui.IsItemHovered())
							ImGui.SetTooltip(Localization.Localize("gui.copy-to-clipboard", "Copy to Clipboard"));

						ImGui.PopID();

						if (ImGui.Button(Localization.Localize("gui.open-browser", "Open in Browser")))
							Exporter.TryOpenURL(ExportResponse.Url!);
					}

					string json = ExportResponse.Clipboard ?? string.Empty;
					if (!string.IsNullOrWhiteSpace(json)) {
						ImGui.InputTextMultiline(Localization.Localize("gui.json", "JSON"), ref json, (uint) url.Length, Vector2.Zero, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.ReadOnly);

						ImGui.SameLine();
						ImGui.PushID($"copy-to-clipboard#json");
						if (ImGuiComponents.IconButton(FontAwesomeIcon.Clipboard)) {
							ImGui.SetClipboardText(json);

							string msg = Localization.Localize("gui.copied-to-clipboard", "Copied to Clipboard");
							Ui.Plugin.NotificationManager.AddNotification(new() {
								MinimizedText = msg,
								Content = msg,
								Type = Dalamud.Interface.ImGuiNotification.NotificationType.Success
							});
						}
						if (ImGui.IsItemHovered())
							ImGui.SetTooltip(Localization.Localize("gui.copy-to-clipboard", "Copy to Clipboard"));

						ImGui.PopID();
					}

					//ImGui.SameLine();
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

				ImGui.SameLine();
				if (ImGui.Button("XivGear"))
					ExportTask = Ui.Plugin.Exporter.ExportXivGear(CachedSet);

				ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - 32);

				ImGui.PushID($"opensettings");
				if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog))
					Ui.OpenConfig();
				if (ImGui.IsItemHovered())
					ImGui.SetTooltip(Localization.Localize("gui.settings", "Gearset Helper Settings"));
				ImGui.PopID();
			}

			if (ImGui.CollapsingHeader(Localization.Localize("gui.food-sync", "Food / Sync Down"), ImGuiTreeNodeFlags.None)) {
				int levelsync = SelectedLevelSync;
				if (ImGui.InputInt(Localization.Localize("gui.level-sync", "Level Sync"), ref levelsync, 1, 10)) {
					SelectedLevelSync = (byte) Math.Clamp(levelsync, 1, 100);
					if (CachedSet.UpdateSync(SelectedLevelSync, 0))
						CachedSet.Recalculate();
					SelectedIlvlSync = CachedSet.ILvlSync;
				}

				ImGui.SameLine();
				ImGuiComponents.HelpMarker(Localization.Localize("gui.about-level-sync", "Simulate stats at a specific synced level or item level. Melded materia do not apply when synced down."));

				int ilvlsync = (int) (SelectedIlvlSync == 0 ? CachedSet.ILvlSync : SelectedIlvlSync);
				if (ImGui.InputInt(Localization.Localize("gui.ilvl-sync", "Item Level Sync"), ref ilvlsync, 5, 10)) {
					SelectedIlvlSync = (uint) Math.Clamp(ilvlsync, 5, 795);
					if (CachedSet.UpdateSync(SelectedLevelSync, SelectedIlvlSync))
						CachedSet.Recalculate();
				}

				int gbonus = (int) SelectedGroupBonus;
				if (ImGui.InputInt(Localization.Localize("gui.group-bonus", "Party Bonus %"), ref gbonus, 1, 5)) {
					SelectedGroupBonus = (uint) Math.Clamp(gbonus, 0, 5);
					if (CachedSet.UpdateGroupBonus(SelectedGroupBonus))
						CachedSet.Recalculate();
				}

				if (SelectedFood is not null || CachedSet.RelevantFood.Count > 0) {
					if (DrawFoodCombo(
						Localization.Localize("gui.food", "Food"),
						SelectedFood,
						CachedSet.RelevantFood,
						out Food? choice,
						ref FoodFiltered,
						ref FoodFilter,
						ref FoodFocused
					)) {
						SelectedFood = choice;
						CachedSet.UpdateFood(choice);
					}

					if (SelectedFood is not null) {
						ImGui.SameLine();
						ImGui.PushID("food#link");
						if (ImGuiComponents.IconButton(FontAwesomeIcon.Link))
							Ui.Plugin.ChatGui.LinkItem(SelectedFood.ItemRow(), SelectedFood.HQ);
						if (ImGui.IsItemHovered())
							ImGui.SetTooltip(Localization.Localize("gui.link-item", "Link Item in Chat"));
						ImGui.PopID();

						ImGui.SameLine();
						ImGui.PushID("food#clear");
						if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash)) {
							SelectedFood = null;
							CachedSet.UpdateFood(null);
						}
						if (ImGui.IsItemHovered())
							ImGui.SetTooltip(Localization.Localize("gui.remove-this", "Remove"));
						ImGui.PopID();
					}
				}

				if (SelectedMedicine is not null || CachedSet.RelevantMedicine.Count > 0) {
					if (DrawFoodCombo(
						Localization.Localize("gui.medicine", "Medicine"),
						SelectedMedicine,
						CachedSet.RelevantMedicine,
						out Food? choice,
						ref MedicineFiltered,
						ref MedicineFilter,
						ref MedicineFocused
					)) {
						SelectedMedicine = choice;
						CachedSet.UpdateMedicine(choice);
					}

					if (SelectedMedicine is not null) {
						ImGui.SameLine();
						ImGui.PushID("medicine#link");
						if (ImGuiComponents.IconButton(FontAwesomeIcon.Link))
							Ui.Plugin.ChatGui.LinkItem(SelectedMedicine.ItemRow(), SelectedMedicine.HQ);
						if (ImGui.IsItemHovered())
							ImGui.SetTooltip(Localization.Localize("gui.link-item", "Link Item in Chat"));
						ImGui.PopID();

						ImGui.SameLine();
						ImGui.PushID("medicine#clear");
						if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash)) {
							SelectedMedicine = null;
							CachedSet.UpdateMedicine(null);
						}
						if (ImGui.IsItemHovered())
							ImGui.SetTooltip(Localization.Localize("gui.remove-this", "Remove"));
						ImGui.PopID();
					}
				}
			}

			if (ImGui.CollapsingHeader(Localization.Localize("gui.attributes", "Attributes"), ImGuiTreeNodeFlags.DefaultOpen)) {
				DrawStatTable(CachedSet.Attributes.Values, CachedSet.Params, true, includeTiers: true, includeFood: (CachedSet.Food is not null || CachedSet.Medicine is not null), includeBonus: SelectedGroupBonus != 0, gcd: CachedSet.GCD);
			}

			if (CachedSet.DamageValues.Count > 0 && ImGui.CollapsingHeader(Localization.Localize("gui.damage", "Estimated Damage"), ImGuiTreeNodeFlags.None)) {
				DrawDamageTable(CachedSet.DamageValues);
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

						var icon = GetIcon(item)?.GetWrapOrEmpty();
						int height = icon == null ? 0 : Math.Min(icon.Height, (int) (32 * scale));

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

						if (item == null)
							ImGui.TextColored(ImGuiColors.ParsedGrey, Localization.Localize("gui.empty-slots", "(Empty Slots)"));
						else {
							ImGui.Text(item.Name);

							ImGui.SameLine(ImGui.GetColumnWidth() - 30);
							ImGui.PushID($"materia#link#{item.RowId}");
							if (ImGuiComponents.IconButton(FontAwesomeIcon.Link))
								Ui.Plugin.ChatGui.LinkItem(item, false);
							if (ImGui.IsItemHovered())
								ImGui.SetTooltip(Localization.Localize("gui.link-item", "Link Item in Chat"));
							ImGui.PopID();
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

					var icon = GetIcon(item, rawItem.HighQuality)?.GetWrapOrEmpty();
					int height = icon is null ? 0 : Math.Min(icon.Height, (int) (32 * scale));
					if (icon != null) {
						ImGui.Image(icon.ImGuiHandle, new Vector2(height, height));
						ImGui.SameLine();
						ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (height - ImGui.GetFontSize()) / 2);
					}

					ImGui.Text(rawItem.HighQuality ? $"{item.Name} {(char) SeIconChar.HighQuality}" : item.Name);

					if (CachedSet.ILvlSync > 0 && item.LevelItem.Row > CachedSet.ILvlSync) {
						ImGui.SameLine();
						ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (height - ImGui.GetFontSize()) / 2);
						ImGui.TextColored(ImGuiColors.ParsedGrey, $"(At i{CachedSet.ILvlSync})");
					} else if (item.ExtendedItemLevel.Value is ExtendedItemLevel level) {
						ImGui.SameLine();
						ImGui.SetCursorPosY(ImGui.GetCursorPosY() + (height - ImGui.GetFontSize()) / 2);
						ImGui.TextColored(ImGuiColors.ParsedGrey, $"(i{level.RowId})");
					}

					ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - 32);

					ImGui.PushID($"item#link#{rawItem.ID}");
					if (ImGuiComponents.IconButton(FontAwesomeIcon.Link))
						Ui.Plugin.ChatGui.LinkItem(item, rawItem.HighQuality);
					if (ImGui.IsItemHovered())
						ImGui.SetTooltip(Localization.Localize("gui.link-item", "Link Item in Chat"));
					ImGui.PopID();

					if (stats is not null && stats.Count > 0)
						DrawStatTable(stats.Values, CachedSet.Params, false, true);
				}
			}
		}
		ImGui.End();

		if (visible != oldVisible)
			OnVisibleChange();
	}

	internal static void DrawOutdatedMathWarning() {
		ImGui.TextWrapped(Localization.Localize("gui.outdated-math", "The formulas used to calculate these values have not been updated for Dawntrail yet, so they may not be accurate."));
	}

	internal static void DrawCalculatedTable(IEnumerable<CalculatedStat> calculated) {
		DrawOutdatedMathWarning();

		ImGui.BeginTable("CalcTable", 2, ImGuiTableFlags.RowBg);

		/*ImGui.TableSetupColumn(Localization.Localize("gui.name", "Name"), ImGuiTableColumnFlags.WidthStretch, 1f);
		ImGui.TableSetupColumn(Localization.Localize("gui.value", "Value"), ImGuiTableColumnFlags.None, 1f);

		ImGui.TableHeadersRow();
		*/

		foreach (var entry in calculated) {
			ImGui.TableNextRow();

			ImGui.TableNextColumn();
			ImGui.Text(Localization.Localize(entry.Key, entry.Label));

			ImGui.TableNextColumn();
			ImGui.Text(entry.Value.Replace("%", "%%"));
		}

		ImGui.EndTable();
	}

	internal static void DrawDamageTable(IEnumerable<KeyValuePair<int, DamageValues>> data) {
		DrawOutdatedMathWarning();

		ImGui.BeginTable("DamageTable", 6, ImGuiTableFlags.Resizable | ImGuiTableFlags.RowBg);

		ImGui.TableNextRow(ImGuiTableRowFlags.Headers);

		ImGui.TableNextColumn();
		ImGui.TableHeader(Localization.Localize("gui.potency", "Potency"));
		if (ImGui.IsItemHovered())
			ImGui.SetTooltip(Localization.Localize("gui.potency.tip", "The potency of the simulated attack."));

		ImGui.TableNextColumn();
		ImGui.TableHeader(Localization.Localize("gui.dmg-average", "Average"));
		if (ImGui.IsItemHovered())
			ImGui.SetTooltip(Localization.Localize("gui.dmg-average.tip", "Your expected average damage, ±5%%. This takes into account your critical hit and direct hit rates."));

		ImGui.TableNextColumn();
		ImGui.TableHeader(Localization.Localize("gui.dmg-base", "Base"));
		if (ImGui.IsItemHovered())
			ImGui.SetTooltip(Localization.Localize("gui.dmg-base.tip", "Your expected normal hit damage, ±5%%. This is for attacks that are not critical hits or direct hits."));

		ImGui.TableNextColumn();
		ImGui.TableHeader(Localization.Localize("gui.dmg-crit", "Crit"));
		if (ImGui.IsItemHovered())
			ImGui.SetTooltip(Localization.Localize("gui.dmg-crit.tip", "Your expected critical hit damage, ±5%%. This is for attacks that are critical hits, but not direct hits."));

		ImGui.TableNextColumn();
		ImGui.TableHeader(Localization.Localize("gui.dmg-dh", "DH"));
		if (ImGui.IsItemHovered())
			ImGui.SetTooltip(Localization.Localize("gui.dmg-dh.tip", "Your expected direct hit damage, ±5%%. This is for attacks that are direct hits, but not critical hits."));


		ImGui.TableNextColumn();
		ImGui.TableHeader(Localization.Localize("gui.dmg-dhc", "DCrit"));
		if (ImGui.IsItemHovered())
			ImGui.SetTooltip(Localization.Localize("gui.dmg-dhc.tip", "Your expected critical direct hit damage, ±5%%. This is for attacks that are critical hits and direct hits."));

		/*
		ImGui.TableSetupColumn(Localization.Localize("gui.potency", "Potency"), ImGuiTableColumnFlags.WidthStretch, 1f);
		ImGui.TableSetupColumn(Localization.Localize("gui.dmg-average", "Average"), ImGuiTableColumnFlags.WidthStretch, 1f);
		ImGui.TableSetupColumn(Localization.Localize("gui.dmg-base", "Base"), ImGuiTableColumnFlags.WidthStretch, 1f);
		ImGui.TableSetupColumn(Localization.Localize("gui.dmg-crit", "Crit"), ImGuiTableColumnFlags.WidthStretch, 1f);
		ImGui.TableSetupColumn(Localization.Localize("gui.dmg-dh", "DH"), ImGuiTableColumnFlags.WidthStretch, 1f);
		ImGui.TableSetupColumn(Localization.Localize("gui.dmg-dhc", "DCrit"), ImGuiTableColumnFlags.WidthStretch, 1f);
		*/

		//ImGui.TableHeadersRow();

		foreach (var entry in data) {
			int key = entry.Key;
			var value = entry.Value;

			ImGui.TableNextRow();

			ImGui.TableNextColumn();
			ImGui.Text(key.ToString());

			ImGui.TableNextColumn();
			ImGui.Text(value.Average.ToString("N0"));

			ImGui.TableNextColumn();
			ImGui.TextColored(ImGuiColors.DalamudGrey, value.Base.ToString("N0"));

			ImGui.TableNextColumn();
			ImGui.Text(value.CriticalHit.ToString("N0"));

			ImGui.TableNextColumn();
			ImGui.TextColored(ImGuiColors.DalamudGrey, value.DirectHit.ToString("N0"));

			ImGui.TableNextColumn();
			ImGui.Text(value.DirectCriticalHit.ToString("N0"));
		}

		ImGui.EndTable();
	}

	internal static void DrawStatTable(
		IEnumerable<StatData> stats,
		Dictionary<uint, ExtendedBaseParam> paramDictionary,
		bool includeBase = false,
		bool includeRemaining = false,
		bool includeTiers = false,
		bool includeFood = false,
		bool includeBonus = false,
		StatData? gcd = null
	) {
		int cols = 8;
		if (includeBase)
			cols += 2;
		if (includeRemaining)
			cols += 2;
		if (includeFood)
			cols += 2;
		if (includeBonus)
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

		if (includeBonus) {
			ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 10f);
			ImGui.TableSetupColumn("Party", ImGuiTableColumnFlags.None, 1f);
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

			if (includeBonus) {
				ImGui.TableNextColumn();
				ImGui.TextColored(ImGuiColors.DalamudGrey3, "+");

				ImGui.TableNextColumn();
				if (stat.GroupBonus > 0)
					ImGui.TextColored(ImGuiColors.ParsedGreen, stat.GroupBonus.ToString());
				else
					ImGui.TextColored(ImGuiColors.DalamudGrey, stat.GroupBonus.ToString());

				if (ImGui.IsItemHovered())
					ImGui.SetTooltip(Localization.Localize("attr.group-bonus", "The attribute points gained from the Party Bonus."));
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

			// GCD Tiering
			if (includeTiers && gcd != null && (param.RowId == (int) Stat.SKS || param.RowId == (int) Stat.SPS)) {
				ImGui.TableNextRow();
				ImGui.TableNextColumn();
				ImGui.Text($"   ... (GCD)");

				if (ImGui.IsItemHovered())
					ImGui.SetTooltip(Localization.Localize("attr.gcd", "This line displays the number of attribute points needed to change your GCD, while the previous line's tiers are for DoT damage."));

				if (includeBase) {
					ImGui.TableNextColumn(); // Base
					ImGui.TableNextColumn(); // +
				}
				ImGui.TableNextColumn(); // Gear
				ImGui.TableNextColumn(); // +
				ImGui.TableNextColumn(); // Delta
				ImGui.TableNextColumn(); // -
				ImGui.TableNextColumn(); // Waste
				if (includeFood) {
					ImGui.TableNextColumn(); // +
					ImGui.TableNextColumn(); // Food
				}
				if (includeBonus) {
					ImGui.TableNextColumn(); // +
					ImGui.TableNextColumn(); // Party
				}
				ImGui.TableNextColumn(); // =
				ImGui.TableNextColumn(); // Total
				ImGui.TableNextColumn(); // (space)

				ImGui.TableNextColumn(); // Prev
				if (gcd.PreviousTier == 0)
					ImGui.TextColored(ImGuiColors.ParsedGreen, gcd.PreviousTier.ToString());
				else
					ImGui.TextColored(ImGuiColors.DalamudGrey, gcd.PreviousTier.ToString());

				if (ImGui.IsItemHovered())
					ImGui.SetTooltip(Localization.Localize("attr.prev-tier", "The number of attribute points since the previous tier."));

				ImGui.TableNextColumn(); // Next
				ImGui.TextColored(ImGuiColors.DalamudGrey, gcd.NextTier.ToString());

				if (ImGui.IsItemHovered())
					ImGui.SetTooltip(Localization.Localize("attr.next-tier", "The number of attribute points to the next tier."));
			}

		}

		ImGui.EndTable();
	}

	#endregion

}
