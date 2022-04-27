using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

using Dalamud.Logging;

using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using GearsetHelperPlugin.Sheets;

namespace GearsetHelperPlugin.Models;

internal class EquipmentSet {

	#region Constructor

	public EquipmentSet(List<MeldedItem>? items = null) {
		Items = items ?? new();

		for (int i = 0; i < Items.Count; i++)
			ItemAttributes.Add(new());
	}

	#endregion

	#region Properties

	#region Player Data

	public string? PlayerName { get; set; }
	public byte Gender { get; set; }
	public byte Race { get; set; }
	public byte Tribe { get; set; }

	public byte Level { get; set; }
	public uint Class { get; set; }
	public uint EffectiveClass { get; set; }

	#endregion

	#region Gear Data

	public byte EffectiveLevel => LevelSync != 0 && LevelSync < Level ? LevelSync : Level;

	public byte LevelSync { get; set; }

	public uint ILvlSync { get; set; }

	/// <summary>
	/// Food to apply to the stats.
	/// </summary>
	public Food? Food { get; set; }

	/// <summary>
	/// Medicine to apply to the stats.
	/// </summary>
	public Food? Medicine { get; set; }


	/// <summary>
	/// Whether or not this equipment set includes a soul crystal.
	/// </summary>
	public bool HasCrystal { get; set; }

	/// <summary>
	/// Whether or not this equipment set includes an off-hand.
	/// </summary>
	public bool HasOffhand { get; set; }

	/// <summary>
	/// The average item level of this equipment set.
	/// </summary>
	public ushort ItemLevel { get; set; }

	/// <summary>
	/// The items included in this equipment set.
	/// </summary>
	public List<MeldedItem> Items { get; }

	#endregion

	#region Attributes

	public Dictionary<uint, ExtendedBaseParam> Params { get; } = new();
	public Dictionary<uint, StatData> Attributes { get; } = new();
	public List<Dictionary<uint, StatData>> ItemAttributes { get; } = new();

	#endregion

	#region Calculated Stats

	public List<CalculatedStat> Calculated { get; } = new();

	#endregion

	#region Relevant Foods

	public List<Food> RelevantFood { get; } = new();

	public List<Food> RelevantMedicine { get; } = new();

	#endregion

	#region Materia Caching

	public int EmptyMeldSlots { get; set; }

	public Dictionary<uint, int> MateriaCount { get; } = new();

	#endregion

	#region Sheet Helpers

	public ParamGrow? GrowRow() {
		if (!Data.CheckSheets() || Level == 0)
			return null;

		return Data.GrowSheet.GetRow(EffectiveLevel);
	}

	public Race? RaceRow() {
		if (!Data.CheckSheets() || Race == 0)
			return null;
		return Data.RaceSheet.GetRow(Race);
	}

	public Tribe? TribeRow() {
		if (!Data.CheckSheets() || Tribe == 0)
			return null;
		return Data.TribeSheet.GetRow(Tribe);
	}

	public ClassJob? JobRow() {
		if (!Data.CheckSheets() || Class == 0)
			return null;
		return Data.ClassSheet.GetRow(Class);
	}

	public ClassJob? EffectiveJobRow() {
		if (!Data.CheckSheets() || EffectiveClass == 0)
			return null;
		return Data.ClassSheet.GetRow(EffectiveClass);
	}

	#endregion

	#endregion

	#region Update Methods

	/// <summary>
	/// Update the player data for this equipment set. This method sets
	/// <see cref="PlayerName"/>, <see cref="Level"/>, <see cref="Race"/>,
	/// <see cref="Gender"/>, and <see cref="Tribe"/>.
	/// </summary>
	/// <param name="name"></param>
	/// <param name="gender"></param>
	/// <param name="race"></param>
	/// <param name="tribe"></param>
	/// <param name="level"></param>
	/// <returns>Whether or not the value of <see cref="Level"/> or
	/// <see cref="Tribe"/> have changed.</returns>
	internal bool UpdatePlayer(string? name, byte gender, byte race, byte tribe, byte level) {
		PlayerName = name;

		// Ensure that the Level is accurate
		foreach(var item in Items) {
			var data = item.Row();
			if (data is not null && data.LevelEquip > level)
				level = data.LevelEquip;
		}

		bool changed = Tribe != tribe || Level != level;

		Gender = gender;
		Race = race;
		Tribe = tribe;
		Level = level;

		return changed;
	}

