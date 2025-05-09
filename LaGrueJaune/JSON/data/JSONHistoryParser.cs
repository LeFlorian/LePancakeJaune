using DSharpPlus;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LaGrueJaune.config
{
    internal class JSONHistoryParser
    {
        public JSONHistory json;

        public async Task ReadJSON()
        {
            using (StreamReader sr = new StreamReader("JSON/history.json"))
            {
                string reader = await sr.ReadToEndAsync();
                Dictionary<ulong,JSONHistory.Description> data = JsonConvert.DeserializeObject<Dictionary<ulong,JSONHistory.Description>>(reader);

                this.json = new JSONHistory();
                this.json.History = data;
                Console.WriteLine($"History: {data.Count}");

            }

            /*
            foreach (var test in json.History.Values)
            {
                test.customVocalConfig = new JSONHistory.Description.CustomVocalConfig();
            }

            await WriteJSON();
            Console.WriteLine("Cleared");*/
        }

        public async Task WriteJSON()
        {
            using (StreamWriter sw = new StreamWriter("JSON/history.json"))
            {
                await sw.WriteLineAsync(JsonConvert.SerializeObject(json.History, Formatting.Indented));
                sw.Close();
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

        public async Task AddVocalConfig(ulong authorID, JSONHistory.Description.CustomVocalConfig config)
        {
            if (json == null)
            {
                await ReadJSON();
            }

            if (json.History.ContainsKey(authorID))
            {
                json.History[authorID].customVocalConfig = config;

                await WriteJSON();
            }

        }

        public async Task ClearAbsentUsers()
        {
            if (json == null)
            {
                await ReadJSON();
            }

            List<ulong> absentUsers = new List<ulong>();
            IReadOnlyCollection<DiscordMember> members = await Program.Guild.GetAllMembersAsync();

            foreach (var user in json.historyClone)
            {
                DiscordMember findedMember = members.FirstOrDefault(ctx => ctx.Id == user.Key);

                if (findedMember == default)
                {
                    absentUsers.Add(user.Key);
                }
            }

            string messageLog = $"{absentUsers.Count} users cleaned:\n";
            foreach (var user in absentUsers)
            {
                messageLog += $"{user} : {json.History[user].author}\n";
                json.History.Remove(user);
            }

            Console.WriteLine(messageLog);

            await WriteJSON();
        }

    }

    public class JSONHistory
    {
        public class Description
        {
            public string author;
            public DateTime publicationDate = default;
            public double numberOfDay;
            public Uri link;
            public bool isKickable = true;
            public string kickReason = "Si tu reçois ça, c'est probablement que je me suis trompé et que je t'ai kick par inadvertance...";

            public Prevent prevent = new Prevent();
            public class Prevent
            {
                public int amount;
                public DateTime last = default;
            }

            public CustomVocalConfig customVocalConfig = new CustomVocalConfig();
            public class CustomVocalConfig
            {
                
                public string name = "";
                public int bitrate = 0;
                public int user_limit = 0;
                public VideoQualityMode videoQualityMode = 0;
                
            }
        }

        public Dictionary<ulong, Description> History = new Dictionary<ulong, Description>();


        public Dictionary<ulong, Description> historyClone
        {
            get
            {
                string json = JsonConvert.SerializeObject(History, Formatting.Indented);
                return JsonConvert.DeserializeObject<Dictionary<ulong, JSONHistory.Description>>(json);
            }
        }
    }
}
