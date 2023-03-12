using System.Text.RegularExpressions;
using System.Net.Sockets;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using McMaster.Extensions.CommandLineUtils;

namespace WTChatViewer;

class Program
{
    static Program()
    {
        string exeDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
        ConfigFileOption = Path.Combine(exeDirectory, "config.json");
    }
    [Argument(0, ShowInHelpText = false)]
    static string? ConfigFileArgument { get; set; }
    [Option("-c|--config", Description = "Specify configuration file path.")]
    static string ConfigFileOption { get; set; }
    [Option("-t|--test", Description = "Test passing configuration. Specify text to pass.")]
    static string? TestText { get; set; }
    static readonly HttpClient httpClient = new HttpClient();
    static void PressKeyToExit(ConsoleKey exitKey)
    {
        Console.WriteLine($"Press {exitKey.ToString()} to exit.");
        while (true)
        {
            if (Console.ReadKey(true).Key == ConsoleKey.Q)
            {
                Environment.Exit(0);
            }
        }
    }
    static void Main(string[] args)
    {
        CommandLineApplication.Execute<Program>(args);
    }
    private async Task OnExecute()
    {
        Console.Title = "WTChatViewer";
        Console.WriteLine("WTChatViewer by OER1057");
        _ = Task.Run(() => PressKeyToExit(ConsoleKey.Q)); // 受け取らなくてもいいけど警告が出る

        Config config = GetConfig(ConfigFileArgument ?? ConfigFileOption);

        if (TestText != null)
        {
            if (config.PassEnable)
            {
                Console.WriteLine(TestText);
                try
                {
                    Process.Start(config.PassFileName, config.PassArguments.Replace("%Text", TestText));
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
            Parallel.ForEach(newGameChats, async gameChat =>
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
                    (translatedText, isTranslated) = await TranslateText(originalText, config.TargetLang);
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
            responseString = await httpClient.GetStringAsync(apiEndpoint);
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
    static async Task<(string, bool)> TranslateText(string text, string targetLang)
    {
        string escapedText = Uri.EscapeDataString(text);
        string apiEndpoint = $"https://translate.googleapis.com/translate_a/single?client=gtx&dt=t&sl=auto&tl={targetLang}&q={escapedText}";
        string responseString = await httpClient.GetStringAsync(apiEndpoint);
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
