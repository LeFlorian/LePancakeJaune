using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using LibreTranslate;
using LaGrueJaune.config;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using static LaGrueJaune.commands.CommandsBank;
using LibreTranslate.Net;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;

namespace LaGrueJaune.commands
{
    public class CommandsBank : BaseCommandModule
    {
        [Command]
        [RequireUserPermissions(Permissions.Administrator)]
        public async Task Who(CommandContext ctx)
        {
            await ctx.Channel.SendMessageAsync($"You are <@{ctx.User.Id}> !");
        }

        [Command("SetUserPurge")]
        [RequireUserPermissions(Permissions.Administrator)]
        public async Task SetUserToPurge(CommandContext ctx)
        {
            ctx.Message.DeleteAsync();
            Program.historyParser.json.History[ctx.Member.Id].publicationDate = DateTime.MinValue;
        }

        [Command("Purge")]
        [RequireUserPermissions(Permissions.Administrator)]
        public async Task PurgeList(CommandContext ctx)
        {
            ctx.Message.DeleteAsync();

            await CleanHistory(ctx);

            Program.isPurgeMessage = true;
            DiscordMessageBuilder msg = await Program.GetPurgeMessage(Program.purgeListPageIndex);
            Program.actualPurgeMessage = await ctx.Channel.SendMessageAsync(msg);
        }

        [Command("SetKickable")]
        [RequireUserPermissions(Permissions.Administrator)]
        public async Task SetUserKickable(CommandContext ctx, int IDinList, bool kickable)
        {
            ctx.Message.DeleteAsync();

            Program.userToPurge.History.ElementAt(IDinList - 1).Value.isKickable = kickable;
            await Program.actualPurgeMessage.ModifyAsync(await Program.GetPurgeMessage(Program.purgeListPageIndex));
        }

        [Command("ViewReason")]
        [RequireBotPermissions(Permissions.Administrator)]
        public async Task ViewReason(CommandContext ctx, int IDinList)
        {
            ctx.Message.DeleteAsync();
            var historyUser = Program.userToPurge.History.ElementAt(IDinList - 1);

            await ctx.Channel.SendMessageAsync($"<@{historyUser.Key}> est dans la liste des kick pour la raison suivante:\n{historyUser.Value.kickReason}");
        }

        [Command("ViewReason")]
        [RequireBotPermissions(Permissions.Administrator)]
        public async Task ViewReason(CommandContext ctx, ulong ID)
        {
            ctx.Message.DeleteAsync();
            var historyUser = Program.userToPurge.History[ID];

            await ctx.Channel.SendMessageAsync($"<@{ID}> est dans la liste des kick pour la raison suivante:\n{historyUser.kickReason}");
        }


        [Command("PurgeKick")]
        [RequireUserPermissions(Permissions.Administrator)]
        public async Task KickAllUsersInTheList(CommandContext ctx)
        {
            ctx.Message.DeleteAsync();

            await CleanHistory(ctx);
            int nbOfKickedUsers = 0;

            foreach (var user in Program.userToPurge.History)
            {
                var member = await Program.Guild.GetMemberAsync(user.Key);

                if (user.Value.isKickable)
                {
                    Console.Write($"{user.Value.author} kicked for reason:\n{user.Value.kickReason}");

                    // Envoyer un message privé à l'utilisateur
                    var dmChannel = await member.CreateDmChannelAsync();
                    await dmChannel.SendMessageAsync(user.Value.kickReason);

                    await member.RemoveAsync(user.Value.kickReason);
                    nbOfKickedUsers++;
                }

            }

            await CleanHistory(ctx);
            await ctx.Channel.SendMessageAsync($"Kicked {nbOfKickedUsers} innactives users in the list");

        }

        [Command("CleanHistory")]
        [RequireUserPermissions(Permissions.Administrator)]
        public async Task CleanHistory(CommandContext ctx)
        {
            ctx.Message.DeleteAsync();

            List<ulong> membersToClean = new List<ulong>();
            var members = await Program.Guild.GetAllMembersAsync();

            foreach (var user in Program.historyParser.json.History)
            {
                if (!members.Any(x => x.Id == user.Key))
                {
                    membersToClean.Add(user.Key);
                }
            }

            foreach (var user in membersToClean)
            {
                Program.historyParser.json.History.Remove(user);
            }

            await Program.historyParser.WriteJSON();
        }

        [Command("Help")]
        [RequireUserPermissions(Permissions.Administrator)]
        public async Task Help(CommandContext ctx)
        {
            ctx.Message.DeleteAsync();

            string description = "";
            foreach (var command in Program.Commands.RegisteredCommands)
            {
                description += $"\n- {command.Key}";
            }


            var embed = new DiscordEmbedBuilder()
            {
                Title = "Help :",
                Description = description,
                Color = DiscordColor.Gold

            };

            var message = new DiscordMessageBuilder();
            message.AddEmbed(embed);

            await ctx.Channel.SendMessageAsync(embed: embed);
        }

        [Command("History")]
        [RequireUserPermissions(Permissions.Administrator)]
        public async Task ShowHistory(CommandContext ctx)
        {
            ctx.Message.DeleteAsync();

            var message = await Program.GetPurgeMessage(1, false);

            Program.isPurgeMessage = false;
            Program.actualPurgeMessage = await ctx.Channel.SendMessageAsync(message);
        }

