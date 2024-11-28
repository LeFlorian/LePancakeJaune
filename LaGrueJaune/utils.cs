using DSharpPlus;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using Quartz;
using System.Threading.Tasks;
using LaGrueJaune.config;
using static LaGrueJaune.config.JSONAnniversaires;

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
                .WithFooter("La Grue Jaune", Program.Guild.IconUrl)
                ;
            return builder;

        }

        public static Action<DiscordMessageBuilder> buildActionNotes(DiscordMember member, String note, int page)
        {
            int nbTotal = Program.notesParser.json.Notes[member.Id].listeNotes.Count();
            DiscordEmbedBuilder builder = Utils.BuildEmbedNotes(member, note, page, nbTotal);
            var previous = new DiscordButtonComponent(ButtonStyle.Primary, $"{member.Id}-{2 * page - 1}", "Précédent", false);
            var next = new DiscordButtonComponent(ButtonStyle.Primary, $"{member.Id}-{2 * page}", "Suivant", false);
            IEnumerable<DiscordComponent> components = new DiscordComponent[] { previous, next };

            return new Action<DiscordMessageBuilder>((DiscordMessageBuilder) => DiscordMessageBuilder.WithEmbed(builder).AddComponents(components));

        }

        public static DiscordEmbedBuilder addMonth(DiscordEmbedBuilder builder, Dictionary<string, MemberAnniversaire> anniversaires, string monthLabel, string monthNumber)
        {
            string monthAnnivs = "";
            string prevDay = "";
            foreach (KeyValuePair<string, MemberAnniversaire> anniv in anniversaires.Where(a => monthNumber.Equals(a.Value.dateAnniv.Substring(3))))
            {
                // On vérifie si le jour d'anniversaire est différent ou non du précédent
                string currentDay = anniv.Value.dateAnniv.Substring(0, 2);
                if (!currentDay.Equals(prevDay))
                {
                    monthAnnivs += $"\n{anniv.Value.dateAnniv.Substring(0, 2)}: <@{anniv.Key}>";
                }
                else
                {
                    monthAnnivs += $" - <@{anniv.Key}>";
                }

            prevDay = currentDay;
            }
            if (!"".Equals(monthAnnivs))
            {
                return builder.AddField(monthLabel, monthAnnivs, false);
            }
            else
            {
                return builder;
            }
            
        }

        public static DiscordEmbedBuilder BuildEmbedAnniv(Dictionary<string, MemberAnniversaire> anniversaires)
        {

            DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Gold)
                .WithTitle($"Calendrier des anniversaires")
                .WithTimestamp(System.DateTime.Now)
                ;

            builder = addMonth(builder, anniversaires, "Janvier", "01");
            builder = addMonth(builder, anniversaires, "Février", "02");
            builder = addMonth(builder, anniversaires, "Mars", "03");
            builder = addMonth(builder, anniversaires, "Avril", "04");
            builder = addMonth(builder, anniversaires, "Mai", "05");
            builder = addMonth(builder, anniversaires, "Juin", "06");
            builder = addMonth(builder, anniversaires, "Juillet", "07");
            builder = addMonth(builder, anniversaires, "Août", "08");
            builder = addMonth(builder, anniversaires, "Septembre", "09");
            builder = addMonth(builder, anniversaires, "Octobre", "10");
            builder = addMonth(builder, anniversaires, "Novembre", "11");
            builder = addMonth(builder, anniversaires, "Décembre", "12");

            return builder;

        }

        public static DiscordEmbedBuilder BuildEmbedReco(DiscordUser member,
            string nom,
            string type,
            string adresse,
            string url,
            string prix,
            string com,
            string note)
        {
            if ("bar".Equals(type))
            {
                type = "Bar :beers:";
            }

            string reco = $"{url}\n" +
                $":money_with_wings: {prix} :star: {note}/5 :adult: <@{member.Id}>\n" +
                $"{com}" +
                $"```{adresse}```";

            string com2 = "Cherchez plus j'ai trouvé la meilleure adresse de Nantes, no troll, 300 avis, 4,9 étoiles sur 5 et c'est mérité.Cuisine Ethiopienne/Erythréenne, la cuisinière/patronne (nommée Fruta) est bonne vibe, et sa bouffe incroyable, le serveur (son mari?) est super sympa aussi, le lieu est cosy (attention par contre quand vous ressortez la ville, ses bruits, son odeur, est agressive), les prix sont abordables, il y avait de la place pour 2 sans résa un samedi à 13h00...Edith Piaf : En lisant les quelques avis négatifs parmis les 300, j'ajoute quelques infos, oui on mange à la main, oui c'est un peu épicé (mais en vrai pas tant même pour un blanc habitué à la crême normande plus qu'au chili comme moi), oui il n'y avait pas de dessert (car fait à la main et elle n'en avait pas fait cette fois ci), un peu d'attente en effet, pas testé les plats végé/végan donc je sais pas leur qualité, tout ça ne change pas ma note !";
            string com21 = com2.Substring(0, 800);
            string com22 = com2.Substring(800);
            string reco21 = $"https://chezfruta.com/\n" +
                $":money_with_wings: +/- 18 :star: 5/5 :adult: <@{member.Id}>\n" +
                $"{com21}";
            string reco22 = com22 + $"```2 Rue Copernic, 44000 Nantes```";

            DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Gold)
                .WithTitle("Bars")
                .AddField(nom, reco, false)
                .AddField("Chez Fruta", reco21, false)
                .AddField("** **", reco22, false)
                .AddField(nom, reco, true)
                ;

            return builder;
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
