using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Cultiway.Core.AIGCLib;

public class Manager
{
    public static string BaseURL => ModClass.I.GetConfig()["AIGCSettings"]["BASE_URL"].TextVal;
    public static string APIKey => ModClass.I.GetConfig()["AIGCSettings"]["API_KEY"].TextVal;
    struct ChatResponse
    {
        public ChatResponseChoice[] choices;
    }
    struct ChatResponseChoice
    {
        public ChatResponseMessage message;
    }

    struct ChatResponseMessage
    {
        public string content;
    }
    public static async Task<string> RequestResponseContent(string prompt, int index = 0, float temperature = 1.5f)
    {
        if (string.IsNullOrEmpty(BaseURL) || string.IsNullOrEmpty(APIKey))
        {
            return string.Empty;
        }
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseURL}/chat/completions");
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("Authorization", $"Bearer {APIKey}");
        var content = new StringContent(
            """
                {
                  "messages": [
                    {
                      "content": "You are a helpful assistant",
                      "role": "system"
                    },
                    {
                      "content": "prompt",
                      "role": "user"
                    }
                  ],
                  "model": "deepseek-chat",
                  "frequency_penalty": 0,
                  "max_tokens": 2048,
                  "presence_penalty": 0,
                  "response_format": {
                    "type": "text"
                  },
                  "stop": null,
                  "stream": false,
                  "stream_options": null,
                  "temperature": temp_value,
                  "top_p": 1,
                  "tools": null,
                  "tool_choice": "none",
                  "logprobs": false,
                  "top_logprobs": null
                }
                """
                .Replace("prompt", prompt)
                .Replace("temp_value", $"{temperature:F1}"),
            null,
            "application/json");
        request.Content = content;
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var res = await response.Content.ReadAsStringAsync();
        var response_obj = JsonConvert.DeserializeObject<ChatResponse>(res);
        return response_obj.choices[Math.Min(index, response_obj.choices.Length)].message.content;
    }
    public static async Task<string> RequestResponse(string prompt)
    {
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseURL}/chat/completions");
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("Authorization", $"Bearer {APIKey}");
        var content = new StringContent(
            """
            {
              "messages": [
                {
                  "content": "You are a helpful assistant",
                  "role": "system"
                },
                {
                  "content": "prompt",
                  "role": "user"
                }
              ],
              "model": "deepseek-chat",
              "frequency_penalty": 0,
              "max_tokens": 2048,
              "presence_penalty": 0,
              "response_format": {
                "type": "text"
              },
              "stop": null,
              "stream": false,
              "stream_options": null,
              "temperature": 1,
              "top_p": 1,
              "tools": null,
              "tool_choice": "none",
              "logprobs": false,
              "top_logprobs": null
            }
            """.Replace("prompt", prompt),
            null,
            "application/json");
        request.Content = content;
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var res = await response.Content.ReadAsStringAsync();
        return res;
    }
}