	internal bool UpdateSync(byte levelSync, uint ilvlSync) {
		if (ilvlSync == 0 && Data.CheckSheets()) {
			byte elevel = levelSync < Level ? levelSync : Level;
			if (elevel >= 90)
				ilvlSync = 665;
			else {
				ParamGrow? growth = Data.GrowSheet.GetRow(levelSync);
				if (growth is not null)
					ilvlSync = growth.ItemLevelSync;
			}
		}

		bool changed = levelSync != LevelSync && ilvlSync != ILvlSync;

		LevelSync = levelSync;
		ILvlSync = ilvlSync;

		return changed;
	}

	/// <summary>
	/// Scan the items in this equipment set and use the mainhand and job
	/// crystal to determine which class the character is. This method
	/// sets <see cref="HasCrystal"/>, <see cref="Class"/>, and
	/// <see cref="EffectiveClass"/>.
	/// </summary>
	/// <returns>Whether or not the values have changed as a result of
	/// this calculation.</returns>
	internal bool CalculateClass() {
		if (!Data.CheckSheets())
			return false;

		bool OldHasCrystal = HasCrystal;
		bool OldHasOffhand = HasOffhand;
		uint OldClass = Class;
		uint OldEffectiveClass = EffectiveClass;

		HasCrystal = false;
		HasOffhand = false;
		Class = 0;
		EffectiveClass = 0;

		ExtendedClassJobCategory? category = null;

		for(int i = 0; i < Items.Count; i++) {
			MeldedItem item = Items[i];
			ExtendedItem? data = item.Row();
			if (data is null)
				continue;

			EquipSlotCategory? slot = data.EquipSlotCategory.Value;
			if (slot != null) {
				if (slot.MainHand == 1)
					category = data.ExtendedClassJobCategory.Value;
				if (slot.OffHand == 1) {
					PluginLog.Log($"Off-hand: {data.Name}");
					HasOffhand = true;
				}
				if (slot.SoulCrystal == 1)
					HasCrystal = true;
			}
		}

		// This should never really happen, but just to be safe, let's
		// be safe about it.
		if (category != null) {
			ClassJob? job = null;

			// There is probably a better way to handle this but we don't
			// calculate this frequently, so who cares?

			// First, search for a job specifically. No classes.
			foreach (var row in Data.ClassSheet) {
				if (row.JobIndex == 0)
					continue;

				if (category.Classes[row.RowId]) {
					job = row;
					break;
				}
			}

			// Widen the search to ANY class if we didn't find a job, which
			// should only happen in the case of crafters and gatherers.
			if (job == null) {
				foreach (var row in Data.ClassSheet) {
					if (category.Classes[row.RowId]) {
						job = row;
						break;
					}
				}
			}

			// Now, if we found a job, save it.
			if (job != null) {
				Class = job.RowId;
				EffectiveClass = job.RowId;

				// If we don't have a soul crystal, and this job has
				// a parent job, then switch it up for stat calculation
				// but leave the class set on gearset for exporting
				// correctly.
				if (!HasCrystal && job.ClassJobParent.Row != 0)
					EffectiveClass = job.ClassJobParent.Row;
			}
		}

		// Did anything change?
		return OldHasOffhand != HasOffhand || OldHasCrystal != HasCrystal || OldClass != Class || OldEffectiveClass != EffectiveClass;
	}

