using Lumina.Excel.GeneratedSheets;
using System.Collections.Generic;

using GearsetHelperPlugin.Sheets;
using System.Data;

namespace GearsetHelperPlugin;

internal enum GameClass : byte {
	Adventurer = 0,
	Gladiator = 1,
	Pugilist = 2,
	Marauder = 3,
	Lancer = 4,
	Archer = 5,
	Conjurer = 6,
	Thaumaturge = 7,
	Carpenter = 8,
	Blacksmith = 9,
	Armorer = 10,
	Goldsmith = 11,
	Leatherworker = 12,
	Weaver = 13,
	Alchemist = 14,
	Culinarian = 15,
	Miner = 16,
	Botanist = 17,
	Fisher = 18,
	Paladin = 19,
	Monk = 20,
	Warrior = 21,
	Dragoon = 22,
	Bard = 23,
	WhiteMage = 24,
	BlackMage = 25,
	Arcanist = 26,
	Summoner = 27,
	Scholar = 28,
	Rogue = 29,
	Ninja = 30,
	Machinist = 31,
	DarkKnight = 32,
	Astrologian = 33,
	Samurai = 34,
	RedMage = 35,
	BlueMage = 36,
	Gunbreaker = 37,
	Dancer = 38,
	Reaper = 39,
	Sage = 40
}


internal static partial class Data {

	public static ClassJob? ToRow(this GameClass job) {
		if (!CheckSheets())
			return null;
		return ClassSheet.GetRow((uint) job);
	}

	public static GameClass ToGameClass(this ClassJob job) {
		return (GameClass) job.RowId;
	}

	public static GameClass? ToParentJob(this GameClass job) {
		return job.ToRow()?.ClassJobParent?.Value?.ToGameClass();
	}

	public static bool IsPhysical(this ClassJob job) => job.ClassJobCategory.Row == 30;

	public static bool IsMagical(this ClassJob job) => job.ClassJobCategory.Row == 31;

	public static bool IsCrafter(this ClassJob job) => job.ClassJobCategory.Row == 33;

	public static bool IsGatherer(this ClassJob job) => job.ClassJobCategory.Row == 32;	

	public static bool IsTank(this ClassJob job) => job.Role == 1;

	public static bool IsMelee(this ClassJob job) => job.Role == 2;

	public static bool IsRanged(this ClassJob job) => job.Role == 3;

	public static bool IsHealer(this ClassJob job) => job.Role == 4;

	public static bool IsPhysicalRanged(this ClassJob job) => job.IsRanged() && job.IsPhysical();

	public static bool IsMagicalRanged(this ClassJob job) => job.IsRanged() && job.IsMagical();

	public static bool IsPhysical(this GameClass gc) => gc.ToRow()?.IsPhysical() ?? false;

	public static bool IsMagical(this GameClass gc) => gc.ToRow()?.IsMagical() ?? false;

	public static bool IsCrafter(this GameClass gc) => gc.ToRow()?.IsCrafter() ?? false;

	public static bool IsGatherer(this GameClass gc) => gc.ToRow()?.IsGatherer() ?? false;

	public static bool IsTank(this GameClass gc) => gc.ToRow()?.IsTank() ?? false;

	public static bool IsHealer(this GameClass gc) => gc.ToRow()?.IsHealer() ?? false;

	public static bool IsRanged(this GameClass gc) => gc.ToRow()?.IsRanged() ?? false;

	public static bool IsMelee(this GameClass gc) => gc.ToRow()?.IsMelee() ?? false;

	public static bool IsPhysicalRanged(this GameClass gc) => gc.ToRow()?.IsPhysicalRanged() ?? false;

	public static bool IsMagicalRanged(this GameClass gc) => gc.ToRow()?.IsMagicalRanged() ?? false;


	public static IEnumerable<ExtendedAction> GetMatchingActions(this GameClass job, uint level) {
		if (!CheckSheets())
			yield break;

		uint jobId = (uint) job;

		GameClass? parent = job.ToParentJob();
		uint? parentId = parent.HasValue ? (uint) parent.Value : null;

		foreach(var action in ActionSheet) {
			if (action.IsPvP || !action.IsPlayerAction || action.ClassJobLevel > level)
				continue;

			uint row = action.ClassJob.Row;
			if ( row == jobId || (parentId.HasValue && parentId.Value == row) )
				yield return action;
		}
	}

