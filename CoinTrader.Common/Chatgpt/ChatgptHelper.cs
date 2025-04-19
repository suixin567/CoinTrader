using System;
using System.Collections.Generic;
using System.IO;
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
            presence_penalty = 1.0,
            frequency_penalty = 0.1,
            repetition_penalty = 1.1,
            n = 1,
            model = inputs.Model,
            messages = messagesHistory
        };

        var json = JsonConvert.SerializeObject(requestData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // 避免重复添加 header
        if (!client.DefaultRequestHeaders.Contains("Authorization"))
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {inputs.ApiKey}");
        if (!client.DefaultRequestHeaders.Contains("Accept"))
            client.DefaultRequestHeaders.Add("Accept", "application/json");

        try
        {
            Logger.Instance.LogDebug("Chatgpt 准备post");
            var response = await client.PostAsync(url, content);
            Logger.Instance.LogDebug("Chatgpt post完成");

            var stream = await response.Content.ReadAsStreamAsync();
            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (!line.StartsWith("data:"))
                        continue;

                    var data = line.Substring("data:".Length).Trim();

                    if (data == "[DONE]")
                    {
                        result.Text = result.Text.Trim();
                        break;
                    }

                    try
                    {
                        var parsedData = JsonConvert.DeserializeObject<dynamic>(data);

                        var choice = parsedData.choices[0];
                        var finish = (string)choice.finish_reason;
                        if (!string.IsNullOrEmpty(finish) && finish == "stop")
                        {
                            result.Text = result.Text.Trim();
                            break;
                        }

                        var delta = choice.delta;
                        if (delta != null)
                        {
                            if (delta.role != null)
                            {
                                result.Role = (string)delta.role;
                            }

                            if (delta.content != null)
                            {
                                result.Text += (string)delta.content;
                            }

                            result.Detail = parsedData;
                            inputs.OnProgress?.Invoke(result.Text);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.LogDebug($"解析异常：{ex.Message}");
                        // 忽略解析失败的块
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
