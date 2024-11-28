using DSharpPlus.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace LaGrueJaune.config
{
    internal class JSONRolesParser
    {
        public RoleConfig json;

        public async Task ReadJSON()
        {
            using (StreamReader sr = new StreamReader("JSON/roles.json"))
            {
                string reader = await sr.ReadToEndAsync();
                RoleConfig data = JsonConvert.DeserializeObject<RoleConfig>(reader);

                this.json = new RoleConfig();
                this.json = data;

                if (this.json.incompatibleRolesByRole == null)
                    this.json.incompatibleRolesByRole = new Dictionary<ulong, List<ulong>>();
                
                Console.WriteLine($"Roles: {this.json.incompatibleRolesByRole.Count}");
            }
        }

        public async Task WriteJSON()
        {
            using (StreamWriter sw = new StreamWriter("JSON/roles.json"))
            {
                await sw.WriteLineAsync(JsonConvert.SerializeObject(this.json, Formatting.Indented));
            }
        }

        public async Task AddIncompatibility(ulong roleID, ulong incompatibleRoleID)
        {
            AddIncompatibilityInside(roleID, incompatibleRoleID);
            AddIncompatibilityInside(incompatibleRoleID, roleID);

            void AddIncompatibilityInside(ulong tRoleID, ulong tIncompatibleRoleID)
            {
                if (json.incompatibleRolesByRole.ContainsKey(tRoleID))
                {
                    if (!json.incompatibleRolesByRole[tRoleID].Contains(tIncompatibleRoleID))
                    {
                        json.incompatibleRolesByRole[tRoleID].Add(tIncompatibleRoleID);
                    }
                }
                else
                {
                    List<ulong> newListRole = new List<ulong>()
                    {
                        tIncompatibleRoleID
                    };
                    json.incompatibleRolesByRole.Add(tRoleID, newListRole);
                }
            }

            await WriteJSON();
        }

        public async Task RemoveIncompatibility(ulong roleID, ulong precedentIncompatibleRoleID)
        {
            RemoveIncompatibilityInside(roleID, precedentIncompatibleRoleID);
            RemoveIncompatibilityInside(precedentIncompatibleRoleID, roleID);

            void RemoveIncompatibilityInside(ulong tRoleID, ulong tPrecedentIncompatibleRoleID)
            {
                if (json.incompatibleRolesByRole.ContainsKey(tRoleID))
                {
                    if (json.incompatibleRolesByRole[tRoleID].Contains(tPrecedentIncompatibleRoleID))
                    {
                        json.incompatibleRolesByRole[tRoleID].Remove(tPrecedentIncompatibleRoleID);
                    }
                }
            }

            await WriteJSON();
        }
    }

    public class RoleConfig
    {
        public Dictionary<ulong, List<ulong>> incompatibleRolesByRole;
    }
}
