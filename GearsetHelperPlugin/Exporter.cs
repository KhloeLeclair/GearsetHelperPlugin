using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

using GearsetHelperPlugin.Models;
using GearsetHelperPlugin.Sheets;

using Lumina.Excel.GeneratedSheets;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GearsetHelperPlugin;

internal class Exporter : IDisposable {

	private readonly Plugin Plugin;
	private readonly HttpClient Client;

	internal Exporter(Plugin plugin) {
		Plugin = plugin;
		Client = new HttpClient();

		Client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GearsetHelper", "1.0"));
	}

	public void Dispose() {
		Client.Dispose();
	}

	public bool CanExportEtro {
		get {
			if (!string.IsNullOrEmpty(Plugin.Config.EtroApiKey))
				return true;

			return false;
		}
	}

	public Task<ExportResponse> ExportTeamcraft(EquipmentSet gearset) {
		return Task.Run(() => Task_ExportTeamcraft(gearset));
	}

	public Task<ExportResponse> ExportXivGear(EquipmentSet gearset) {
		return Task.Run(() => Task_ExportXIVGear(gearset));
	}

	public Task<ExportResponse> ExportEtro(EquipmentSet gearset) {
		return Task.Run(() => Task_ExportEtro(gearset));
	}

	public Task<EtroLoginResponse> LoginEtro(string username, string password) {
		return Task.Run(() => Task_EtroLogin(username, password));
	}

	public Task<ExportResponse> ExportAriyala(EquipmentSet gearset) {
		return Task.Run(() => Task_ExportAriyala(gearset));
	}

	internal static void TryOpenURL(string url) {
		try {
			ProcessStartInfo ps = new(url) {
				UseShellExecute = true,
			};

			Process.Start(ps);
		} catch {
			/* Do nothing~ */
		}
	}

	internal class ExportResponse {
		public bool ShowSuccess { get; set; } = true;
		public string? Instructions { get; set; }
		public string? Clipboard { get; set; }
		public string? Url { get; set; }
		public string? Error { get; set; }
	}

	#region XIVGear Export

	private static readonly Dictionary<uint, string?> XIVGEAR_SLOT_MAP = new() {
		[1] = "Weapon",
		[2] = "OffHand",
		[13] = "Weapon",
		[3] = "Head",
		[4] = "Body",
		[5] = "Hand",
		[7] = "Legs",
		[8] = "Feet",
		[9] = "Ears",
		[10] = "Neck",
		[11] = "Wrist",
		[12] = null, // "RingLeft|RingRight",
		[17] = null
	};

