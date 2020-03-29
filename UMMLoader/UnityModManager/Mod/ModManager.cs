using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using UnityEngine;

namespace UnityModManagerNet
{
	public partial class UnityModManager
	{
		private static readonly Version VER_0 = new Version();
		private static readonly Version VER_0_13 = new Version(0, 13);

		/// <summary>
		/// Contains version of UnityEngine
		/// </summary>
		public static Version unityVersion { get; private set; }

		/// <summary>
		/// Contains version of a game, if configured [0.15.0]
		/// </summary>
		public static Version gameVersion { get; private set; } = new Version();

		/// <summary>
		/// Contains version of UMM
		/// </summary>
		public static Version version { get; private set; } = typeof(UnityModManager).Assembly.GetName().Version;


		private static ModuleDefMD thisModuleDef = ModuleDefMD.Load(typeof(UnityModManager).Module);

		private static bool forbidDisableMods;

		public class Repository
		{
			[Serializable]
			public class Release : IEquatable<Release>
			{
				public string Id;
				public string Version;
				public string DownloadUrl;

				public bool Equals(Release other)
				{
					return Id.Equals(other.Id);
				}

				public override bool Equals(object obj)
				{
					if (ReferenceEquals(null, obj))
					{
						return false;
					}
					return obj is Release obj2 && Equals(obj2);
				}

				public override int GetHashCode()
				{
					return Id.GetHashCode();
				}
			}

			public Release[] Releases;
		}

		public class ModSettings
		{
			public virtual void Save(ModEntry modEntry)
			{
				Save(this, modEntry);
			}

			public virtual string GetPath(ModEntry modEntry)
			{
				return Path.Combine(modEntry.Path, "Settings.xml");
			}

			public static void Save<T>(T data, ModEntry modEntry) where T : ModSettings, new()
			{
				Save<T>(data, modEntry, null);
			}

			/// <summary>
			/// [0.20.0]
			/// </summary>
			public static void Save<T>(T data, ModEntry modEntry, XmlAttributeOverrides attributes) where T : ModSettings, new()
			{
				var filepath = data.GetPath(modEntry);
				try
				{
					using (var writer = new StreamWriter(filepath))
					{
						var serializer = new XmlSerializer(typeof(T), attributes);
						serializer.Serialize(writer, data);
					}
				}
				catch (Exception e)
				{
					modEntry.Logger.Error($"Can't save {filepath}.");
					modEntry.Logger.LogException(e);
				}
			}

			public static T Load<T>(ModEntry modEntry) where T : ModSettings, new()
			{
				var t = new T();
				var filepath = t.GetPath(modEntry);
				if (File.Exists(filepath))
				{
					try
					{
						using (var stream = File.OpenRead(filepath))
						{
							var serializer = new XmlSerializer(typeof(T));
							var result = (T)serializer.Deserialize(stream);
							return result;
						}
					}
					catch (Exception e)
					{
						modEntry.Logger.Error($"Can't read {filepath}.");
						modEntry.Logger.LogException(e);
					}
				}

				return t;
			}
			public static T Load<T>(ModEntry modEntry, XmlAttributeOverrides attributes) where T : ModSettings, new()
			{
				var t = new T();
				var filepath = t.GetPath(modEntry);
				if (File.Exists(filepath))
				{
					try
					{
						using (var stream = File.OpenRead(filepath))
						{
							var serializer = new XmlSerializer(typeof(T), attributes);
							var result = (T)serializer.Deserialize(stream);
							return result;
						}
					}
					catch (Exception e)
					{
						modEntry.Logger.Error($"Can't read {filepath}.");
						modEntry.Logger.LogException(e);
					}
				}

				return t;
			}

		}

		public class ModInfo : IEquatable<ModInfo>
		{
			public string Id;

			public string DisplayName;

			public string Author;

			public string Version;

			public string ManagerVersion;

			public string GameVersion;

			public string[] Requirements;

			public string AssemblyName;

			public string EntryMethod;

			public string HomePage;

			public string Repository;

			/// <summary>
			/// Used for RoR2 game [0.17.0]
			/// </summary>
			[NonSerialized]
			public bool IsCheat = true;

			public static implicit operator bool(ModInfo exists)
			{
				return exists != null;
			}

			public bool Equals(ModInfo other)
			{
				return Id.Equals(other.Id);
			}

			public override bool Equals(object obj)
			{
				if (ReferenceEquals(null, obj))
				{
					return false;
				}
				return obj is ModInfo modInfo && Equals(modInfo);
			}

			public override int GetHashCode()
			{
				return Id.GetHashCode();
			}
		}


		public static readonly List<ModEntry> modEntries = new List<ModEntry>();
		public static string modsPath { get; private set; }

