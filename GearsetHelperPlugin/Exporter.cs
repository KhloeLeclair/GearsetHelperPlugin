using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Dalamud.Logging;

using System.Net.Http;
using System.Net.Http.Headers;

using Lumina.Excel.GeneratedSheets;
using GearsetExportPlugin.Sheets;

namespace GearsetExportPlugin;

internal class Exporter : IDisposable {

	private readonly Plugin Plugin;
	private readonly HttpClient Client;

	public bool Exporting { get; private set; } = false;

	public string? Error { get; private set; } = null;

	internal Exporter(Plugin plugin) {
		Plugin = plugin;
		Client = new HttpClient();

		Client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GearsetHelper", "1.0"));
	}

	public void Dispose() {
		Client.Dispose();
	}

	public void ClearError() {
		if (!Exporting)
			Error = null;
	}

	public bool CanExportEtro {
		get {
			if (!string.IsNullOrEmpty(Plugin.Config.EtroApiKey))
				return true;

			if (!string.IsNullOrEmpty(Plugin.Config.EtroUsername) && !string.IsNullOrEmpty(Plugin.Config.EtroPassword))
				return true;

			return false;
		}
	}

	public void ExportEtro(Gearset gearset) {
		if (Exporting || !CanExportEtro)
			return;

		Task.Run(async () => await Task_ExportEtro(gearset));
	}

	public void ExportAriyala(Gearset gearset) {
		if (Exporting)
			return;

		Task.Run(async () => await Task_ExportAriyala(gearset));
	}

