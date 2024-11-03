﻿using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using LaGrueJaune.config;
using static LaGrueJaune.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Scripting.Hosting;

namespace LaGrueJaune.commands
{
    public class SlashCommandsBank : ApplicationCommandModule
    {
        #region Debug
        [SlashCommand("Template", "Command template: Do nothing.")]
        [SlashRequireUserPermissions(Permissions.Administrator)]
        public async Task Template(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            //Some time consuming task like a database call or a complex operation

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Commande exécutée."));
        }

        [SlashCommand("who", "Command to test if the bot is working.")]
        [SlashRequireUserPermissions(Permissions.Administrator)]
        public async Task Who(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(new DiscordEmbedBuilder().WithDescription($"You are <@{ctx.User.Id}> !"));
        }
        #endregion

        #region Purge
        [SlashCommand("SelfAddingToPurge", "Command to adding itself in the purge list.")]
        [SlashRequireUserPermissions(Permissions.Administrator)]
        public async Task SelfAddingToPurge(InteractionContext ctx)
        {
            Program.historyParser.json.History[ctx.Member.Id].publicationDate = DateTime.MinValue;

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource);
        }


        [SlashCommand("PurgeList", "Make the list for the purge.")]
        [SlashRequireUserPermissions(Permissions.Administrator)]
        public async Task PurgeList(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            await Program.CleanHistory();

            Program.isPurgeMessage = true;
            DiscordMessageBuilder msg = await Program.GetPurgeMessage(Program.purgeListPageIndex);

            Program.actualPurgeMessage = await ctx.Channel.SendMessageAsync(msg);

            var response = new DiscordWebhookBuilder().AddEmbed(msg.Embed);
            await ctx.EditResponseAsync(response);
        }

        [SlashCommand("SetUserKickable", "Set a user in purge list to be kickable or not.")]
        [SlashRequireUserPermissions(Permissions.Administrator)]
        public async Task SetUserKickable(
            InteractionContext ctx,
            [Option("ID","The ID of the user in the purge list.")] long IDinList, 
            [Option("Kickable", "If the user is kickable")] bool kickable)
        {

            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);


            Program.userToPurge.History.ElementAt((int)IDinList - 1).Value.isKickable = kickable;
            await Program.actualPurgeMessage.ModifyAsync(await Program.GetPurgeMessage(Program.purgeListPageIndex));

