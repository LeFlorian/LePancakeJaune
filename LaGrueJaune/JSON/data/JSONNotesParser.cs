using DSharpPlus.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LaGrueJaune.config
{
    internal class JSONNotesParser
    {
        public JSONNotes json;

        public async Task ReadJSON()
        {
            using (StreamReader sr = new StreamReader("JSON/notes.json"))
            {
                string reader = await sr.ReadToEndAsync();
                Dictionary<ulong,JSONNotes.MemberNotes> data = JsonConvert.DeserializeObject<Dictionary<ulong,JSONNotes.MemberNotes>>(reader);

                this.json = new JSONNotes();
                this.json.Notes = data;
                Console.WriteLine($"Notes: {data.Count}");
            }
        }

        public async Task WriteJSON()
        {
            using (StreamWriter sw = new StreamWriter("JSON/notes.json"))
            {
                await sw.WriteLineAsync(JsonConvert.SerializeObject(json.Notes, Formatting.Indented));
            }
        }

        public async Task AddNotes(ulong memberID, string text)
        {
            if (json == null)
            {
               await ReadJSON();
            }
            
            if (json.Notes.ContainsKey(memberID))
            {
                json.Notes[memberID].listeNotes.Add(text);
            }
            else
            {
                JSONNotes.MemberNotes newMemberNotes = new JSONNotes.MemberNotes();
                newMemberNotes.listeNotes.Add(text);
                json.Notes.Add(memberID, newMemberNotes);
            }

            await WriteJSON();
        }
    }

    public class JSONNotes
    {
        public class MemberNotes
        {
            public string member;
            public double numberOfNotes;
            public List<string> listeNotes = new List<string>();
        }

        public Dictionary<ulong, MemberNotes> Notes = new Dictionary<ulong, MemberNotes>();
    }
}
