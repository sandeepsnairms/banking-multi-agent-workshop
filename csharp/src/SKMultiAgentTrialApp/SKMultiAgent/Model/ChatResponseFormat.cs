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
            Blank

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
                            "SelectionCriteria": {
                                "type": "object",
                                "properties": {
                                    "type": "object",
                                    "properties": {
                                       "AgentName": { "type": "string" },
                                       "Reason": { "type": "string" },
                                    },
                                    "required": ["AgentName", "Reason"],
                                    "additionalProperties": false
                                }
                            }
                        },
                        "required": ["SelectionCriteria"],
                        "additionalProperties": true
                    }
                    """;

                    return jsonSchemaFormat;
                default:
                    return "";
            }
            
        }
    }
    

}
