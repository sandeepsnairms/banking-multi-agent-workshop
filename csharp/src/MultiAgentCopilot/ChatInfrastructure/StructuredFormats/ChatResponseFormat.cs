using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace MultiAgentCopilot.ChatInfrastructure.StructuredFormats
{
    internal static class ChatResponseFormatBuilder
    {
        internal enum ChatResponseStratergy
        {
            Continuation,
            Termination

        }

        internal static string BuildFormat(ChatResponseStratergy stratergyType)
        {
            switch (stratergyType)
            {
                case ChatResponseStratergy.Continuation:
                    string jsonSchemaFormat_Continuation = """
                    {

                        "type": "object", 
                            "properties": {
                                "AgentName": { "type": "string", "description":"name of the selected agent" },
                                "Reason": { "type": "string","description":"reason for selecting the agent" }
                            },
                            "required": ["AgentName", "Reason"],
                            "additionalProperties": false

                    }
                    """;

                    return jsonSchemaFormat_Continuation;
                case ChatResponseStratergy.Termination:
                    string jsonSchemaFormat_termination = """
                    {

                        "type": "object", 
                            "properties": {
                                "ShouldContinue": { "type": "boolean", "description":"Does conversation require further agent participation" },
                                "Reason": { "type": "string","description":"List the conditions that evaluated to true for further agent participation" }
                            },
                            "required": ["ShouldContinue", "Reason"],
                            "additionalProperties": false

                    }
                    """;

                    return jsonSchemaFormat_termination;
                default:
                    return "";
            }

        }
    }


}