		[Obsolete("Please use modsPath!!!!This is compatible with mod of ver before 0.13")]
		public static string OldModsPath = "";

		internal static Param Params { get; set; } = new Param();
		internal static GameInfo Config { get; set; } = new GameInfo();

		internal static bool started = false;
		internal static bool initialized = false;

		public static bool Initialize()
		{
			if (initialized)
				return true;

			initialized = true;

			Logger.Clear();

			Logger.Log($"Initialize.");
			Logger.Log($"Version: {version}.");
			try
			{
				Logger.Log($"OS: {Environment.OSVersion} {Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")}.");
				Logger.Log($"Net Framework: {Environment.Version}.");
			}
			catch (Exception e)
			{
				Debug.LogException(e);
			}

			unityVersion = ParseVersion(Application.unityVersion);
			Logger.Log($"Unity Engine: {unityVersion}.");

			Config = GameInfo.Load();
			if (Config == null)
			{
				return false;
			}

			Logger.Log($"Game: {Config.Name}.");

			Params = Param.Load();

			modsPath = Path.Combine(Environment.CurrentDirectory, Config.ModsDirectory);
			if (!Directory.Exists(modsPath))
			{
				var modsPath2 = Path.Combine(Path.GetDirectoryName(Environment.CurrentDirectory), Config.ModsDirectory);

				if (Directory.Exists(modsPath2))
				{
					modsPath = modsPath2;
				}
				else
				{
					Directory.CreateDirectory(modsPath);
				}
			}
			Logger.filepath = Path.Combine(modsPath, "UnityModManager.log");
			Logger.Log($"Mods path: {modsPath}.");
			OldModsPath = modsPath;

			//SceneManager.sceneLoaded += SceneManager_sceneLoaded; // Incompatible with Unity5

			AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

			return true;
		}

		private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
		{
			var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
			if (assembly != null)
				return assembly;

			if (args.Name.StartsWith("0Harmony,"))
			{
				var regex = new Regex(@"Version=(\d+\.\d+)");
				var match = regex.Match(args.Name);
				if (match.Success)
				{
					var ver = match.Groups[1].Value;
					string filepath = Path.Combine(Path.GetDirectoryName(typeof(UnityModManager).Assembly.Location), $"0Harmony-{ver}.dll");
					if (File.Exists(filepath))
					{
						try
						{
							return Assembly.LoadFile(filepath);
						}
						catch (Exception e)
						{
							Logger.Error(e.ToString());
						}
					}
				}
			}

			return null;
		}

		public static void Start()
		{
			try
			{
				_Start();
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				OpenUnityFileLog();
			}
		}

		//private static void ParseGameVersion()
		//{
		//	if (string.IsNullOrEmpty(Config.GameVersionPoint))
		//		return;
		//	try
		//	{
		//		Logger.Log("Start parsing game version.");
		//		if (!Injector.TryParseEntryPoint(Config.GameVersionPoint, out string assembly, out string className, out string methodName, out _))
		//			return;
		//		var asm = Assembly.Load(assembly);
		//		if (asm == null)
		//		{
		//			Logger.Error($"File '{assembly}' not found.");
		//			return;
		//		}

		//		var foundClass = asm.GetType(className);
		//		if (foundClass == null)
		//		{
		//			Logger.Error($"Class '{className}' not found.");
		//			return;
		//		}

		//		var foundMethod = foundClass.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
		//		if (foundMethod == null)
		//		{
		//			var foundField = foundClass.GetField(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
		//			if (foundField != null)
		//			{
		//				gameVersion = ParseVersion(foundField.GetValue(null).ToString());
		//				Logger.Log($"Game version detected as '{gameVersion}'.");
		//				return;
		//			}

		//			Logger.Error($"Method '{methodName}' not found.");
		//			return;
		//		}

		//		gameVersion = ParseVersion(foundMethod.Invoke(null, null).ToString());
		//		Logger.Log($"Game version detected as '{gameVersion}'.");
		//	}
		//	catch (Exception e)
		//	{
		//		Debug.LogException(e);
		//		OpenUnityFileLog();
		//	}
		//}

