using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Scripting.Hosting.Shell;
using Newtonsoft.Json;
using RandomNameGeneratorLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace LaGrueJaune.config
{
    internal class JSONNewsFeedParser
    {
        public JSONNewsFeed json;

        public async Task ReadJSON()
        {
            using (StreamReader sr = new StreamReader("JSON/newsFeed.json"))
            {
                string reader = await sr.ReadToEndAsync();
                List<string> data = JsonConvert.DeserializeObject<List<string>>(reader);

                this.json = new JSONNewsFeed();
                this.json.NewsFeed = data;
                Console.WriteLine($"NewsFeed: {data.Count}");
            }
        }

        public async Task WriteJSON()
        {
            using (StreamWriter sw = new StreamWriter("JSON/newsFeed.json"))
            {
                await sw.WriteLineAsync(JsonConvert.SerializeObject(json.NewsFeed, Formatting.Indented));
            }
        }

        public async Task resetJSON()
        {
            using (StreamWriter sw = new StreamWriter("newsFeed.json"))
            {
                await sw.WriteLineAsync(JsonConvert.SerializeObject(new Dictionary<string, string>(), Formatting.Indented));
            }
        } 

        public async Task AddNews(string titre, string debut)
        {
            if (json == null)
            {
                await ReadJSON();
            }

            // On ne traite pas l'évènement s'il est déjà listé avec la même date de début
            if (!json.NewsFeed.Contains(titre + " - " + debut))
            {
                json.NewsFeed.Add(titre + " - " + debut);
            }

            await WriteJSON();
        }
    }

    public class JSONNewsFeed
    {

        public List<string> NewsFeed = new List<string>();
    }
}
