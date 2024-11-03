using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using LaGrueJaune.config;
using Microsoft.Scripting.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;

namespace LaGrueJaune
{
    public class Utils
    {

        public static DiscordEmbedBuilder BuildEmbedNotes(DiscordUser member, string note, int page, int nbTotal)
        {

            DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Gold)
                .WithTitle(member.Username)
                .WithAuthor("Listes des notes")
                //.WithDescription("Notes enregistrées")
                .WithThumbnail(member.AvatarUrl)
                .AddField($"Note n°{page}/{nbTotal}", note)
                .WithFooter("La Grue Jaune", "https://cdn.discordapp.com/icons/1019524827268251690/7e079db678a2ef0764cbafef1d03ae44.webp?size=160")
                ;

            return builder;
        }

        public static Action<DiscordMessageBuilder> buildAction(DiscordMember member, String note, int page)
        {
            int nbTotal = Program.notesParser.json.Notes[member.Id].listeNotes.Count();
            DiscordEmbedBuilder builder = Utils.BuildEmbedNotes(member, note, page, nbTotal);
            var previous = new DiscordButtonComponent(ButtonStyle.Primary, $"{member.Id}-{2*page-1}", "Précédent", false);
            var next = new DiscordButtonComponent(ButtonStyle.Primary, $"{member.Id}-{2*page}", "Suivant", false);
            IEnumerable<DiscordComponent> components = new DiscordComponent[] {previous,next};

            return new Action<DiscordMessageBuilder>((DiscordMessageBuilder) => DiscordMessageBuilder.WithEmbed(builder).AddComponents(components));

        }
    }
}