	public void CalculateRelevantFood() {
		RelevantFood.Clear();

		Dictionary<Food, int> matches = new();
		Dictionary<Food, int> level = new();

		bool dohl = false;
		ClassJob? job = JobRow();
		if (job is not null)
			dohl = job.Role == 0;

		foreach (var food in Data.Food) {
			uint ilvl = food.ILvl;
			if (ilvl < (dohl ? Data.FoodMinIlvlDoHL : Data.FoodMinIlvl))
				continue;

			bool match = false;
			matches[food] = 0;

			foreach (var stat in food.Stats) {
				// Don't count vitality.
				if (stat.Key == (int) Stat.VIT)
					continue;

				if (Attributes.ContainsKey(stat.Key)) {
					matches[food]++;
					match = true;
				}
			}

			if (match)
				RelevantFood.Add(food);
		}

		RelevantFood.Sort((a, b) => {
			int value = a.ILvl.CompareTo(b.ILvl);
			if (value != 0)
				return -value;

			matches.TryGetValue(a, out int aMatches);
			matches.TryGetValue(b, out int bMatches);

			value = aMatches.CompareTo(bMatches);
			if (value != 0)
				return -value;

			return 0;
		});
	}

	public void CalculateRelevantMedicine() {
		RelevantMedicine.Clear();

		Dictionary<Food, int> matches = new();
		Dictionary<Food, int> level = new();

		bool dohl = false;
		ClassJob? job = JobRow();
		if (job is not null)
			dohl = job.Role == 0;

		foreach (var food in Data.Medicine) {
			uint ilvl = food.ILvl;
			if (ilvl < (dohl ? Data.FoodMinIlvlDoHL : Data.FoodMinIlvl))
				continue;

			bool match = false;
			matches[food] = 0;

			foreach (var stat in food.Stats) {
				// Don't count vitality.
				if (stat.Key == (int) Stat.VIT)
					continue;

				if (Attributes.ContainsKey(stat.Key)) {
					matches[food]++;
					match = true;
				}
			}

			if (match)
				RelevantMedicine.Add(food);
		}

		RelevantMedicine.Sort((a, b) => {
			int value = a.ILvl.CompareTo(b.ILvl);
			if (value != 0)
				return -value;

			matches.TryGetValue(a, out int aMatches);
			matches.TryGetValue(b, out int bMatches);

			value = aMatches.CompareTo(bMatches);
			if (value != 0)
				return -value;

			return 0;
		});
	}

	public void UpdateMedicine(Food? food, bool update = true) {
		if (food == Medicine)
			return;

		Medicine = food;
		if (update) {
			CalculateBaseStats();
			CalculateAdditional();
		}
	}

	public void UpdateMedicine(uint foodId, bool update = true) {
		Food? food = null;
		foreach (var fd in Data.Medicine) {
			if (fd.FoodID == foodId) {
				food = fd;
				break;
			}
		}

		UpdateMedicine(food, update);
	}

	public void UpdateFood(Food? food, bool update = true) {
		if (food == Food)
			return;

		Food = food;
		if (update) {
			CalculateBaseStats();
			CalculateAdditional();
		}
	}

	public void UpdateFood(uint foodId, bool update = true) {
		Food? food = null;
		foreach(var fd in Data.Food) {
			if (fd.FoodID == foodId) {
				food = fd;
				break;
			}
		}

		UpdateFood(food, update);
	}

	#endregion

	#region Stat Calculation

	/// <summary>
	/// Perform all necessary calculations for this equipment set,
	/// in the correct order.
	/// </summary>
	public void Recalculate() {
		CalculateClass();
		CalculateItemStats();
		CalculateBaseStats();
		CalculateAdditional();
		CalculateRelevantFood();
		CalculateRelevantMedicine();
	}

