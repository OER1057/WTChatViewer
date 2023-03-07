using System.Text.RegularExpressions;
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
        Console.WriteLine("WTChatViewer");
        Config config = GetConfig();
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
            foreach (GameChat gameChat in newGameChats)
            {
                lastId = gameChat.Id;

                string chatText = gameChat.Msg;
                CleanText(ref chatText);
                Console.Write(chatText);

                (string translatedText, bool isTranslated) = Translate(chatText, config.TargetLang);
                if (isTranslated)
                {
                    Console.Write(" ("); // たぶん文字列処理しないほうが速い
                    Console.Write(translatedText);
                    Console.Write(")\n");
                }
                else
                {
                    Console.Write("\n");
                }

                string textToPass = isTranslated ? translatedText : chatText;
                ReplaceText(ref textToPass, config.ReplaceList);
                Debug.WriteLine(config.ReadPath);
                Debug.WriteLine(config.ReadArg.Replace("%Text", textToPass));
                Process.Start(config.ReadPath, config.ReadArg.Replace("%Text", textToPass));
            }
            Thread.Sleep(config.Interval);
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
    static (string, bool) Translate(string text, string targetLang)
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
    public class Config
    {
        public int Interval { get; set; } = 500;
        public bool IgnoreEnemy { get; set; } = false;
        public string[] IgnoreSenders { get; set; } = new string[0];
        public bool TrnsEnable { get; set; } = false;
        public string TargetLang { get; set; } = "";
        public bool ReadEnable { get; set; } = false;
        public string ReadPath { get; set; } = "";
        public string ReadArg { get; set; } = "";
        public ReplacePair[] ReplaceList { get; set; } = new ReplacePair[0];
    }
    public class ReplacePair
    {
        public string From { get; set; } = "x";
        public string To { get; set; } = "x";
    }
    static Config GetConfig(string fileName = "config.json")
    {
        return JsonSerializer.Deserialize<Config>(File.ReadAllText(fileName)) ?? new Config();
    }
    static void ReplaceText(ref string text, ReplacePair[] pairs)
    {
        foreach (ReplacePair pair in pairs)
        {
            text = Regex.Replace(text, pair.From, pair.To);
        }
    }
    static void CleanText(ref string text)
    {
        text = Regex.Replace(text, "<.*?>", "");
        text = text.Replace("\t", "");
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
}
