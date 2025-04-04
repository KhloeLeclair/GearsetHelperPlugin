using System;
using System.Collections.Generic;
using System.Globalization;

using GearsetHelperPlugin.Sheets;

using Lumina.Excel.Sheets;

namespace GearsetHelperPlugin.Models;

internal class EquipmentSet {

	#region Constructor

	public EquipmentSet(List<MeldedItem>? items = null) {
		Items = items ?? [];

		for (int i = 0; i < Items.Count; i++)
			ItemAttributes.Add([]);
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
	/// The percentage stats should be modified due to group bonus.
	/// </summary>
	public float GroupBonus { get; set; } = 0f;

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

	public Dictionary<uint, BaseParam> Params { get; } = [];
	public Dictionary<uint, StatData> Attributes { get; } = [];
	public List<Dictionary<uint, StatData>> ItemAttributes { get; } = [];

	public StatData? GCD { get; private set; } = null;

	public StatData? WeaponDamage { get; private set; } = null;

	#endregion

	#region Calculated Stats

	public List<CalculatedStat> Calculated { get; } = [];

	#endregion

	#region Relevant Foods

	public List<Food> RelevantFood { get; } = [];

	public List<Food> RelevantMedicine { get; } = [];

	#endregion

	#region Materia Caching

	public int EmptyMeldSlots { get; set; }

	public Dictionary<uint, int> MateriaCount { get; } = [];

	#endregion

	#region That's A Lotta Damage

	public Dictionary<int, DamageValues> DamageValues { get; } = [];

	#endregion

	#region Sheet Helpers

	public ParamGrow? GrowRow() {
		if (!Data.CheckSheets() || !Data.GrowSheet.TryGetRow(EffectiveLevel, out var row))
			return null;
		return row;
	}

	public Race? RaceRow() {
		if (!Data.CheckSheets() || !Data.RaceSheet.TryGetRow(Race, out var row))
			return null;
		return row;
	}

	public Tribe? TribeRow() {
		if (!Data.CheckSheets() || !Data.TribeSheet.TryGetRow(Tribe, out var row))
			return null;
		return row;
	}

	public ClassJob? JobRow() {
		if (!Data.CheckSheets() || !Data.ClassSheet.TryGetRow(Class, out var row))
			return null;
		return row;
	}

	public ClassJob? EffectiveJobRow() {
		if (!Data.CheckSheets() || !Data.ClassSheet.TryGetRow(EffectiveClass, out var row))
			return null;
		return row;
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
		foreach (var item in Items) {
			var data = item.Row();
			if (data.HasValue && data.Value.LevelEquip > level)
				level = data.Value.LevelEquip;
		}

		bool changed = Tribe != tribe || Level != level;

		Gender = gender;
		Race = race;
		Tribe = tribe;
		Level = level;

		return changed;
	}

	internal bool UpdateGroupBonus(uint groupBonus) {
		float value = groupBonus / 100f;

		if (value == GroupBonus)
			return false;

		GroupBonus = value;
		return true;
	}

	internal bool UpdateSync(byte levelSync, uint ilvlSync) {
		if (ilvlSync == 0 && Data.CheckSheets()) {
			byte elevel = levelSync < Level ? levelSync : Level;
			if (elevel >= 100)
				ilvlSync = 795;
			else if (Data.GrowSheet.TryGetRow(levelSync, out var growth))
				ilvlSync = growth.ItemLevelSync;
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

		ClassJobCategory? category = null;

		for (int i = 0; i < Items.Count; i++) {
			MeldedItem item = Items[i];
			Item? data = item.Row();
			if (!data.HasValue)
				continue;

			if (data.Value.EquipSlotCategory.IsValid) {
				var slot = data.Value.EquipSlotCategory.Value;
				if (slot.MainHand == 1)
					category = data.Value.ClassJobCategory.Value;
				if (slot.OffHand == 1) {
					//PluginLog.Log($"Off-hand: {data.Name}");
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

				if (category.Value.Classes(row.RowId)) {
					job = row;
					break;
				}
			}

			// Widen the search to ANY class if we didn't find a job, which
			// should only happen in the case of crafters and gatherers.
			if (!job.HasValue) {
				foreach (var row in Data.ClassSheet) {
					if (category.Value.Classes(row.RowId)) {
						job = row;
						break;
					}
				}
			}

			// Now, if we found a job, save it.
			if (job.HasValue) {
				Class = job.Value.RowId;
				EffectiveClass = job.Value.RowId;

				// If we don't have a soul crystal, and this job has
				// a parent job, then switch it up for stat calculation
				// but leave the class set on gearset for exporting
				// correctly.
				if (!HasCrystal && job.Value.ClassJobParent.IsValid)
					EffectiveClass = job.Value.ClassJobParent.RowId;
			}
		}

		// Did anything change?
		return OldHasOffhand != HasOffhand || OldHasCrystal != HasCrystal || OldClass != Class || OldEffectiveClass != EffectiveClass;
	}

	public void CalculateRelevantFood() {
		RelevantFood.Clear();

		Dictionary<Food, int> matches = [];
		Dictionary<Food, int> level = [];

		bool dohl = false;
		ClassJob? job = JobRow();
		if (job.HasValue)
			dohl = job.Value.Role == 0;

		foreach (var food in Data.Food) {
			uint ilvl = food.ILvl;
			if (ilvl < (dohl ? Data.FoodMinIlvlDoHL : Data.FoodMinIlvl))
				continue;

			if (!food.HQ && Data.FoodHQOnly)
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

			value = a.FoodID.CompareTo(b.FoodID);
			if (value != 0)
				return -value;

			value = a.HQ.CompareTo(b.HQ);
			if (value != 0)
				return -value;

			return 0;
		});
	}

	public void CalculateRelevantMedicine() {
		RelevantMedicine.Clear();

		Dictionary<Food, int> matches = [];
		Dictionary<Food, int> level = [];

		bool dohl = false;
		ClassJob? job = JobRow();
		if (job.HasValue)
			dohl = job.Value.Role == 0;

		foreach (var food in Data.Medicine) {
			uint ilvl = food.ILvl;
			if (ilvl < (dohl ? Data.FoodMinIlvlDoHL : Data.FoodMinIlvl))
				continue;

			if (!food.HQ && Data.FoodHQOnly)
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

			value = a.FoodID.CompareTo(b.FoodID);
			if (value != 0)
				return -value;

			value = a.HQ.CompareTo(b.HQ);
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

	public void UpdateFood(uint foodId, bool hq = false, bool update = true) {
		Food? food = null;
		foreach (var fd in Data.Food) {
			if (fd.FoodID == foodId && fd.HQ == hq) {
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
			));
		}

		if (job.HasValue) {
			string spec;
			if (job.Value.DohDolJobIndex > 0 && HasCrystal)
				spec = $" ({Dalamud.Localization.Localize("calc.specialist", "Specialist")})";
			else
				spec = string.Empty;

			Calculated.Add(new CalculatedStat(
				"calc.class",
				"Class",
				CultureInfo.CurrentCulture.TextInfo.ToTitleCase(job.Value.Name.ExtractText().ToString()) + spec
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

		if (!job.HasValue || !job.Value.ClassJobCategory.IsValid)
			return;

		if (job.Value.DohDolJobIndex >= 0) {
			// DoH / DoL Calculated Stuff
			// ... is nothing~
			return;
		}

		// Combat Stuff
		CalculateHPStuff(job.Value, growth.Value);
		CalculateWeaponDamage(job.Value, growth.Value);
		CalculateCritStuff(growth.Value);
		CalculateDHStuff(growth.Value);
		CalculateDetStuff(growth.Value);
		CalculateDefenseStuff(growth.Value);
		CalculateMagDefenseStuff(growth.Value);

		// Tanks
		if (job.Value.Role == 1) {
			CalculateTenacityStuff(growth.Value);
		}

		// Healers
		if (job.Value.Role == 4) {
			CalculatePietyStuff(growth.Value);
		}

		CalculateSpeedStuff(growth.Value, wantSpell: job.Value.ClassJobCategory.RowId == 31);
	}

	private void CalculateWeaponDamage(ClassJob job, ParamGrow growth) {

		if (WeaponDamage is null)
			return;

		// First, we need to calculate the weapon damage multiplier.

		// To do this, we need to know what the job's primary stat is
		// and get its base value at level.
		Stat primaryStat = (Stat) job.PrimaryStat;
		int baseValue = Data.GetBaseStatAtLevel(primaryStat, EffectiveLevel);

		// Now we need to get the mod.
		int jobMod = primaryStat switch {
			Stat.STR => job.ModifierStrength,
			Stat.DEX => job.ModifierDexterity,
			Stat.VIT => job.ModifierVitality,
			Stat.INT => job.ModifierIntelligence,
			Stat.MND => job.ModifierMind,
			Stat.PIE => job.ModifierPiety,
			_ => -100
		};

		// Invalid value, return~
		if (jobMod == -100)
			return;

		// Now, we need to calculate a lot of stuff.

		// ====================================================================
		// 1. The weapon damage multiplier.
		// ====================================================================
		float dmgMulti = FL(WeaponDamage.Value + ((baseValue * jobMod) / 1000f)) / 100f;

		// ====================================================================
		// 2. A critical damage multiplier.
		// ====================================================================
		float critMulti, critRate;
		{
			Attributes.TryGetValue((uint) Stat.CRT, out StatData? stat);
			Data.COEFFICIENTS.TryGetValue((uint) Stat.CRT, out int coefficient);

			int total = stat is null ? 0 : stat.ExtraFood;

			// Calculate the Critical Hit Multiplier (Percentage in Range: 0..1 (can exceed 1))
			critMulti = (FL(coefficient * (float) total / growth.LevelModifier) + 1400f) / 1000f;

			// Calculate the Critical Hit Rate (Percentage in Range: 0..1)
			critRate = (FL(coefficient * (float) total / growth.LevelModifier) + 50f) / 1000f;
		}

		// ====================================================================
		// 3. Main Stat Multiplier
		// ====================================================================
		float mainMulti;
		{
			Attributes.TryGetValue((uint) primaryStat, out StatData? stat);

			float scalar = Data.GetAttackScalar(job, EffectiveLevel);
			if (scalar <= 0) {
				DamageValues.Clear();
				return;
			}

			int total = stat is null ? 0 : stat.Value - baseValue;

			mainMulti = MathF.Max(
				0,
				(TRUNC(scalar * (float) total / baseValue) + 100f) / 100f
			);
		}

		// ====================================================================
		// 4. A determination multiplier.
		// ====================================================================
		float detMulti;
		{
			Attributes.TryGetValue((uint) Stat.DET, out StatData? stat);
			Data.COEFFICIENTS.TryGetValue((uint) Stat.DET, out int coefficient);

			int total = stat is null ? 0 : stat.ExtraFood;

			detMulti = (FL(coefficient * (float) total / growth.LevelModifier) + 1000f) / 1000f;
		}

		// ====================================================================
		// 5. A direct hit multiplier.
		// ====================================================================
		float dhMulti, dhRate;
		{
			Attributes.TryGetValue((uint) Stat.DH, out StatData? stat);
			Data.COEFFICIENTS.TryGetValue((uint) Stat.DH, out int coefficient);

			int total = stat is null ? 0 : stat.ExtraFood;

			// Calculate the Direct Hit Rate (Percentage in Range: 0..1)
			dhRate = FL(coefficient * (float) total / growth.LevelModifier) / 1000f;
			dhMulti = 1.25f;
		}

		// ====================================================================
		// 6. A tenacity multiplier.
		// ====================================================================
		float tenMulti;
		{
			Attributes.TryGetValue((uint) Stat.TEN, out StatData? stat);
			Data.COEFFICIENTS.TryGetValue((uint) Stat.TEN, out int coefficient);

			int total = stat is null ? 0 : stat.ExtraFood;

			tenMulti = (1000f + MathF.Floor(coefficient * (float) total / growth.LevelModifier)) / 1000f;
		}

		// ====================================================================
		// 7. Any extra job-related or buff-related multipliers.
		// ====================================================================
		float extra = Data.GetTraitModifier(job.ToGameClass(), EffectiveLevel);

		// Calculate useful values for the average column.
		float avgCritMulti = 1f + critRate * (critMulti - 1f);
		float avgDhMulti = 1f + dhRate * (dhMulti - 1f);

		// Now build the data.
		DamageValues.Clear();

		//PluginLog.Debug($"Source Values: dmg={dmgMulti}, main={mainMulti}, critM={critMulti} critR={critRate}, crit={avgCritMulti}, det={detMulti}, dh={dhMulti}, ten={tenMulti}, ext={extra}");

		foreach (int potency in Data.GetExamplePotencies(job.ToGameClass(), EffectiveLevel)) {
			float expectBase;
			if (job.IsMagical())
				expectBase = FL(FL(FL(detMulti * mainMulti) * FL(dmgMulti * potency)) * tenMulti);
			else
				expectBase = FL(FL(FL(FL(potency * mainMulti) * detMulti) * tenMulti) * dmgMulti);

			float expectNormal = FL(expectBase * extra) + (potency < 100 ? 1 : 0);

			float expected = FL(FL(expectNormal * avgCritMulti) * avgDhMulti);
			float expectCrit = FL(expectNormal * critMulti);
			float expectDH = FL(expectNormal * dhMulti);
			float expectWhoa = FL(expectCrit * dhMulti);

			DamageValues.Add(potency, new(expectNormal, expected, expectCrit, expectDH, expectWhoa));
		}

		//PluginLog.Debug($"Values={DamageValues}");
	}

	private static float FL(float input) {
		float floored = MathF.Floor(input);
		if (input - floored >= 0.99999995f)
			return floored + 1;
		return floored;
	}

	private static float TRUNC(float input) => input >= 0 ? FL(input) : -FL(-input);


	private void CalculateHPStuff(ClassJob job, ParamGrow growth) {
		// TODO: Figure out why the base game values aren't accurate.
		return;

		int hp;
		if (EffectiveLevel == 90)
			hp = 3000;
		else
			return;

		Attributes.TryGetValue((uint) Stat.VIT, out StatData? stat);
		bool isTank = job.Role == 1;
		float scale = isTank ? 34.6f : 24.3f;

		int basevit = Data.GetBaseStatAtLevel(Stat.VIT, EffectiveLevel);
		int total = stat is null ? basevit : (stat.Value - basevit);

		/*PluginLog.Log($"HP: {hp}");
		PluginLog.Log($"HP Modifier: {growth.HpModifier}");
		PluginLog.Log($"Modifer HP: {job.ModifierHitPoints}");
		PluginLog.Log($"Base Vit: {basevit}");
		PluginLog.Log($"Total Vit: {stat?.Value ?? basevit}");
		PluginLog.Log($"Scale: {isTank} -- {scale}");*/

		Calculated.Add(new CalculatedStat(
			"calc.hp",
			"HP",
			(Math.Floor((/*growth.HpModifier*/hp * job.ModifierHitPoints) / 100f) + Math.Floor(total * scale)).ToString("N0")
		));
	}

	private void CalculateTenacityStuff(ParamGrow growth) {
		Attributes.TryGetValue((uint) Stat.TEN, out StatData? stat);
		Data.COEFFICIENTS.TryGetValue((uint) Stat.TEN, out int coefficient);

		int total = stat is null ? 0 : stat.ExtraFood;

		// The coefficient for Tenacity defense is different than the coefficient for damage.
		// Hence, using 200 here.

		Calculated.Add(new CalculatedStat(
			"calc.ten-mitigation",
			"Tenacity Mitigation",
			(Math.Floor(200 /*coefficient*/ * (float) total / growth.LevelModifier) / 1000f).ToString("P1")
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

		int total = stat is null ? 0 : stat.ExtraFood;

		Calculated.Add(new CalculatedStat(
			"calc.pie-tick",
			"MP per Tick",
			(Math.Floor(coefficient * (float) total / growth.LevelModifier) + 200).ToString("N0")
		));
	}

	private void CalculateCritStuff(ParamGrow growth) {
		Attributes.TryGetValue((uint) Stat.CRT, out StatData? stat);
		Data.COEFFICIENTS.TryGetValue((uint) Stat.CRT, out int coefficient);

		int total = stat is null ? 0 : stat.ExtraFood;

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

		int total = stat is null ? 0 : stat.ExtraFood;

		Calculated.Add(new CalculatedStat(
			"calc.dh-rate",
			"Direct Hit Rate",
			(Math.Floor(coefficient * (float) total / growth.LevelModifier) / 1000f).ToString("P1")
		));
	}

	private void CalculateDetStuff(ParamGrow growth) {
		Attributes.TryGetValue((uint) Stat.DET, out StatData? stat);
		Data.COEFFICIENTS.TryGetValue((uint) Stat.DET, out int coefficient);

		int total = stat is null ? 0 : stat.ExtraFood;

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

	internal static double CalculateGCD(int coefficient, int extra, int levelModifier, int modifier = 0, int haste = 0) {
		return Math.Floor(
			Math.Floor(
				Math.Floor(
					Math.Floor((1000.0 - Math.Floor(coefficient * (double) extra / levelModifier)) * 2500.0 / 1000.0) *
					Math.Floor(
						(
							Math.Floor(
								Math.Floor((100.0 - /*arrow*/ 0) * (100.0 - modifier) / 100.0) *
								(100.0 - haste) / 100.0
							) -
							/*feyWind*/ 0
						) *
						(/*selfBuff2*/0 - 100.0) /
						100.0
					) /
					-100.0
				) *
				/*RoF*/ 100.0 /
				1000.0
			) *
			/*umbralAstral3*/100.0 /
			100.0
		) / 100.0;
	}

	internal static IEnumerable<int> CalculateGcdTiers(int coefficient, int levelModifier, int modifier = 0, int haste = 0, int start = 0, int limit = 99999) {
		double previous = double.MinValue;

		int i = start;
		while (i <= limit) {
			double value = CalculateGCD(coefficient, i, levelModifier, modifier, haste);
			if (value != previous) {
				previous = value;
				yield return i;
			}

			i++;
		}
	}

	private void UpdateGCDTiers(ParamGrow growth, bool wantSpell) {
		uint statID = (uint) (wantSpell ? Stat.SPS : Stat.SKS);

		Attributes.TryGetValue(statID, out StatData? stat);
		Data.COEFFICIENTS.TryGetValue(statID, out int coefficient);

		int total = stat is null ? 0 : stat.ExtraFood;

		// We need to find two tiers:
		// 1. The last tier before our value
		// 2. The next tier after our value

		int previousTier = int.MinValue;
		int nextTier = int.MaxValue;

		int range = 100;

		foreach (int tier in CalculateGcdTiers(coefficient, growth.LevelModifier, 0, start: total - range, limit: total + range)) {
			if (tier <= total && tier > previousTier)
				previousTier = tier;
			else if (tier > total) {
				nextTier = tier;
				break;
			}
		}

		GCD = new((uint) Stat.GCD) {
			PreviousTier = previousTier - total,
			NextTier = nextTier - total
		};
	}


	private void CalculateSpeedStuff(ParamGrow growth, bool wantSpell) {
		uint statID = (uint) (wantSpell ? Stat.SPS : Stat.SKS);

		Attributes.TryGetValue(statID, out StatData? stat);
		Data.COEFFICIENTS.TryGetValue(statID, out int coefficient);

		int total = stat is null ? 0 : stat.ExtraFood;

		Calculated.Add(new CalculatedStat(
			wantSpell ? "calc.sps-multi" : "calc.sks-multi",
			wantSpell ? "Spell Speed Multiplier" : "Skill Speed Multiplier",
			((Math.Floor(coefficient * (float) total / growth.LevelModifier) + 1000) / 1000f).ToString("P1")
		));

		// Old, simplified math.
		//double gcd = Math.Floor(2500 * (Math.Ceiling(coefficient * (float) -total / growth.LevelModifier) + 1000) / 10000f) / 100f;

		double gcd = CalculateGCD(coefficient, total, growth.LevelModifier, 0);

		Calculated.Add(new CalculatedStat(
			"calc.gcd",
			"Global Cooldown",
			gcd.ToString("N2")
		));

		// Black Mage
		if (EffectiveClass == (int) GameClass.BlackMage && EffectiveLevel >= 52) {
			// Ley Lines: 15%
			gcd = CalculateGCD(coefficient, total, growth.LevelModifier, 15);

			Calculated.Add(new CalculatedStat(
				"calc.gcd-ll",
				"Global Cooldown (Ley Lines)",
				gcd.ToString("N2")
			));
		}

		// Monk Check
		if (EffectiveClass == 2 || EffectiveClass == 20) {
			// Greased Lightning
			//       Lv  1:   GL: 5%
			//       Lv 20:  EGL: 10%
			// (JOB) Lv 40: EGL2: 15%
			// (JOB) Lv 76: EGL3: 20%
			bool monk = EffectiveClass == 20;
			int modifier = 5;

			if (monk && EffectiveLevel >= 76)
				modifier = 20;
			else if (monk && EffectiveLevel >= 40)
				modifier = 15;
			else if (EffectiveLevel >= 20)
				modifier = 10;

			gcd = CalculateGCD(coefficient, total, growth.LevelModifier, modifier);

			Calculated.Add(new CalculatedStat(
				"calc.gcd-gl",
				"Global Cooldown (GL)",
				gcd.ToString("N2")
			));
		}

		// Ninja Check
		if (EffectiveClass == 30 && EffectiveLevel >= 45) {
			// Hyoton: 15%
			gcd = CalculateGCD(coefficient, total, growth.LevelModifier, 15);

			Calculated.Add(new CalculatedStat(
				"calc.gcd-hu",
				"Global Cooldown (Huton)",
				gcd.ToString("N2")
			));
		}

		// Viper
		if (EffectiveClass == (int) GameClass.Viper && EffectiveLevel >= 65) {
			// Swiftscaled: 15%
			gcd = CalculateGCD(coefficient, total, growth.LevelModifier, 15);

			Calculated.Add(new CalculatedStat(
				"calc.gcd-ssc",
				"Global Cooldown (Swiftscaled)",
				gcd.ToString("N2")
			));
		}

		// Pictomancer
		if (EffectiveClass == (int) GameClass.Pictomancer && EffectiveLevel >= 82) {
			// Hyperphantasia: 25%
			gcd = CalculateGCD(coefficient, total, growth.LevelModifier, 25);

			Calculated.Add(new CalculatedStat(
				"calc.gcd-hyp",
				"Global Cooldown (Hyperphantasia)",
				gcd.ToString("N2")
			));
		}

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

		value.Base += extra;

		if (multiplier != 1)
			value.Base = (int) Math.Round(value.Base * multiplier);

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

		// If we have a tribe, modify the base stats by the tribe's offsets.
		Tribe? tribe = TribeRow();
		if (tribe.HasValue) {
			ModifyBaseStat(Stat.STR, extra: tribe.Value.STR);
			ModifyBaseStat(Stat.DEX, extra: tribe.Value.DEX);
			ModifyBaseStat(Stat.VIT, extra: tribe.Value.VIT);
			ModifyBaseStat(Stat.INT, extra: tribe.Value.INT);
			ModifyBaseStat(Stat.MND, extra: tribe.Value.MND);
			ModifyBaseStat(Stat.PIE, extra: tribe.Value.PIE);
		}

		// If we have a job, modify the base stats by the job's multipliers.
		ClassJob? job = EffectiveJobRow();
		if (job.HasValue) {
			ModifyBaseStat(Stat.STR, job.Value.ModifierStrength / 100f);
			ModifyBaseStat(Stat.DEX, job.Value.ModifierDexterity / 100f);
			ModifyBaseStat(Stat.VIT, job.Value.ModifierVitality / 100f);
			ModifyBaseStat(Stat.INT, job.Value.ModifierIntelligence / 100f);
			ModifyBaseStat(Stat.MND, job.Value.ModifierMind / 100f);
			ModifyBaseStat(Stat.PIE, job.Value.ModifierPiety / 100f);
		}

		// Apply food.
		foreach (var entry in Attributes) {
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
			foreach (var stat in Medicine.Stats.Values) {
				if (Attributes.TryGetValue(stat.StatID, out var value)) {
					if (stat.Relative) {
						value.Food += Math.Min(stat.MaxValue, (int) Math.Floor(value.ValueNoFood * stat.Multiplier));
					} else
						value.Food += stat.MaxValue;
				}
			}

		// Apply the group bonus.
		foreach (var entry in Attributes) {
			Stat key = (Stat) entry.Key;
			entry.Value.GroupBonus = 0;
			if (GroupBonus != 0f && (key == Stat.STR || key == Stat.DEX || key == Stat.VIT || key == Stat.INT || key == Stat.MND)) {
				int bonus = (int) MathF.Floor(entry.Value.ValueNoFood * GroupBonus);
				entry.Value.GroupBonus = bonus;
			}
		}

		// Finally, calculate tiers.
		ParamGrow? growth = GrowRow();
		if (growth.HasValue) {
			foreach (var entry in Attributes)
				entry.Value.UpdateTiers(growth.Value.LevelModifier, job?.Role == 1);

			// Update the GCD
			UpdateGCDTiers(growth.Value, job.HasValue && job.Value.IsMagical());
		}
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

		/*bool hasOffHand = HasOffhand;

		// If not, Paladins have an off-hand anyways.
		if (EffectiveClass == 1 || EffectiveClass == 19)
			hasOffHand = true;
		// And so do DoH / DoL.
		else if (EffectiveJobRow() is ClassJob job && job.DohDolJobIndex >= 0)
			hasOffHand = true;*/

		// Clear existing stat data.
		foreach (var stat in Attributes.Values) {
			stat.Gear = 0;
			stat.Delta = 0;
			stat.Waste = 0;
		}

		// Clear existing weapon damage too.
		WeaponDamage = null;

		// Loop through all the items.
		for (int i = 0; i < Items.Count; i++) {
			MeldedItem rawItem = Items[i];
			Dictionary<uint, StatData> stats = ItemAttributes[i];
			stats.Clear();

			if (rawItem.Row() is not Item item)
				continue;

			bool synced = ILvlSync > 0 && ILvlSync < item.LevelItem.RowId && ILvlSync < Data.LevelSheet.Count;
			ItemLevel level;
			if (synced) {
				if (!Data.LevelSheet.TryGetRow(ILvlSync, out level))
					continue;
			} else if (item.LevelItem.IsValid)
				level = item.LevelItem.Value;
			else
				continue;

			// Item Level Calculation
			EquipSlotCategory? slot = item.EquipSlotCategory.ValueNullable;
			if (slot is not null && slot.Value.SoulCrystal == 0) {
				uint amount = level.RowId;

				// Special handling for equipment that has ilvl sync. If the
				// item's ilvl at our current level is lower than its maximum
				// ilvl, then use that. Never increase the ilvl from this.
				if (item.SubStatCategory == 2) {
					if (Data.GrowSheet.TryGetRow(EffectiveLevel, out var growth) && growth.ItemLevelSync < amount)
						amount = growth.ItemLevelSync;
				}

				totalLevel += amount;

				// The following columns might have a value of -1, which means
				// the item effectively takes up those slots too, so we need to
				// add the item level to the total repeatedly for accurate calculations.
				if (slot.Value.OffHand == -1)
					totalLevel += amount;
				if (slot.Value.Head == -1)
					totalLevel += amount;
				if (slot.Value.Gloves == -1)
					totalLevel += amount;
				if (slot.Value.Legs == -1)
					totalLevel += amount;
				if (slot.Value.Feet == -1)
					totalLevel += amount;
			}

			// Stat Calculation

			// Weapon Stats
			if (slot.HasValue && slot.Value.MainHand == 1) {
				// Determine what kind of weapon damage we've got.
				bool want_magic = EffectiveClass == 129 || item.DamageMag >= item.DamagePhys;

				WeaponDamage = new StatData((uint) (want_magic ? Stat.MagDMG : Stat.PhysDMG)) {
					Gear = want_magic ? item.DamageMag : item.DamagePhys
				};

				// Handle High-Quality
				if (rawItem.HighQuality) {
					foreach (var entry in item.GetSpecialParams()) {
						uint statID = entry.Item1.RowId;
						if (statID == WeaponDamage.ID)
							WeaponDamage.Gear += entry.Item2;
					}
				}

				// Handle limits too.
				uint sid = WeaponDamage.ID;
				if (Params.TryGetValue(sid, out var param) || (Data.ParamSheet != null && Data.ParamSheet.TryGetRow(sid, out param))) {
					Params[sid] = param;

					if (synced) {
						ushort factor = item.EquipSlotCategory.RowId switch {
							1 => param.OneHandWeaponPercent,
							2 => param.OffHandPercent,
							13 => param.TwoHandWeaponPercent,
							14 => param.OneHandWeaponPercent,
							_ => 0,
						};

						float percentage = factor / 1000f;

						ushort value = sid switch {
							(int) Stat.PhysDMG => level.PhysicalDamage,
							(int) Stat.MagDMG => level.MagicalDamage,
							_ => 0,
						};

						int val = (int) Math.Round(
							value * percentage,
							MidpointRounding.AwayFromZero
						);

						WeaponDamage.Limit = val;
						if (WeaponDamage.Gear > val) {
							int difference = WeaponDamage.Gear - val;
							WeaponDamage.Gear = val;
						} else
							WeaponDamage.Limit = WeaponDamage.Gear;

					} else {
						// As far as I can determine, this is how the game handles
						// these values. This may be slightly inaccurate.
						WeaponDamage.Limit = (int) Math.Round(
							(level.BaseParam(sid) * param.EquipSlotCategoryPct(item.EquipSlotCategory.RowId))
							/ 1000f,
							MidpointRounding.AwayFromZero
						);
					}
				}

				// Ensure the limit holds the stats.
				int min = WeaponDamage.Base + WeaponDamage.Gear;
				if (WeaponDamage.Limit < min)
					WeaponDamage.Limit = min;

				// Now update the waste value, since we have a limit, and add
				// the waste to the main attribute.
				WeaponDamage.UpdateWaste();
			}

			// Base Item Stats
			if (item.DamagePhys > 0)
				AddGearStat(stats, (uint) Stat.PhysDMG, gear: (short) item.DamagePhys);
			if (item.DamageMag > 0)
				AddGearStat(stats, (uint) Stat.MagDMG, gear: (short) item.DamageMag);

			if (item.DefensePhys > 0)
				AddGearStat(stats, (uint) Stat.DEF, gear: (short) item.DefensePhys);
			if (item.DefenseMag > 0)
				AddGearStat(stats, (uint) Stat.MDF, gear: (short) item.DefenseMag);

			foreach (var entry in item.GetParams()) {
				uint statID = entry.Item1.RowId;
				short value = entry.Item2;

				AddGearStat(stats, statID, gear: value);
			}

			// High-Quality Stats
			if (rawItem.HighQuality)
				foreach (var entry in item.GetSpecialParams()) {
					uint statID = entry.Item1.RowId;
					short value = entry.Item2;

					AddGearStat(stats, statID, gear: value);
				}

			// Melded Materia
			int melds = 0;

			foreach (var rawMeld in rawItem.Melds) {
				if (rawMeld.ID == 0 || rawMeld.Grade < 0)
					continue;

				if (rawMeld.Row() is not Materia materia || rawMeld.Grade >= materia.Value.Count)
					continue;

				uint statID = materia.BaseParam.RowId;
				short value = materia.Value[rawMeld.Grade];

				uint materiaItem = materia.Item[rawMeld.Grade].RowId;
				//Plugin.INSTANCE.Logger.Debug($"Materia: {rawMeld.ID} -- Item: {materiaItem}");

				// If there's no item, then it's a relic stat.
				if (materiaItem == 0)
					AddGearStat(stats, statID, gear: value);
				else {
					AddGearStat(stats, statID, delta: value);

					// Track this item.
					melds++;

					MateriaCount[materiaItem] = MateriaCount.GetValueOrDefault(materiaItem) + 1;
				}
			}

			if (melds < item.MateriaSlotCount)
				EmptyMeldSlots += item.MateriaSlotCount - melds;

			// Finally, for each stat, we need to calculate the limit and
			// waste values. Then add the waste to the main Attributes.
			foreach (var stat in stats.Values) {
				uint statID = stat.ID;
				if (!Params.TryGetValue(statID, out var param))
					continue;

				// If we're synced, we need to calculate the stat limit
				// for gear of this slot, use that as the limit, and
				// reduce the gear score if it exceeds the value.
				if (synced) {
					if (stat.Gear == 0)
						stat.Limit = 0;
					else {
						ushort factor = param.EquipSlotCategoryPct(item.EquipSlotCategory.RowId, shouldThrow: false);

						float percentage = factor / 1000f;

						ushort value = level.BaseParam(statID, shouldThrow: false);

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
						(level.BaseParam(statID) * param.EquipSlotCategoryPct(item.EquipSlotCategory.RowId))
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

		// Make sure we have the correct speed stat (SkS / SpS)
		Stat speedStat = ((GameClass) EffectiveClass).IsMagical() ? Stat.SPS : Stat.SKS;
		EnsureStat(null, (uint) speedStat);

		// Finally, set the item level.
		int slots = 12;
		//Plugin.INSTANCE.Logger.Info($"Total Level: {totalLevel} -- Slots: {slots} -- Raw: {totalLevel / (float) slots}");
		ItemLevel = (ushort) Math.Round(totalLevel / (float) slots, MidpointRounding.ToZero);
	}

	private bool EnsureStat(Dictionary<uint, StatData>? stats, uint statID) {
		// Skip empty stats.
		if (statID == 0)
			return false;

		// TODO: Proper handling for "Main Attribute" and
		// "Secondary Attribute". For now, just skip them.
		if (statID == (int) Stat.MainAttribute || statID == (int) Stat.SecondaryAttribute)
			return false;

		// Get the parameter data row.
		if (!Params.ContainsKey(statID)) {
			if (Data.ParamSheet is null || !Data.ParamSheet.TryGetRow(statID, out var param))
				return false;

			Params[statID] = param;
		}

		// Ensure the equipment set has a record for this stat.
		if (!Attributes.ContainsKey(statID))
			Attributes[statID] = new StatData(statID);

		if (stats is not null) {
			// And track this stat on the item.
			if (!stats.ContainsKey(statID))
				stats[statID] = new(statID);
		}

		return true;
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
		// Make sure we have valid data to track.
		if ((gear == 0 && delta == 0) || !EnsureStat(stats, statID))
			return;

		// Add the value from this piece of equipment to the record.
		if (gear > 0)
			Attributes[statID].Gear += gear;
		if (delta > 0)
			Attributes[statID].Delta += delta;

		// And track this stat on the item.
		if (stats is not null) {
			if (gear > 0)
				stats[statID].Gear += gear;
			if (delta > 0)
				stats[statID].Delta += delta;
		}
	}

	#endregion

}