	private ExportResponse Task_ExportXIVGear(EquipmentSet gearset) {
		ExportResponse result = new();

		try {
			Plugin.Logger.Info("Exporting gearset to XivGear.");

			var jobData = gearset.JobRow();
			if (jobData == null) {
				result.Error = "Unable to detect class.";
				return result;
			}

			string job = jobData.Abbreviation.ToString().ToUpper();
			string jobName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(jobData.Name) ?? "Job";

			string name = $"i{gearset.ItemLevel} {jobName}";
			if (!string.IsNullOrEmpty(gearset.PlayerName))
				name = $"{name} ({gearset.PlayerName})";

			if (name.Length > 30)
				name = name[..30];

			var items = new JObject();

			var set = new JObject() {
				{ "name", name },
				{ "items", items },
			};

			if (gearset.Food != null)
				set.Add("food", gearset.Food.ItemID);

			bool had_right = false;

			foreach (var rawItem in gearset.Items) {
				ExtendedItem? item = rawItem.Row();
				if (item == null)
					continue;

				uint slot = item.EquipSlotCategory.Row;
				string? mappedSlot;
				if (slot == 12) {
					mappedSlot = had_right ? "RingLeft" : "RingRight";
					had_right = true;

				} else if (!XIVGEAR_SLOT_MAP.TryGetValue(slot, out mappedSlot)) {
					Plugin.Logger.Warning($"Unknown Slot for Item: {item.Name} -- Slot: {slot}");
					continue;
				}

				if (mappedSlot == null)
					continue;

				var encodedItem = new JObject();
				var melds = new JArray();

				encodedItem.Add("id", rawItem.ID);
				encodedItem.Add("materia", melds);

				items.Add(mappedSlot, encodedItem);

				foreach (var raw in rawItem.Melds) {
					if (raw.ID == 0)
						continue;

					var materia = raw.Row();
					if (materia == null || raw.Grade >= materia.Item.Length)
						continue;

					var mitem = materia.Item[raw.Grade]?.Value;
					if (mitem == null)
						continue;

					melds.Add(new JObject() {
						{ "id", mitem.RowId }
					});
				}
			}

			if (gearset.Food is not null)
				items.Add("food", gearset.Food.ItemID);

			if (!ARIYALA_RACE_ID_MAP.TryGetValue(gearset.Tribe, out uint race))
				race = 0;

			var inv = new JArray(gearset.Items.Select(x => x.ID).ToArray());
			if (gearset.Food is not null)
				inv.Add(gearset.Food.ItemID);

			int level = gearset.EffectiveLevel;
			if (level < 90) level = 90;
			if (level > 90) level = 100;

			var obj = new JObject {
				{ "name", name },
				{ "sets", new JArray() { set } },
				{ "level", level },
				{ "job", job },
				{ "partyBonus", (int) Math.Floor(gearset.GroupBonus * 100) },
			};

			// We don't include the tribe because they have mapped the data in a very
			// stupid way. Users can set this themselves.
			//if (gearset.TribeRow() is Tribe tribe)
			//	obj.Add("race", tribe.Masculine.ToString());

			result.ShowSuccess = false;
			result.Instructions = Dalamud.Localization.Localize("export.visit-xivgear", "Visit the provided URL and paste the JSON below to import your gearset into XivGear.");
			result.Url = "https://xivgear.app/?page=importsheet";
			result.Clipboard = obj.ToString(Formatting.Indented);

		} catch (Exception ex) {
			Plugin.Logger.Error($"An error occurred while exporting gearset to XivGear.\nDetails: {ex}");
			result.Error = "An error occurred. See the log for details.";
		}

		return result;
	}

	#endregion

	#region Teamcraft Export

	private ExportResponse Task_ExportTeamcraft(EquipmentSet gearset) {
		ExportResponse result = new();

		try {

			Dictionary<uint, int> items = new();

			foreach (var item in gearset.Items) {
				Item? data = item.Row();
				var cat = data?.EquipSlotCategory.Value;
				if (cat is null || cat.SoulCrystal == 1)
					continue;

				items[item.ID] = items.GetValueOrDefault(item.ID) + 1;
			}

			if (gearset.Food is not null)
				items[gearset.Food.ItemID] = 1;

			if (gearset.Medicine is not null)
				items[gearset.Medicine.ItemID] = 1;

			string packed = string.Join(';', items.Select(x => $"{x.Key},null,{x.Value}"));
			byte[] bytes = Encoding.UTF8.GetBytes(packed);
			string encoded = Convert.ToBase64String(bytes);

			result.Url = $"https://ffxivteamcraft.com/import/{encoded}";

		} catch (Exception ex) {
			Plugin.Logger.Error($"An error occurred while exporting gearset to Teamcraft.\nDetails: {ex}");
			result.Error = "An error occurred. See the log for details.";
		}

		return result;
	}

	#endregion

	#region Etro Export

	#region Etro Data

	private static readonly Dictionary<uint, string?> ETRO_SLOT_MAP = new() {
		[1] = "weapon",
		[2] = "offHand",
		[13] = "weapon",
		[3] = "head",
		[4] = "body",
		[5] = "hands",
		[7] = "legs",
		[8] = "feet",
		[9] = "ears",
		[10] = "neck",
		[11] = "wrists",
		[12] = "fingerR",
		[17] = null
	};

	#endregion

	#region Etro Types

	internal class EtroJob {
		public int Id { get; set; }
		public string? Name { get; set; }
		public string? Abbrev { get; set; }
	}

	internal class EtroResponse {
		public string? Id { get; set; }

