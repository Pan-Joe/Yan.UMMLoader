using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using UnityEngine;

namespace UnityModManagerNet
{
	public partial class UnityModManager
	{
        public sealed class Param
        {
            [Serializable]
            public class Mod
            {
                [XmlAttribute]
                public string Id;
                [XmlAttribute]
                public bool Enabled = true;
            }

            public KeyBinding Hotkey = new KeyBinding();
            public int CheckUpdates = 1;
            public int ShowOnStart = 1;
            public float WindowWidth;
            public float WindowHeight;
            public float UIScale = 1f;

            public List<Mod> ModParams = new List<Mod>();

            static readonly string filepath = Path.Combine(Path.GetDirectoryName(typeof(Param).Assembly.Location), "Params.xml");

            public void Save()
            {
                try
                {
                    ModParams.Clear();
                    foreach (var mod in modEntries)
                    {
                        ModParams.Add(new Mod { Id = mod.Info.Id, Enabled = mod.Enabled });
                    }
                    using (var writer = new StreamWriter(filepath))
                    {
                        var serializer = new XmlSerializer(typeof(Param));
                        serializer.Serialize(writer, this);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error($"Can't write file '{filepath}'.");
                    Debug.LogException(e);
                }
            }

            public static Param Load()
            {
                if (File.Exists(filepath))
                {
                    try
                    {
                        using (var stream = File.OpenRead(filepath))
                        {
                            var serializer = new XmlSerializer(typeof(Param));
                            var result = serializer.Deserialize(stream) as Param;

                            return result;
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"Can't read file '{filepath}'.");
                        Debug.LogException(e);
                    }
                }
                return new Param();
            }

            internal void ReadModParams()
            {
                foreach (var item in ModParams)
                {
                    var mod = FindMod(item.Id);
                    if (mod != null)
                    {
                        mod.Enabled = item.Enabled;
                    }
                }
            }
        }

        [XmlRoot("Config")]
		public class GameInfo
		{

            public string Name;
            public string Folder;
            public string ModsDirectory;
            public string ModInfo;
            public string EntryPoint;
            public string StartingPoint;
            public string UIStartingPoint;
            public string GameExe;
            public string GameVersionPoint;

            static readonly string filepath = Path.Combine(Path.GetDirectoryName(typeof(GameInfo).Assembly.Location), "Config.xml");


            public static GameInfo Load()
			{
                GameInfo result = null;

                try
				{
                    using (var stream = File.OpenRead(filepath))
                        result = new XmlSerializer(typeof(GameInfo)).Deserialize(stream) as GameInfo;

                    var stringFields = typeof(GameInfo).GetFields(BindingFlags.Public | BindingFlags.Instance).Where(x => x.FieldType == typeof(string)).ToArray();

                    foreach(var i in stringFields)
                    {
                        Logger.Log($"{i.Name} : {i.GetValue(result)}");
                    }
                    return result;
                }
                catch (Exception e)
				{
					Logger.Error($"Can't read file '{filepath}'.");
					Debug.LogException(e);
                    return result;

                }
			}
		}
	}
}