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
using static LaGrueJaune.config.JSONNewsFeed;
using static LaGrueJaune.config.JSONNotes;

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
                Dictionary<string, NewsInfo> data = new Dictionary<string, NewsInfo>();
                try
                {
                    data = JsonConvert.DeserializeObject<Dictionary<string, NewsInfo>>(reader);
                }
                catch (Exception ex)
                { 
                    Console.WriteLine(ex.Message);
                }

                this.json = new JSONNewsFeed();
                this.json.News = data;
                Console.WriteLine($"NewsFeed: {data.Count}");
            }
        }

        public async Task WriteJSON()
        {
            using (StreamWriter sw = new StreamWriter("JSON/newsFeed.json"))
            {
                await sw.WriteLineAsync(JsonConvert.SerializeObject(json.News, Formatting.Indented));
            }
        }

        public async Task resetJSON()
        {
            using (StreamWriter sw = new StreamWriter("newsFeed.json"))
            {
                await sw.WriteLineAsync(JsonConvert.SerializeObject(new Dictionary<string, string>(), Formatting.Indented));
            }
        } 

        public async Task AddNews(string titre, NewsInfo info)
        {
            if (json == null)
            {
                await ReadJSON();
            }

            // On ne traite pas l'évènement s'il est déjà listé
            if (!json.News.ContainsKey(titre))
            {
                json.News.Add(titre, info);
            }

            await WriteJSON();
        }
    }

    public class JSONNewsFeed
    {
        public class NewsInfo
        {
            public string dateDebut;
            public string dateFin;
            public Boolean isNew;
            public string message;
        }

        public Dictionary<string, NewsInfo> News = new Dictionary<string, NewsInfo>();
    }
}
