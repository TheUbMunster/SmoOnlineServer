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
    private ushort? ServerPort 
    {
        get
        {
            return serverPort ??= 12000;
        }
        set => serverPort = value;
    }

    private string? ServerIP { get; set; }
    private string? IngameName { get; set; }
    private byte? defaultVolume;
    private byte? DefaultVolume 
    {
        get
        {
            return defaultVolume ??= 150;
        }
        set => defaultVolume = value; 
    }
    private Dictionary<string, byte> userVolumes = new Dictionary<string, byte>(); //TODO: CHANGE TO (userId, vol)

    #region Getters/Setters
    public void SetUserVolumePreference(string discordName, byte volume)
    {
        userVolumes[discordName] = volume;
        SaveSettings();
    }

    public byte GetUserVolumePreference(string discordName)
    {
        if (userVolumes.ContainsKey(discordName))
            return userVolumes[discordName];
        else
            return userVolumes[discordName] = DefaultVolume!.Value;
    }

    public void SetDefaultVolume(byte vol)
    {
        DefaultVolume = vol;
    }

    public byte GetDefaultVolume()
    {
        return DefaultVolume!.Value;
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