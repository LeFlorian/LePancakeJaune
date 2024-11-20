using DSharpPlus;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using Quartz;
using System.Threading.Tasks;

namespace LaGrueJaune
{
    public static class Utils
    {

        public static DiscordEmbedBuilder BuildEmbedNotes(DiscordUser member, string note, int page, int nbTotal)
        {
            DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Gold)
                .WithTitle($"Liste des notes")
                .WithDescription($"**<@{member.Id}>** - Note n°{page}/{nbTotal}\n\n{note}")
                .WithAuthor("Staff de La Grue Jaune")
                //.WithDescription("Notes enregistrées")
                .WithThumbnail(member.AvatarUrl)
                .WithFooter("La Grue Jaune", "https://cdn.discordapp.com/icons/1019524827268251690/7e079db678a2ef0764cbafef1d03ae44.webp?size=160")
                ;
            return builder;

        }

        public static Action<DiscordMessageBuilder> buildAction(DiscordMember member, String note, int page)
        {
            int nbTotal = Program.notesParser.json.Notes[member.Id].listeNotes.Count();
            DiscordEmbedBuilder builder = Utils.BuildEmbedNotes(member, note, page, nbTotal);
            var previous = new DiscordButtonComponent(ButtonStyle.Primary, $"{member.Id}-{2 * page - 1}", "Précédent", false);
            var next = new DiscordButtonComponent(ButtonStyle.Primary, $"{member.Id}-{2 * page}", "Suivant", false);
            IEnumerable<DiscordComponent> components = new DiscordComponent[] { previous, next };

            return new Action<DiscordMessageBuilder>((DiscordMessageBuilder) => DiscordMessageBuilder.WithEmbed(builder).AddComponents(components));

        }
        public static IEnumerable<IEnumerable<T>> Chunk<T>(this IEnumerable<T> source, int chunkSize)
        {
            if (chunkSize <= 0) throw new ArgumentException("La taille du chunk doit être supérieure à 0.", nameof(chunkSize));

            var chunk = new List<T>(chunkSize);
            foreach (var item in source)
            {
                chunk.Add(item);
                if (chunk.Count == chunkSize)
                {
                    yield return chunk.ToArray();
                    chunk.Clear();
                }
            }

            if (chunk.Count > 0)
                yield return chunk.ToArray();
        }
    }

    // Job de bon anniversaire exécuté par trigger à 8h chaque matin
    public class birthdayWatch : IJob
    {
        async Task IJob.Execute(IJobExecutionContext context)
        {
            await Program.wishBirthday();
        }
    }
}