	private void CalculateAdditional() {
		Calculated.Clear();

		ClassJob? job = EffectiveJobRow();

		if (!string.IsNullOrEmpty(PlayerName))
			Calculated.Add(new CalculatedStat(
				"calc.name",
				"Player Name",
				PlayerName
			));

		if (Tribe != 0 && TribeRow() is Tribe tribe && RaceRow() is Race race) {
			Calculated.Add(new CalculatedStat(
				"calc.tribe",
				"Race / Clan",
				Gender == 0 ?
					$"{race.Masculine} / {tribe.Masculine}" :
					$"{race.Feminine} / {tribe.Feminine}"
			)) ;
		}

		if (job is not null) {
			string spec;
			if (job.DohDolJobIndex > 0 && HasCrystal)
				spec = $" ({Dalamud.Localization.Localize("calc.specialist", "Specialist")})";
			else
				spec = string.Empty;

			Calculated.Add(new CalculatedStat(
				"calc.class",
				"Class",
				CultureInfo.CurrentCulture.TextInfo.ToTitleCase(job.Name) + spec
			));
		}

		if (Level != 0)
			Calculated.Add(new CalculatedStat(
				"calc.level",
				"Level",
				EffectiveLevel.ToString()
			));

		if (ItemLevel != 0)
			Calculated.Add(new CalculatedStat(
				"calc.item-level",
				"Average Item Level",
				ItemLevel.ToString()
			));

		ParamGrow? growth = GrowRow();
		if (growth is null)
			return;

		if (job is null || job.ClassJobCategory.Value is not ClassJobCategory cat)
			return;

		if (job.DohDolJobIndex >= 0) {
			// DoH / DoL Calculated Stuff
			// ... is nothing~
			return;
		}

		// Combat Stuff
		//CalculateHPStuff(job, growth);
		CalculateCritStuff(growth);
		CalculateDHStuff(growth);
		CalculateDetStuff(growth);
		CalculateDefenseStuff(growth);
		CalculateMagDefenseStuff(growth);

		bool magicJob = cat.RowId == 31;

		// Tanks
		if (job.Role == 1) {
			CalculateTenacityStuff(growth);
		}

		// Healers
		if (job.Role == 4) {
			CalculatePietyStuff(growth);
		}

		CalculateSpeedStuff(growth, wantSpell: magicJob);
	}

	private void CalculateHPStuff(ClassJob job, ParamGrow growth) {
		// TODO: Figure out why this isn't accurate.

		Attributes.TryGetValue((uint) Stat.VIT, out StatData? stat);
		bool isTank = job.Role == 1;
		float scale = isTank ? 34.6f : 24.3f;

		int basevit = Data.GetBaseStatAtLevel(Stat.VIT, EffectiveLevel);
		int total = stat is null ? basevit : (stat.Value - basevit);

		Calculated.Add(new CalculatedStat(
			"calc.hp",
			"HP",
			(Math.Floor((growth.HpModifier * job.ModifierHitPoints) / 100f) + Math.Floor(total * scale)).ToString("N0")
		));
	}

	private void CalculateTenacityStuff(ParamGrow growth) {
		Attributes.TryGetValue((uint) Stat.TEN, out StatData? stat);
		Data.COEFFICIENTS.TryGetValue((uint) Stat.TEN, out int coefficient);

		int total = stat is null ? 0 : stat.Extra;

		Calculated.Add(new CalculatedStat(
			"calc.ten-mitigation",
			"Tenacity Mitigation",
			(Math.Floor(coefficient * (float) total / growth.LevelModifier) / 1000f).ToString("P1")
		));

		Calculated.Add(new CalculatedStat(
			"calc.ten-multi",
			"Tenacity Multiplier",
			((1000 + Math.Floor(coefficient * (float) total / growth.LevelModifier)) / 1000f).ToString("P1")
		));
	}

	private void CalculatePietyStuff(ParamGrow growth) {
		Attributes.TryGetValue((uint) Stat.PIE, out StatData? stat);
		Data.COEFFICIENTS.TryGetValue((uint) Stat.PIE, out int coefficient);

		int total = stat is null ? 0 : stat.Extra;

		Calculated.Add(new CalculatedStat(
			"calc.pie-tick",
			"MP per Tick",
			(Math.Floor(coefficient * (float) total / growth.LevelModifier) + 200).ToString("N0")
		));
	}

	private void CalculateCritStuff(ParamGrow growth) {
		Attributes.TryGetValue((uint) Stat.CRT, out StatData? stat);
		Data.COEFFICIENTS.TryGetValue((uint) Stat.CRT, out int coefficient);

		int total = stat is null ? 0 : stat.Extra;

		Calculated.Add(new CalculatedStat(
			"calc.crit-strength",
			"Critical Hit Multiplier",
			((Math.Floor(coefficient * (float) total / growth.LevelModifier) + 1400) / 1000f).ToString("P1")
		));

		Calculated.Add(new CalculatedStat(
			"calc.crit-rate",
			"Critical Hit Rate",
			((Math.Floor(coefficient * (float) total / growth.LevelModifier) + 50) / 1000f).ToString("P1")
		));
	}

