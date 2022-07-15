using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

public class Settings
{
    public static Settings Instance = null!;

    static Settings()
    {
        if (File.Exists("settings.json"))
        {
            string text = File.ReadAllText("settings.json");
            try
            {
                Instance = JsonSerializer.Deserialize<Settings>(text/*, new JsonSerializerOptions() {  }*/) ?? new Settings();
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
            Instance.SaveSettings();
        }
    }

    private string? serverIP = null;
    public string? ServerIP
    {
        get => serverIP;
        set
        {
            serverIP = value;
            SaveSettings();
        }
    }

    private ushort? serverPort;
    public ushort ServerPort 
    {
        get
        {
            return serverPort ??= 12000;
        }
        set
        {
            serverPort = value;
            SaveSettings();
        }
    }

    private string? ingameName = null;
    public string? IngameName
    {
        get => ingameName;
        set
        {
            ingameName = value;
            SaveSettings();
        }
    }
    private Dictionary<string, byte> userVolumes = new Dictionary<string, byte>();

    public void SetUserVolumePreference(string discordName, byte volume)
    {
        userVolumes[discordName] = volume;
        SaveSettings();
    }

    public byte? GetUserVolumePreference(string discordName)
    {
        if (userVolumes.ContainsKey(discordName))
            return userVolumes[discordName];
        else return null;
    }

    private void SaveSettings()
    {
        File.WriteAllText("settings.json", JsonSerializer.Serialize(Instance));
    }
}