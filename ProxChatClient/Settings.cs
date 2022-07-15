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
            Instance.SaveSettings();
        }
    }


    private ushort? serverPort;
    public ushort? ServerPort 
    {
        get
        {
            return serverPort ??= 12000;
        }
        set => serverPort = value;
    }

    public string? ServerIP { get; set; }
    public string? IngameName { get; set; }
    private Dictionary<string, byte> userVolumes = new Dictionary<string, byte>();

    #region Getters/Setters
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

    public void SetPort(ushort? port)
    {
        ServerPort = port;
        SaveSettings();
    }

    public ushort? GetPort()
    {
        return ServerPort;
    }

    public void SetIP(string? ip)
    {
        ServerIP = ip;
        SaveSettings();
    }

    public string? GetIP()
    {
        return ServerIP;
    }

    public void SetIGName(string? igName)
    {
        IngameName = igName;
        SaveSettings();
    }

    public string? GetIGName()
    {
        return IngameName;
    }
    #endregion

    private void SaveSettings()
    {
        File.WriteAllText("settings.json", JsonSerializer.Serialize(Instance));
    }
}