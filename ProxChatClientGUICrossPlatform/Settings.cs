using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
public class Settings
{
    public class VolumePreferences
    {
        public Dictionary<string, byte>? UsernameToVolume { get; set; }
        public byte this[string username]
        {
            get
            {
                if (UsernameToVolume == null)
                {
                    UsernameToVolume = new Dictionary<string, byte>();
                    UsernameToVolume.Add(username, Instance.DefaultVolume!.Value);
                }
                else if (!UsernameToVolume.ContainsKey(username))
                {
                    UsernameToVolume.Add(username, Instance.DefaultVolume!.Value);
                }
                return UsernameToVolume[username];
            }
            set
            {
                if (UsernameToVolume == null)
                {
                    UsernameToVolume = new Dictionary<string, byte>();
                }
                UsernameToVolume[username] = value;
            }
        }
    }

    public static Settings Instance = null!;

    static Settings()
    {
        if (File.Exists("settings.json"))
        {
            string text = File.ReadAllText("settings.json");
            try
            {
                Instance = JsonSerializer.Deserialize<Settings>(text) ?? new Settings();
                Console.WriteLine("Loaded settings from json");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Couldn't load settings {e}");
                Instance = new Settings();
            }
        }
        else
        {
            Instance = new Settings();
            SaveSettings();
        }
    }

    public long? DiscordAppID { get; set; }
    public ushort? ServerPort { get; set; } = 12000;
    public string? ServerHost { get; set; }
    public string? IngameName { get; set; }
    public byte? DefaultVolume { get; set; } = 50;
    public VolumePreferences? VolumePrefs { get; set; } = new VolumePreferences();
    /// <summary>
    /// //"Always On", "Push-To-Talk", "Push-To-Mute"
    /// </summary>
    public string? SpeakMode { get; set; } = "Always On"; 
    public int? PushToTeam { get; set; }
    public int? PushToGlobal { get; set; }
    public int? ToggleDeafen { get; set; }
    /// <summary>
    /// for "Always On", is toggle mute, for "PTT" and "PTM" it's the action key for that action
    /// </summary>
    public int? SpeakAction { get; set; }
    public bool? PercievedVolumeSliderEnabled { get; set; } = false;

    public static void SaveSettings()
    {
        string json = JsonSerializer.Serialize(Instance, new JsonSerializerOptions() { WriteIndented = true });
        File.WriteAllText("settings.json", json);
    }
}