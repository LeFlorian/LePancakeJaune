using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using LaGrueJaune.commands;
using LaGrueJaune.config;
using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static LaGrueJaune.config.JSONAnniversaires;
using HtmlAgilityPack;
using static LaGrueJaune.Utils;

namespace LaGrueJaune
{
    public enum ButtonFunction
    {
        [ChoiceName("Null")]
        Null,
        [ChoiceName("AddOrRemoveRole")]
        AddOrRemoveRole
    }

    internal class Program
    {
        public static DiscordGuild Guild;
        public static DiscordClient Client;
        public static CommandsNextExtension Commands;
        public static SlashCommandsExtension SlashCommands;

        public static JSONConfigReader jsonReader;
        public static JSONConfig config;

        public static JSONHistoryParser historyParser;
        public static JSONNotesParser notesParser;
        public static JSONConversationParser conversationParser;
        public static JSONAnniversairesParser anniversairesParser;
        public static JSONRolesParser rolesParser;
        public static JSONHistory userToPurge;
        public static JSONNewsFeedParser newsFeedParser;

        public static int purgeListPageIndex = 1;
        public static int numberOfUserPerPages = 20;

        public static DiscordMessage actualPurgeMessage;
        public static bool isPurgeMessage;

        public static int eventNumber;

        static async Task Main(string[] args)
        {
            #region Json setup
            jsonReader = new JSONConfigReader();
            await jsonReader.ReadJSON();
            config = jsonReader.config;

            historyParser = new JSONHistoryParser();
            await historyParser.ReadJSON();
            notesParser = new JSONNotesParser();
            await notesParser.ReadJSON();
            conversationParser = new JSONConversationParser();
            await conversationParser.ReadJSON();
            anniversairesParser = new JSONAnniversairesParser();
            await anniversairesParser.ReadJSON();
            rolesParser = new JSONRolesParser();
            await rolesParser.ReadJSON();
            newsFeedParser = new JSONNewsFeedParser();
            await newsFeedParser.ReadJSON();

            #endregion

            #region Client setup
            DiscordConfiguration discordConfig = new DiscordConfiguration()
            {
                Intents = DiscordIntents.All,
                Token = config.token,
                TokenType = TokenType.Bot,
                AutoReconnect = true
            };

            Client = new DiscordClient(discordConfig);

            Client.UseInteractivity(new InteractivityConfiguration()
            {
                Timeout = TimeSpan.FromMinutes(2)
            });

            Guild = await Client.GetGuildAsync(config.ID_guild);

            Client.Ready += Client_Ready;
            Client.MessageCreated += OnMessageCreated;
            Client.MessageCreated += OnDm;
            Client.ComponentInteractionCreated += OnButtonInteractionCreated;
            Client.ScheduledGuildEventCreated += OnEventCreated;
            Client.ScheduledGuildEventDeleted += OnEventRemove;
            Client.ScheduledGuildEventUserAdded += OnUserJoinEvent;
            Client.ScheduledGuildEventUserRemoved += OnUserLeaveEvent;
            Client.ScheduledGuildEventCompleted += OnEventCompleted;
            Client.VoiceStateUpdated += OnUserJoinOrLeaveVoiceChannel;
            Client.UnknownEvent += UnknownEvent;
 

            #endregion

            #region CommandsNext setup
            CommandsNextConfiguration commandsNextConfig = new CommandsNextConfiguration()
            {
                StringPrefixes = new string[] { config.prefix },
                EnableMentionPrefix = true,
                EnableDms = true,
                EnableDefaultHelp = false
            };

            Commands = Client.UseCommandsNext(commandsNextConfig);
            Commands.RegisterCommands<CommandsBank>();
            Commands.CommandErrored += OnCommandError;
            Commands.CommandExecuted += OnCommandExecute;

            #endregion

            #region SlashCommands setup

            SlashCommands = Client.UseSlashCommands();
            SlashCommands.RegisterCommands<SlashCommandsBank>();

            Console.WriteLine($"Registered slash commands: {SlashCommands.RegisteredCommands.Count}");

            #endregion

            await Client.ConnectAsync();

            // Trigger pour lancer les fonctions à 8h chaque matin
            var trigger = TriggerBuilder.Create()
                .WithDailyTimeIntervalSchedule(s => s
                    .WithIntervalInHours(24)
                    .StartingDailyAt(TimeOfDay.HourAndMinuteOfDay(8, 0)) // 8h du matin
                )
                .Build();

            // Planification du job commonWatch déclenché par le trigger à 8h
            StdSchedulerFactory factory = new StdSchedulerFactory();
            IScheduler scheduler = await factory.GetScheduler();
            await scheduler.Start();
            await scheduler.ScheduleJob(JobBuilder.Create<commonWatch>().Build(), trigger);
        
            await Task.Delay(-1);
        }
        

