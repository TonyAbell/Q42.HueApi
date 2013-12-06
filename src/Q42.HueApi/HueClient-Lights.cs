﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Q42.HueApi.Extensions;
using Newtonsoft.Json;

namespace Q42.HueApi
{
  /// <summary>
  /// Partial HueClient, contains requests to the /lights/ url
  /// </summary>
  public partial class HueClient
  {
    /// <summary>
    /// Asynchronously retrieves an individual light.
    /// </summary>
    /// <param name="id">The light's Id.</param>
    /// <returns>The <see cref="Light"/> if found, <c>null</c> if not.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="id"/> is empty or a blank string.</exception>
    public async Task<Light> GetLightAsync(string id)
    {
      if (id == null)
        throw new ArgumentNullException("id");
      if (id.Trim() == String.Empty)
        throw new ArgumentException("id can not be empty or a blank string", "id");

      CheckInitialized();

      HttpClient client = new HttpClient();
      string stringResult = await client.GetStringAsync(new Uri(String.Format("{0}lights/{1}", ApiBase, id))).ConfigureAwait(false);

      JToken token = JToken.Parse(stringResult);
      if (token.Type == JTokenType.Array)
      {
        // Hue gives back errors in an array for this request
        JObject error = (JObject)token.First["error"];
        if (error["type"].Value<int>() == 3) // Light not found
          return null;

        throw new Exception(error["description"].Value<string>());
      }

      return token.ToObject<Light>();
    }

    /// <summary>
    /// Sets the light name
    /// </summary>
    /// <param name="id"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public async Task SetLightNameAsync(string id, string name)
    {
      if (id == null)
        throw new ArgumentNullException("id");
      if (id.Trim() == String.Empty)
        throw new ArgumentException("id can not be empty or a blank string", "id");

      CheckInitialized();

      string command = JsonConvert.SerializeObject(new { name = name});

      HttpClient client = new HttpClient();
      await client.PutAsync(new Uri(String.Format("{0}lights/{1}", ApiBase, id)), new StringContent(command)).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously gets all lights registered with the bridge.
    /// </summary>
    /// <returns>An enumerable of <see cref="Light"/>s registered with the bridge.</returns>
    public async Task<IEnumerable<Light>> GetLightsAsync()
    {
      CheckInitialized();

      Bridge bridge = await GetBridgeAsync().ConfigureAwait(false);
      return bridge.Lights;
    }

    /// <summary>
    /// Send a lightCommand to a list of lights
    /// </summary>
    /// <param name="command"></param>
    /// <param name="lightList">if null, send command to all lights</param>
    /// <returns></returns>
    public Task SendCommandAsync(LightCommand command, IEnumerable<string> lightList = null)
    {
      if (command == null)
        throw new ArgumentNullException("command");

      string jsonCommand = JsonConvert.SerializeObject(command, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });

      return SendCommandRawAsync(jsonCommand, lightList);
    }


    /// <summary>
    /// Send a json command to a list of lights
    /// </summary>
    /// <param name="command"></param>
    /// <param name="lightList">if null, send command to all lights</param>
    /// <returns></returns>
    public Task SendCommandRawAsync(string command, IEnumerable<string> lightList = null)
    {
      if (command == null)
        throw new ArgumentNullException("command");

      CheckInitialized();

      if (lightList == null || !lightList.Any())
      {
        return SendGroupCommandAsync(command);
      }
      else
      {
        return lightList.ForEachAsync(_parallelRequests, async (lightId) =>
        {
          HttpClient client = new HttpClient();
          await client.PutAsync(new Uri(ApiBase + string.Format("lights/{0}/state", lightId)), new StringContent(command)).ConfigureAwait(false);

        });
      }
    }


    /// <summary>
    /// Set the next Hue color
    /// </summary>
    /// <param name="lightList"></param>
    /// <returns></returns>
    public Task SetNextHueColorAsync(IEnumerable<string> lightList = null)
    {
      //Invalid JSON, but it works
      string command = "{\"hue\":+10000,\"sat\":255}";

      return SendCommandRawAsync(command, lightList);

    }


    /// <summary>
    /// Start searching for new lights
    /// </summary>
    /// <returns></returns>
    public async Task SearchNewLightsAsync()
    {
      CheckInitialized();

      HttpClient client = new HttpClient();
      var response = await client.PostAsync(new Uri(String.Format("{0}lights", ApiBase)), null).ConfigureAwait(false);


    }

    /// <summary>
    /// Gets a list of lights that were discovered the last time a search for new lights was performed. The list of new lights is always deleted when a new search is started.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task<List<Light>> GetNewLightsAsync()
    {
      CheckInitialized();

      HttpClient client = new HttpClient();
      string stringResult = await client.GetStringAsync(new Uri(String.Format("{0}lights/new", ApiBase))).ConfigureAwait(false);

#if DEBUG
      //stringResult = "{\"7\": {\"name\": \"Hue Lamp 7\"},   \"8\": {\"name\": \"Hue Lamp 8\"},    \"lastscan\": \"2012-10-29T12:00:00\"}";
#endif

      List<Light> results = new List<Light>();

      JToken token = JToken.Parse(stringResult);
      if (token.Type == JTokenType.Object)
      {
        //Each property is a light
        var jsonResult = (JObject)token;

        foreach(var prop in jsonResult.Properties())
        {
          if (prop.Name != "lastscan")
          {
            Light newLight = new Light();
            newLight.Id = prop.Name;
            newLight.Name = prop.First["name"].ToString();

            results.Add(newLight);

          }
        }
       
      }

      return results;

    }
  }
}
