// Copyright (c) Microsoft. All rights reserved.

#define TRACE_CONSOLE

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using Azure;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Agents.History;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.Chat;
using SKMultiAgent.Helper;
using SKMultiAgent.KernelPlugins;
using SKMultiAgent.Log;
using SKMultiAgent.Model;
using ChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;
using Microsoft.Extensions.Logging.Console;

namespace SKMultiAgent
{
    public static class Program
    {
        public static async Task Main()
        {
            var bot = new BankingBot();
            bot.Load();
            bool continueRun = true;
            while (continueRun)
            {
                continueRun = bot.Run().GetAwaiter().GetResult();          
            }
            
        }
    }

 
}