            await ctx.DeleteResponseAsync();
        }

        [SlashCommand("ViewReason", "View the reason for the user to be in the purge list")]
        [SlashRequireUserPermissions(Permissions.Administrator)]
        public async Task ViewReason(
            InteractionContext ctx, 
            [Option("ID", "The ID of the user in the purge list.")] long IDinList)
        {

            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);


            var historyUser = Program.userToPurge.History.ElementAt((int)IDinList - 1);

            await ctx.Channel.SendMessageAsync($"<@{historyUser.Key}> est dans la liste des kick pour la raison suivante:\n{historyUser.Value.kickReason}");

            await ctx.DeleteResponseAsync();
        }

        [SlashCommand("ViewReasonMember", "View the reason for the user to be in the purge list")]
        [SlashRequireUserPermissions(Permissions.Administrator)]
        public async Task ViewReasonMember(
            InteractionContext ctx, 
            [Option("Member","Member in the purge list.")] DiscordUser member)
        {

            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var historyUser = Program.userToPurge.History[member.Id];

            await ctx.Channel.SendMessageAsync($"<@{member.Id}> est dans la liste des kick pour la raison suivante:\n{historyUser.kickReason}");

            await ctx.DeleteResponseAsync();
        }

        [SlashCommand("PurgeKick", "Kick all the user kickable in the list")]
        [SlashRequireUserPermissions(Permissions.Administrator)]
        public async Task PurgeKick(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            await Program.CleanHistory();
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

            await Program.CleanHistory();
            await ctx.Channel.SendMessageAsync($"Kicked {nbOfKickedUsers} innactives users in the list");

            await ctx.DeleteResponseAsync();
        }
        #endregion

        #region History
        [SlashCommand("AddHistory", "Add user in the history.")]
        [SlashRequireUserPermissions(Permissions.Administrator)]
        public async Task AddHistory(
            InteractionContext ctx, 
            [Option("MessageURL","The last message of the user.")] string messageUrl)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

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

            await ctx.DeleteResponseAsync();
        }


        [SlashCommand("ah", "Add user in the history.")]
        [SlashRequireUserPermissions(Permissions.Administrator)]
        public async Task AH(
            InteractionContext ctx,
            [Option("MessageURL", "The last message of the user.")] string messageUrl)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            await AddHistory(ctx, messageUrl);

            await ctx.DeleteResponseAsync();
        }

        [SlashCommand("ForceAddHistory", "Add user in the history using it's name and the date.")]
        [SlashRequireUserPermissions(Permissions.Administrator)]
        public async Task ForceAddHistory(InteractionContext ctx, 
            [Option("Member","Member to force add in the history.")] DiscordUser user, 
            [Option("Date","Date of the force adding (write jj/mm/aaaa)")] string dates)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            DateTime date = DateTime.Parse(dates);

            JSONHistory.Description desc = new JSONHistory.Description()
            {
                publicationDate = date
            };

            desc.author = user.Username;

            await Program.historyParser.AddHistory(user.Id, desc);

            await Program.actualPurgeMessage.ModifyAsync(await Program.GetPurgeMessage(Program.purgeListPageIndex));

            await ctx.DeleteResponseAsync();
        }

        [SlashCommand("fah", "Add user in the history using it's name and the date.")]
        [SlashRequireUserPermissions(Permissions.Administrator)]
        public async Task FAH(InteractionContext ctx, 
            [Option("Member", "Member to force add in the history.")] DiscordUser user,
            [Option("Date", "Date of the force adding (write jj/mm/aaaa)")] string dates)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            await ForceAddHistory(ctx, user, dates);

            await ctx.DeleteResponseAsync();
        }
        #endregion

        #region Embeds

        #endregion


        #region Common
        [SlashCommand("Help", "Show all commands.")]
        [SlashRequireUserPermissions(Permissions.Administrator)]
        public async Task Help(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            string description = "";
            foreach (var command in Program.SlashCommands.RegisteredCommands)
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

            await ctx.DeleteResponseAsync();
        }

        [SlashCommand("Ban", "Ban a user and send the reason in DM.")]
        [SlashRequireUserPermissions(Permissions.Administrator)]
        public async Task Ban(InteractionContext ctx, [Option("User", "User to ban.")] DiscordUser user, [Option("Reason","The reason for the ban.")] string reason = "")
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            // Envoyer un message privé à l'utilisateur
            DiscordMember member = await Program.Guild.GetMemberAsync(user.Id);

            var dmChannel = await member.CreateDmChannelAsync();
            await dmChannel.SendMessageAsync($"Tu a été banni de la grue jaune pour la raison suivante:\n {reason}");

            string stashedNickname = member.Nickname;

            await member.BanAsync(0, reason);
            await ctx.Channel.SendMessageAsync($"{stashedNickname} a été banni pour la raison suivante:\n{reason}");

            await ctx.DeleteResponseAsync();
        }

        #endregion

        #region Notes
        [SlashCommand("Note", "Ajoute une note pour le membre spécifié")]
        [SlashRequireUserPermissions(Permissions.ModerateMembers)]

        public async Task Note(InteractionContext ctx, 
            [Option("Membre", "Membre")] DiscordUser member,
            [Option("Texte", "Texte à ajouter en tant que note")] string phrase)
        {
            if (ctx.Guild == null)
            {
                DiscordFollowupMessageBuilder errorBuilder = new DiscordFollowupMessageBuilder().WithContent("Cette commande n'est pas autorisée en MP.");
                await ctx.Interaction.CreateFollowupMessageAsync(errorBuilder);
                return;
            }

            await Program.notesParser.AddNotes(member.Id, $"{phrase}\n{DateTime.Now.ToString()}");

            int nbNotes = Program.notesParser.json.Notes[member.Id].listeNotes.Count;

            await ctx.Interaction.DeferAsync(ephemeral: false);
            string total = $"Note n°{nbNotes.ToString()} ajoutée pour <@{member.Id}>.";
            DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().WithContent(total);
            await ctx.Interaction.CreateFollowupMessageAsync(builder);

        }

        [SlashCommand("NoteList", "Affiche la liste des notes du membre spécifié")]
        [SlashRequireUserPermissions(Permissions.ModerateMembers)]
        public async Task NoteList(InteractionContext ctx, [Option("Membre", "Membre")] DiscordUser member)
        {
            if (ctx.Guild == null)
            {
                DiscordFollowupMessageBuilder errorBuilder = new DiscordFollowupMessageBuilder().WithContent("Cette commande n'est pas autorisée en MP.");
                await ctx.Interaction.CreateFollowupMessageAsync(errorBuilder);
                return;
            }

            await ctx.Interaction.DeferAsync(ephemeral: false);

            if (Program.notesParser.json.Notes.Keys.Contains(member.Id))
            {
                int page = 1;
                int nbTotal = Program.notesParser.json.Notes[member.Id].listeNotes.Count();

                string note = Program.notesParser.json.Notes[member.Id].listeNotes.First();
                DiscordEmbedBuilder builder = BuildEmbedNotes(member, note, page, nbTotal);
                var previous = new DiscordButtonComponent(ButtonStyle.Primary, $"{member.Id}-{2 * page - 1}", "Précédent", false);
                var next = new DiscordButtonComponent(ButtonStyle.Primary, $"{member.Id}-{2 * page}", "Suivant", false);
                IEnumerable<DiscordComponent> components = new DiscordComponent[] { previous, next };
                DiscordMessageBuilder message = new DiscordMessageBuilder().WithEmbed(builder);
                DiscordFollowupMessageBuilder reponse = new DiscordFollowupMessageBuilder(message).AddComponents(components);

                await ctx.Interaction.CreateFollowupMessageAsync(reponse);
            }

            else
            {
                DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().WithContent($"<@{member.Id}> n'a pas de note ! :person_shrugging:");
                await ctx.Interaction.CreateFollowupMessageAsync(builder);
            }
        }

        [SlashCommand("NoteClear", "Supprime la note du membre spécifié ou toute sa liste")]
        [SlashRequireUserPermissions(Permissions.ModerateMembers)]
        public async Task noteClear(InteractionContext ctx, [Option("Membre", "Membre")] DiscordUser member, [Option("Note", "Numéro de note à supprimer, 0 pour toutes")] string number)
        {
            if (ctx.Guild == null)
            {
                DiscordFollowupMessageBuilder errorBuilder = new DiscordFollowupMessageBuilder().WithContent("Cette commande n'est pas autorisée en MP.");
                await ctx.Interaction.CreateFollowupMessageAsync(errorBuilder);
                return;
            }

            await ctx.Interaction.DeferAsync(ephemeral: false);
            int index = Convert.ToInt32(number);
            if (index == 0)
            {
                Program.notesParser.json.Notes.Remove(member.Id);
                await Program.notesParser.WriteJSON();
                DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().WithContent($":broom: Toutes les notes de <@{member.Id}> ont été supprimées.");
                await ctx.Interaction.CreateFollowupMessageAsync(builder);
            }
            else
            {
                List<string> list = Program.notesParser.json.Notes[member.Id].listeNotes;
                list.Remove(list[index - 1]);
                await Program.notesParser.WriteJSON();
                DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().WithContent($":broom: Note n°{number} supprimée pour <@{member.Id}>.");
                await ctx.Interaction.CreateFollowupMessageAsync(builder);
            }

        }

        #endregion
    }
}