	private void CalculateDHStuff(ParamGrow growth) {
		Attributes.TryGetValue((uint) Stat.DH, out StatData? stat);
		Data.COEFFICIENTS.TryGetValue((uint) Stat.DH, out int coefficient);

		int total = stat is null ? 0 : stat.Extra;

		Calculated.Add(new CalculatedStat(
			"calc.dh-rate",
			"Direct Hit Rate",
			(Math.Floor(coefficient * (float) total / growth.LevelModifier) / 1000f).ToString("P1")
		));
	}

	private void CalculateDetStuff(ParamGrow growth) {
		Attributes.TryGetValue((uint) Stat.DET, out StatData? stat);
		Data.COEFFICIENTS.TryGetValue((uint) Stat.DET, out int coefficient);

		int total = stat is null ? 0 : stat.Extra;

		Calculated.Add(new CalculatedStat(
			"calc.det-multi",
			"Determination Multiplier",
			((Math.Floor(coefficient * (float) total / growth.LevelModifier) + 1000) / 1000f).ToString("P1")
		));
	}

	private void CalculateDefenseStuff(ParamGrow growth) {
		Attributes.TryGetValue((uint) Stat.DEF, out StatData? stat);
		Data.COEFFICIENTS.TryGetValue((uint) Stat.DEF, out int coefficient);

		int total = stat is null ? Data.GetBaseStatAtLevel(Stat.DEF, EffectiveLevel) : stat.Value;

		Calculated.Add(new CalculatedStat(
			"calc.def-mitigation",
			"Damage Mitigation",
			(Math.Floor(coefficient * (float) total / growth.LevelModifier) / 100f).ToString("P1")
		));
	}

	private void CalculateMagDefenseStuff(ParamGrow growth) {
		Attributes.TryGetValue((uint) Stat.MDF, out StatData? stat);
		Data.COEFFICIENTS.TryGetValue((uint) Stat.MDF, out int coefficient);

		int total = stat is null ? Data.GetBaseStatAtLevel(Stat.MDF, EffectiveLevel) : stat.Value;

		Calculated.Add(new CalculatedStat(
			"calc.mdef-mitigation",
			"Magic Damage Mitigation",
			(Math.Floor(coefficient * (float) total / growth.LevelModifier) / 100f).ToString("P1")
		));
	}

	private void CalculateSpeedStuff(ParamGrow growth, bool wantSpell) {
		uint statID = (uint) (wantSpell ? Stat.SPS : Stat.SKS);

		Attributes.TryGetValue(statID, out StatData? stat);
		Data.COEFFICIENTS.TryGetValue(statID, out int coefficient);

		int total = stat is null ? 0 : stat.Extra;

		Calculated.Add(new CalculatedStat(
			wantSpell ? "calc.sps-multi" : "calc.sks-multi",
			wantSpell ? "Spell Speed Multiplier" : "Skill Speed Multiplier",
			((Math.Floor(coefficient * (float) total / growth.LevelModifier) + 1000) / 1000f).ToString("P1")
		));

		Calculated.Add(new CalculatedStat(
			"calc.gcd",
			"Global Cooldown",
			(Math.Floor(2500 * (Math.Ceiling(coefficient * (float) -total / growth.LevelModifier) + 1000) / 10000f) / 100f).ToString("N2")
		));
	}

	/// <summary>
	/// Modify the base stats of this gear set, applying a multiplier
	/// and also adding a value. Ensure the base stat never goes below zero.
	/// </summary>
	/// <param name="stat">The stat we're modifying</param>
	/// <param name="multiplier">The multiplier to apply</param>
	/// <param name="extra">The extra value to add</param>
	private void ModifyBaseStat(Stat stat, float multiplier = 1f, int extra = 0) {
		if (!Attributes.TryGetValue((uint) stat, out var value) || value.Base <= 0)
			return;

		if (multiplier != 1)
			value.Base = (int) Math.Floor(value.Base * multiplier);

		value.Base += extra;
		if (value.Base < 0)
			value.Base = 0;
	}

