using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using LaGrueJaune.config;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static LaGrueJaune.config.JSONAnniversaires;
using static LaGrueJaune.Utils;

namespace LaGrueJaune.commands
{
    public class SlashCommandsBank : ApplicationCommandModule
    {
        public static DiscordMessage currentEditingMessage;

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

            await ctx.DeleteResponseAsync();
        }

        [SlashCommand("SetUserKickable", "Set a user in purge list to be kickable or not.")]
        [SlashRequireUserPermissions(Permissions.Administrator)]
        public async Task SetUserKickable(
            InteractionContext ctx,
            [Option("ID", "The ID of the user in the purge list.")] long IDinList,
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
            [Option("Member", "Member in the purge list.")] DiscordUser member)
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
            [Option("MessageURL", "The last message of the user.")] string messageUrl)
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
            [Option("Member", "Member to force add in the history.")] DiscordUser user,
            [Option("Date", "Date of the force adding (write jj/mm/aaaa)")] string dates)
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


        [SlashCommand("EmbedCreate", "Make an embeded message")]
        [SlashRequireUserPermissions(Permissions.ModerateMembers)]
        public async Task EmbedCreate(InteractionContext ctx,
            [Option("Title", "Embed title")] string title = "",
            [Option("Description", "Embed description")] string description = "",
            [Option("Color", "Embed color")] string hexColor = "",
            [Option("ImageUrl", "Embed image url")] string imageUrl = "",
            [Option("TitleUrl", "Embed title url")] string titleUrl = "",
            [Option("AuthorName", "Embed author name")] string authorName = "",
            [Option("AuthorUrl", "Embed author url")] string authorUrl = "",
            [Option("AuthorIconUrl", "Embed author icon url")] string authorIconUrl = "",
            [Option("FooterText", "Embed footer text")] string footerText = "",
            [Option("FooterIconUrl", "Embed footer icon url")] string footerIconUrl = "",
            [Option("ThumbmailUrl", "Embed thumbmail url")] string thumbmailUrl = ""
            )
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var embed = new DiscordEmbedBuilder() { Title = "Work in progress" };
            if (string.IsNullOrEmpty(hexColor))
                hexColor = "DCB454";

            DiscordMessage message = await ctx.Channel.SendMessageAsync(embed: embed);
            currentEditingMessage = message;

            await EmbedModify(ctx, title, description, hexColor, imageUrl, titleUrl, authorName, authorUrl, authorIconUrl, footerText, footerIconUrl, thumbmailUrl, true);

            await SelectMessage(ctx, currentEditingMessage.JumpLink.OriginalString, true);

            await ctx.DeleteResponseAsync();
        }

        [SlashCommand("EmbedModify", "Modify an embeded message")]
        [SlashRequireUserPermissions(Permissions.ModerateMembers)]
        public async Task EmbedModify(InteractionContext ctx,
            [Option("Title", "Embed title")] string title = "",
            [Option("Description", "Embed description")] string description = "",
            [Option("Color", "Embed color")] string hexColor = "",
            [Option("ImageUrl", "Embed image url")] string imageUrl = "",
            [Option("TitleUrl", "Embed title url")] string titleUrl = "",
            [Option("AuthorName", "Embed author name")] string authorName = "",
            [Option("AuthorUrl", "Embed author url")] string authorUrl = "",
            [Option("AuthorIconUrl", "Embed author icon url")] string authorIconUrl = "",
            [Option("FooterText", "Embed footer text")] string footerText = "",
            [Option("FooterIconUrl", "Embed footer icon url")] string footerIconUrl = "",
            [Option("ThumbmailUrl", "Embed thumbmail url")] string thumbmailUrl = "",
            [Option("ComeFromOtherFunction", "Functionnal var, don't use.")] bool commingFromOtherFunction = false
            )
        {
            if (!commingFromOtherFunction)
                await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            if (currentEditingMessage != null)
            {
                await SelectMessage(ctx, currentEditingMessage.JumpLink.OriginalString, true);

                if (currentEditingMessage.Embeds.Count > 0)
                {
                    DiscordEmbedBuilder newEmbed = new DiscordEmbedBuilder(currentEditingMessage.Embeds[0]);

                    Console.WriteLine(newEmbed.Title + "_" + newEmbed.Description + "_" + newEmbed.Color);


                    if (!string.IsNullOrEmpty(title))
                        newEmbed.WithTitle(title);

                    if (!string.IsNullOrEmpty(description))
                        newEmbed.WithDescription(description);

                    if (!string.IsNullOrEmpty(hexColor))
                    {
                        try
                        {
                            newEmbed.WithColor(new DiscordColor(hexColor));
                        }
                        catch
                        {
                            newEmbed.WithColor(new DiscordColor("DCB454"));
                        }
                    }

                    if (newEmbed.Author == null && (!string.IsNullOrEmpty(authorName) || !string.IsNullOrEmpty(authorUrl) || !string.IsNullOrEmpty(authorIconUrl)))
                    {
                        newEmbed.WithAuthor(" ");
                    }

                    if (!string.IsNullOrEmpty(authorName))
                        newEmbed.WithAuthor(
                            name: authorName,
                            url: newEmbed.Author.Url,
                            iconUrl: newEmbed.Author.IconUrl);

                    if (Program.IsValidUri(authorUrl))
                        newEmbed.WithAuthor(
                            url: authorUrl,
                            name: newEmbed.Author.Name,
                            iconUrl: newEmbed.Author.IconUrl);

                    if (Program.IsValidUri(authorIconUrl))
                        newEmbed.WithAuthor(
                            iconUrl: authorIconUrl,
                            name: newEmbed.Author.Name,
                            url: newEmbed.Author.Url);

                    if (newEmbed.Footer == null && (!string.IsNullOrEmpty(footerText) || !string.IsNullOrEmpty(footerIconUrl)))
                    {
                        newEmbed.WithFooter(" ");
                    }

                    if (!string.IsNullOrEmpty(footerText))
                        newEmbed.WithFooter(
                            text: footerText,
                            iconUrl: newEmbed.Footer.IconUrl);

                    if (Program.IsValidUri(footerIconUrl))
                        newEmbed.WithFooter(
                            iconUrl: footerIconUrl,
                            text: newEmbed.Footer.Text);

                    if (Program.IsValidUri(imageUrl))
                        newEmbed.WithImageUrl(imageUrl);


                    if (Program.IsValidUri(thumbmailUrl))
                        newEmbed.WithThumbnail(url: thumbmailUrl);

                    if (Program.IsValidUri(titleUrl))
                        newEmbed.WithUrl(titleUrl);

                    DiscordMessageBuilder message = new DiscordMessageBuilder(currentEditingMessage);
                    message.Embed = newEmbed;

                    try
                    {
                        currentEditingMessage = await currentEditingMessage.ModifyAsync(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
            }

            if (!commingFromOtherFunction)
                await ctx.DeleteResponseAsync();
        }



        [SlashCommand("SelectMessage", "Select an embeded message")]
        [SlashRequireUserPermissions(Permissions.ModerateMembers)]
        public async Task SelectMessage(InteractionContext ctx,
            [Option("Message", "The url of the embed to modify")] string messageUrl,
            [Option("Force", "Do not use")] bool forceResponse = false)
        {
            if (!forceResponse)
                await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource, new DiscordInteractionResponseBuilder() { IsEphemeral = true });

            DiscordMessage message = await Program.GetMessageFromURI(messageUrl);
            Console.WriteLine(message);

            if (message == null)
            {
                var warningMSG = await ctx.Channel.SendMessageAsync($"Message non trouvé.");

                await Task.Delay(5000);

                warningMSG.DeleteAsync();
            }
            else
            {
                currentEditingMessage = message;
            }


            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Message sélectionné."));
        }

        #endregion

        #region Buttons

        [SlashCommand("ButtonAdd", "Add a button to a selected message")]
        [SlashRequireUserPermissions(Permissions.Administrator)]
        public async Task ButtonAdd(InteractionContext ctx,
            [Option("Style", "Button style")] ButtonStyle bs = ButtonStyle.Primary,
            [Option("Label", "Button text")] string label = "",
            [Option("Status", "Is the button is active or not")] bool active = true,
            [Option("LinkedFunction", "Function of the button when pressed")] ButtonFunction function = ButtonFunction.Null,
            [Option("Role", "Role to assign when pressed the button")] DiscordRole role = default,
            [Option("Emoji", "Emoji on the button")] DiscordEmoji emoji = default,
            [Option("IgnoredRole", "If the user has this role, it can't get the role by this button")] DiscordRole ignoredRole = default)
        {
            if (currentEditingMessage == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                    {
                        IsEphemeral = true,
                        Content = "Pas de messages sélectionnés"
                    });
                return; // On quitte la fonction pour éviter une erreur
            }

            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            DiscordMessageBuilder message = new DiscordMessageBuilder(currentEditingMessage);


            var buttons = new List<DiscordComponent>();
            foreach (DiscordComponent dc in currentEditingMessage.Components)
            {
                if (dc is DiscordActionRowComponent)
                {
                    var row = (DiscordActionRowComponent)dc;
                    buttons.AddRange(row.Components);
                }
            }

            DiscordComponentEmoji dce = null;
            if (emoji != null)
                dce = new DiscordComponentEmoji(emoji);

            ulong roleID = Program.GetRoleIDOrZero(role);
            ulong ignoredRoleId = Program.GetRoleIDOrZero(ignoredRole);

            // Création du bouton
            var newButton = new DiscordButtonComponent(
                bs,
                $"{Guid.NewGuid()}:{function}:{roleID}:{ignoredRoleId}", // ID du bouton
                label,
                !active,
                dce
            );

            buttons.Add(newButton);

            // Organisation des composants en lignes
            var actionRows = new List<DiscordActionRowComponent>();
            foreach (var chunk in Utils.Chunk(buttons, 5))
            {
                var actionRow = new DiscordActionRowComponent(chunk);
                actionRows.Add(actionRow); // Ajoute chaque ligne complète à la liste
            }

            // Ajoute les lignes au message
            message.ClearComponents(); // Nettoie les anciens composants
            foreach (var actionRow in actionRows)
            {
                message.AddComponents(actionRow.Components); // Ajoute chaque ligne complète
            }

            Console.WriteLine("End : "+message.Components.Count);

            try
            {
                // Modification du message
                currentEditingMessage = await currentEditingMessage.ModifyAsync(message);

                await ctx.DeleteResponseAsync(); // Supprime la réponse d'attente
            }
            catch (Exception e)
            {
                Console.WriteLine($"Erreur lors de la modification du message : {e}");
                await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                    .WithContent("Une erreur est survenue lors de la modification du message."));
                await Task.Delay(5000);
                await ctx.DeleteResponseAsync();
            }
        }

        [SlashCommand("DropdownAdd", "Add a dropdown to a selected message")]
        [SlashRequireUserPermissions(Permissions.Administrator)]
        public async Task DropdownAdd(InteractionContext ctx,
            [Option("DropdownLabel", "Dropdown text")] string placeHolder = null,
            [Option("DropdownStatus", "Is the dropdown is active or not")] bool active = true,
            [Option("FirstOptionLabel", "Label of the first option")] string firstOptionLabel = "",
            [Option("FirstOptionDescription", "Description of the first option")] string firstOptionDescription = "",
            [Option("FirstOptionEmoji", "Function of the first option when selected")] DiscordEmoji firstOptionEmoji = default,
            [Option("FirstOptionLinkedFunction", "Function of the first option when selected")] ButtonFunction firstOptionFunction = ButtonFunction.Null,
            [Option("FirstOptionRole", "Role to assign when selected the first option")] DiscordRole firstOptionRole = default)
        {
            if (currentEditingMessage == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                    {
                        IsEphemeral = true,
                        Content = "Pas de messages sélectionnés"
                    });
                return; // On quitte la fonction pour éviter une erreur
            }

            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            ulong roleID = 0;
            if (firstOptionRole != default)
                roleID = firstOptionRole.Id;

            //Create the first option:
            var options = new List<DiscordSelectComponentOption>()
            {
                new DiscordSelectComponentOption(
                firstOptionLabel,
                $"{Guid.NewGuid()}:{firstOptionFunction}:{roleID}",
                firstOptionDescription,
                false,
                new DiscordComponentEmoji(firstOptionEmoji))
            };

            var dropdown = new DiscordSelectComponent($"{Guid.NewGuid()}:{ButtonFunction.AddOrRemoveRoleBySelectComp}", placeHolder, options);

            var message = new DiscordMessageBuilder(currentEditingMessage);
            message.AddComponents(dropdown);

            try
            {
                // Modification du message
                currentEditingMessage = await currentEditingMessage.ModifyAsync(message);

                await ctx.DeleteResponseAsync(); // Supprime la réponse d'attente
            }
            catch (Exception e)
            {
                Console.WriteLine($"Erreur lors de la modification du message : {e}");
                await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                    .WithContent("Une erreur est survenue lors de la modification du message."));
                await Task.Delay(5000);
                await ctx.DeleteResponseAsync();
            }
        }
        [SlashCommand("DropdownOptionAdd", "Add an option to a dropdown in a selected message")]
        [SlashRequireUserPermissions(Permissions.Administrator)]
        public async Task DropdownOptionAdd(InteractionContext ctx,
            [Option("Label", "Label of the option")] string label = "",
            [Option("Description", "Description of the option")] string description = "",
            [Option("Emoji", "Function of the option when selected")] DiscordEmoji emoji = default,
            [Option("LinkedFunction", "Function of the option when selected")] ButtonFunction function = ButtonFunction.Null,
            [Option("Role", "Role to assign when selected the option")] DiscordRole role = default)
        {
            if (currentEditingMessage == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                    {
                        IsEphemeral = true,
                        Content = "Pas de messages sélectionnés"
                    });
                return; // On quitte la fonction pour éviter une erreur
            }

            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            var message = new DiscordMessageBuilder(currentEditingMessage);
            DiscordSelectComponent currentSelect = null;

            foreach (DiscordComponent dc in message.Components)
            {
                if (dc is DiscordActionRowComponent)
                {
                    var row = (DiscordActionRowComponent)dc;
                    
                    foreach (var dc2 in row.Components)
                    {
                        if (dc2 is DiscordSelectComponent)
                        {
                            currentSelect = (DiscordSelectComponent)dc2;
                        }
                    }
                }
            }

            ulong roleID = 0;
            if (role != default)
                roleID = role.Id;

            var options = new List<DiscordSelectComponentOption>();

            foreach (var o in currentSelect.Options)
            {
                options.Add(o);
            }


            //Create the new option:
            var newOption = new DiscordSelectComponentOption(
                label,
                $"{Guid.NewGuid()}:{function}:{roleID}",
                description,
                false,
                new DiscordComponentEmoji(emoji));

            options.Add(newOption);

            var dropdown = new DiscordSelectComponent($"{Guid.NewGuid()}:{ButtonFunction.AddOrRemoveRoleBySelectComp}", currentSelect.Placeholder, options);

            message.ClearComponents();
            message.AddComponents(dropdown);

            try
            {
                // Modification du message
                currentEditingMessage = await currentEditingMessage.ModifyAsync(message);

                await ctx.DeleteResponseAsync(); // Supprime la réponse d'attente
            }
            catch (Exception e)
            {
                Console.WriteLine($"Erreur lors de la modification du message : {e}");
                await ctx.EditResponseAsync(new DiscordWebhookBuilder()
                    .WithContent("Une erreur est survenue lors de la modification du message."));
                await Task.Delay(5000);
                await ctx.DeleteResponseAsync();
            }
        }

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
        public async Task Ban(InteractionContext ctx, [Option("User", "User to ban.")] DiscordUser user, [Option("Reason", "The reason for the ban.")] string reason = "")
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            // Envoyer un message privé à l'utilisateur
            DiscordMember member = await Program.Guild.GetMemberAsync(user.Id);

            var dmChannel = await member.CreateDmChannelAsync();
            await dmChannel.SendMessageAsync($"Tu a été banni de la grue jaune pour la raison suivante:\n {reason}");

            string stashedNickname = member.DisplayName;

            await member.BanAsync(0, reason);
            await ctx.Channel.SendMessageAsync($"{stashedNickname} a été banni pour la raison suivante:\n{reason}");

            await ctx.DeleteResponseAsync();
        }

        [SlashCommand("Clear", "Supprime les n derniers messages du salon")]
        [SlashRequireUserPermissions(Permissions.ModerateMembers)]
        public async Task Clear(InteractionContext ctx, [Option("Nombre", "Nombre de messages à supprimer")] long n = 0)
        {

            if (n > 50)
            {
                await ctx.Interaction.DeferAsync(ephemeral: true);
                DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().WithContent($"Erreur: La limite du nombre de messages à supprimer est de 50.");
                await ctx.Interaction.CreateFollowupMessageAsync(builder);
            }

            else
            {
                await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

                var messages = ctx.Channel.GetMessagesAsync((int)n + 1).Result;

                foreach (var message in messages)
                {
                    await message.DeleteAsync();
                }

                await ctx.DeleteResponseAsync();
            }
        }

        #endregion

        #region Notes
        [SlashCommand("Note", "Ajoute une note pour le membre spécifié")]
        [SlashCommandPermissions(Permissions.ModerateMembers)]

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

            await ctx.Interaction.DeferAsync(ephemeral: false);

            // Sauvegarde de la note avec horodatage
            await Program.notesParser.AddNotes(member.Id, $"{phrase}\n\n-# <@{ctx.User.Id}> - {DateTime.Now.ToString()}");

            // Construction et envoi de la réponse
            int nbNotes = Program.notesParser.json.Notes[member.Id].listeNotes.Count;
            string total = $"Note n°{nbNotes.ToString()} ajoutée pour <@{member.Id}>.";
            DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().WithContent(total);
            await ctx.Interaction.CreateFollowupMessageAsync(builder);

        }

        [SlashCommand("NoteList", "Affiche la liste des notes du membre spécifié")]
        [SlashCommandPermissions(Permissions.ModerateMembers)]
        public async Task NoteList(InteractionContext ctx, [Option("Membre", "Membre")] DiscordUser member)
        {
            if (ctx.Guild == null)
            {
                DiscordFollowupMessageBuilder errorBuilder = new DiscordFollowupMessageBuilder().WithContent("Cette commande n'est pas autorisée en MP.");
                await ctx.Interaction.CreateFollowupMessageAsync(errorBuilder);
                return;
            }

            await ctx.Interaction.DeferAsync(ephemeral: false);

            // Cas où le membre a des notes
            if (Program.notesParser.json.Notes.Keys.Contains(member.Id))
            {
                int page = 1;
                int nbTotal = Program.notesParser.json.Notes[member.Id].listeNotes.Count();

                // Construction de l'embed avec boutons interactifs
                string note = Program.notesParser.json.Notes[member.Id].listeNotes.First();
                DiscordEmbedBuilder builder = BuildEmbedNotes(member.Id, member.AvatarUrl, note, page, nbTotal);
                var previous = new DiscordButtonComponent(ButtonStyle.Primary, $"{member.Id}-1", "Précédent", false);
                previous.Disable();
                var next = new DiscordButtonComponent(ButtonStyle.Primary, $"{member.Id}-2", "Suivant", false);
                if (page == nbTotal)
                {
                    next.Disable();
                }
                IEnumerable<DiscordComponent> components = new DiscordComponent[] { previous, next};
                DiscordMessageBuilder message = new DiscordMessageBuilder().WithEmbed(builder);
                DiscordFollowupMessageBuilder reponse = new DiscordFollowupMessageBuilder(message).AddComponents(components);

                await ctx.Interaction.CreateFollowupMessageAsync(reponse);
            }

            // Cas où le membre n'a pas de notes
            else
            {
                DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().WithContent($"<@{member.Id}> n'a pas de note ! :person_shrugging:");
                await ctx.Interaction.CreateFollowupMessageAsync(builder);
            }
        }

        [SlashCommand("NoteClear", "Supprime la note du membre spécifié ou toute sa liste")]
        [SlashCommandPermissions(Permissions.ModerateMembers)]
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

            // Suppression complète de la liste des notes
            if (index == 0)
            {
                Program.notesParser.json.Notes.Remove(member.Id);
                await Program.notesParser.WriteJSON();
                DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().WithContent($":broom: Toutes les notes de <@{member.Id}> ont été supprimées.");
                await ctx.Interaction.CreateFollowupMessageAsync(builder);
            }

            // Suppression d'une note spécifique
            else
            {
                List<string> list = Program.notesParser.json.Notes[member.Id].listeNotes;

                // Cas où le numéro de note est invalide
                if (index - 1 >= list.Count)
                {
                    DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().WithContent($"Numéro de note invalide.");
                    await ctx.Interaction.CreateFollowupMessageAsync(builder);
                }

                // Suppression de la note et réponse
                else
                {
                    if (list.Count == 1)
                    {
                        Program.notesParser.json.Notes.Remove(member.Id);
                    }
                    else
                    {
                        list.Remove(list[index - 1]);
                    }
                    await Program.notesParser.WriteJSON();
                    DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().WithContent($":broom: Note n°{number} supprimée pour <@{member.Id}>.");
                    await ctx.Interaction.CreateFollowupMessageAsync(builder);
                }
            }

        }

        #endregion

        #region Conversations
        [SlashCommand("convClear", "Supprime le fil anonyme spécifié")]
        [SlashCommandPermissions(Permissions.ModerateMembers)]

        public async Task convClear(InteractionContext ctx, [Option("Fil", "Fil à supprimer")] DiscordChannel thread)
        {
            if (ctx.Guild == null)
            {
                DiscordFollowupMessageBuilder errorBuilder = new DiscordFollowupMessageBuilder().WithContent("Cette commande n'est pas autorisée en MP.");
                await ctx.Interaction.CreateFollowupMessageAsync(errorBuilder);
                return;
            }

            await ctx.Interaction.DeferAsync(ephemeral: false);

            if (thread != null)
            {
                // On nettoie le fichier json
                var conv = Program.conversationParser.json.Conversations.Where(c => c.Value.threadId == thread.Id).FirstOrDefault();
                Program.conversationParser.json.Conversations.Remove(conv.Key);
                await Program.conversationParser.WriteJSON();

                // Puis on supprime le thread
                await thread.DeleteAsync();
                DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().WithContent($":broom: Le fil {thread.Name} a bien été supprimé.");
                await ctx.Interaction.CreateFollowupMessageAsync(builder);
            }
            else
            {
                DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().WithContent($"Ce fil ne semble pas exister. :person_shrugging:");
                await ctx.Interaction.CreateFollowupMessageAsync(builder);
            }
        }

        [SlashCommand("convIgnore", "Ignore les nouveaux message entrants du fil anonyme spécifiée")]
        [SlashCommandPermissions(Permissions.ModerateMembers)]
        public async Task convIgnore(InteractionContext ctx, [Option("Fil", "Fil à ignorer")] DiscordChannel thread, [Option("Annuler", "Mettre oui pour réactiver les messages entrants du thread")] string cancel = "non")
        {
            if (ctx.Guild == null)
            {
                DiscordFollowupMessageBuilder errorBuilder = new DiscordFollowupMessageBuilder().WithContent("Cette commande n'est pas autorisée en MP.");
                await ctx.Interaction.CreateFollowupMessageAsync(errorBuilder);
                return;
            }

            await ctx.Interaction.DeferAsync(ephemeral: false);
            if (!"oui".Equals(cancel))
            {
                // On met à jour le fichier JSON
                var conv = Program.conversationParser.json.Conversations.Where(c => c.Value.threadId == thread.Id).FirstOrDefault();
                conv.Value.statut = "ignoré";
                await Program.conversationParser.WriteJSON();

                DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().WithContent($"Les nouveaux messages entrants de <#{thread.Id}> sont désormais bloqués.");
                await ctx.Interaction.CreateFollowupMessageAsync(builder);
            }

            else
            {
                // On réinitialise les statuts du fichier JSON
                foreach (var conv in Program.conversationParser.json.Conversations)
                {
                    conv.Value.statut = null;
                };

                await Program.conversationParser.WriteJSON();
                DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().WithContent($"Les messages entrants de <#{thread.Id}> sont désormais réactivés");
                await ctx.Interaction.CreateFollowupMessageAsync(builder);
            }
        }
        #endregion

        #region birthday
        [SlashCommand("annivList", "Rédige la liste des anniversaires en embed")]
        [SlashCommandPermissions(Permissions.ModerateMembers)]
        public async Task annivList(InteractionContext ctx)
        {
            await ctx.Interaction.DeferAsync(ephemeral: true);

            if (ctx.Guild == null)
            {
                DiscordFollowupMessageBuilder errorBuilder = new DiscordFollowupMessageBuilder().WithContent("Cette commande n'est pas autorisée en MP.");
                await ctx.Interaction.CreateFollowupMessageAsync(errorBuilder);
                return;
            }

            DiscordEmbedBuilder builderCommandes = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Gold)
                .WithTitle($"Commandes")
                .WithThumbnail("https://i.imgur.com/wmZ63pr.png")
                ;

            builderCommandes.AddField("Pour ajouter votre anniversaire", "```/ajoutanniv```", false);
            builderCommandes.AddField("Pour retirer votre anniversaire", "```/retraitanniv```", false);
            builderCommandes.AddField("Pour que le bot vous souhaite un bon anniversaire", "```/bonannivon```", false);
            builderCommandes.AddField("Pour que le bot ne vous souhaite pas un bon anniversaire", "```/bonannivoff```", false);

            await ctx.Channel.SendMessageAsync(builderCommandes);

            DiscordEmbedBuilder builderAnniv = BuildEmbedAnniv(Program.anniversairesParser.json.Anniversaires);
            await ctx.Channel.SendMessageAsync(builderAnniv);

            DiscordFollowupMessageBuilder builderOK = new DiscordFollowupMessageBuilder().WithContent($"Liste OK.");
            await ctx.Interaction.CreateFollowupMessageAsync(builderOK);

        }

        [SlashCommand("ajoutAnniv", "Ajoute mon anniversaire à la liste")]
        [SlashCommandPermissions(Permissions.AccessChannels)]
        public async Task ajoutAnniv(InteractionContext ctx, [Option("Jour", "Préciser le jour")] string jour, [Option("Mois", "Préciser le mois")] string mois)
        {
            await ctx.Interaction.DeferAsync(ephemeral: true);

            if (ctx.Guild == null)
            {
                DiscordFollowupMessageBuilder errorBuilder = new DiscordFollowupMessageBuilder().WithContent("Cette commande n'est pas autorisée en MP.");
                await ctx.Interaction.CreateFollowupMessageAsync(errorBuilder);
                return;
            }

            string outMessage = "Ajout effectué.";
            if (Program.anniversairesParser.json.Anniversaires.ContainsKey(ctx.User.Id.ToString())){
                outMessage = "Vous êtes déjà dans la liste, la date a été mise à jour.";
            }

            string dateAnniv = jour.PadLeft(2, '0') + '/' + mois.PadLeft(2, '0');

            // Vérification de la validité de la date
            DateTime date;
            bool isDate = DateTime.TryParse(dateAnniv, out date);
            if (!isDate)
            {
                outMessage = "Cette date n'est pas valide !";
            }

            else
            {
                await Program.anniversairesParser.updateAnnivInEmbed(ctx.User.Id.ToString(), dateAnniv, ctx.Channel, true);
            }

            DiscordFollowupMessageBuilder builderOK = new DiscordFollowupMessageBuilder().WithContent(outMessage);
            await ctx.Interaction.CreateFollowupMessageAsync(builderOK);

        }

        [SlashCommand("modoAjoutAnniv", "Ajoute un anniversaire à la liste")]
        [SlashCommandPermissions(Permissions.ModerateMembers)]
        public async Task modoAjoutAnniv(InteractionContext ctx, [Option("Membre", "Préciser le membre")] DiscordUser membre, [Option("Jour", "Préciser le jour")] string jour, [Option("Mois", "Préciser le mois")] string mois)
        {
            await ctx.Interaction.DeferAsync(ephemeral: true);

            if (ctx.Guild == null)
            {
                DiscordFollowupMessageBuilder errorBuilder = new DiscordFollowupMessageBuilder().WithContent("Cette commande n'est pas autorisée en MP.");
                await ctx.Interaction.CreateFollowupMessageAsync(errorBuilder);
                return;
            }

            string outMessage = "Ajout effectué.";
            if (Program.anniversairesParser.json.Anniversaires.ContainsKey(membre.Id.ToString()))
            {
                outMessage = "Le membre est déjà dans la liste, la date a été mise à jour.";
            }

            string dateAnniv = jour.PadLeft(2, '0') + '/' + mois.PadLeft(2, '0');

            // Vérification de la validité de la date
            DateTime date;
            bool isDate = DateTime.TryParse(dateAnniv, out date);
            if (!isDate)
            {
                outMessage = "Cette date n'est pas valide !";
            }

            else
            {
                await Program.anniversairesParser.updateAnnivInEmbed(membre.Id.ToString(), dateAnniv, ctx.Channel, true);
            }

            DiscordFollowupMessageBuilder builderOK = new DiscordFollowupMessageBuilder().WithContent(outMessage);
            await ctx.Interaction.CreateFollowupMessageAsync(builderOK);

        }

        [SlashCommand("retraitAnniv", "Retire mon anniversaire de la liste")]
        [SlashCommandPermissions(Permissions.AccessChannels)]
        public async Task retraitAnniv(InteractionContext ctx)
        {
            await ctx.Interaction.DeferAsync(ephemeral: true);

            if (ctx.Guild == null)
            {
                DiscordFollowupMessageBuilder errorBuilder = new DiscordFollowupMessageBuilder().WithContent("Cette commande n'est pas autorisée en MP.");
                await ctx.Interaction.CreateFollowupMessageAsync(errorBuilder);
                return;
            }

            string outMessage = "Retrait effectué.";
            if (!Program.anniversairesParser.json.Anniversaires.ContainsKey(ctx.User.Id.ToString()))
            {
                outMessage = "Vous n'êtes pas dans la liste, aucune action n'a été effectuée.";
            }

            else
            {
                await Program.anniversairesParser.updateAnnivInEmbed(ctx.User.Id.ToString(), null, ctx.Channel, false);
            }

            DiscordFollowupMessageBuilder builderOK = new DiscordFollowupMessageBuilder().WithContent(outMessage);
            await ctx.Interaction.CreateFollowupMessageAsync(builderOK);

        }

        [SlashCommand("modoRetraitAnniv", "Retire un anniversaire de la liste")]
        [SlashCommandPermissions(Permissions.ModerateMembers)]
        public async Task modoRetraitAnniv(InteractionContext ctx, [Option("Membre", "Préciser le membre")] DiscordUser membre)
        {
            await ctx.Interaction.DeferAsync(ephemeral: true);

            if (ctx.Guild == null)
            {
                DiscordFollowupMessageBuilder errorBuilder = new DiscordFollowupMessageBuilder().WithContent("Cette commande n'est pas autorisée en MP.");
                await ctx.Interaction.CreateFollowupMessageAsync(errorBuilder);
                return;
            }

            string outMessage = "Retrait effectué.";
            if (!Program.anniversairesParser.json.Anniversaires.ContainsKey(membre.Id.ToString()))
            {
                outMessage = "Ce membre n'est pas dans la liste, aucune action n'a été effectuée.";
            }

            else
            {
                await Program.anniversairesParser.updateAnnivInEmbed(membre.Id.ToString(), null, ctx.Channel, false);
            }

            DiscordFollowupMessageBuilder builderOK = new DiscordFollowupMessageBuilder().WithContent(outMessage);
            await ctx.Interaction.CreateFollowupMessageAsync(builderOK);

        }

        [SlashCommand("annivMaj", "Récupère la liste des anniversaires du salon")]
        [SlashCommandPermissions(Permissions.ModerateMembers)]
        public async Task annivMaj(InteractionContext ctx)
        {
            await ctx.Interaction.DeferAsync(ephemeral: true);

            if (ctx.Guild == null)
            {
                DiscordFollowupMessageBuilder errorBuilder = new DiscordFollowupMessageBuilder().WithContent("Cette commande n'est pas autorisée en MP.");
                await ctx.Interaction.CreateFollowupMessageAsync(errorBuilder);
                return;
            }

            // On récupère la liste de messages du salon en retirant le message de l'interaction
            var listMessagesTmp = await ctx.Guild.GetChannel(Program.config.ID_annivChannel).GetMessagesAsync(12);
            List<DiscordMessage> listMessages = listMessagesTmp.ToList();
            listMessages.Reverse();
            int month = 1;

            // On vide le fichier JSON du contenu présent en gardant la valeur ignored
            List<String> ignoredList = new List<String>();
            foreach (KeyValuePair<string, MemberAnniversaire> memberAnniv in Program.anniversairesParser.json.Anniversaires) 
            {
                if (memberAnniv.Value.ignored)
                {
                    ignoredList.Add(memberAnniv.Key);
                }
            }
            await Program.anniversairesParser.resetJSON();
            Program.anniversairesParser.json.Anniversaires = new Dictionary<string, MemberAnniversaire>();

            // On écrit le fichier JSON pour chaque ligne dans chaque message
            string dateAnniv = "";
            foreach (DiscordMessage message in listMessages)
            {
                foreach (String line in message.Content.Split('\n'))
                {
                    // On vérifie que la ligne démarre par un entier pour ignorer les headers
                    if (int.TryParse(line.Substring(0, 1), out int value))
                    {
                        string[] content = line.Split(':');
                        dateAnniv = content[0];
                        dateAnniv += "/" + month.ToString().PadLeft(2, '0');

                        // On extrait les IDs du message pour enregistrer les anniversaires individuellement
                        MatchCollection matches = Regex.Matches(line, @"<@(.*?)>");
                        foreach (Match match in matches)
                        {
                            bool ignored = false;
                            String memberId = match.Groups[1].ToString();
                            if (ignoredList.Contains(memberId))
                            {
                                ignored = true;
                            }
                            await Program.anniversairesParser.AddAnniv(memberId, dateAnniv, ignored);
                        }
                    }
                }
                month++;
            }

            DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().WithContent($"OK");
            await ctx.Interaction.CreateFollowupMessageAsync(builder);
        }

        [SlashCommand("modoAnnivOff", "Exclut un membre de la liste des anniversaires à souhaiter")]
        [SlashCommandPermissions(Permissions.ModerateMembers)]
        public async Task modoAnnivOff(InteractionContext ctx, [Option("Membre", "Membre à exclure de la liste")] DiscordUser member)
        {
            await ctx.Interaction.DeferAsync(ephemeral: true);

            if (ctx.Guild == null)
            {
                DiscordFollowupMessageBuilder errorBuilder = new DiscordFollowupMessageBuilder().WithContent("Cette commande n'est pas autorisée en MP.");
                await ctx.Interaction.CreateFollowupMessageAsync(errorBuilder);
                return;
            }

            DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().WithContent($"Échec: ce membre n'est pas dans la liste !");

            foreach (KeyValuePair<string, MemberAnniversaire> memberAnniv in Program.anniversairesParser.json.Anniversaires)
            {
                if (member.Id.ToString().Equals(memberAnniv.Key))
                {
                    if (Program.anniversairesParser.json.Anniversaires[member.Id.ToString()].ignored)
                    {
                        builder = builder.WithContent($"<@{member.Id}> est déjà dans la liste d'exclusion !");
                    }
                    else
                    {
                        Program.anniversairesParser.json.Anniversaires[member.Id.ToString()].ignored = true;
                        await Program.anniversairesParser.WriteJSON();
                        builder = builder.WithContent($"<@{member.Id}> a été ajouté à la liste d'exclusion.");
                    }
                }
            }
           
            await ctx.Interaction.CreateFollowupMessageAsync(builder);
        }

        [SlashCommand("modoAnnivOn", "Retire de la liste d'exclusion pour souhaiter bon anniversaire")]
        [SlashCommandPermissions(Permissions.ModerateMembers)]
        public async Task annivFiltreReset(InteractionContext ctx, [Option("Membre", "Membre à ne plus exclure")] DiscordUser member = null, [Option("Tous", "Préciser \"oui\" pour un reset complet")] String all = "non")
        {
            await ctx.Interaction.DeferAsync(ephemeral: true);

            if (ctx.Guild == null)
            {
                DiscordFollowupMessageBuilder errorBuilder = new DiscordFollowupMessageBuilder().WithContent("Cette commande n'est pas autorisée en MP.");
                await ctx.Interaction.CreateFollowupMessageAsync(errorBuilder);
                return;
            }

            DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().WithContent($"Il faut renseigner au moins un paramètre !");

            if (member != null)
            {
                if (!Program.anniversairesParser.json.Anniversaires[member.Id.ToString()].ignored)
                {
                    builder = builder.WithContent($"<@{member.Id}> n'est pas dans la liste d'exclusion !");
                }
                else
                {
                    Program.anniversairesParser.json.Anniversaires[member.Id.ToString()].ignored = false;
                    await Program.anniversairesParser.WriteJSON();
                    builder = builder.WithContent($"<@{member.Id}> a été retiré de la liste d'exclusion.");
                }
            }

            if ("oui".Equals(all))
            {
                foreach (KeyValuePair<string, MemberAnniversaire> memberAnniv in Program.anniversairesParser.json.Anniversaires)
                {
                    Program.anniversairesParser.json.Anniversaires[memberAnniv.Key].ignored = false;
                }
                await Program.anniversairesParser.WriteJSON();
                builder = builder.WithContent($"Toutes les personnes ont été retirées de la liste d'exclusion");
            }
            await ctx.Interaction.CreateFollowupMessageAsync(builder);
        }

        [SlashCommand("bonAnnivOff", "Le bot ne me souhaitera pas bon anniversaire")]
        [SlashCommandPermissions(Permissions.AccessChannels)]
        public async Task annivFiltreOn(InteractionContext ctx)
        {
            await ctx.Interaction.DeferAsync(ephemeral: true);

            if (ctx.Guild == null)
            {
                DiscordFollowupMessageBuilder errorBuilder = new DiscordFollowupMessageBuilder().WithContent("Cette commande n'est pas autorisée en MP.");
                await ctx.Interaction.CreateFollowupMessageAsync(errorBuilder);
                return;
            }

            DiscordUser member = ctx.User;

            DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().WithContent($"Échec: Vous n'êtes pas listé dans <#{Program.config.ID_annivChannel}>!");

            foreach (KeyValuePair<string, MemberAnniversaire> memberAnniv in Program.anniversairesParser.json.Anniversaires)
            {
                if (member.Id.ToString().Equals(memberAnniv.Key))
                {
                    if (Program.anniversairesParser.json.Anniversaires[member.Id.ToString()].ignored == true)
                    {
                        builder = builder.WithContent($"Vous êtes déjà exclu des anniversaires à souhaiter !");
                    }
                    else
                    {
                        Program.anniversairesParser.json.Anniversaires[member.Id.ToString()].ignored = true;
                        await Program.anniversairesParser.WriteJSON();

                        builder = builder.WithContent($"Je ne vous souhaiterais plus bon anniversaire.");
                    }
                }
            }

            await ctx.Interaction.CreateFollowupMessageAsync(builder);
        }

        [SlashCommand("bonAnnivOn", "Le bot me souhaitera bon anniversaire")]
        [SlashCommandPermissions(Permissions.AccessChannels)]
        public async Task annivFiltreOff(InteractionContext ctx)
        {
            await ctx.Interaction.DeferAsync(ephemeral: true);

            if (ctx.Guild == null)
            {
                DiscordFollowupMessageBuilder errorBuilder = new DiscordFollowupMessageBuilder().WithContent("Cette commande n'est pas autorisée en MP.");
                await ctx.Interaction.CreateFollowupMessageAsync(errorBuilder);
                return;
            }

            DiscordUser member = ctx.User;

            DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().WithContent($"Échec: Vous n'êtes pas listé dans <#{Program.config.ID_annivChannel}>!");

            foreach (KeyValuePair<string, MemberAnniversaire> memberAnniv in Program.anniversairesParser.json.Anniversaires)
            {
                if (member.Id.ToString().Equals(memberAnniv.Key))
                {
                    if (Program.anniversairesParser.json.Anniversaires[member.Id.ToString()].ignored == false)
                    {
                        builder = builder.WithContent($"Vous faites déjà parti des anniversaires à souhaiter !");
                    }
                    else
                    {
                        Program.anniversairesParser.json.Anniversaires[member.Id.ToString()].ignored = false;
                        await Program.anniversairesParser.WriteJSON();
                        builder = builder.WithContent("Je vous souhaiterais bon anniversaire.");
                    }
                }
            }

            await ctx.Interaction.CreateFollowupMessageAsync(builder);
        }
        #endregion

        #region Recommandations
        [SlashCommand("recommandation", "Recommander une adresse")]
        [SlashCommandPermissions(Permissions.ModerateMembers)]
        public async Task recoAdresse(InteractionContext ctx, 
            [Option("Nom", "Nom du lieu")] string nom, 
            [Option("Type", "Type de lieu")] string type, 
            [Option("Adresse", "Adresse du lieu")] string adresse, 
            [Option("Prix", "Prix approximatif")] string prix,
            [Option("Commentaire", "Description pour donner envie de tester le lieu")] string com,
            [Option("Note", "Votre note sur 5")] string note,
            [Option("Site", "Lien du site web")] string url = null)
        {

            await ctx.Interaction.DeferAsync(ephemeral: true);
            if (ctx.Guild == null)
            {
                DiscordFollowupMessageBuilder errorBuilder = new DiscordFollowupMessageBuilder().WithContent("Cette commande n'est pas autorisée en MP.");
                await ctx.Interaction.CreateFollowupMessageAsync(errorBuilder);
                return;
            }

            DiscordFollowupMessageBuilder builder = new DiscordFollowupMessageBuilder().WithContent($"Recommandation ajoutée.");

            DiscordEmbedBuilder embedReco1 = BuildEmbedReco(ctx.User, nom, type, adresse, url, prix, com, note);
            DiscordEmbedBuilder embedReco2 = BuildEmbedReco(ctx.User, nom, type, adresse, url, prix, com, note);
            DiscordMessageBuilder message = new DiscordMessageBuilder().AddEmbed(embedReco1).AddEmbed(embedReco2);
            message.WithContent("# :beers: Bars");

            var outMessage = await ctx.Channel.SendMessageAsync(message);

            DiscordMessageBuilder links = new DiscordMessageBuilder().WithContent($"## Catégories\n" +
                $"[Bar]({outMessage.JumpLink}) " +
                $"- [Restaurant]({outMessage.JumpLink}) ");
            await ctx.Channel.SendMessageAsync(links);

            await ctx.Interaction.CreateFollowupMessageAsync(builder);
        }
        #endregion

        #region newsfeed
        [SlashCommand("newsFeed", "Affiche le fil d'actu")]
        [SlashCommandPermissions(Permissions.ModerateMembers)]
        public async Task newsFeed(InteractionContext ctx, [Option("Poster", "Poster les nouveaux évènements")] Boolean postNews = true)
        {
            await ctx.Interaction.DeferAsync(ephemeral: true);
            await Program.updateNewsFeed(postNews);
            await ctx.DeleteResponseAsync();
        }
        #endregion

        #region Roles

        [SlashCommandGroup("Roles", "Gestion des rôles et assignation")]
        public class Roles : ApplicationCommandModule
        {
            [SlashCommand("AddRoleIncompatibility", "Ajoute une incompatibilité de rôle quand la fonction 'AddRole' est invoké")]
            [SlashRequireUserPermissions(Permissions.Administrator)]
            public async Task AddRoleIncompatibility(InteractionContext ctx,
                [Option("Role_A","Premier rôle")] DiscordRole roleA = default,
                [Option("Role_B", "Deuxième rôle")] DiscordRole roleB = default)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

                await Program.rolesParser.AddIncompatibility(roleA.Id, roleB.Id);

                await ctx.DeleteResponseAsync();
            }

            [SlashCommand("RemoveRoleIncompatibility", "Retire une incompatibilité de rôle.")]
            [SlashRequireUserPermissions(Permissions.Administrator)]
            public async Task RemoveRoleIncompatibility(InteractionContext ctx,
                [Option("Role_A", "Premier rôle")] DiscordRole roleA = default,
                [Option("Role_B", "Deuxième rôle")] DiscordRole roleB = default)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

                await Program.rolesParser.RemoveIncompatibility(roleA.Id, roleB.Id);

                await ctx.DeleteResponseAsync();
            }
        }
        
        #endregion
    }
}
