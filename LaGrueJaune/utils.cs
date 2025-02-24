using DSharpPlus;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using Quartz;
using System.Threading.Tasks;
using LaGrueJaune.config;
using static LaGrueJaune.config.JSONAnniversaires;
using HtmlAgilityPack;
using System.Runtime.CompilerServices;
using IronPython.Compiler.Ast;
using System.Diagnostics.Eventing.Reader;
using static LaGrueJaune.config.JSONNewsFeed;

namespace LaGrueJaune
{
    public static class Utils
    {

        public static DiscordEmbedBuilder BuildEmbedNotes(ulong memberId, string avatarUrl, string note, int page, int nbTotal)
        {
            DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Gold)
                .WithTitle($"Notes")
                .WithDescription($":adult: <@{memberId}> :notepad_spiral: {page}/{nbTotal}\n\n" +
                $"{note}")
                //.WithAuthor("Staff de La Grue Jaune")
                //.WithDescription("Notes enregistrées")
                .WithThumbnail(avatarUrl)
                //.WithFooter("La Grue Jaune", Program.Guild.IconUrl)
                ;
            return builder;

        }

        public static Action<DiscordMessageBuilder> buildActionNotes(ulong memberId, string avatarUrl, String note, int page)
        {
            int nbTotal = Program.notesParser.json.Notes[memberId].listeNotes.Count();
            DiscordEmbedBuilder builder = Utils.BuildEmbedNotes(memberId, avatarUrl, note, page, nbTotal);
            var previous = new DiscordButtonComponent(ButtonStyle.Primary, $"{memberId}-{2 * page - 1}", "Précédent", false);
            if (page == 1) {
                previous.Disable();
            }
            var next = new DiscordButtonComponent(ButtonStyle.Primary, $"{memberId}-{2 * page}", "Suivant", false);
            if (page == nbTotal) {
                next.Disable();
            }
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
                .WithTitle("Calendrier")
                .WithThumbnail(Program.config.URL_annivPicture)
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

            string reco = $"### {nom}\n" +
                $"{url}\n\n" +
                $":money_with_wings: {prix} :star: {note}/5 :adult: <@{member.Id}>\n" +
                $"> {com}\n\n" +
                $"```{adresse}```\n";

            string com2 = "Cherchez plus j'ai trouvé la meilleure adresse de Nantes, no troll, 300 avis, 4,9 étoiles sur 5 et c'est mérité.Cuisine Ethiopienne/Erythréenne, la cuisinière/patronne (nommée Fruta) est bonne vibe, et sa bouffe incroyable, le serveur (son mari?) est super sympa aussi, le lieu est cosy (attention par contre quand vous ressortez la ville, ses bruits, son odeur, est agressive), les prix sont abordables, il y avait de la place pour 2 sans résa un samedi à 13h00...Edith Piaf : En lisant les quelques avis négatifs parmis les 300, j'ajoute quelques infos, oui on mange à la main, oui c'est un peu épicé (mais en vrai pas tant même pour un blanc habitué à la crême normande plus qu'au chili comme moi), oui il n'y avait pas de dessert (car fait à la main et elle n'en avait pas fait cette fois ci), un peu d'attente en effet, pas testé les plats végé/végan donc je sais pas leur qualité, tout ça ne change pas ma note !";
            string com21 = com2.Substring(0, 800);
            string com22 = com2.Substring(800);
            string reco21 = $"https://chezfruta.com/\n" +
                $":money_with_wings: +/- 18 :star: 5/5 :adult: <@{member.Id}>\n" +
                $"{com21}";
            string reco22 = com22 + $"```2 Rue Copernic, 44000 Nantes```";

            DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Gold)
                .WithDescription(reco)
                //.AddField("Chez Fruta", reco21, false)
                //.AddField("** **", reco22, false)
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