        [Command("AddHistory")]
        [RequireUserPermissions(Permissions.Administrator)]
        public async Task AddHistory(CommandContext ctx, string messageUrl)
        {
            ctx.Message.DeleteAsync();

            DiscordMessage message = await Program.GetMessageFromURI(messageUrl);
            if (message == null)
            {
                var warningMSG = await ctx.Channel.SendMessageAsync($"Error occured. Try !forceaddhistory [userID] [Date]");

                await Task.Delay(5000);

                warningMSG.DeleteAsync();
            }
            else
            {
                JSONHistory.Description desc = new JSONHistory.Description()
                {
                    author = message.Author.Username,
                    link = message.JumpLink,
                    publicationDate = message.CreationTimestamp.DateTime
                };

                await Program.historyParser.AddHistory(message.Author.Id, desc);

                if (Program.actualPurgeMessage != null)
                    await Program.actualPurgeMessage.ModifyAsync(await Program.GetPurgeMessage(Program.purgeListPageIndex));
            }

        }


        [Command("AH")]
        [RequireUserPermissions(Permissions.Administrator)]
        public async Task AH(CommandContext ctx, string messageUrl)
        {
            await AddHistory(ctx, messageUrl);
        }

        [Command("FAH")]
        [RequireUserPermissions(Permissions.Administrator)]
        public async Task ForceAddHistory(CommandContext ctx, ulong userID, DateTime date)
        {
            ctx.Message.DeleteAsync();

            Console.WriteLine(date);

            JSONHistory.Description desc = new JSONHistory.Description()
            {
                publicationDate = date
            };

            var author = await Program.Guild.GetMemberAsync(userID);
            desc.author = author.Username;

            await Program.historyParser.AddHistory(userID, desc);

            await Program.actualPurgeMessage.ModifyAsync(await Program.GetPurgeMessage(Program.purgeListPageIndex));
        }

        [Command("FAH")]
        [RequireUserPermissions(Permissions.Administrator)]
        public async Task FAH(CommandContext ctx, ulong userID, string dates)
        {
            ctx.Message.DeleteAsync();

            DateTime date = DateTime.Parse(dates);

            await ForceAddHistory(ctx, userID, date);
        }

        [Command("ban")]
        [RequireUserPermissions(Permissions.Administrator)]
        public async Task Ban(CommandContext ctx, DiscordMember user, string reason = "")
        {
            await ctx.Message.DeleteAsync();

            // Envoyer un message privé à l'utilisateur
            var dmChannel = await user.CreateDmChannelAsync();
            await dmChannel.SendMessageAsync($"Tu a été banni de la grue jaune pour la raison suivante:\n {reason}");

            await user.BanAsync(0, reason);
            await ctx.Channel.SendMessageAsync($"{user.Nickname} a été banni pour la raison suivante:\n{reason}");

        }

        [Command("slashCommands")]
        [RequireUserPermissions(Permissions.Administrator)]
        public async Task slashCommands(CommandContext ctx)
        {
            await ctx.Message.DeleteAsync();

            string commands = "";

            Console.WriteLine(Program.SlashCommands.RegisteredCommands.Count);

            foreach (var command in Program.SlashCommands.RegisteredCommands)
            {
                commands += $"{command.Key}\n";
            }

            await ctx.Channel.SendMessageAsync($"{commands}");
        }

        [Command("note")]
        [RequireUserPermissions(Permissions.ModerateMembers)]
        public async Task addNote(CommandContext ctx, ulong memberId, params string[] text)
        {
            string phrase = "";
            foreach (string word in text)
            {
                phrase += $"{word} ";
            }

            await Program.notesParser.AddNotes(memberId, $"{phrase.Remove(phrase.Length - 1)} - {DateTime.Now.ToString()}");
            int nbNotes = Program.notesParser.json.Notes[memberId].listeNotes.Keys.Max();
            string total;
            if (nbNotes.Equals(1)) {
                total = "Cette personne a 1 note à son actif.";
               }
            else
            {
                total = $"Cette personne a {nbNotes.ToString()} notes à son actif.";
            }
            await ctx.Channel.SendMessageAsync($"La note sur l'utilisateur <@{memberId.ToString()}> a bien été ajoutée.");
        }

        [Command("noteList")]
        [RequireUserPermissions(Permissions.ModerateMembers)]
        public async Task getNotes(CommandContext ctx, ulong memberId)
        {
            string notesTxt = "";
            foreach (KeyValuePair<int, String>  note in Program.notesParser.json.Notes[memberId].listeNotes){
                notesTxt += $"\n{note.Key.ToString()}: {note.Value.ToString()}";
            }
            await ctx.Channel.SendMessageAsync($"Listes des notes sur l'utilisateur <@{memberId.ToString()}>:{notesTxt}");
        }

        [Command("noteClear")]
        [RequireUserPermissions(Permissions.ModerateMembers)]
        public async Task getNotes(CommandContext ctx, ulong memberId, int index)
        {
            Program.notesParser.json.Notes[memberId].listeNotes.Remove(index);
            await Program.notesParser.WriteJSON();
            await ctx.Channel.SendMessageAsync($"Note correctement supprimée.");
        }

        [Command("noteClear")]
        [RequireUserPermissions(Permissions.ModerateMembers)]
        public async Task getNotes(CommandContext ctx, ulong memberId, string arg = "all")
        {
            Program.notesParser.json.Notes.Remove(memberId);
            await Program.notesParser.WriteJSON();
            await ctx.Channel.SendMessageAsync($"Toutes les notes de la personne ont été supprimées.");
        }
    }
}