        #region Events
        private static Task Client_Ready(DiscordClient sender, ReadyEventArgs args)
        {
            UpdateDescription();
            return Task.CompletedTask;
        }

        private static Task OnCommandError(CommandsNextExtension sender, CommandErrorEventArgs args)
        {
            Console.WriteLine(args.Exception.Message);

            return Task.CompletedTask;
        }

        private static Task OnCommandExecute(CommandsNextExtension sender, CommandExecutionEventArgs args)
        {
            Console.WriteLine($"used {args.Command.Name}");

            return Task.CompletedTask;
        }

        private static async Task OnMessageCreated(DiscordClient sender, MessageCreateEventArgs args)
        {
            if (args.Author.IsBot || args.Message.Content.StartsWith(config.prefix))
                return;

            await AddDescriptionInHistory(args.Author.Id, args.Author.Username, DateTime.Now, args.Message.JumpLink);

            return;
        }

        private static async Task OnButtonInteractionCreated(DiscordClient sender, ComponentInteractionCreateEventArgs args)
        {

            #region Purge

            DiscordInteractionResponseBuilder dir = null;

            switch (args.Interaction.Data.CustomId)
            {
                case "previous":

                    dir = new DiscordInteractionResponseBuilder(await GetPurgeMessage(purgeListPageIndex -1,isPurgeMessage));
                    await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, dir);

                    break;

                case "next":
                    DiscordMessageBuilder builder = await GetPurgeMessage(purgeListPageIndex + 1, isPurgeMessage);
                    dir = new DiscordInteractionResponseBuilder(builder);
                    await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, dir);

                    break;

                case "ID":

                    dir = new DiscordInteractionResponseBuilder(await GetPurgeMessage(purgeListPageIndex, isPurgeMessage));
                    await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, dir);