        public static DiscordEmbedBuilder NewsBuilder(string bclUrl, HtmlWeb web, HtmlDocument doc, int newsIndex, Dictionary<string, NewsInfo> NewsFeedTmp)
        {
            int i = newsIndex;
            string titre = System.Net.WebUtility.HtmlDecode(doc.DocumentNode.SelectSingleNode($"//*[@id=\"page-0\"]/div/div/div/div/div/div[2]/div[{i}]/div/article/div/header/h3/a").InnerText.Trim());
            string beginDate = doc.DocumentNode.SelectSingleNode($"//*[@id=\"page-0\"]/div/div/div/div/div/div[2]/div[{i}]/div/article/div/p").InnerText.Trim();
            HtmlNode endDateTmp = doc.DocumentNode.SelectSingleNode($"//*[@id=\"page-0\"]/div/div/div/div/div/div[2]/div[{i}]/div/article/div/div/span[i[@class=\"fa-solid fa-calendar-day fa-fw\"]]");
            string endDate = "";
            if (endDateTmp != null)
            {
                endDate = " - " + endDateTmp.InnerText.Trim();
            }

            NewsInfo info = new NewsInfo();
            info.dateDebut = beginDate;
            info.dateFin = endDate;
            info.isNew = true;
            NewsFeedTmp.Add(titre, info);

            // On vérifie si l'évènement a déjà été traité ou non
            if (Program.newsFeedParser.json.News.ContainsKey(titre)){
                NewsFeedTmp[titre].isNew = false;
                NewsFeedTmp[titre].message = Program.newsFeedParser.json.News[titre].message;
                return null;
            }

            string url = doc.DocumentNode.SelectSingleNode($"//*[@id=\"page-0\"]/div/div/div/div/div/div[2]/div[{i}]/div/article/div/header/h3/a").Attributes["href"].Value;
            var tmpTypes = doc.DocumentNode.SelectNodes($"//*[@id=\"page-0\"]/div/div/div/div/div/div[2]/div[{i}]/div/article/div/div/span[i[@class=\"fa-solid fa-tag fa-fw\"]]");
            string types = "";
            foreach (HtmlNode type in tmpTypes)
            {
                types += type.InnerText.Trim() + ", ";
            }

            types = types.Trim().Trim(',');
            string lieu = doc.DocumentNode.SelectSingleNode($"//*[@id=\"page-0\"]/div/div/div/div/div/div[2]/div[{i}]/div/article/div/div/span[i[@class=\"fa-solid fa-map-pin fa-fw\"]]").InnerText.Trim();
            HtmlNode payantTmp = doc.DocumentNode.SelectSingleNode($"//*[@id=\"page-0\"]/div/div/div/div/div/div[2]/div[{i}]/div/article/div/div/span[i[@class=\"fa-solid fa-money-bill-1-wave fa-fw\"]]");
            HtmlNode gratuitTmp = doc.DocumentNode.SelectSingleNode($"//*[@id=\"page-0\"]/div/div/div/div/div/div[2]/div[{i}]/div/article/div/div/span[i[@class=\"fa-regular fa-face-smile fa-fw\"]]");
            string prix = "";

            if (payantTmp != null)
            {
                prix = payantTmp.InnerText.Trim();
            }
            else if (gratuitTmp != null) { prix = gratuitTmp.InnerText.Trim(); }
            else { prix = "Tarif non renseigné"; }

            HtmlDocument descDoc = web.Load(url);
            HtmlNode descTmp = descDoc.DocumentNode.SelectSingleNode("//*[contains(@id, 'post')]//*[text()][1]");
            var imgUrlTmp = descDoc.DocumentNode.SelectSingleNode("//*[contains(@id, 'post')]//img");
            string desc = "Pas de description";
            string imgUrl = "";
            if (imgUrlTmp != null)
            {
                imgUrl = imgUrlTmp.Attributes["src"].Value;
            }
            if (descTmp != null)
            {
                desc = descTmp.InnerText.Trim();
            }

            string evtDesc =
                $"-# {System.Net.WebUtility.HtmlDecode(desc)}";

            /*
            if (evtDesc.Length > 150)
            {
                char[] specialChars = { '.', '?', '!' };
                int index = evtDesc.IndexOfAny(specialChars);

                if (index != -1)
                {
                    int index2 = evtDesc.IndexOfAny(specialChars,index+1);

                    if (index2 != -1)
                        evtDesc = evtDesc.Substring(0, index2+1);
                }
            }*/

            string evtInfos =
                $":cityscape: {types}\n" +
                $":calendar: {beginDate}{endDate}\n" +
                $":money_with_wings: {prix}\n" +
                $":pushpin: {lieu}";

            DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Blurple)
            .WithTitle(System.Net.WebUtility.HtmlDecode(titre))
            .WithUrl(url)
            //.AddField("Description", evtDesc, true)
            //.AddField("Infos",evtInfos,true)
            .WithDescription($"{evtInfos}\n\n{evtDesc}")
            .WithThumbnail(imgUrl)
            //.WithFooter("https://www.bigcitylife.fr/agenda/")
            ;

            Program.newsFeedParser.AddNews(titre, info);

