using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace SKMultiAgent.Model
{
    internal static class ChatResponseFormatBuilder
    {
        internal enum ChatResponseFormatBuilderType
        {
            Defaut,
            Blank,
            Termination

        }

        internal static string BuildFormat(ChatResponseFormatBuilderType formatType)
        {
            switch(formatType)
            {
                case ChatResponseFormatBuilderType.Defaut:
                    string jsonSchemaFormat = """
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

                    return jsonSchemaFormat;
                case ChatResponseFormatBuilderType.Termination:
                    string jsonSchemaFormat2 = """
                    {

                        "type": "object", 
                            "properties": {
                                "ShouldContinue": { "type": "bool", "description":"Does conversation require further agent participation" },
                                "Reason": { "type": "string","description":"Reason for further agent participation" }
                            },
                            "required": ["IsComplete", "Reason"],
                            "additionalProperties": false

                    }
                    """;

                    return jsonSchemaFormat2;
                default:
                    return "";
            }
            
        }
    }
    

}