		private static void _Start()
		{
			if (!Initialize())
			{
				Logger.Log($"Cancel start due to an error.");
				OpenUnityFileLog();
				return;
			}
			if (started)
			{
				Logger.Log($"Cancel start. Already started.");
				return;
			}

			started = true;

			if (!string.IsNullOrEmpty(Config.GameVersionPoint))
			{
				try
				{
					Logger.Log($"Start parsing game version.");
					if (Injector.TryParseEntryPoint(Config.GameVersionPoint, out var assembly, out var className, out var methodName, out _))
					{
						var asm = Assembly.Load(assembly);
						if (asm == null)
						{
							Logger.Error($"File '{assembly}' not found.");
							goto Next;
						}
						var foundClass = asm.GetType(className);
						if (foundClass == null)
						{
							Logger.Error($"Class '{className}' not found.");
							goto Next;
						}
						var foundMethod = foundClass.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
						if (foundMethod == null)
						{
							var foundField = foundClass.GetField(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
							if (foundField != null)
							{
								gameVersion = ParseVersion(foundField.GetValue(null).ToString());
								Logger.Log($"Game version detected as '{gameVersion}'.");
								goto Next;
							}

							UnityModManager.Logger.Error($"Method '{methodName}' not found.");
							goto Next;
						}

						gameVersion = ParseVersion(foundMethod.Invoke(null, null).ToString());
						Logger.Log($"Game version detected as '{gameVersion}'.");
					}
				}
				catch (Exception e)
				{
					Debug.LogException(e);
					OpenUnityFileLog();
				}
			}

		Next:

			GameScripts.Init();

			GameScripts.OnBeforeLoadMods();

			if (Directory.Exists(modsPath))
			{
				Logger.Log($"Parsing mods.");

				Dictionary<string, ModEntry> mods = new Dictionary<string, ModEntry>();

				int countMods = 0;

				foreach (string dir in Directory.GetDirectories(modsPath))
				{
					string jsonPath = Path.Combine(dir, Config.ModInfo);
					if (!File.Exists(Path.Combine(dir, Config.ModInfo)))
					{
						jsonPath = Path.Combine(dir, Config.ModInfo.ToLower());
					}
					if (File.Exists(jsonPath))
					{
						countMods++;
						Logger.Log($"Reading file '{jsonPath}'.");
						try
						{
							//ModInfo modInfo = JsonUtility.FromJson<ModInfo>(File.ReadAllText(jsonPath));
							ModInfo modInfo = TinyJson.JSONParser.FromJson<ModInfo>(File.ReadAllText(jsonPath));
							if (string.IsNullOrEmpty(modInfo.Id))
							{
								Logger.Error($"Id is null.");
								continue;
							}
							if (mods.ContainsKey(modInfo.Id))
							{
								Logger.Error($"Id '{modInfo.Id}' already uses another mod.");
								continue;
							}
							if (string.IsNullOrEmpty(modInfo.AssemblyName))
								modInfo.AssemblyName = modInfo.Id + ".dll";

							ModEntry modEntry = new ModEntry(modInfo, dir + Path.DirectorySeparatorChar);
							mods.Add(modInfo.Id, modEntry);
						}
						catch (Exception exception)
						{
							Logger.Error($"Error parsing file '{jsonPath}'.");
							Debug.LogException(exception);
						}
					}
					else
					{
						//Logger.Log($"File not found '{jsonPath}'.");
					}
				}

				if (mods.Count > 0)
				{
					Logger.Log($"Sorting mods.");
					TopoSort(mods);

					Params.ReadModParams();

					Logger.Log($"Loading mods.");
					foreach (var mod in modEntries)
					{
						if (!mod.Enabled)
						{
							mod.Logger.Log("To skip (disabled).");
						}
						else
						{
							mod.Active = true;
						}
					}
				}

				Logger.Log($"Finish. Successful loaded {modEntries.Count(x => !x.ErrorOnLoading)}/{countMods} mods.".ToUpper());
				Console.WriteLine();
				Console.WriteLine();
			}

			GameScripts.OnAfterLoadMods();

			if (!UI.Load())
			{
				Logger.Error($"Can't load UI.");
			}
		}

		private static void DFS(string id, Dictionary<string, ModEntry> mods)
		{
			if (modEntries.Any(m => m.Info.Id == id))
			{
				return;
			}
			foreach (var req in mods[id].Requirements.Keys)
			{
				if (mods.ContainsKey(req))
					DFS(req, mods);
			}
			modEntries.Add(mods[id]);
		}

		private static void TopoSort(Dictionary<string, ModEntry> mods)
		{
			foreach (var id in mods.Keys)
			{
				DFS(id, mods);
			}
		}

		public static ModEntry FindMod(string id)
		{
			return modEntries.FirstOrDefault(x => x.Info.Id == id);
		}

		public static Version GetVersion()
		{
			return version;
		}

		public static void SaveSettingsAndParams()
		{
			Params.Save();
			foreach (var mod in modEntries)
			{
				if (mod.Active && mod.OnSaveGUI != null)
				{
					try
					{
						mod.OnSaveGUI(mod);
					}
					catch (Exception e)
					{
						mod.Logger.LogException("OnSaveGUI", e);
					}
				}
			}
		}
	}
}