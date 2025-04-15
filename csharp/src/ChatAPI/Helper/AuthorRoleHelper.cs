using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultiAgentCopilot.Helper
{
    internal static class AuthorRoleHelper
    {
        private static readonly Dictionary<string, AuthorRole> RoleMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "system", AuthorRole.System },
            { "assistant", AuthorRole.Assistant },
            { "user", AuthorRole.User },
            { "tool", AuthorRole.Tool }
        };

        public static AuthorRole? FromString(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            return RoleMap.TryGetValue(name, out var role) ? role : null;
        }
    }
}
