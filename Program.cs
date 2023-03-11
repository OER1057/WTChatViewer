﻿using System.Text.RegularExpressions;
using System.Net.Sockets;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WTChatViewer;
class Program
{
    static readonly HttpClient httpClient = new HttpClient();
    static async Task Main(string[] args)
    {
        Console.Title = "WTChatViewer";
        Console.WriteLine("WTChatViewer by OER1057");

        string exeDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
        string configFile = Path.Combine(exeDirectory, "config.json");
        bool testMode = false;
        string testText = "";

        for (int i = 0; i < args.Length; i++)
        {
            if (new[] { "-t", "--test" }.Contains(args[i]))
            {
                testMode = true;
                if ((i + 1) < args.Length && !args[i + 1].StartsWith('-'))
                {
                    i++;
                    testText = args[i];
                }
                else
                {
                    Console.Error.WriteLine("usage: WTChatViewer configfile [-t | --test testtext]");
                    Environment.Exit(1);
                }
            }
            else if (!string.IsNullOrEmpty(args[i]))
            {
                configFile = args[i];
            }
        }

        Config config = GetConfig(configFile);

        if (testMode)
        {
            if (config.PassEnable)
            {
                Console.WriteLine(testText);
                try
                {
                    Process.Start(config.PassFileName, config.PassArguments.Replace("%Text", testText));
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"Exception: {exception.Message}");
                    Console.WriteLine("Test failed.");
                    Environment.Exit(1);
                }
            }
            else
            {
                Console.WriteLine("Passing is not enabled.");
            }
            Environment.Exit(0);
        }

        int lastId = 0;
        while (true)
        {
            if (!IsWarThunderRunning())
            {
                Console.WriteLine("War Thunder is not running. Wait for stating . . .");
                while (!IsWarThunderRunning())
                {
                    Thread.Sleep(config.Interval);
                }
                Console.WriteLine("War Thunder detected.");
                lastId = 0;
            }

            GameChat[] newGameChats = await GetGameChatsAsync(lastId);
            Parallel.ForEach(newGameChats, gameChat =>
            {
                lastId = Math.Max(gameChat.Id, lastId);

                if (config.IgnoreEnemy && gameChat.Enemy == true
                || config.IgnoreSenders.Contains(gameChat.Sender))
                {
                    return;
                }

                string originalText = gameChat.Msg;
                CleanText(ref originalText);

                bool isTranslated = false;
                string translatedText = ""; // 外に書かないと怒られる
                if (config.TranslateEnable)
                {
                    (translatedText, isTranslated) = TranslateText(originalText, config.TargetLang);
                }

                if (isTranslated)
                {
                    Console.WriteLine($"{originalText} ({translatedText})");
                }
                else
                {
                    Console.WriteLine(originalText);
                }

                if (config.PassEnable)
                {
                    string textToPass = isTranslated ? translatedText : originalText;
                    ReplaceText(ref textToPass, config.ReplaceList);
                    try
                    {
                        Process.Start(config.PassFileName, config.PassArguments.Replace("%Text", textToPass));
                    }
                    catch (Exception exception)
                    {
                        Console.Error.WriteLine($"Exception: {exception.Message}");
                        Console.Error.WriteLine("Passing is disabled.");
                        config.PassEnable = false;
                    }
                }
            });
            Thread.Sleep(config.Interval);
        }
    }
    public class Config
    {
        public int Interval { get; set; } = 500;
        public bool IgnoreEnemy { get; set; } = false;
        public string[] IgnoreSenders { get; set; } = new string[0];
        public bool TranslateEnable { get; set; } = false;
        public string TargetLang { get; set; } = "";
        public bool PassEnable { get; set; } = false;
        public string PassFileName { get; set; } = "";
        public string PassArguments { get; set; } = "";
        public ReplacePair[] ReplaceList { get; set; } = new ReplacePair[0];
    }
    public class ReplacePair
    {
        public string From { get; set; } = "x";
        public string To { get; set; } = "x";
    }
    static Config GetConfig(string fileName = "config.json")
    {
        try
        {
            Config config = JsonSerializer.Deserialize<Config>(File.ReadAllText(fileName)) ?? new Config();
            Console.WriteLine($"{Path.GetFullPath(fileName)} loaded.");
            return config;
        }
        catch (FileNotFoundException exception)
        {
            Console.Error.WriteLine($"Exception: {exception.Message}");
            Console.Error.WriteLine("Using default config.");
            return new Config();
        }
    }
    static bool IsWarThunderRunning()
    {
        if (Process.GetProcessesByName("aces").Length == 0) { return false; }
        using (var tcpClient = new TcpClient())
        {
            try
            {
                string apiEndpoint = "127.0.0.1"; // localhostだとなぜか遅い
                tcpClient.Connect(apiEndpoint, 8111);
            }
            catch (SocketException)
            {
                return false;
            }
        }
        return true;
    }
    public class GameChat
    {
        [JsonPropertyName("id")]
        public int Id { get; set; } = 0;

        [JsonPropertyName("msg")]
        public string Msg { get; set; } = "";

        [JsonPropertyName("sender")]
        public string Sender { get; set; } = "";

        [JsonPropertyName("enemy")]
        public bool Enemy { get; set; } = false;

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = "";

        [JsonPropertyName("time")]
        public int Time { get; set; } = 0;
    }
    static async Task<GameChat[]> GetGameChatsAsync(int lastId)
    {
        string apiEndpoint = $"http://127.0.0.1:8111/gamechat?lastId={lastId}"; // localhostだとなぜか遅い
        string responseString;
        try
        {
            var response = await httpClient.GetAsync(apiEndpoint);
            responseString = await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException)
        {
            responseString = "[]";
        }
        return JsonSerializer.Deserialize<GameChat[]>(responseString) ?? new GameChat[0];
    }
    static void CleanText(ref string text)
    {
        text = Regex.Replace(text, "<.*?>", "");
        text = text.Replace("\t", "");
    }
    static (string, bool) TranslateText(string text, string targetLang)
    {
        string escapedText = Uri.EscapeDataString(text);
        string apiEndpoint = $"https://translate.googleapis.com/translate_a/single?client=gtx&dt=t&sl=auto&tl={targetLang}&q={escapedText}";
        string responseString = httpClient.GetAsync(apiEndpoint).Result.Content.ReadAsStringAsync().Result;
        var responseJson = JsonDocument.Parse(responseString).RootElement;
        string translatedText = responseJson[0][0][0].GetString() ?? "";
        string detectedLang = responseJson[2].GetString() ?? "";
        bool isTranslated = detectedLang.ToLower() != targetLang.ToLower();
        return (translatedText, isTranslated);
    }
    static void ReplaceText(ref string text, ReplacePair[] pairs)
    {
        foreach (ReplacePair pair in pairs)
        {
            text = Regex.Replace(text, pair.From, pair.To);
        }
    }
}