		[JsonProperty("access_token")]
		public string? AccessToken { get; set; }

		[JsonProperty("refresh_token")]
		public string? RefreshToken { get; set; }
	}

	internal class EtroError {
		public string? Detail { get; set; }

		[JsonProperty("non_field_errors")]
		public string?[]? OtherErrors { get; set; }
	}

	internal class EtroLoginResponse {
		public string? ApiKey { get; set; }
		public string? RefreshKey { get; set; }
		public string? Error { get; set; }
	}

	#endregion

	private async Task<EtroLoginResponse> Task_EtroLogin(string username, string password) {
		var result = new EtroLoginResponse();
		try {
			var obj = new JObject() {
				{"username", username},
				{"password", password}
			};

			var content = new StringContent(obj.ToString(Formatting.None), Encoding.UTF8, "application/json");

			var request = new HttpRequestMessage() {
				RequestUri = new Uri("https://etro.gg/api/auth/login/"),
				Method = HttpMethod.Post,
				Content = content
			};

			var response = await Client.SendAsync(request);

			if (response.IsSuccessStatusCode) {
				string? value = await response.Content.ReadAsStringAsync();
				Plugin.Logger.Info($"Success. Response: {value}");
				var parsedResponse = JsonConvert.DeserializeObject<EtroResponse>(value);
				if (parsedResponse != null && !string.IsNullOrEmpty(parsedResponse.AccessToken)) {
					result.ApiKey = parsedResponse.AccessToken;
					if (!string.IsNullOrEmpty(parsedResponse.RefreshToken))
						result.RefreshKey = parsedResponse.RefreshToken;
				} else
					result.Error = "Etro returned an invalid response.";

			} else {
				string? value = await response.Content.ReadAsStringAsync();
				Plugin.Logger.Error($"Failure. Error: {response.StatusCode}\nDetails:{value}");
				result.Error = "Etro returned invalid response.";
				var parsedError = JsonConvert.DeserializeObject<EtroError>(value);
				if (parsedError != null) {
					if (!string.IsNullOrEmpty(parsedError.Detail))
						result.Error = $"Etro returned an error: {parsedError.Detail}";
					else if (parsedError.OtherErrors != null) {
						string? err = string.Join(", ", parsedError.OtherErrors.Where(x => !string.IsNullOrEmpty(x)));
						if (!string.IsNullOrEmpty(err))
							result.Error = err;
					}
				}
			}

		} catch (Exception ex) {
			Plugin.Logger.Error($"An error occurred while logging in to Etro.\nDetails: {ex}");
			result.Error = ex.Message;
		}

		return result;
	}

