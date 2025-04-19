using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CoinTrader.Common;
using Newtonsoft.Json;

public class SendMessageResult
{
    public string Id { get; set; }
    public string Text { get; set; }
    public string Role { get; set; }
    public object Detail { get; set; }
}

public class SendMessageInput
{
    public Action<string> OnProgress { get; set; }
    public int MaxToken { get; set; }
    public string ApiKey { get; set; }
    public string Model { get; set; }
    public float Temperature { get; set; }
    public string ProxyUrl { get; set; }
    public string Prompt { get; set; }
}

public class Message
{
    public string Role { get; set; }
    public string Content { get; set; }
}

public class Tokenizer
{
    public static int GetTokenCount(string text)
    {
        // 模拟token计算
        return text.Length / 4;  // 简单估算每个token大约为4个字符
    }
}

public class NovitaApiService
{
    private static readonly HttpClient client = new HttpClient();

    private string GetFullUrl(string proxyUrl)
    {
        return "https://api.novita.ai/v3/openai/chat/completions";
    }

    public async Task<SendMessageResult> SendMessageFromNovitaAsync(List<Message> messagesHistory, SendMessageInput inputs)
    {
        var result = new SendMessageResult { Text = string.Empty };
        var url = GetFullUrl(inputs.ProxyUrl);

        var requestData = new
        {
            stream = true,
            temperature = 0.7,
            top_p = 0.6,
            min_p = 0.7,
            top_k = 60,
            presence_penalty = 1,
            frequency_penalty = 0.1,
            repetition_penalty = 1.1,
            n = 1,
            model = inputs.Model,
            messages = messagesHistory
        };

        var json = JsonConvert.SerializeObject(requestData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {inputs.ApiKey}");
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        try
        {
            Logger.Instance.LogDebug("Chatgpt 准备post");
            var response = await client.PostAsync(url, content);
            Logger.Instance.LogDebug("Chatgpt post完成");
            var stream = await response.Content.ReadAsStreamAsync();

            var buffer = new StringBuilder();
            using (var reader = new System.IO.StreamReader(stream))
            {
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    buffer.Append(line);
                    if (line.Contains("\n\n"))
                    {
                        var data = line.Replace("data:", "").Trim();
                        bool isEnd = false;

                        try
                        {
                            var parsedData = JsonConvert.DeserializeObject<dynamic>(data);
                            isEnd = parsedData.choices[0].finish_reason == "stop";
                            if (isEnd)
                            {
                                result.Text = result.Text.Trim();
                                break;
                            }

                            if (data != "[DONE]")
                            {
                                var parsedChoice = parsedData.choices[0].delta;
                                result.Text += parsedChoice.content.ToString();
                                result.Role = parsedChoice.role;
                                result.Detail = parsedData;
                            }
                        }
                        catch (Exception)
                        {
                            isEnd = false;
                        }

                        inputs.OnProgress?.Invoke(result.Text);
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger.Instance.LogDebug("Chatgpt Error while sending message");
            throw new HttpRequestException("Error while sending message", ex);
        }
    }
}
