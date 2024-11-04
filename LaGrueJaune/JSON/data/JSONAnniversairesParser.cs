using DSharpPlus;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using RandomNameGeneratorLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace LaGrueJaune.config
{
    internal class JSONAnniversairesParser
    {
        public JSONAnniversaires json;

        public async Task ReadJSON()
        {
            using (StreamReader sr = new StreamReader("anniversaires.json"))
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
            using (StreamWriter sw = new StreamWriter("anniversaires.json"))
            {
                await sw.WriteLineAsync(JsonConvert.SerializeObject(json.Anniversaires, Formatting.Indented));
            }
        }

    public async Task AddAnniv(string memberTag, string dateAnniv)
        {
            if (json == null)
            {
                await ReadJSON();
            }

            if (json.Anniversaires.ContainsKey(memberTag))
            {
                json.Anniversaires[memberTag].dateAnniv = dateAnniv;
            }

            else
            {
                JSONAnniversaires.MemberAnniversaire newAnniversaire = new JSONAnniversaires.MemberAnniversaire();
                newAnniversaire.dateAnniv = dateAnniv;
                json.Anniversaires.Add(memberTag, newAnniversaire);
            }
            await WriteJSON();
        }
    }

    public class JSONAnniversaires
    {
        public class MemberAnniversaire
        {
            public string dateAnniv;
        }

        public Dictionary<string, MemberAnniversaire> Anniversaires = new Dictionary<string, MemberAnniversaire>();
    }
}