	private async Task<ExportResponse> Task_ExportEtro(EquipmentSet gearset) {
		ExportResponse result = new();

		try {
			if (!CanExportEtro) {
				result.Error = "Unable to export to Etro. Not authenticated.";
				return result;
			}

			Plugin.Logger.Info("Exporting gearset to Etro.");

			ClassJob? jobData = gearset.JobRow();
			if (jobData == null) {
				result.Error = "Unable to detect class.";
				return result;
			}

			uint minIlvl = 999;
			uint maxIlvl = 1;

			string jobName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(jobData.Name) ?? "Job";
			int job = (int) jobData.RowId;

			string name = $"i{gearset.ItemLevel} {jobName}";
			if (!string.IsNullOrEmpty(gearset.PlayerName))
				name = $"{name} ({gearset.PlayerName})";

			if (name.Length > 30)
				name = name[..30];

			var materiaMap = new JObject();
			var obj = new JObject() {
				["name"] = name,
				["materia"] = materiaMap,
				["job"] = job
			};

			bool had_right = false;

			foreach (var item in gearset.Items) {
				var data = item.Row();
				if (data == null)
					continue;

				uint slot = data.EquipSlotCategory.Row;
				string? mappedSlot;
				string materiaSlot = item.ID.ToString();

				if (slot == 12) {
					mappedSlot = had_right ? "fingerL" : "fingerR";
					materiaSlot += had_right ? "L" : "R";
					had_right = true;

				} else if (!ETRO_SLOT_MAP.TryGetValue(slot, out mappedSlot)) {
					Plugin.Logger.Warning($"Unknown Slot for Item: {data.Name} -- Slot: {slot}");
					continue;
				}

				if (mappedSlot == null)
					continue;

				if (obj.ContainsKey(mappedSlot)) {
					Plugin.Logger.Warning($"Duplicate item slot usage for Item: {data.Name} -- Slot: {slot} = {mappedSlot}");
					continue;
				}

				uint ilvl = data.LevelItem.Row;
				int level = data.LevelEquip;

				if (ilvl < minIlvl)
					minIlvl = ilvl;
				if (ilvl > maxIlvl)
					maxIlvl = ilvl;

				obj.Add(mappedSlot, item.ID);

				var melds = new JObject();
				int i = 1;

				foreach (var raw in item.Melds) {
					if (raw.ID == 0)
						continue;

					var materia = raw.Row();
					if (materia == null || raw.Grade >= materia.Item.Length)
						continue;

					var mitem = materia.Item[raw.Grade]?.Value;
					if (mitem == null)
						continue;

					melds.Add(i.ToString(), mitem.RowId);
					i++;
				}

				if (i > 1)
					materiaMap.Add(materiaSlot, melds);
			}

			obj.Add("minItemLevel", minIlvl);
			obj.Add("maxItemLevel", maxIlvl);

			if (gearset.Tribe != 0)
				obj.Add("clan", gearset.Tribe);

			if (gearset.Food is not null)
				obj.Add("food", gearset.Food.FoodID);

			if (gearset.Medicine is not null)
				obj.Add("medicine", gearset.Medicine.FoodID);

			if (jobData.Role == 0 && gearset.HasCrystal)
				obj.Add("buffs", new JObject() {
					{ "specialist", true }
				});

			Plugin.Logger.Debug($"Result:\n{obj.ToString(Formatting.None)}");

			var content = new StringContent(obj.ToString(Formatting.None), Encoding.UTF8, "application/json");

			var request = new HttpRequestMessage() {
				RequestUri = new Uri("https://etro.gg/api/gearsets/"),
				Method = HttpMethod.Post,
				Content = content
			};

			if (!string.IsNullOrEmpty(Plugin.Config.EtroApiKey))
				request.Headers.Add("Authorization", $"Bearer {Plugin.Config.EtroApiKey}");

			var response = await Client.SendAsync(request);

			if (response.IsSuccessStatusCode) {
				string? value = await response.Content.ReadAsStringAsync();
				Plugin.Logger.Info($"Success. Response: {value}");
				var parsedResponse = JsonConvert.DeserializeObject<EtroResponse>(value);
				if (parsedResponse != null && !string.IsNullOrEmpty(parsedResponse.Id)) {
					result.Url = $"https://etro.gg/gearset/{parsedResponse.Id}";

				} else {
					result.Error = "Etro returned invalid response.";
				}

			} else {
				string? value = await response.Content.ReadAsStringAsync();
				Plugin.Logger.Error($"Failure. Error: {response.StatusCode}\nDetails:{value}");
				var parsedError = JsonConvert.DeserializeObject<EtroError>(value);
				if (parsedError != null && !string.IsNullOrEmpty(parsedError.Detail)) {
					result.Error = $"Etro returned an error: {parsedError.Detail}";
				} else {
					result.Error = "Etro returned invalid response.";
				}
			}

		} catch (Exception ex) {
			Plugin.Logger.Error($"An error occurred while exporting gearset to Etro.\nDetails: {ex}");
			result.Error = "An error occurred. See the log for details.";
		}

		return result;
	}


	#endregion

	#region Ariyala Export

	#region Ariyala Data