                    break;
            }

            #endregion

            #region Notes
            try
            {
                ulong userId = Convert.ToUInt64(args.Id.Split('-')[0]);
                
                // Ajout d'une note
                if ("Add".Equals(args.Id.Split('-')[1]))
                {
                    TextInputComponent textInput = new TextInputComponent("test", "value");
                    IEnumerable<DiscordComponent> components = new DiscordComponent[] { textInput};
                    DiscordMessageBuilder message = new DiscordMessageBuilder();
                    DiscordInteractionResponseBuilder builder = new DiscordInteractionResponseBuilder(message);
                    Console.WriteLine("test1");
                    await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, builder);
                    Console.WriteLine("test2");
                }

                // Parcours des notes
                else
                {
                    int buttonId = Int32.Parse(args.Id.Split('-')[1]);
                    DiscordEmbed embed = args.Message.Embeds.First();

                    List<string> list = Program.notesParser.json.Notes[userId].listeNotes;

                    if (buttonId.Equals(1))
                    {
                        return;
                    }

                    // Page suivante
                    else if (buttonId % 2 == 0 && buttonId / 2 < list.Count)
                    {
                        var action = buildActionNotes(userId, embed.Thumbnail.Url.ToString(), list[buttonId / 2], buttonId / 2 + 1);
                        await args.Message.ModifyAsync(action);
                    }
                    // Page précédente
                    else if (buttonId % 2 != 0 && buttonId >= 3)
                    {
                        var action = buildActionNotes(userId, embed.Thumbnail.Url.ToString(), list[(buttonId - 1) / 2 - 1], (buttonId - 1) / 2);
                        await args.Message.ModifyAsync(action);
                    }
                    await args.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage);
                }
            }
            catch { }
            #endregion

            #region Other Buttons
            string[] buttonInfo = args.Interaction.Data.CustomId.Split(':');

            if (buttonInfo.Length > 1)
            {
                DiscordMessage linkedMessage = args.Message;
                Enum.TryParse(buttonInfo[1], out ButtonFunction linkedFunction);
                DiscordRole linkedRole = args.Guild.GetRole(ulong.Parse(buttonInfo[2]));

                switch (linkedFunction)
                {
                    case ButtonFunction.AddOrRemoveRole:

                        await AddOrRemoveRoleToSelfUser(sender, args, linkedRole);

                        break;

                }

                await args.Interaction.CreateFollowupMessageAsync(new DiscordFollowupMessageBuilder().WithContent($"Fonction: {linkedFunction}\nRole: {linkedRole}"));
            }
            #endregion
        }

        //EVENT JOIN
        private static Task OnEventCreated(DiscordClient sender, ScheduledGuildEventCreateEventArgs args)
        {
            UpdateDescription();

            return Task.CompletedTask;
        }

        //EVENT REMOVE
        private static async Task OnEventRemove(DiscordClient sender, ScheduledGuildEventDeleteEventArgs args)
        {
            await EventMinus(args.Event.UserCount);
        }

        private static async Task OnEventCompleted(DiscordClient sender, ScheduledGuildEventCompletedEventArgs args)
        {
            await EventMinus(args.Event.UserCount);
        }

        private static Task EventMinus(int? userCount)
        {
            UpdateDescription();

            return Task.CompletedTask;
        }

        //USER JOIN
        private static async Task OnUserJoinEvent(DiscordClient sender, ScheduledGuildEventUserAddEventArgs args)
        {
            if (args.User.IsBot)
                return;

            await AddDescriptionInHistory(args.User.Id, args.User.Username, DateTime.Now);

            return;
        }

        //USER LEAVE
        private static Task OnUserLeaveEvent(DiscordClient sender, ScheduledGuildEventUserRemoveEventArgs args)
        {
            return Task.CompletedTask;
        }

        private static Task UnknownEvent(DiscordClient sender, UnknownEventArgs args)
        {
            return Task.CompletedTask;
        }

        private static async Task OnUserJoinOrLeaveVoiceChannel(DiscordClient sender, VoiceStateUpdateEventArgs args)
        {
            if (args.User.IsBot)
                return;



            await AddDescriptionInHistory(args.User.Id, args.User.Username , DateTime.Now);

            return;
        }
        #endregion

        #region Functions

        public static DiscordButtonComponent GetPreviousButton()
        {
            DiscordButtonComponent buttonPrevious = new DiscordButtonComponent(
                ButtonStyle.Primary, 
                "previous", 
                null,
                false,
                new DiscordComponentEmoji("⬅️"));

            return buttonPrevious;
        }

        public static DiscordButtonComponent GetNextButton()
        {
            DiscordButtonComponent buttonNext = new DiscordButtonComponent(
                ButtonStyle.Primary,
                "next",
                null,
                false,
                new DiscordComponentEmoji("➡️"));

            return buttonNext;
        }

        public static DiscordButtonComponent GetIDButton(int pageNumber)
        {
            Console.WriteLine($"{purgeListPageIndex}/{pageNumber}");
            DiscordButtonComponent buttonID = new DiscordButtonComponent(
                ButtonStyle.Primary,
                "ID",
                $"{purgeListPageIndex}/{pageNumber}");

            return buttonID;
        }

        public static async Task<DiscordMessageBuilder> GetPurgeMessage(int pageNumber, bool isPurge = true)
        {
            purgeListPageIndex = pageNumber;

            JSONHistory json;

            if (isPurge)
            {
                await MakePurgeList();
                json = userToPurge;
            }
            else
            {
                json = historyParser.json;
            }


            string description = "";
            for (int i = (purgeListPageIndex - 1) * numberOfUserPerPages; i < purgeListPageIndex * numberOfUserPerPages; i++)
            {
                if (i < json.History.Count)
                {
                    ulong userID = json.History.ElementAt(i).Key;
                    JSONHistory.Description desc = json.History.ElementAt(i).Value;

                    string emojiKickable = ":white_check_mark:";
                    if (desc.isKickable)
                        emojiKickable = ":red_square:";

                    description += $"\n-# {emojiKickable} {i + 1} : <@{userID}> {desc.link} ({desc.author}) ~ {desc.numberOfDay} jours";
                }
            }
            var embed = new DiscordEmbedBuilder()
            {
                Title = "Liste de la purge :",
                Description = description,
                Color = DiscordColor.Gold

            };

            var buttonPrevious = GetPreviousButton();
            if (pageNumber <= 1)
            {
                buttonPrevious.Disable();
            }

            var buttonNext = GetNextButton();
            if (pageNumber >= (json.History.Count / numberOfUserPerPages) + 1)
            {
                buttonNext.Disable();
            }

            var buttonID = GetIDButton((json.History.Count / numberOfUserPerPages) + 1);

            var message = new DiscordMessageBuilder();
            message.AddEmbed(embed);
            message.AddComponents(buttonPrevious, buttonID, buttonNext);
            return message;
        }

        private static async Task AddDescriptionInHistory(ulong authorID, string authorName, DateTime publicationDate, Uri messageLink = default)
        {
            JSONHistory.Description newMessage = new JSONHistory.Description()
            {
                author = authorName,
                publicationDate = publicationDate
            };

            if (messageLink != default)
                newMessage.link = messageLink;

            await historyParser.AddHistory(authorID, newMessage);

            return;
        }


        public static async Task MakePurgeList()
        {
            userToPurge = new JSONHistory();

            foreach (var mostRecentMessage in historyParser.json.History)
            {
                double differenceInDays =Math.Ceiling((DateTime.Now - mostRecentMessage.Value.publicationDate).TotalDays);
                mostRecentMessage.Value.numberOfDay = differenceInDays;

                if (differenceInDays > 35) 
                {
                    mostRecentMessage.Value.kickReason = $"Tu as été kick pour innactivé ({differenceInDays} jours depuis la dernière activité)";
                    AddUserPurge(mostRecentMessage.Key, mostRecentMessage.Value);
                }
                else if (differenceInDays > 10)
                {
                    if (Guild.Members.ContainsKey(mostRecentMessage.Key))
                    {
                        DiscordMember member = Guild.Members[mostRecentMessage.Key];

                        if (HasRole(member, "Nouveau.elle"))
                        {
                            mostRecentMessage.Value.kickReason = $"Tu as été kick car tu n'as pas complété toutes les étapes d'inscription depuis {differenceInDays} jours";
                            AddUserPurge(mostRecentMessage.Key, mostRecentMessage.Value);
                        }
                    }
                }
            }

            var discordMembers = await Guild.GetAllMembersAsync();

            foreach (var user in discordMembers)
            {
                if (user.IsBot)
                {
                    continue;
                }

                if ((!HasRole(user, "Nouveau.elle") && !HasRole(user, "Membre") && !HasRole(user,"Staff")) || (HasRole(user, "Nouveau.elle") && !HasRole(user, "Membre") && !HasRole(user, "Staff")))
                {
                    JSONHistory.Description desc;
                    bool toKick = false;

                    if (historyParser.json.History.ContainsKey(user.Id))
                    {
                        desc = historyParser.json.History[user.Id];
                        desc.numberOfDay = Math.Ceiling((DateTime.Now - desc.publicationDate).TotalDays);

                        if (desc.numberOfDay > 10)
                        {
                            toKick = true;
                        }
                    }
                    else if (Math.Ceiling((DateTime.Now - user.JoinedAt.DateTime).TotalDays) > 10)
                    {
                        toKick = true;

                        desc = new JSONHistory.Description();
                        desc.publicationDate = user.JoinedAt.DateTime;
                        desc.author = user.Username;
                        desc.numberOfDay = Math.Ceiling((DateTime.Now - desc.publicationDate).TotalDays);
                    }
                    else
                    {
                        desc = null;
                    }

                    if (toKick)
                    {
                        if (HasRole(user, "Nouveau.elle"))
                        {
                            desc.kickReason = $"Tu as été kick car tu n'as pas complété toutes les étapes d'inscription depuis {desc.numberOfDay} jours";
                        }
                        else
                        {
                            desc.kickReason = $"Tu as été kick car tu n'as pas accepté le règlement depuis {desc.numberOfDay} jours";
                        }

                        AddUserPurge(user.Id, desc);
                    }
                }
            }
        }

        public static async void CheckAndSendMessageToPreventPrugeIsComming()
        {
            Console.WriteLine($"Checking for prevent messages: {historyParser.json.History.Count} history");
            await historyParser.ClearAbsentUsers();
            List<DiscordMember> warnedMembers = new List<DiscordMember>();

            foreach (var mostRecentMessage in historyParser.json.historyClone)
            {
                double differenceInDays = Math.Ceiling((DateTime.Now - mostRecentMessage.Value.publicationDate).TotalDays);
                mostRecentMessage.Value.numberOfDay = differenceInDays;

                DiscordMember member = await Guild.GetMemberAsync(mostRecentMessage.Key);
                int dayChecker = 30;
                string messageToSend = "Bonjour,\n" +
                            "Afin de garder le serveur de La Grue Jaune actif nous retirons les personnes inactives régulièrement. Tu reçois ce message car cela fait plus de 30 jours que tu es inactif.\n" +
                            "Si tu ne souhaite pas être retiré merci d'envoyer un message sur le serveur.\n" +
                            "-# Ceci est un message automatique.";


                //Check si l'utilisateur a le role de nouveau
                DiscordRole newMemberRole = Guild.GetRole(1019575287576543252);
                if (member.Roles.Contains(newMemberRole))
                {
                    //Si l'utilisateur a le rôle de nouveau alors les jours à check sont plus court.
                    dayChecker = 10;
                    messageToSend = "Bonjour,\n" +
                            "Afin de garder le serveur de La Grue Jaune actif nous retirons les personnes inactives régulièrement. Tu reçois ce message car cela fait plus de 10 jours que ta présentation est manquante, incomplète ou non conforme.\n" +
                            "Si tu ne souhaite pas être retiré merci de compléter ta présentation.\n" +
                            "-# Ceci est un message automatique.";
                }

                if (differenceInDays > dayChecker)
                {
                    if (mostRecentMessage.Value.prevent.amount <= 0)
                    {

                        //Je lui envoie un message pour lui dire qu'il doit parler sur le serveur
                        var dmChannel = await member.CreateDmChannelAsync();
                        await dmChannel.SendMessageAsync(messageToSend);


                        mostRecentMessage.Value.prevent.amount += 1;
                        mostRecentMessage.Value.prevent.last = DateTime.Now;
                        
                        warnedMembers.Add(member);

                        await historyParser.AddHistory(mostRecentMessage.Key, mostRecentMessage.Value);
                    }
                    else
                    {
                        double lastPreventDay = Math.Ceiling((DateTime.Now - mostRecentMessage.Value.prevent.last).TotalDays);

                        if (lastPreventDay > 60)
                        {
                            mostRecentMessage.Value.prevent.amount = 0;

                            await historyParser.AddHistory(mostRecentMessage.Key, mostRecentMessage.Value);
                        }
                    }
                }
            }

            #region Log

            string message = $"Prevent {warnedMembers.Count} members can be kick:\n";
            foreach (var m in warnedMembers)
            {
                message += $"{m.DisplayName}\n";
            }

            Console.WriteLine(message);
            #endregion

        }

        public static bool HasRole(DiscordMember member, string roleName)
        {
            // Vérifier si le membre possède un rôle avec le nom spécifié
            return member.Roles.Any(role => role.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase));
        }

        private static async void UpdateDescription()
        {
            var events = await Guild.GetEventsAsync();
            eventNumber = events.Count;

            DiscordActivity activity = new DiscordActivity($"{eventNumber} événements !",ActivityType.Competing);

            Client.UpdateStatusAsync(activity);
        }

        public static async Task<DiscordMessage> GetMessageFromURI(string messageUrl)
        {
            // Extraire les IDs du lien
            var segments = new Uri(messageUrl).Segments;
            if (segments.Length < 4)
            {
                Console.WriteLine("URL de message invalide.");
                return null;
            }

            ulong guildId = ulong.Parse(segments[2].TrimEnd('/'));
            ulong channelId = ulong.Parse(segments[3].TrimEnd('/'));
            ulong messageId = ulong.Parse(segments[4]);

            DiscordGuild localGuild = await Client.GetGuildAsync(guildId);
            if (localGuild == null)
            {
                return null;
            }
            // Récupérer le canal
            var channel = localGuild.GetChannel(channelId);

            // Si le canal/thread est toujours null, retournez null
            if (channel == null)
            {
                Console.WriteLine("Canal introuvable.");
                return null;
            }

            // Récupérer le message
            var message = await channel.GetMessageAsync(messageId);
            if (message == null)
            {
                Console.WriteLine("Message introuvable.");
                return null;
            }

            Console.WriteLine($"{channel.Name}: {message.Author.Username}\n{message.Content}");

            // Répondre avec le contenu du message
            return message;
        }

        private static void AddUserPurge(ulong id, JSONHistory.Description desc)
        {
            if (userToPurge.History.ContainsKey(id))
            {
                userToPurge.History[id]= desc;
            }
            else
            {
                userToPurge.History.Add(id, desc);
            }
        }
        
        

        private static async Task OnDm(DiscordClient sender, MessageCreateEventArgs args)
        {
            if (args.Author.IsBot || args.Message.Content.StartsWith(config.prefix))
            {
                return;
            }

            // Dm reçu
            if (args.Guild == null)
            {
                await conversationParser.privateConversation(args.Author.Id, args.Message, Guild.GetChannel(config.ID_staffChannel), Client);
                return;
            }

            // Réponse dans un thread servant de conversation privée
            var conv = conversationParser.json.Conversations.Where(c => c.Value.threadId == args.Channel.Id).FirstOrDefault();
            if (args.Channel.IsThread && conv.Value != null)
            {
                // Recherche du membre en comparant le hash de l'ID
                foreach (DiscordMember member in args.Guild.Members.Values)
                {
                    byte[] tmpHash = ASCIIEncoding.ASCII.GetBytes(member.Id.ToString());
                    string anonymId = System.Text.Encoding.UTF8.GetString(new MD5CryptoServiceProvider().ComputeHash(tmpHash));
                    if (anonymId.Equals(conv.Key))
                    {
                        // Construction du message avec les fichiers joints
                        string urls = "";
                        foreach (DiscordAttachment file in args.Message.Attachments)
                        {
                            urls += $" {file.Url}";
                        }

                        await member.SendMessageAsync(args.Message.Content + urls
                            + $"\n-# Envoyé par <@{args.Message.Author.Id}>");
                        await args.Message.CreateReactionAsync(DiscordEmoji.FromName(Client, ":white_check_mark:"));
                        return;
                    }
                }
                await args.Message.CreateReactionAsync(DiscordEmoji.FromName(Client, ":x:"));
                await args.Message.RespondAsync("Désolé, je n'ai pas pu retrouver le destinaire.");
            }
        }

        public static async Task CleanHistory()
        {
            List<ulong> membersToClean = new List<ulong>();
            var members = await Guild.GetAllMembersAsync();

            foreach (var user in historyParser.json.History)
            {
                if (!members.Any(x => x.Id == user.Key))
                {
                    membersToClean.Add(user.Key);
                }
            }

            foreach (var user in membersToClean)
            {
                historyParser.json.History.Remove(user);
            }

            await historyParser.WriteJSON();
        }

        public static bool IsValidUri(string uriString)
        {
            if (string.IsNullOrWhiteSpace(uriString))
                return false;

            return Uri.TryCreate(uriString, UriKind.Absolute, out Uri uriResult)
                   && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        public static Task OnHeightAM()
        {
            CheckAndSendMessageToPreventPrugeIsComming();

            WishBirthday();

            updateNewsFeed();

            return Task.CompletedTask;
        }

        // Tâche qui souhaite bon anniversaire en comparant la date du jour avec la date des anniversaires
        public static Task WishBirthday()
        {
            string currentDate = DateTime.Now.ToString().Substring(0, 5);

            foreach (KeyValuePair<string, MemberAnniversaire> memberAnniv in anniversairesParser.json.Anniversaires)
            {
                if (currentDate.Equals(memberAnniv.Value.dateAnniv) && !memberAnniv.Value.ignored)
                {
                    Client.SendMessageAsync(Guild.GetChannel(config.ID_generalChannel), $"Bon anniversaire <@{memberAnniv.Key}> ! :partying_face: :tada:");
                }
            };
            return Task.CompletedTask;
        }

        public static Task updateNewsFeed()
        {

            // Liste temporaire des évènements pour remettre à jour la liste stockée
            Dictionary<string, string> NewsFeedTmp = new Dictionary<string, string>();

            // Traitement de la première page (évènements en cours et proches)
            string bclUrl = "https://www.bigcitylife.fr/agenda/";
            HtmlWeb web = new HtmlAgilityPack.HtmlWeb();
            HtmlDocument doc = web.Load(bclUrl);
            int i = 1;
            var titre = doc.DocumentNode.SelectSingleNode($"//*[@id=\"page-0\"]/div/div/div/div/div/div[2]/div[{i}]/div/article/div/header/h3/a");
            while (titre != null)
            {
                DiscordEmbedBuilder embedBuilder = NewsBuilder(bclUrl, web, doc, i, NewsFeedTmp);
                if (embedBuilder != null)
                {
                    DiscordMessageBuilder builder = new DiscordMessageBuilder().AddEmbed(embedBuilder);
                    Guild.GetChannel(config.ID_newsFeedChannel).SendMessageAsync(builder);
                }

                i += 1;
                titre = doc.DocumentNode.SelectSingleNode($"//*[@id=\"page-0\"]/div/div/div/div/div/div[2]/div[{i}]/div/article/div/header/h3/a");
            }

            // Traitement de la deuxième page (évènements à venir)
            bclUrl = "https://www.bigcitylife.fr/agenda/liste/page/2/?hide_subsequent_recurrences=1";
            doc = web.Load(bclUrl);
            int j = 1;
            titre = doc.DocumentNode.SelectSingleNode($"//*[@id=\"page-0\"]/div/div/div/div/div/div[2]/div[{j}]/div/article/div/header/h3/a");
            while (titre != null && !"".Equals(titre))
            {
                DiscordEmbedBuilder embedBuilder = NewsBuilder(bclUrl, web, doc, j, NewsFeedTmp);
                if (embedBuilder != null)
                {
                    DiscordMessageBuilder builder = new DiscordMessageBuilder().AddEmbed(embedBuilder);
                    Guild.GetChannel(config.ID_newsFeedChannel).SendMessageAsync(builder);
                }

                j += 1;
                titre = doc.DocumentNode.SelectSingleNode($"//*[@id=\"page-0\"]/div/div/div/div/div/div[2]/div[{j}]/div/article/div/header/h3/a");
            }

            Program.newsFeedParser.json.NewsFeed = NewsFeedTmp;
            Program.newsFeedParser.WriteJSON();
            return Task.CompletedTask;
        }

        #region Buttons functions
        public static async Task AddOrRemoveRoleToSelfUser(DiscordClient sender, ComponentInteractionCreateEventArgs args, DiscordRole role)
        {
            DiscordMember member = await args.Guild.GetMemberAsync(args.User.Id);

            if (member.Roles.Contains(role))
            {
                await member.RevokeRoleAsync(role);
            }
            else
            {
                await member.GrantRoleAsync(role);

                if (rolesParser.json.incompatibleRolesByRole.ContainsKey(role.Id))
                {
                    foreach (ulong incompatibleRole in rolesParser.json.incompatibleRolesByRole[role.Id])
                    {
                        try
                        {
                            await member.RevokeRoleAsync(args.Guild.GetRole(incompatibleRole));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }
                    }
                }
            }
        }

        #endregion
        #endregion
    }
}
