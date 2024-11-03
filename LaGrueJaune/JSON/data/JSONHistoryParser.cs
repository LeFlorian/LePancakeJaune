using DSharpPlus.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace LaGrueJaune.config
{
    internal class JSONHistoryParser
    {
        public JSONHistory json;

        public async Task ReadJSON()
        {
            using (StreamReader sr = new StreamReader("history.json"))
            {
                string reader = await sr.ReadToEndAsync();
                Dictionary<ulong,JSONHistory.Description> data = JsonConvert.DeserializeObject<Dictionary<ulong,JSONHistory.Description>>(reader);

                this.json = new JSONHistory();
                this.json.History = data;
                Console.WriteLine($"History: {data.Count}");
            }
        }

        public async Task WriteJSON()
        {
            using (StreamWriter sw = new StreamWriter("history.json"))
            {
                await sw.WriteLineAsync(JsonConvert.SerializeObject(json.History, Formatting.Indented));
            }
        }

        public async Task AddHistory(ulong authorID, JSONHistory.Description desc)
        {
            if (json == null)
            {
               await ReadJSON();
            }

            if (json.History.ContainsKey(authorID))
            {
                if (json.History[authorID].publicationDate < desc.publicationDate)
                {
                    json.History[authorID] = desc;
                }
            }
            else
            {
                json.History.Add(authorID, desc);
            }

            await WriteJSON();
        }
    }

    public class JSONHistory
    {
        public class Description
        {
            public string author;
            public DateTime publicationDate;
            public double numberOfDay;
            public Uri link;
            public bool isKickable = true;
            public string kickReason = "Si tu reçois ça, c'est probablement que je me suis trompé et que je t'ai kick par inadvertance..."; 
        }

        public Dictionary<ulong, Description> History = new Dictionary<ulong, Description>();
    }
}
