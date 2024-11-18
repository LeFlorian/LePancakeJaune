﻿using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using LaGrueJaune.commands;
using LaGrueJaune.config;
using Quartz.Impl;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static LaGrueJaune.Utils;
using static LaGrueJaune.config.JSONAnniversaires;
using System.Security.Policy;
using System.Diagnostics.Metrics;

namespace LaGrueJaune
{
    public enum ButtonFunction
    {
        [ChoiceName("Null")]
        Null,
        [ChoiceName("AddRole")]
        AddRole
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
        public static JSONHistory userToPurge;

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

            // Trigger pour vérifier les anniversaires à 8h chaque matin
            var trigger = TriggerBuilder.Create()
                .WithDailyTimeIntervalSchedule(s => s
                .WithIntervalInHours(24)
                    //.WithIntervalInHours(24) // daily
                    .StartingDailyAt(TimeOfDay.HourAndMinuteOfDay(8, 0)) // 8h du matin
                )
                .Build();

            // Planification du job birthdayWatch déclenché par le trigger à 8h
            StdSchedulerFactory factory = new StdSchedulerFactory();
            IScheduler scheduler = await factory.GetScheduler();
            await scheduler.Start();
            await scheduler.ScheduleJob(JobBuilder.Create<birthdayWatch>().Build(), trigger);
        
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

            JSONHistory.Description newMessage = new JSONHistory.Description()
            {
                author = args.Author.Username,
                publicationDate = DateTime.Now,
                link = args.Message.JumpLink
            };

            await historyParser.AddHistory(args.Author.Id, newMessage);

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

                    dir = new DiscordInteractionResponseBuilder(await GetPurgeMessage(purgeListPageIndex +1, isPurgeMessage));
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
                int buttonId = Int32.Parse(args.Id.Split('-')[1]);

                List<string> list = Program.notesParser.json.Notes[userId].listeNotes;

                if (buttonId.Equals(1))
                {
                    return;
                }
                // Page suivante
                else if (buttonId % 2 == 0 && buttonId / 2 < list.Count)
                {
                    var action = buildAction(args.Guild.Members.Values.Where(m => m.Id.Equals(userId)).First(), list[buttonId / 2], buttonId / 2 + 1);
                    await args.Message.ModifyAsync(action);
                }
                // Page précédente
                else if (buttonId % 2 != 0 && buttonId >= 3)
                {
                    var action = buildAction(args.Guild.Members.Values.Where(m => m.Id.Equals(userId)).First(), list[(buttonId - 1) / 2 - 1], (buttonId - 1) / 2);
                    await args.Message.ModifyAsync(action);
                }
            }
            catch
            {

            }
            #endregion

            #region Other Buttons
            string[] buttonInfo = args.Interaction.Data.CustomId.Split(':');

            DiscordMessage linkedMessage = args.Message;
            string linkedFunction = buttonInfo[0];
            DiscordRole linkedRole = args.Guild.GetRole(ulong.Parse(buttonInfo[1]));

            
            switch (linkedFunction)
            {
                case "AddRole":
                    AddRoleToSelfUser(sender,args,linkedRole);
                    break;

            }

            await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,new DiscordInteractionResponseBuilder().WithContent($"Fonction: {linkedFunction}\nRole: {linkedRole}"));
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
        private static Task OnUserJoinEvent(DiscordClient sender, ScheduledGuildEventUserAddEventArgs args)
        {
            return Task.CompletedTask;
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

                    description += $"\n{emojiKickable} {i+1}:\t<@{userID}>\t{desc.link}\t{desc.numberOfDay} days";
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

            var buttonID = GetIDButton((json.History.Count/numberOfUserPerPages)+1);

            var message = new DiscordMessageBuilder();
            message.AddEmbed(embed);
            message.AddComponents(buttonPrevious, buttonID, buttonNext);
            return message;
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
                    mostRecentMessage.Value.kickReason = $"Tu as été kick pour innactivé ({differenceInDays} jours depuis le dernier message)";
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

        // Tâche qui souhaite bon anniversaire en comparant la date du jour avec la date des anniversaires
        public static Task wishBirthday()
        {
            string currentDate = DateTime.Now.ToString().Substring(0,5);

            foreach (KeyValuePair<string, MemberAnniversaire> memberAnniv in Program.anniversairesParser.json.Anniversaires)
            {
                if (currentDate.Equals(memberAnniv.Value.dateAnniv) && !memberAnniv.Value.ignored){
                    Client.SendMessageAsync(Guild.GetChannel(config.ID_generalChannel), $"Bon anniversaire <@{memberAnniv.Key}> ! :partying_face: :tada:");
                }
            };
            return Task.CompletedTask;
        }

        #region Buttons functions
        public static async Task AddRoleToSelfUser(DiscordClient sender, ComponentInteractionCreateEventArgs args, DiscordRole role)
        {
            DiscordMember member = await args.Guild.GetMemberAsync(args.User.Id);
            await member.GrantRoleAsync(role);
        }

        #endregion
        #endregion
    }
}
