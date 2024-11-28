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
    internal class JSONAnniversairesParser
    {
        public JSONAnniversaires json;

        public async Task ReadJSON()
        {
            using (StreamReader sr = new StreamReader("JSON/anniversaires.json"))
            {
                string reader = await sr.ReadToEndAsync();
                Dictionary<string,JSONAnniversaires.MemberAnniversaire> data = JsonConvert.DeserializeObject<Dictionary<string,JSONAnniversaires.MemberAnniversaire>>(reader);

                this.json = new JSONAnniversaires();
                this.json.Anniversaires = data;
                Console.WriteLine($"Anniversaires: {data.Count}");
            }
        }

        public async Task WriteJSON()
        {
            using (StreamWriter sw = new StreamWriter("JSON/anniversaires.json"))
            {
                await sw.WriteLineAsync(JsonConvert.SerializeObject(json.Anniversaires, Formatting.Indented));
            }
        }

        public async Task resetJSON()
        {
            using (StreamWriter sw = new StreamWriter("anniversaires.json"))
            {
                await sw.WriteLineAsync(JsonConvert.SerializeObject(new Dictionary<string, JSONAnniversaires.MemberAnniversaire>(), Formatting.Indented));
            }
        }

        public async Task keepGuildMembersOnly()
        {
            var guildMembers = Program.Guild.GetAllMembersAsync().Result;
            var annivMembers = json.Anniversaires.Keys.ToList();
            foreach (string id in annivMembers)
            {
                bool isGuildMember = false;
                foreach (DiscordMember member in guildMembers)
                {
                    if (id.Equals(member.Id.ToString()))
                    {
                        isGuildMember = true;
                    }
                }
                if (!isGuildMember)
                {
                    json.Anniversaires.Remove(id);
                }
            }
            await WriteJSON();
        }

        public async Task AddAnniv(string memberID, string dateAnniv, bool ignored)
        {
            if (json == null)
            {
                await ReadJSON();
            }

            if (json.Anniversaires.ContainsKey(memberID))
            {
                json.Anniversaires[memberID].dateAnniv = dateAnniv;
            }

            else
            {
                JSONAnniversaires.MemberAnniversaire newAnniversaire = new JSONAnniversaires.MemberAnniversaire();
                newAnniversaire.dateAnniv = dateAnniv;
                newAnniversaire.ignored = ignored;
                json.Anniversaires.Add(memberID, newAnniversaire);
            }

            // Tri dans l'ordre des dates d'anniversaire
            json.Anniversaires = json.Anniversaires.OrderBy(a => a.Value.dateAnniv.Substring(3) + a.Value.dateAnniv.Substring(0,2)).ToDictionary(a => a.Key, a => a.Value);

            await WriteJSON();
        }

        public async Task updateAnnivInEmbed(string memberId, string dateAnniv, DiscordChannel channel, bool ajout)
        {
            if (ajout) {
                await AddAnniv(memberId, dateAnniv, false);
            }
            else
            {
                this.json.Anniversaires.Remove(memberId);
            }
            await keepGuildMembersOnly();

            DiscordEmbedBuilder builderAnniv = Utils.BuildEmbedAnniv(Program.anniversairesParser.json.Anniversaires);
            DiscordEmbed embedAnniv = builderAnniv.Build();

            var annivMessages = Program.Guild.GetChannel(Program.config.ID_annivChannel).GetMessagesAsync(1).Result;
            var messageAnniv = annivMessages.First();

            // Cas où la commande est exécutée dans le salon anniversaires
            if (channel.Equals(Program.Guild.GetChannel(Program.config.ID_annivChannel)))
            {
                annivMessages = Program.Guild.GetChannel(Program.config.ID_annivChannel).GetMessagesAsync(2).Result;
                messageAnniv = annivMessages.First();
            }

            await messageAnniv.ModifyAsync(embedAnniv);
        }
    }

    public class JSONAnniversaires
    {
        public class MemberAnniversaire
        {
            public string dateAnniv;
            public bool ignored;
        }

        public Dictionary<string, MemberAnniversaire> Anniversaires = new Dictionary<string, MemberAnniversaire>();
    }
}