	private static readonly Dictionary<uint, uint> ARIYALA_RACE_ID_MAP = new() {
		{ 1, 0 },   // Midlander
		{ 2, 1 },   // Highlander
		{ 3, 6 },   // Wildwood
		{ 4, 7 },   // Duskwight
		{ 5, 4 },   // Plainsfolk
		{ 6, 5 },   // Dunesfolk
		{ 7, 2 },   // Seaker of the Sun
		{ 8, 3 },   // Keeper of the Moon
		{ 9, 9 },   // Sea Wolf
		{ 10, 8 },  // Hellsguard
		{ 11, 11 }, // Raen
		{ 12, 10 }, // Xaela
		{ 13, 14 }, // Helions
		{ 14, 15 }, // The Lost
		{ 15, 12 }, // Rava
		{ 16, 13 }, // Veena
	};

	private static readonly Dictionary<uint, string> ARIYALA_STAT_MAP = new() {
		[1] = "STR",  // Strength
		[2] = "DEX",  // Dexterity
		[3] = "VIT",  // Vitality
		[4] = "INT",  // Intelligence
		[5] = "MND",  // Mind
		[6] = "PIE",  // Piety
		[19] = "TEN", // Tenacity
		[22] = "DHT", // Direct Hit
		[27] = "CRT", // Critical Hit
		[44] = "DET", // Determination
		[45] = "SKS", // Skill Speed
		[46] = "SPS", // Spell Speed

		[10] = "GP",  // GP
		[72] = "GTH", // Gathering
		[73] = "PCP", // Perception

		[11] = "CP",  // CP
		[70] = "CMS", // Craftsmanship
		[71] = "CRL", // Control
	};

	private static readonly string[] VALID_JOBS = new string[] {
		"PLD",
		"WAR",
		"GNB",
		"DRK",

		"WHM",
		"SCH",
		"AST",
		"SGE",

		"MNK",
		"DRG",
		"NIN",
		"SAM",
		"RPR",
		"VPR",
		"PCT",

		"BRD",
		"MCH",
		"DNC",

		"BLM",
		"SMN",
		"RDM",
		"BLU",

		"CRP",
		"BSM",
		"ARM",
		"GSM",
		"LTW",
		"WVR",
		"ALC",
		"CUL",

		"MIN",
		"BTN",
		"FSH"
	};

	private static readonly Dictionary<uint, string?> ARIYALA_SLOT_MAP = new() {
		[1] = "mainhand",
		[2] = "offhand",
		[13] = "mainhand",
		[3] = "head",
		[4] = "chest",
		[5] = "hands",
		[7] = "legs",
		[8] = "feet",
		[9] = "ears",
		[10] = "neck",
		[11] = "wrist",
		[12] = "ringRight",
		[17] = null
	};

	#endregion