	private static void TryOpenURL(string url) {
		try {
			ProcessStartInfo ps = new(url) {
				UseShellExecute = true,
			};

			Process.Start(ps);
		} catch {
			/* Do nothing~ */
		}
	}

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
	}

	internal class EtroError {
		public string? Detail { get; set; }
	}

	#endregion

	private async Task Task_ExportEtro(Gearset gearset) {
		Exporting = true;
		Error = null;

		try {
			PluginLog.LogInformation("Exporting gearset to Etro.");

			if (gearset.Category == null) {
				Error = "Unable to detect class.";
				Exporting = false;
				return;
			}

			uint minIlvl = 999;
			uint maxIlvl = 1;

			var ItemSheet = Plugin.DataManager.GetExcelSheet<ExtendedItem>();
			var MateriaSheet = Plugin.DataManager.GetExcelSheet<Materia>();
			if (ItemSheet == null || MateriaSheet == null) {
				Error = "Unable to load data.";
				Exporting = false;
				return;
			}

			string rawJobs = await Client.GetStringAsync("https://etro.gg/api/jobs/");
			var jobs = JsonConvert.DeserializeObject<EtroJob[]>(rawJobs);
			string jobName = gearset.Category?.Name ?? "Job";

			int? job = null;
			if (jobs != null && gearset.Category != null)
				foreach (var entry in jobs) {
					string? valid = entry.Abbrev;
					if (!string.IsNullOrEmpty(valid)) {
						var prop = gearset.Category.GetType().GetProperty(valid, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
						if (prop != null && prop.GetValue(gearset.Category) is bool b && b) {
							job = entry.Id;
							jobName = entry.Name ?? jobName;
							break;
						}
					}
				}

			if (job == null) {
				Error = "Unable to map job to Etro.";
				Exporting = false;
				return;
			}

			var materiaMap = new JObject();
			var obj = new JObject() {
				["name"] = string.IsNullOrEmpty(gearset.PlayerName) ? $"Exported {jobName} Gearset" : $"{gearset.PlayerName}'s {jobName} Gear",
				["materia"] = materiaMap,
				["job"] = job
			};

			bool had_right = false;

			foreach (var item in gearset.Items) {
				var data = item.GetItem(ItemSheet);
				if (data == null)
					continue;

				uint slot = data.EquipSlotCategory.Row;
				string? mappedSlot;
				string materiaSlot = item.ItemID.ToString();

				if (slot == 12) {
					mappedSlot = had_right ? "fingerL" : "fingerR";
					materiaSlot += had_right ? "L" : "R";
					had_right = true;

				} else if (!ETRO_SLOT_MAP.TryGetValue(slot, out mappedSlot)) {
					PluginLog.Information($"Unknown Slot for Item: {data.Name} -- Slot: {slot}");
					continue;
				}

				if (mappedSlot == null)
					continue;

				if (obj.ContainsKey(mappedSlot)) {
					PluginLog.Information($"Duplicate item slot usage for Item: {data.Name} -- Slot: {slot} = {mappedSlot}");
					continue;
				}

				uint ilvl = data.LevelItem.Row;
				int level = data.LevelEquip;

				if (ilvl < minIlvl)
					minIlvl = ilvl;
				if (ilvl > maxIlvl)
					maxIlvl = ilvl;

				obj.Add(mappedSlot, item.ItemID);

				var melds = new JObject();
				int i = 1;

				foreach (var raw in item.Melds) {
					if (raw.ID == 0)
						continue;

					var materia = raw.GetMateria(MateriaSheet);
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

			PluginLog.Information($"Result:\n{obj.ToString(Formatting.None)}");

			var content = new StringContent(obj.ToString(Formatting.None), Encoding.UTF8, "application/json");

			var request = new HttpRequestMessage() {
				RequestUri = new Uri("https://etro.gg/api/gearsets/"),
				Method = HttpMethod.Post,
				Content = content
			};

			if (!string.IsNullOrEmpty(Plugin.Config.EtroApiKey))
				request.Headers.Add("Authorization", $"Bearer {Plugin.Config.EtroApiKey}");
			else if (!string.IsNullOrEmpty(Plugin.Config.EtroUsername) && !string.IsNullOrEmpty(Plugin.Config.EtroPassword)) {
				string auth = $"{Plugin.Config.EtroUsername}:{Plugin.Config.EtroPassword}";
				string encoded = Convert.ToBase64String(UTF8Encoding.UTF8.GetBytes(auth));
				request.Headers.Add("Authorization", $"Basic {encoded}");
			}

			var response = await Client.SendAsync(request);

			if (response.IsSuccessStatusCode) {
				string? value = await response.Content.ReadAsStringAsync();
				PluginLog.LogInformation($"Success. Response: {value}");
				var parsedResponse = JsonConvert.DeserializeObject<EtroResponse>(value);
				if (parsedResponse != null && !string.IsNullOrEmpty(parsedResponse.Id)) {
					TryOpenURL($"https://etro.gg/gearset/{parsedResponse.Id}");

				} else {
					Error = "Etro returned invalid response.";
				}

			} else {
				string? value = await response.Content.ReadAsStringAsync();
				PluginLog.LogError($"Failure. Error: {response.StatusCode}\nDetails:{value}");
				var parsedError = JsonConvert.DeserializeObject<EtroError>(value);
				if (parsedError != null && !string.IsNullOrEmpty(parsedError.Detail)) {
					Error = $"Etro returned an error: {parsedError.Detail}";
				} else {
					Error = "Etro returned invalid response.";
				}
			}

		} catch (Exception ex) {
			PluginLog.Error($"An error occurred while exporting gearset to Etro.\nDetails: {ex}");
			Error = "An error occurred.";
		}

		Exporting = false;
	}


	#endregion

	#region Ariyala Export

	#region Ariyala Data

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

	private async Task Task_ExportAriyala(Gearset gearset) {
		Exporting = true;
		Error = null;

		try {
			PluginLog.LogInformation("Exporting gearset to Ariyala.");

			if (gearset.Category == null) {
				Error = "Unable to detect class.";
				Exporting = false;
				return;
			}

			var items = new JObject();
			var materiaData = new JObject();
			var inventory = new JArray();

			uint minIlvl = 999;
			uint maxIlvl = 1;

			int minLevel = 90;
			int maxLevel = 1;

			var ItemSheet = Plugin.DataManager.GetExcelSheet<ExtendedItem>();
			var MateriaSheet = Plugin.DataManager.GetExcelSheet<Materia>();
			if (ItemSheet == null || MateriaSheet == null) {
				Error = "Unable to load data.";
				Exporting = false;
				return;
			}

			string? job = null;
			foreach(string valid in VALID_JOBS) {
				var prop = gearset.Category.GetType().GetProperty(valid, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
				if (prop != null && prop.GetValue(gearset.Category) is bool b && b) {
					job = valid;
					break;
				}
			}

			if (job == null) {
				Error = "Unable to map job to Ariyala.";
				Exporting = false;
				return;
			}

			bool had_right = false;

			foreach (var item in gearset.Items) {
				var data = item.GetItem(ItemSheet);
				if (data == null)
					continue;

				uint slot = data.EquipSlotCategory.Row;
				string? mappedSlot;
				if (slot == 12) {
					mappedSlot = had_right ? "ringLeft" : "ringRight";
					had_right = true;

				} else if (!ARIYALA_SLOT_MAP.TryGetValue(slot, out mappedSlot)) {
					PluginLog.Information($"Unknown Slot for Item: {data.Name} -- Slot: {slot}");
					continue;
				}

				if (mappedSlot == null)
					continue;

				uint ilvl = data.LevelItem.Row;
				int level = data.LevelEquip;

				if (ilvl < minIlvl)
					minIlvl = ilvl;
				if (ilvl > maxIlvl)
					maxIlvl = ilvl;

				if (level < minLevel)
					minLevel = level;
				if (level > maxLevel)
					maxLevel = level;

				items.Add(mappedSlot, item.ItemID);

				List<string> melds = new();

				foreach (var raw in item.Melds) {
					if (raw.ID == 0)
						continue;

					var materia = raw.GetMateria(MateriaSheet);
					if (materia == null || raw.Grade >= materia.Value.Length)
						continue;

					uint stat = materia.BaseParam.Row;
					if (!ARIYALA_STAT_MAP.TryGetValue(stat, out string? mappedStat)) {
						PluginLog.Information($"Unknown Stat for Materia: {materia.Item[raw.Grade]?.Value?.Name} -- Stat: {stat}");
						continue;
					}

					if (mappedStat == null)
						continue;

					melds.Add($"{mappedStat}:{raw.Grade}");
				}

				if (melds.Count > 0)
					materiaData.Add($"{mappedSlot}-{item.ItemID}", new JArray(melds));
			}

			var datasets = new JObject() {
				[job] = new JObject() {
					["normal"] = new JObject() {
						["items"] = items,
						["materiaData"] = materiaData,
						["bonusStats"] = new JObject() {}
					},
					["base"] = new JObject() {
						["items"] = new JObject(),
						["materiaData"] = new JObject(),
						["bonusStats"] = new JObject() {}
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

			var obj = new JObject {
				{ "version", 6 },
				{ "content", job },
				{ "datasets", datasets },
				{ "raceID", 2 },
				{ "level", 90 },
				{ "filter", filter },
				{ "myInventory", new JArray(gearset.Items.Select(x => x.ItemID).ToArray()) }
			};

			PluginLog.Information($"Result:\n{obj.ToString(Newtonsoft.Json.Formatting.None)}");

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
				PluginLog.LogInformation($"Success. ID: {value}");
				if (!string.IsNullOrEmpty(value))
					TryOpenURL($"https://ffxiv.ariyala.com/{value}");
			} else {
				string? value = await response.Content.ReadAsStringAsync();
				PluginLog.LogError($"Failure. Error: {response.StatusCode}\nDetails:{value}");
				Error = "Ariyala returned invalid response.";
			}

		} catch (Exception ex) {
			/* Do nothing~ */
			PluginLog.Error($"An error occurred while exporting gearset to Ariyala.\nDetails: {ex}");
			Error = "An error occurred.";
		}

		Exporting = false;
	}

	#endregion

}