	/// <summary>
	/// Calculate the base stats of the player wearing the equipment. This
	/// uses the player's <see cref="Level"/>, <see cref="EffectiveClass"/>,
	/// and <see cref="Tribe"/>.
	/// </summary>
	private void CalculateBaseStats() {

		// First, set all stats based on what level the player is. Use the
		// EffectiveLevel in case we're simulating a level sync.
		foreach (var entry in Attributes)
			entry.Value.Base = Data.GetBaseStatAtLevel((Stat) entry.Key, EffectiveLevel);

		// If we have a job, modify the base stats by the job's multipliers.
		ClassJob? job = EffectiveJobRow();
		if (job is not null) {
			ModifyBaseStat(Stat.STR, job.ModifierStrength / 100f);
			ModifyBaseStat(Stat.DEX, job.ModifierDexterity / 100f);
			ModifyBaseStat(Stat.VIT, job.ModifierVitality / 100f);
			ModifyBaseStat(Stat.INT, job.ModifierIntelligence / 100f);
			ModifyBaseStat(Stat.MND, job.ModifierMind / 100f);
			ModifyBaseStat(Stat.PIE, job.ModifierPiety / 100f);
		}

		// If we have a tribe, modify the base stats by the tribe's offsets.
		Tribe? tribe = TribeRow();
		if (tribe is not null) {
			ModifyBaseStat(Stat.STR, extra: tribe.STR);
			ModifyBaseStat(Stat.DEX, extra: tribe.DEX);
			ModifyBaseStat(Stat.VIT, extra: tribe.VIT);
			ModifyBaseStat(Stat.INT, extra: tribe.INT);
			ModifyBaseStat(Stat.MND, extra: tribe.MND);
			ModifyBaseStat(Stat.PIE, extra: tribe.PIE);
		}

		// Apply food.
		foreach(var entry in Attributes) {
			entry.Value.Food = 0;
			if (Food is not null && Food.Stats.TryGetValue(entry.Key, out FoodStat? fstat)) {
				if (fstat.Relative) {
					entry.Value.Food = Math.Min(fstat.MaxValue, (int) Math.Floor(entry.Value.ValueNoFood * fstat.Multiplier));
				} else
					entry.Value.Food = fstat.MaxValue;
			}
		}

		// Apply medicine.
		if (Medicine is not null)
			foreach(var stat in Medicine.Stats.Values) {
				if (Attributes.TryGetValue(stat.StatID, out var value)) {
					if (stat.Relative) {
						value.Food += Math.Min(stat.MaxValue, (int) Math.Floor(value.ValueNoFood * stat.Multiplier));
					} else
						value.Food += stat.MaxValue;
				}
			}

		// Finally, calculate tiers.
		ParamGrow? growth = GrowRow();
		if (growth is not null)
			foreach (var entry in Attributes)
				entry.Value.UpdateTiers(growth.LevelModifier);
	}