	private async Task<ExportResponse> Task_ExportAriyala(EquipmentSet gearset) {
		ExportResponse result = new();

		try {
			Plugin.Logger.Info("Exporting gearset to Ariyala.");

			var jobData = gearset.JobRow();
			if (jobData == null) {
				result.Error = "Unable to detect class.";
				return result;
			}

			// This is very stupid code, but I can't be bothered to refactor it now.
			string? job = null;
			string abbrev = jobData.Abbreviation.ToString().ToUpper();
			if (VALID_JOBS.Contains(abbrev))
				job = abbrev;

			if (job == null) {
				result.Error = "Unable to map job to Ariyala.";
				return result;
			}

			var items = new JObject();
			var materiaData = new JObject();
			var inventory = new JArray();

			uint minIlvl = 999;
			uint maxIlvl = 1;

			int minLevel = 100;
			int maxLevel = 1;

			bool had_right = false;

			foreach (var rawItem in gearset.Items) {
				ExtendedItem? item = rawItem.Row();
				if (item == null)
					continue;

				uint slot = item.EquipSlotCategory.Row;
				string? mappedSlot;
				if (slot == 12) {
					mappedSlot = had_right ? "ringLeft" : "ringRight";
					had_right = true;

				} else if (!ARIYALA_SLOT_MAP.TryGetValue(slot, out mappedSlot)) {
					Plugin.Logger.Warning($"Unknown Slot for Item: {item.Name} -- Slot: {slot}");
					continue;
				}

				if (mappedSlot == null)
					continue;

				uint ilvl = item.LevelItem.Row;
				int level = item.LevelEquip;

				if (ilvl < minIlvl)
					minIlvl = ilvl;
				if (ilvl > maxIlvl)
					maxIlvl = ilvl;

				if (level < minLevel)
					minLevel = level;
				if (level > maxLevel)
					maxLevel = level;

				items.Add(mappedSlot, rawItem.ID);

				List<string> melds = new();

				foreach (var rawMateria in rawItem.Melds) {
					if (rawMateria.ID == 0)
						continue;

					var materia = rawMateria.Row();
					if (materia == null || rawMateria.Grade >= materia.Value.Length)
						continue;

					uint stat = materia.BaseParam.Row;
					if (!ARIYALA_STAT_MAP.TryGetValue(stat, out string? mappedStat)) {
						Plugin.Logger.Warning($"Unknown Stat for Materia: {materia.Item[rawMateria.Grade]?.Value?.Name} -- Stat: {stat}");
						continue;
					}

					if (mappedStat == null)
						continue;

					melds.Add($"{mappedStat}:{rawMateria.Grade}");
				}

				if (melds.Count > 0)
					materiaData.Add($"{mappedSlot}-{rawItem.ID}", new JArray(melds));
			}

			if (gearset.Food is not null)
				items.Add("food", gearset.Food.ItemID);

			var datasets = new JObject() {
				[job] = new JObject() {
					["normal"] = new JObject() {
						["items"] = items,
						["materiaData"] = materiaData,
						["bonusStats"] = new JObject() { }
					},
					["base"] = new JObject() {
						["items"] = new JObject(),
						["materiaData"] = new JObject(),
						["bonusStats"] = new JObject() { }
					}
				}
			};

			var filter = new JObject() {
				["iLevel"] = new JObject() {
					["min"] = minIlvl,
					["max"] = maxIlvl
				},
				["equipLevel"] = new JObject() {
					["min"] = minLevel,
					["max"] = maxLevel
				},
				["rarity"] = new JObject() {
					["white"] = true,
					["green"] = true,
					["blue"] = true,
					["relic"] = true,
					["aetherial"] = true
				},
				["category"] = new JObject() {
					["general"] = true,
					["crafted"] = true,
					["pvp"] = false,
					["food"] = true,
					["str"] = true
				}
			};

			if (!ARIYALA_RACE_ID_MAP.TryGetValue(gearset.Tribe, out uint race))
				race = 0;

			var inv = new JArray(gearset.Items.Select(x => x.ID).ToArray());
			if (gearset.Food is not null)
				inv.Add(gearset.Food.ItemID);

			var obj = new JObject {
				{ "version", 6 },
				{ "content", job },
				{ "datasets", datasets },
				{ "raceID", race },
				{ "level", 90 },
				{ "filter", filter },
				{ "myInventory", inv }
			};

			Plugin.Logger.Debug($"Result:\n{obj.ToString(Newtonsoft.Json.Formatting.None)}");

			var content = new StringContent(obj.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/x-www-form-urlencoded");

			var request = new HttpRequestMessage() {
				RequestUri = new Uri("https://ffxiv.ariyala.com/store.app"),
				Method = HttpMethod.Post,
				Content = content
			};

			//request.Headers.Add("Origin", "https://ffxiv.ariyala.com/");

			var response = await Client.SendAsync(request);

			if (response.IsSuccessStatusCode) {
				string? value = await response.Content.ReadAsStringAsync();
				Plugin.Logger.Info($"Success. ID: {value}");
				if (!string.IsNullOrEmpty(value))
					result.Url = $"https://ffxiv.ariyala.com/{value}";
			} else {
				string? value = await response.Content.ReadAsStringAsync();
				Plugin.Logger.Error($"Failure. Error: {response.StatusCode}\nDetails:{value}");
				result.Error = "Ariyala returned invalid response.";
			}

		} catch (Exception ex) {
			/* Do nothing~ */
			Plugin.Logger.Error($"An error occurred while exporting gearset to Ariyala.\nDetails: {ex}");
			result.Error = "An error occurred. See log for details.";
		}

		return result;
	}

	#endregion

}