	public static IEnumerable<int> GetExamplePotencies(this GameClass job, uint level) {
		// TODO: Get ambitious and actually use the level parameter.

		// Future Khloe, here's some JS to extract numbers from the job guide website:

		/*
tester = /\b(?:\d,?)?\d{1,2}0(?!%)\b/gm;
function toText(node) { return Array.from(node.childNodes).map(node => node instanceof HTMLBRElement ? '\n' : node.textContent).join('') };
function extractAll(text) { tester.lastIndex = -1; const out = []; let match; while((match = tester.exec(text))){ out.push(parseInt(match[0].replace(/,/g, ''), 10)); }; return out};
stuff = [...document.querySelectorAll('tr')].filter(x => /^pve_action_/.test(x.id)).map(x => [x.querySelector('.skill strong').textContent.trim(), extractAll(toText(x.querySelector('.content')))]);
[...new Set(stuff.map(x => x[1]).flat())].sort((a,b) => a-b)
		*/

		// Paladin
		if (job == GameClass.Gladiator || job == GameClass.Paladin)
			return new int[] {
				100, // Base
				200, // Fast Blade
				270, // Spirits Within
				300, // Riot Blade (Combo)
				330, // Rage of Halone (Combo)
				400, // Royal Authority (Combo)
				650, // Holy Spirit (Request-a-cat)
				700, // Goring Blade
				820, // Blade of Truth (Request-a-cat)
				920, // Confiteor (Request-a-cat)
			};

		// Warrior
		if (job == GameClass.Marauder || job == GameClass.Warrior)
			return new int[] {
				100, // Base
				200, // Heavy Swing
				300, // Maim (Combo)
				330, // Inner Beast
				440, // Storm's Path (Combo)
				520, // Fell Cleave
				660, // Inner Chaos
				700  // Primal Rend
			};

		// Store Brand Warrior (but the store is Hot Topic)
		if (job == GameClass.DarkKnight)
			return new int[] {
				100, // Base
				170, // Hard Slash
				240, // Abyssal Drain
				260, // Syphon Strike
				300, // Edge of Darkness
				340, // Souleater
				460, // Edge of Shadow
				500, // Bloodspiller
				600, // Shadowbringer
			};

		// Three DPS in Thancred's Trenchcoat
		if (job == GameClass.Gunbreaker)
			return new int[] {
				100, // Base
				200, // Keen Edge
				250, // Danger Zone
				300, // Brutal Shell
				380, // Burst Strike
				460, // Savage Claw
				540, // Wicked Talon
				720, // Blasting Zone
				1200, // Double Down
			};


		// White Mage
		if (job == GameClass.Conjurer || job == GameClass.WhiteMage)
			return new int[] {
				50,  // Aero
				100, // Base
				140, // Stone
				190, // Stone 2
				220, // Stone 3
				260, // Stone 4
				400, // Assize
				290, // Glare
				310, // Glare 3
				1240, // BLOOD FOR THE BLOOD-- ahem. Afflatus Misery
			};

		// Read a Book (Get your Medical Degree)
		if (job == GameClass.Scholar)
			return new int[] {
				20,  // Bio
				40,  // Bio 2
				70,  // Biolysis
				100, // Base
				150, // Ruin
				165, // Art of War
				180, // Art of War 2
				220, // Ruin 2
				240, // Broil 2
				295, // Broil 4
			};

		// Read a Star (... and still get a medical degree somehow???)
		if (job == GameClass.Astrologian)
			return new int[] {
				40,  // Combust
				50,  // Combust 2
				100, // Base
				120, // Gravity
				130, // Gravity 2
				150, // Malefic
				160, // Malefic 2
				190, // Malefic (just try to guess this) 3
				250, // Fall Malefic
				310, // Earthly Star (but patiently)
			};

		// Read a Gundam (okay do they just give medical degrees out to anyone or)
		if (job == GameClass.Sage)
			return new int[] {
				40,  // Eukrasian Dosis
				60,  // Eukrasian Dosis 2
				100, // Base
				160, // Dyskrasia
				300, // Dosis, Toxikon
				320, // Dosis 2
				330, // Dosis 3
				400, // Phlegma
				490, // Phlegma 2
				600, // Phlegma 3
			};

		// Monk
		if (job == GameClass.Pugilist || job == GameClass.Monk)
			return new int[] {
				100, // Base
				180, // Steel Peak
				300, // True Strike
				340, // Forbidden Chakra
				450, // Celstial Revolution
				550, // Six-sided Star
				600, // Elixir Field
				700, // Rising Phoenix
				850, // Tornado Kick
				1150, // Phantom Rush
			};

		// Jump Man
		if (job == GameClass.Lancer || job == GameClass.Dragoon)
			return new int[] {
				100, // Base
				150, // Piercing Talon, Coerthan Torment
				200, // Mirage Dive
				260, // Chaos Thrust, Fang and Claw, Geirskogul
				280, // Vorpal Thrust, Raiden Thrust
				300, // Dragonfire Dive, Wheeling Thrust, Chaotic Spring
				360, // Nastrond
				400, // Full Thrust, High Jump
				480, // Heavens' Thrust
				620, // Stardiver
			};

		// Naruto
		if (job == GameClass.Rogue || job == GameClass.Ninja)
			return new int[] {
				100, // Base
				150, // Mug, Dream Within a Tream
				200, // Assassinate, Huraijin
				350, // Katon, Hyoton
				400, // Trick Attack
				450, // Fuma Shuriken
				500, // Bhavacakra, Suiton
				560, // Forked Raiju, Fleeting Raiju
				600, // Phantom Kamaitachi, Goka Mekkyaku
				650, // Raiton
				1300, // Hyosho Ranyu
			};

		// While you were wondering why all these values are hard-coded, I studied the blade
		if (job == GameClass.Samurai)
			return new int[] {
				100, // Base, Hissatsu: Gyoten, Hissatsu: Yaten, Fuko
				200, // Hakaze, Shoha 2, Highbanana, Kaeshi: Highbanana
				250, // Hissatsu: Shinten
				280, // Jinpu, Shifu
				300, // Yukikaze, Tenka Goken, Kaeshi: Goken
				380, // Gekko, Kasha
				500, // Hissatsu: Guren
				560, // Shoha,
				640, // Midare Setsugekka, Kaeshi: Setsugekka
				860, // Hissatsu: Senei, Ogi Namikiri, Kaeshi: Namikiri
			};

		// Seasons don't fear this class, nor do the winds, the sun, or the rain
		if (job == GameClass.Reaper)
			return new int[] {
				100, // Base, Whorl of Death, Lemure's Scythe
				200, // Guillotine, Grim Reaping
				240, // Lemure's Slice
				300, // Shadow of Death, Harpe
				400, // Waxing Slice, Unveiled Gibbet, Unveilled Gallows
				460, // Soul Slice
				500, // Infernal Slice
				600, // Harvest Moon
				1000, // Plentiful Harvest
				1100, // Communio
			};

		// Let me play you the song of my people in limsa using a bot
		if (job == GameClass.Archer || job == GameClass.Bard)
			return new int[] {
				100, // Base
				150,
				180,
				200,
				220,
				240,
				280,
				320,
				500,
				600
			};

		// Let me play you a gun
		if (job == GameClass.Machinist)
			return new int[] {
				100, // Base
				150,
				200,
				240,
				320,
				390,
				480,
				600,
				680,
				780
			};

		// I will now perform my peoples traditional dance.
		if (job == GameClass.Dancer)
			return new int[] {
				100, // Base
				200,
				300,
				350,
				480,
				540,
				600,
				720,
				900,
				1200
			};

		// I will now perform my peoples traditional explosion
		if (job == GameClass.Thaumaturge || job == GameClass.BlackMage)
			return new int[] {
				100, // Base
				140,
				180,
				220,
				280,
				310,
				340,
				500,
				600,
				880
			};

		// Read a Book (flunk out of medical school, make it everyone's problem)
		if (job == GameClass.Arcanist || job == GameClass.Summoner)
			return new int[] {
				100, // Base
				150,
				200,
				240,
				300,
				400,
				500,
				600,
				750,
				1300
			};

		// I will now perform my peoples traditional raise macro
		if (job == GameClass.RedMage)
			return new int[] {
				100, // Base
				150,
				210,
				260,
				320,
				380,
				500,
				600,
				680,
				750
			};

		return YieldSamplePotencies();
	}

