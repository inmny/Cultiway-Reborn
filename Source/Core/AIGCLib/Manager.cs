using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Cultiway.Core.AIGCLib;

public class Manager
{
    public static string BaseURL => ModClass.I.GetConfig()["AIGCSettings"]["BASE_URL"].TextVal;
    public static string APIKey => ModClass.I.GetConfig()["AIGCSettings"]["API_KEY"].TextVal;
    public static string Model => ModClass.I.GetConfig()["AIGCSettings"]["MODEL"].TextVal;
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

    struct ChatRequestMessage
    {
        public string role;
        public string content;
    }

    struct ChatRequest
    {
        public ChatRequestMessage[] messages;
        public string model;
        public float temperature;
        public int max_tokens;
        public float frequency_penalty;
        public float presence_penalty;
        public ChatResponseFormat response_format;
        public string[] stop;
        public bool stream;
        public object stream_options;
        public float top_p;
        public object tools;
        public object tool_choice;
        public bool logprobs;
        public object top_logprobs;
    }

    struct ChatResponseFormat
    {
        public string type;
    }

    public static async Task<string> RequestResponseContent(string prompt, string system_prompt="You are a helpful assistant", int index = 0, float temperature = 1.5f)
    {
        if (string.IsNullOrEmpty(BaseURL) || string.IsNullOrEmpty(APIKey))
        {
            return string.Empty;
        }
        var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseURL}/chat/completions");
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("Authorization", $"Bearer {APIKey}");
        
        var chatRequest = new ChatRequest
        {
            messages = new ChatRequestMessage[]
            {
                new ChatRequestMessage { role = "system", content = system_prompt },
                new ChatRequestMessage { role = "user", content = prompt }
            },
            model = Model,
            temperature = temperature,
            max_tokens = 2048,
            frequency_penalty = 0,
            presence_penalty = 0,
            response_format = new ChatResponseFormat { type = "text" },
            stop = null,
            stream = false,
            stream_options = null,
            top_p = 1,
            tools = null,
            tool_choice = null,
            logprobs = false,
            top_logprobs = null
        };
        
        var content_str = JsonConvert.SerializeObject(chatRequest);
        var content = new StringContent(
            content_str,
            null,
            "application/json");
        request.Content = content;
        var response = await client.SendAsync(request);
        var res = await response.Content.ReadAsStringAsync();
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception)
        {
            ModClass.LogErrorConcurrent(res);
            ModClass.LogErrorConcurrent($"Content: {content_str}");
            throw;
        }
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