	/// <summary>
	/// Loop through all the items in this equipment set, adding up
	/// stats from the items and from their melds, as well as
	/// calculating each item's limit for all relevant stats and
	/// so also the wasted attribute points.
	/// </summary>
	private void CalculateItemStats() {
		if (!Data.CheckSheets())
			return;

		uint totalLevel = 0;

		EmptyMeldSlots = 0;
		MateriaCount.Clear();

		bool hasOffHand = HasOffhand;

		// If not, Paladins have an off-hand anyways.
		if (EffectiveClass == 1 || EffectiveClass == 19)
			hasOffHand = true;
		// And so do DoH / DoL.
		else if (EffectiveJobRow() is ClassJob job && job.DohDolJobIndex >= 0)
			hasOffHand = true;

		// Clear existing stat data.
		foreach (var stat in Attributes.Values) {
			stat.Gear = 0;
			stat.Delta = 0;
			stat.Waste = 0;
		}

		// Loop through all the items.
		for (int i = 0; i < Items.Count; i++) {
			MeldedItem rawItem = Items[i];
			Dictionary<uint, StatData> stats = ItemAttributes[i];
			stats.Clear();

			ExtendedItem? item = rawItem.Row();
			if (item is null)
				continue;

			bool synced = ILvlSync > 0 && ILvlSync < item.LevelItem.Row && ILvlSync < Data.LevelSheet.RowCount;
			ExtendedItemLevel? level = synced ?
				Data.LevelSheet.GetRow(ILvlSync) :
				item.ExtendedItemLevel.Value;

			if (level is null)
				continue;

			// Item Level Calculation
			EquipSlotCategory? slot = item.EquipSlotCategory.Value;
			if (slot is not null && slot.SoulCrystal == 0) {
				uint amount = level.RowId;

				// Special handling for equipment that has ilvl sync. If the
				// item's ilvl at our current level is lower than its maximum
				// ilvl, then use that. Never increase the ilvl from this.
				if (item.SubStatCategory == 2) {
					ParamGrow? growth = Data.GrowSheet.GetRow(EffectiveLevel);
					if (growth is not null && growth.ItemLevelSync < amount)
						amount = growth.ItemLevelSync;
				}

				totalLevel += amount;
			}

			// Stat Calculation

			// Base Item Stats
			if (item.DamagePhys > 0)
				AddGearStat(stats, (uint) Stat.PhysDMG, gear: (short) item.DamagePhys);
			if (item.DamageMag > 0)
				AddGearStat(stats, (uint) Stat.MagDMG, gear: (short) item.DamageMag);

			if (item.DefensePhys > 0)
				AddGearStat(stats, (uint) Stat.DEF, gear: (short) item.DefensePhys);
			if (item.DefenseMag > 0)
				AddGearStat(stats, (uint) Stat.MDF, gear: (short) item.DefenseMag);

			foreach(var entry in item.BaseParam) {
				uint statID = entry.BaseParam;
				short value = entry.BaseParamValue;

				AddGearStat(stats, statID, gear: value);
			}

			// High-Quality Stats
			if (rawItem.HighQuality)
				foreach (var entry in item.BaseParamSpecial) {
					uint statID = entry.BaseParamSpecial;
					short value = entry.BaseParamValueSpecial;

					AddGearStat(stats, statID, gear: value);
				}

			// Melded Materia
			int melds = 0;

			foreach (var rawMeld in rawItem.Melds) {
				if (rawMeld.ID == 0 || rawMeld.Grade < 0)
					continue;

				Materia? materia = rawMeld.Row();
				if (materia is null || rawMeld.Grade >= materia.Value.Length)
					continue;

				uint statID = materia.BaseParam.Row;
				short value = materia.Value[rawMeld.Grade];

				AddGearStat(stats, statID, delta: value);

				// Track this item.
				melds++;

				uint materiaItem = materia.Item[rawMeld.Grade].Row;
				if (!MateriaCount.ContainsKey(materiaItem))
					MateriaCount[materiaItem] = 1;
				else
					MateriaCount[materiaItem]++;
			}

			if (melds < item.MateriaSlotCount)
				EmptyMeldSlots += item.MateriaSlotCount - melds;

			// Finally, for each stat, we need to calculate the limit and
			// waste values. Then add the waste to the main Attributes.
			foreach(var stat in stats.Values) {
				uint statID = stat.ID;
				ExtendedBaseParam? param = Params[statID];

				// If we're synced, we need to calculate the stat limit
				// for gear of this slot, use that as the limit, and
				// reduce the gear score if it exceeds the value.
				if (synced) {
					if (stat.Gear == 0)
						stat.Limit = 0;
					else {
						ushort factor = item.EquipSlotCategory.Row switch {
							1 => param.oneHWpnPct,
							2 => param.OHPct,
							3 => param.HeadPct,
							4 => param.ChestPct,
							5 => param.HandsPct,
							6 => param.WaistPct,
							7 => param.LegsPct,
							8 => param.FeetPct,
							9 => param.EarringPct,
							10 => param.NecklacePct,
							11 => param.BraceletPct,
							12 => param.RingPct,
							13 => param.twoHWpnPct,
							14 => param.oneHWpnPct,
							15 => param.ChestHeadPct,
							16 => 0, // Chest Gloves Legs Feet? Probably unknown20
							17 => 0, // Job Crystal
							18 => param.LegsFeetPct,
							19 => param.ChestHeadLegsFeetPct,
							20 => param.ChestLegsGlovesPct,
							21 => param.ChestLegsFeetPct,
							_ => 0,
						};

						float percentage = factor / 1000f;

						ushort value = statID switch {
							(int) Stat.STR => level.Strength,
							(int) Stat.DEX => level.Dexterity,
							(int) Stat.VIT => level.Vitality,
							(int) Stat.INT => level.Intelligence,
							(int) Stat.MND => level.Mind,
							(int) Stat.PIE => level.Piety,
							(int) Stat.PhysDMG => level.PhysicalDamage,
							(int) Stat.MagDMG => level.PhysicalDamage,
							(int) Stat.TEN => level.Tenacity,
							(int) Stat.DEF => level.Defense,
							(int) Stat.MDF => level.MagicDefense,
							(int) Stat.DH  => level.DirectHitRate,
							(int) Stat.CRT => level.CriticalHit,
							(int) Stat.DET => level.Determination,
							(int) Stat.SKS => level.SkillSpeed,
							(int) Stat.SPS => level.SpellSpeed,
							_ => 0,
						};

						int val = (int) Math.Round(
							value * percentage,
							MidpointRounding.AwayFromZero
						);

						stat.Limit = val;
						if (stat.Gear > val) {
							int difference = stat.Gear - val;
							stat.Gear = val;
							Attributes[statID].Gear -= difference;
						} else
							stat.Limit = stat.Gear;
					}
				}

				// If we're *not* synced, calculate the limit.
				else {
					// As far as I can determine, this is how the game handles
					// these values. This may be slightly inaccurate.
					stat.Limit = (int) Math.Round(
						(level.BaseParam[statID] * param.EquipSlotCategoryPct[item.EquipSlotCategory.Row])
						/ 1000f,
						MidpointRounding.AwayFromZero
					);
				}

				// Ensure the limit holds the stats.
				int min = stat.Base + stat.Gear;
				if (stat.Limit < min)
					stat.Limit = min;

				// Now update the waste value, since we have a limit, and add
				// the waste to the main attribute.
				stat.UpdateWaste();
				Attributes[statID].Waste += stat.Waste;
			}
		}

		int slots = 11;
		// Is there an off-hand equipped?
		if (hasOffHand)
			slots++;

		// Finally, set the item level.
		ItemLevel = (ushort) Math.Round(totalLevel / (float) slots, MidpointRounding.AwayFromZero);
	}