	internal static IEnumerable<int> YieldSamplePotencies() {
		for (int potency = 100; potency <= 1000; potency += 50)
			yield return potency;
	}

	public static float GetTraitModifier(this GameClass job, uint level) {
		// Archer, Bard, Machinist
		if (job == GameClass.Archer || job == GameClass.Bard || job == GameClass.Machinist)
			return level switch {
				>= 40 => 1.2f,
				>= 20 => 1.1f,
				_ => 1f
			};

		// Dancer
		if (job == GameClass.Dancer)
			return level switch {
				>= 60 => 1.2f,
				>= 50 => 1.1f,
				_ => 1f
			};

		// Blue Mage
		// Takes priority over Disciple of Magic.
		if (job == GameClass.BlueMage)
			return level switch {
				>= 50 => 1.5f,
				>= 40 => 1.4f,
				>= 30 => 1.3f,
				>= 20 => 1.2f,
				>= 10 => 1.1f,
				_ => 1f
			};

		// Maim and Mend
		if (job.IsMagical())
			return level switch {
				>= 40 => 1.3f,
				>= 20 => 1.1f,
				_ => 1f
			};

		// You get nothing. Good day sir.
		return 1f;
	}

	public static float GetAttackScalar(this GameClass job, uint level) {
		if (job.IsTank())
			return GetTankAttackScalar(level);
		return GetAttackScalar(level);
	}


	public static float GetTankAttackScalar(uint level) {
		if (level <= 80)
			return (int) level + 35f;

		return ((int) level - 80) * 4.1f + 115f;
	}

	public static float GetAttackScalar(uint level) {
		if (level <= 50)
			return 75;

		if (level <= 70)
			return ((int) level - 50) * 2.5f + 75f;

		if (level <= 80)
			return ((int) level - 70) * 4f + 125f;

		return ((int) level - 80) * 3f + 165;
	}

}