            return embedBuilder;
        }

        public static string getFormattedDate(string writtenDate)
        {
            string[] split = writtenDate.Split(' ');
            string day = "";
            string monthWritten = "";
            string year = "";
            if (split.Length == 2)
            {
                day = split[0].PadLeft(2, '0');
                monthWritten = split[1];
                year = "1"; // Année actuelle
            }

            else if (split.Length == 4)
            {
                day = split[2].PadLeft(2, '0');
                monthWritten = split[3];
                year = "0"; // Année précédente
            }

            else if (split.Length == 6)
            {
                day = split[4].PadLeft(2, '0');
                monthWritten = split[5];
                year = "1"; // Année actuelle
            }

            else if (split.Length == 7)
            {
                day = split[4].PadLeft(2, '0');
                monthWritten = split[5];
                year = "2"; // Année suivante, à rajouter manuellement un caractère dans le json, pas d'autre solution...
            }

            string month = "";
            switch (monthWritten)
            {
                case "janvier":
                    month = "01";
                    break;

                case "février":
                    month = "02";
                    break;

                case "mars":
                    month = "03";
                    break;

                case "avril":
                    month = "04";
                    break;

                case "mai":
                    month = "05";
                    break;

                case "juin":
                    month = "06";
                    break;

                case "juillet":
                    month = "07";
                    break;

                case "août":
                    month = "08";
                    break;

                case "septembre":
                    month = "09";
                    break;

                case "octobre":
                    month = "10";
                    break;

                case "novembre":
                    month = "11";
                    break;

                case "Décembre":
                    month = "12";
                    break;

                default:
                    break;
            }

            return day + '/' + month + '/' + year;
        }

        public static (List<string>, string) setNewsDaySummary(List<string> fields, Dictionary<string, NewsInfo> NewsFeedTmp, string cDate, string events, Boolean isSingleDay)
        {
            int compteur = 0;
            foreach (var news in NewsFeedTmp)
            {
                string date = cDate;
                if (news.Value.dateDebut == null)
                {
                    break;
                }

                date += "/1"; // Pour tenir compte de l'année courante

                // Gestion des évènement à date unique
                if (isSingleDay && getFormattedDate(news.Value.dateDebut).Equals(date))
                {
                    if (compteur == 5)
                    {
                        fields.Add(events);
                        events = "";
                        compteur = 0;
                    }

                    events += "-# ";
                    if (news.Value.isNew)
                    {
                        events += ":small_orange_diamond: **Nouveau: **";
                    }
                    else
                    {
                        events += "- ";
                    }
                    string titre = news.Key;
                    string url = NewsFeedTmp[news.Key].message;
                    events += $"[{titre}]({url}) \n";
                    compteur += 1;
                }

                // Gestion des évènement longue durée
                if (!isSingleDay)
                {
                    string[] splitCurrentDate = date.Split('/');
                    string currentDate = splitCurrentDate[1] + splitCurrentDate[0];
                    string[] splitDateDebut = getFormattedDate(news.Value.dateDebut).Split('/');
                    string dateDebut = splitDateDebut[1] + splitDateDebut[0];
                    string dateFin = "";
                    Boolean longEvent = false;
                    if (!"".Equals(news.Value.dateFin))
                    {
                        longEvent = true;
                        string[] splitDateFin = getFormattedDate(news.Value.dateFin).Split('/');
                        dateFin = splitDateFin[1] + splitDateFin[0];
                        if (longEvent
                            && ("0".Equals(splitDateDebut[2]) || dateDebut.CompareTo(currentDate) <= 0)
                            && ("2".Equals(splitDateFin[2]) || dateFin.CompareTo(currentDate) >= 0))
                        {
                            if (compteur == 5)
                            {
                                fields.Add(events);
                                events = "";
                                compteur = 0;
                            }

                            events += "-# ";
                            if (news.Value.isNew)
                            {
                                events += ":small_orange_diamond: **Nouveau: **";
                            }
                            else
                            {
                                events += "- ";
                            }
                            string titre = news.Key;
                            string url = NewsFeedTmp[news.Key].message;
                            events += $"[{titre}]({url})\n";
                            compteur += 1;
                        }
                    }
                }
            }

            return (fields, events);
        }
    }

    // Job de bon anniversaire exécuté par trigger à 8h chaque matin
    public class commonWatch : IJob
    {
        async Task IJob.Execute(IJobExecutionContext context)
        {
            await Program.OnHeightAM();
        }
    }
}