	/// <summary>
	/// Add attribute values from an item to the equipment set. This
	/// adds to the equipment's main stats object, and also adds to the
	/// individual item's stats object. It deals with initializing stats
	/// as well, if the stat hasn't been encountered yet.
	/// </summary>
	/// <param name="stats">The item's stats object</param>
	/// <param name="statID">The ID of the stat</param>
	/// <param name="gear">The points to add to the gear</param>
	/// <param name="delta">The points to add to the delta</param>
	private void AddGearStat(Dictionary<uint, StatData>? stats, uint statID, short gear = 0, short delta = 0) {
		// Skip empty stats.
		if (statID == 0 || (gear == 0 && delta == 0))
			return;

		// TODO: Proper handling for "Main Attribute" and
		// "Secondary Attribute". For now, just skip them.
		if (statID == (int) Stat.MainAttribute || statID == (int) Stat.SecondaryAttribute)
			return;

		// Get the parameter data row.
		if (!Params.ContainsKey(statID)) {
			ExtendedBaseParam? param = Data.ParamSheet?.GetRow(statID);
			if (param is null)
				return;

			Params[statID] = param;
		}

		// Ensure the equipment set has a record for this stat.
		if (!Attributes.ContainsKey(statID))
			Attributes[statID] = new StatData(statID);

		// Add the value from this piece of equipment to the record.
		if (gear > 0)
			Attributes[statID].Gear += gear;
		if (delta > 0)
			Attributes[statID].Delta += delta;

		if (stats is null)
			return;

		// And track this stat on the item.
		if (!stats.ContainsKey(statID))
			stats[statID] = new(statID);

		if (gear > 0)
			stats[statID].Gear += gear;
		if (delta > 0)
			stats[statID].Delta += delta;
	}

	#endregion

}
