using DSharpPlus;
using DSharpPlus.Entities;
using IronPython.Runtime;
using Microsoft.Scripting.Utils;
using Newtonsoft.Json;
using RandomNameGeneratorLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static LaGrueJaune.config.JSONNotes;

namespace LaGrueJaune.config
{
    internal class JSONConversationParser
    {
        public JSONConversation json;

        public async Task ReadJSON()
        {
            using (StreamReader sr = new StreamReader("conversations.json"))
            {
                string reader = await sr.ReadToEndAsync();
                Dictionary<string,JSONConversation.MemberConversation> data = JsonConvert.DeserializeObject<Dictionary<string,JSONConversation.MemberConversation>>(reader);

                this.json = new JSONConversation();
                this.json.Conversations = data;
                Console.WriteLine(data.Count);
            }
        }

        public async Task WriteJSON()
        {
            using (StreamWriter sw = new StreamWriter("conversations.json"))
            {
                await sw.WriteLineAsync(JsonConvert.SerializeObject(json.Conversations, Formatting.Indented));
            }
        }

        public async Task privateConversation(ulong memberID, DiscordMessage dm, DiscordChannel dumpChannel, DiscordClient client)
        {
            if (json == null)
            {
                await ReadJSON();
            }

            // Calcule du hash de l'ID
            byte[] tmpHash = ASCIIEncoding.ASCII.GetBytes(memberID.ToString());
            string anonymId = System.Text.Encoding.UTF8.GetString(new MD5CryptoServiceProvider().ComputeHash(tmpHash));

            // On vérifie si le membre est à ignorer
            if (json.Conversations.Keys.Contains(anonymId) &&  "ignoré".Equals(json.Conversations[anonymId].statut))
            {
                await dm.CreateReactionAsync(DiscordEmoji.FromName(client, ":x:"));
                return;
            }

            // On vérifie si un thread existe déjà pour ce membre
            if (json.Conversations.Keys.Contains(anonymId))
            {
                ulong threadId = json.Conversations[anonymId].threadId;
                DiscordThreadChannel thread = dumpChannel.Threads.Where(t => t.Id.Equals(threadId)).FirstOrDefault();

                // Construction du message avec les fichiers joints
                string urls = "";
                foreach (DiscordAttachment file in dm.Attachments)
                {
                    urls += $" {file.Url}";
                }

                await thread.SendMessageAsync(dm.Content + "\n" + urls);

                await dm.CreateReactionAsync(DiscordEmoji.FromName(client, ":white_check_mark:"));
            }

            else
            {
                // Génère un nom complet aléatoire pour faciliter la lecture
                PersonNameGenerator personGenerator = new PersonNameGenerator();
                string name = personGenerator.GenerateRandomFirstAndLastName();

                // Init du thread
                DiscordMessage alerte = await dumpChannel.SendMessageAsync($"**{name} a démarré une discussion !**");
                DiscordThreadChannel thread = await dumpChannel.CreateThreadAsync(alerte, name, DSharpPlus.AutoArchiveDuration.Week);

                // Construction du message avec les fichiers joints
                string urls = "";
                foreach (DiscordAttachment file in dm.Attachments)
                {
                    urls += $" {file.Url}";
                }
                
                await thread.SendMessageAsync(dm.Content + "\n" + urls);

                await dm.RespondAsync("Bonjour et merci pour ta communication !\n\n" +
                    "Les messages que tu m'envoies sont retransmis de manière complétement anonyme" +
                    " au staff de La Grue Jaune, qui pourra te répondre par mon intermédiaire.\n" +
                    "Après 7 jours d'inactivité la conversation est effacée côté staff.\n\n" +
                    "Cette réaction :white_check_mark: indique lorsque ton message a bien été transmis");
                await dm.CreateReactionAsync(DiscordEmoji.FromName(client, ":white_check_mark:"));
               

                // Sauvegarde du thread dans le json
                JSONConversation.MemberConversation conv = new JSONConversation.MemberConversation();
                conv.threadId = thread.Id;
                json.Conversations.Add(anonymId, conv);
                await WriteJSON();
            }
        }
    }

    public class JSONConversation
    {
        public class MemberConversation
        {
            public ulong threadId;
            public string statut;
        }

        public Dictionary<string, MemberConversation> Conversations = new Dictionary<string, MemberConversation>();
    }
}
