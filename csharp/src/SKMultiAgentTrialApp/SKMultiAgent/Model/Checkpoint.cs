using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using SKMultiAgent.Model;

namespace SKMultiAgent.Model
{
    internal class CheckPoint
    {
        public List<CustomChatMessageContent> Messages { get; set; }
    }

   


    //public class Metadata
    //{
    //    public string Id { get; set; }
    //    public string Usage { get; set; }
    //}

    public class CustomChatMessageContent
    {
        public string Id { get; set; }
        public string Content { get; set; }
        public AuthorRole AuthorRole { get; set; }
        public string AuthorName { get; set; } 

    }
}


